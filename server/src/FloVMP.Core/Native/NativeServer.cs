using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Channels;

namespace FloVMP.Core.Native;

/// <summary>Событие транспорта для главного потока игрового сервера.</summary>
public abstract record NativeEvent(NativeSession Session);
public sealed record NativeJoined(NativeSession Session) : NativeEvent(Session);
public sealed record NativeMessage(NativeSession Session, string[] Parts) : NativeEvent(Session);
public sealed record NativeLeft(NativeSession Session, string Reason) : NativeEvent(Session);

/// <summary>
/// TCP-шлюз нативных клиентов b3889.
///
/// Сеть и рукопожатие живут на пуле потоков; игровая логика — только на
/// главном потоке сервера: он забирает события из <see cref="Events"/>
/// в своём тике. STATE (20 раз в секунду от каждого) в очередь не попадает —
/// разбирается на сетевом потоке и кладётся в сессию как «последнее известное».
///
/// Защита: лимит длины строки, таймаут рукопожатия, лимит подключений с
/// одного IP, лимит сообщений в секунду, ограниченная очередь отправки
/// (медленный клиент не копит память сервера).
/// </summary>
public sealed class NativeServer : IDisposable
{
    // За одним IP бывает много игроков: CGNAT мобильных операторов, семьи,
    // компьютерные клубы. Лимит поднимается переменной FLOVMP_MAX_PER_IP,
    // а подключения с самой машины сервера (127.0.0.1) не ограничиваются.
    public const int DefaultMaxConnectionsPerIp = 12;

    public static int MaxConnectionsPerIp =>
        int.TryParse(Environment.GetEnvironmentVariable("FLOVMP_MAX_PER_IP"), out var v) && v is > 0 and <= 1000
            ? v : DefaultMaxConnectionsPerIp;
    public const int MaxMessagesPerSecond = 120;
    public static readonly TimeSpan HandshakeTimeout = TimeSpan.FromSeconds(15);
    public static readonly TimeSpan IdleTimeout = TimeSpan.FromSeconds(30);

    private readonly TcpListener _listener;
    private readonly Action<string> _log;
    private readonly CancellationTokenSource _stop = new();
    private readonly ConcurrentDictionary<uint, NativeSession> _sessions = new();
    private readonly ConcurrentDictionary<string, int> _perIp = new();
    private readonly Func<uint, bool> _idInUse;
    private readonly object _idLock = new();
    private Task? _acceptLoop;

    public ConcurrentQueue<NativeEvent> Events { get; } = new();
    public string ServerName { get; set; } = "FloV:MP";

    /// <param name="idInUse">Занят ли ID другим (не нативным) игроком — ID общие для всех.</param>
    public NativeServer(IPAddress address, int port, Action<string> log, Func<uint, bool>? idInUse = null)
    {
        _listener = new TcpListener(address, port);
        _log = log;
        _idInUse = idInUse ?? (_ => false);
    }

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;
    public ICollection<NativeSession> Sessions => _sessions.Values;
    public int Count => _sessions.Count;

    public NativeSession? Get(uint id) => _sessions.TryGetValue(id, out var s) && s.Joined ? s : null;

    public void Start()
    {
        _listener.Start(128);
        _acceptLoop = Task.Run(AcceptLoopAsync);
    }

    private async Task AcceptLoopAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            TcpClient client;
            try { client = await _listener.AcceptTcpClientAsync(_stop.Token); }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException) { break; }
            catch (SocketException) when (_stop.IsCancellationRequested) { break; }
            catch (Exception ex) { _log("accept: " + ex.Message); continue; }
            _ = Task.Run(() => HandleAsync(client));
        }
    }

    // ID занят с выдачи до события ухода; после ухода номер 30 с не выдаётся
    // снова — иначе «/kick 3», набранный сразу после выхода игрока 3,
    // попал бы в только что вошедшего с тем же номером.
    private readonly HashSet<uint> _reservedIds = new();
    private readonly Dictionary<uint, DateTime> _releasedIds = new();
    public static readonly TimeSpan IdReuseDelay = TimeSpan.FromSeconds(30);

    private uint AllocateId()
    {
        lock (_idLock)
        {
            var now = DateTime.UtcNow;
            for (uint id = 1; ; id++)
            {
                if (_reservedIds.Contains(id) || _idInUse(id)) continue;
                if (_releasedIds.TryGetValue(id, out var at))
                {
                    if (now - at < IdReuseDelay) continue;
                    _releasedIds.Remove(id);
                }
                _reservedIds.Add(id);
                return id;
            }
        }
    }

    private void ReleaseId(uint id)
    {
        lock (_idLock)
        {
            _reservedIds.Remove(id);
            _releasedIds[id] = DateTime.UtcNow;
        }
    }

    private async Task HandleAsync(TcpClient client)
    {
        var remote = (client.Client.RemoteEndPoint as IPEndPoint)?.Address;
        var ip = remote?.MapToIPv4().ToString() ?? "0.0.0.0";
        var local = remote is not null && IPAddress.IsLoopback(remote);
        if (!local && _perIp.AddOrUpdate(ip, 1, (_, n) => n + 1) > MaxConnectionsPerIp)
        {
            _perIp.AddOrUpdate(ip, 0, (_, n) => n - 1);
            // Раньше соединение просто рвалось, и игрок видел «сервер не отвечает».
            try
            {
                using var reject = client.GetStream();
                WriteRawAsync(reject, NativeProtocol.Format("REJECT",
                    $"с вашего адреса уже подключено {MaxConnectionsPerIp} игроков — попробуйте позже")).Wait(1000);
            }
            catch (Exception) { }
            _log($"отклонён {ip}: превышен лимит подключений с одного адреса ({MaxConnectionsPerIp})");
            client.Dispose();
            return;
        }

        NativeSession? session = null;
        var leaveReason = "соединение закрыто";
        try
        {
            client.NoDelay = true;
            var stream = client.GetStream();
            var reader = new LineReader(stream);

            using var handshakeCts = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
            handshakeCts.CancelAfter(HandshakeTimeout);

            var hello = NativeProtocol.Parse(await reader.ReadLineAsync(handshakeCts.Token) ?? "");
            var problem = ValidateHello(hello);
            if (problem is not null)
            {
                await WriteRawAsync(stream, NativeProtocol.Format("REJECT", problem));
                _log($"отклонён {ip}: {problem}");
                return;
            }

            var publicKey = Convert.FromBase64String(hello[5]);
            var nonce = RandomNumberGenerator.GetBytes(32);
            await WriteRawAsync(stream, NativeProtocol.Format("CHALLENGE", Convert.ToBase64String(nonce)));
            var auth = NativeProtocol.Parse(await reader.ReadLineAsync(handshakeCts.Token) ?? "");
            if (auth.Length < 2 || auth[0] != "AUTH" || !NativeIdentity.Verify(publicKey, nonce, auth[1]))
            {
                await WriteRawAsync(stream, NativeProtocol.Format("REJECT", "не удалось подтвердить личность клиента"));
                _log($"отклонён {ip}: неверная подпись");
                return;
            }

            session = new NativeSession(this, AllocateId(), client, stream, ip,
                name: hello[4].Trim(), identity: NativeIdentity.IdFor(publicKey),
                hwid: ParseHex(hello[6]), mac: ParseHex(hello[7]), clientVersion: hello[3]);
            _sessions[session.Id] = session;
            session.StartWriter(_stop.Token);
            Events.Enqueue(new NativeJoined(session));

            var window = DateTime.UtcNow;
            var inWindow = 0;
            while (!_stop.IsCancellationRequested && !session.Closing)
            {
                using var idle = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token, session.CloseToken);
                idle.CancelAfter(IdleTimeout);
                string? line;
                try { line = await reader.ReadLineAsync(idle.Token); }
                catch (OperationCanceledException) when (!_stop.IsCancellationRequested && !session.Closing)
                {
                    leaveReason = "нет ответа от клиента";
                    break;
                }
                if (line is null) break;

                var now = DateTime.UtcNow;
                if (now - window > TimeSpan.FromSeconds(1)) { window = now; inWindow = 0; }
                if (++inWindow > MaxMessagesPerSecond)
                {
                    leaveReason = "превышен лимит сообщений";
                    break;
                }

                var parts = NativeProtocol.Parse(line);
                switch (parts[0])
                {
                    case "STATE":
                        if (NativePlayerState.TryParse(parts, out var state))
                        {
                            // Водитель не может объявить владельцем транспорта
                            // другого игрока: это поле является частью серверной
                            // авторитетности транспорта, а не доверием к клиенту.
                            if (state.InVehicle && state.Seat == -1)
                                state = state with { VehicleOwner = (int)session.Id };
                            session.SetState(state);
                        }
                        break;
                    case "VSYNC":
                        // Как STATE: 20 раз в секунду от каждого водителя — мимо
                        // очереди событий, «последнее побеждает». Кто водитель,
                        // решает главный поток по реестру.
                        if (NativeVehicleSync.TryParse(parts, out var vsync)) session.SetVehicleSync(vsync);
                        break;
                    case "PING":
                        session.Send(NativeProtocol.Format("PONG", parts.Length > 1 ? parts[1] : ""));
                        break;
                    case "KEEPALIVE":
                        // Сетевой поток клиента шлёт его и тогда, когда игра стоит на
                        // паузе (меню, карта) и скрипты, а с ними STATE, не работают.
                        break;
                    default:
                        Events.Enqueue(new NativeMessage(session, parts));
                        break;
                }
            }
            if (session.Closing) leaveReason = session.CloseReason ?? leaveReason;
        }
        catch (LineTooLongException) { leaveReason = "слишком длинное сообщение"; }
        catch (OperationCanceledException) { leaveReason = "таймаут подключения"; }
        catch (IOException) { }
        catch (SocketException) { }
        catch (FormatException) { leaveReason = "повреждённые данные"; }
        catch (Exception ex) { _log("соединение: " + ex.Message); }
        finally
        {
            if (!local) _perIp.AddOrUpdate(ip, 0, (_, n) => Math.Max(0, n - 1));
            if (session is not null && _sessions.TryRemove(session.Id, out _))
            {
                session.MarkClosed();
                Events.Enqueue(new NativeLeft(session, session.CloseReason ?? leaveReason));
                // Номер освобождается только после события ухода: вход нового
                // игрока с тем же ID встанет в очередь позже, не раньше.
                ReleaseId(session.Id);
            }
            // Дать уйти последнему сообщению (KICK/REJECT) до разрыва.
            try { await Task.Delay(200); } catch { }
            client.Dispose();
        }
    }

    private static string? ValidateHello(string[] p)
    {
        if (p.Length < 8 || p[0] != "HELLO") return "неподдерживаемый клиент";
        if (p[1] != NativeProtocol.Version) return "устаревший клиент FloV:MP — обновите клиент";
        if (p[2] != NativeProtocol.GameVersion)
            return $"нужна GTA V Legacy {NativeProtocol.GameVersion}, у вас {(p[2].Length == 0 ? "неизвестная версия" : p[2])}";
        if (!Chat.PlayerNamePolicy.IsAllowed(p[4].Trim(), out var nameProblem))
            return "недопустимый ник: " + nameProblem;
        try
        {
            if (!NativeIdentity.IsValidPublicKey(Convert.FromBase64String(p[5]))) return "повреждённый ключ клиента";
        }
        catch (FormatException) { return "повреждённый ключ клиента"; }
        return null;
    }

    private static ulong ParseHex(string value) =>
        ulong.TryParse(value, System.Globalization.NumberStyles.HexNumber, null, out var v) ? v : 0;

    internal static async Task WriteRawAsync(Stream stream, string line)
    {
        var bytes = Encoding.UTF8.GetBytes(line + "\n");
        await stream.WriteAsync(bytes);
        await stream.FlushAsync();
    }

    public void Dispose()
    {
        _stop.Cancel();
        try { _listener.Stop(); } catch { }
        foreach (var s in _sessions.Values) s.Close("сервер остановлен");
        try { _acceptLoop?.Wait(TimeSpan.FromSeconds(2)); } catch { }
    }

    /// <summary>Чтение строк с жёстким лимитом длины: StreamReader.ReadLine копит память без предела.</summary>
    private sealed class LineReader
    {
        private readonly Stream _stream;
        private readonly byte[] _buffer = new byte[8192];
        private int _start, _end;

        public LineReader(Stream stream) => _stream = stream;

        public async Task<string?> ReadLineAsync(CancellationToken token)
        {
            var line = new MemoryStream();
            while (true)
            {
                for (var i = _start; i < _end; i++)
                {
                    if (_buffer[i] != (byte)'\n') continue;
                    line.Write(_buffer, _start, i - _start);
                    _start = i + 1;
                    var bytes = line.ToArray();
                    var length = bytes.Length > 0 && bytes[^1] == '\r' ? bytes.Length - 1 : bytes.Length;
                    return Encoding.UTF8.GetString(bytes, 0, length);
                }
                line.Write(_buffer, _start, _end - _start);
                if (line.Length > NativeProtocol.MaxLineBytes) throw new LineTooLongException();
                _start = _end = 0;
                var read = await _stream.ReadAsync(_buffer, token);
                if (read <= 0) return null;
                _end = read;
            }
        }
    }

    private sealed class LineTooLongException : Exception { }
}

/// <summary>Подключённый нативный клиент.</summary>
public sealed class NativeSession
{
    private readonly NativeServer _owner;
    private readonly TcpClient _client;
    private readonly Stream _stream;
    /// <summary>Размер очереди отправки. Переполнение — это отставший клиент:
    /// его отключаем с причиной, а не молча теряем строки (см. Send).</summary>
    public const int OutboxCapacity = 4096;

    // Очередь неограниченная намеренно: ограниченная с DropWrite на переполнении
    // отвечала «записал» и молча теряла строку — так пропадали объекты мира,
    // сообщения чата, появление игроков и урон. Предел держим сами счётчиком:
    // отставшего клиента отключаем с причиной, потерь без предупреждения нет.
    private readonly Channel<string> _outbox = Channel.CreateUnbounded<string>(
        new UnboundedChannelOptions { SingleReader = true });
    private int _queued;
    private readonly CancellationTokenSource _close = new();
    private readonly object _stateLock = new();
    private NativePlayerState _state;
    private long _stateVersion;
    private NativeVehicleSync _vehicleSync;
    private bool _hasVehicleSync;
    private int _closed;
    private int _closeRequested;

    internal NativeSession(NativeServer owner, uint id, TcpClient client, Stream stream, string ip,
                           string name, ulong identity, ulong hwid, ulong mac, string clientVersion)
    {
        _owner = owner;
        _client = client;
        _stream = stream;
        Id = id;
        Ip = ip;
        Name = name;
        Identity = identity;
        HardwareId = hwid;
        MacHash = mac;
        ClientVersion = clientVersion;
    }

    public uint Id { get; }
    public string Ip { get; }
    public string Name { get; }
    /// <summary>Постоянный ID игрока из его ключа (старший бит выставлен — не пересекается с Social Club).</summary>
    public ulong Identity { get; }
    public ulong HardwareId { get; }
    public ulong MacHash { get; }
    public string ClientVersion { get; }
    public bool Joined => _closed == 0;
    public bool Closing => _closeRequested != 0;
    public string? CloseReason { get; private set; }
    internal CancellationToken CloseToken => _close.Token;

    // --- голос (NativeVoice) -------------------------------------------------
    public ulong VoiceToken { get; internal set; }
    private IPEndPoint? _voiceEndpoint;
    public IPEndPoint? VoiceEndpoint { get => Volatile.Read(ref _voiceEndpoint); internal set => Volatile.Write(ref _voiceEndpoint, value); }
    /// <summary>Голос заглушён администрацией (/vmute) — сервер его не пересылает.</summary>
    public volatile bool VoiceMuted;
    /// <summary>Измерение (виртуальный мир) — голос слышен только в своём.</summary>
    public volatile int Dimension;
    internal long VoiceWindowStart;
    internal int VoicePackets;
    public long LastVoiceMs { get; internal set; }

    public NativePlayerState State { get { lock (_stateLock) return _state; } }
    public long StateVersion => Interlocked.Read(ref _stateVersion);
    public bool HasState => StateVersion > 0;

    internal void SetState(NativePlayerState state)
    {
        lock (_stateLock) _state = state;
        Interlocked.Increment(ref _stateVersion);
    }

    /// <summary>Последний VSYNC водителя (реестр транспорта, клиент 1.0.6+).</summary>
    internal void SetVehicleSync(in NativeVehicleSync sync)
    {
        lock (_stateLock) { _vehicleSync = sync; _hasVehicleSync = true; }
    }

    /// <summary>Забрать последний VSYNC. Промежуточные за тик теряются
    /// намеренно: нужна только свежая позиция машины, как и со STATE.</summary>
    public bool TryTakeVehicleSync(out NativeVehicleSync sync)
    {
        lock (_stateLock)
        {
            sync = _vehicleSync;
            if (!_hasVehicleSync) return false;
            _hasVehicleSync = false;
            return true;
        }
    }

    /// <summary>Сервер сам переместил игрока: пока клиент не пришлёт новое, считаем его там.</summary>
    public void OverridePosition(float x, float y, float z)
    {
        lock (_stateLock) _state = _state with { X = x, Y = y, Z = z };
    }

    public void OverrideHeading(float heading)
    {
        lock (_stateLock) _state = _state with { Heading = heading };
    }

    /// <summary>Сколько строк ещё ждёт отправки: снимок мира шлётся порциями,
    /// чтобы не переполнить очередь и не выкинуть игрока на входе.</summary>
    public int Queued => Volatile.Read(ref _queued);

    /// <summary>Поставить сообщение в очередь отправки. Не блокирует главный поток.</summary>
    public bool Send(string line)
    {
        if (_closed != 0) return false;
        if (Interlocked.Increment(ref _queued) > OutboxCapacity)
        {
            Interlocked.Decrement(ref _queued);
            Close("клиент не успевает принимать данные");
            return false;
        }
        if (_outbox.Writer.TryWrite(line)) return true;
        Interlocked.Decrement(ref _queued);
        return false;
    }

    public void Send(string type, params object?[] fields) => Send(NativeProtocol.Format(type, fields));

    /// <summary>Отключить с причиной: клиент увидит её перед разрывом.</summary>
    public void Close(string reason)
    {
        if (Interlocked.Exchange(ref _closeRequested, 1) != 0) return;
        CloseReason = reason;
        _outbox.Writer.TryWrite(NativeProtocol.Format("KICK", reason));
        _outbox.Writer.TryComplete();
        // Писатель допишет KICK и сам закроет сокет.
        _ = Task.Delay(1500).ContinueWith(_ => { try { _close.Cancel(); } catch { } try { _client.Client.Shutdown(SocketShutdown.Both); } catch { } });
    }

    internal void MarkClosed()
    {
        Interlocked.Exchange(ref _closed, 1);
        _outbox.Writer.TryComplete();
    }

    internal void StartWriter(CancellationToken stop)
    {
        _ = Task.Run(async () =>
        {
            var buffer = new MemoryStream();
            try
            {
                var reader = _outbox.Reader;
                while (await reader.WaitToReadAsync(stop))
                {
                    buffer.SetLength(0);
                    while (buffer.Length < 64 * 1024 && reader.TryRead(out var line))
                    {
                        Interlocked.Decrement(ref _queued);
                        var bytes = Encoding.UTF8.GetBytes(line);
                        buffer.Write(bytes);
                        buffer.WriteByte((byte)'\n');
                    }
                    await _stream.WriteAsync(buffer.GetBuffer().AsMemory(0, (int)buffer.Length), stop);
                }
                if (CloseReason is not null)
                {
                    try { _close.Cancel(); } catch { }
                    try { _client.Client.Shutdown(SocketShutdown.Both); } catch { }
                }
            }
            catch { try { _close.Cancel(); } catch { } }
        });
    }
}
