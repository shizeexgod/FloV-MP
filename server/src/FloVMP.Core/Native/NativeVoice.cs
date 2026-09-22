using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;

namespace FloVMP.Core.Native;

/// <summary>
/// Голосовой чат клиентов b3889: UDP-ретранслятор с учётом расстояния.
///
/// Клиент кодирует микрофон в Opus (20 мс кадры) и шлёт на тот же порт, что
/// и TCP-шлюз, но по UDP:
///   клиент → сервер:  token(8) seq(2) opus(0..MaxPayload)   (пустой — «я здесь», для NAT)
///   сервер → клиент:  senderId(4) seq(2) opus(...)
/// Токен выдаётся в WELCOME и связывает UDP-адрес с сессией: подделать чужой
/// голос, не зная токена, нельзя. Сервер пересылает голос только тем, кто в
/// радиусе и в том же измерении; заглушённых (/vmute) не пересылает вовсе.
/// </summary>
public sealed class NativeVoice : IDisposable
{
    public const int TokenBytes = 8;
    public const int MaxPayload = 400;
    public const int MaxPacketsPerSecond = 80; // 50 кадров/с + запас
    private const int HeaderIn = TokenBytes + 2;
    private const int HeaderOut = 6;

    private readonly UdpClient _udp;
    private readonly Action<string> _log;
    private readonly Func<IEnumerable<NativeSession>> _sessions;
    private readonly ConcurrentDictionary<ulong, NativeSession> _byToken = new();
    // Кого слышно рядом: клетки размером с радиус слышимости. Иначе на каждый
    // голосовой пакет пришлось бы перебирать весь онлайн (50 пакетов в секунду
    // от каждого говорящего — при тысяче игроков это миллионы проверок).
    private readonly Dictionary<(int X, int Y), List<NativeSession>> _cells = new();
    private long _cellsBuiltMs;
    private const int CellRebuildMs = 200;
    private readonly CancellationTokenSource _stop = new();
    private Task? _loop;

    /// <summary>Радиус слышимости, м.</summary>
    public float Radius { get; set; } = 25f;

    public NativeVoice(IPAddress address, int port, Func<IEnumerable<NativeSession>> sessions, Action<string> log)
    {
        _udp = new UdpClient(new IPEndPoint(address, port));
        // ICMP «порт недоступен» от ушедшего клиента не должен ронять приём (Windows).
        if (OperatingSystem.IsWindows())
        {
            const int SIO_UDP_CONNRESET = -1744830452;
            try { _udp.Client.IOControl(SIO_UDP_CONNRESET, new byte[] { 0 }, null); } catch { }
        }
        _sessions = sessions;
        _log = log;
    }

    public int Port => ((IPEndPoint)_udp.Client.LocalEndPoint!).Port;

    public void Start() => _loop = Task.Run(ReceiveLoopAsync);

    /// <summary>Выдать сессии токен голоса (уходит клиенту в WELCOME).</summary>
    public string Register(NativeSession session)
    {
        ulong token;
        do token = BinaryPrimitives.ReadUInt64LittleEndian(RandomNumberGenerator.GetBytes(8));
        while (token == 0 || !_byToken.TryAdd(token, session));
        session.VoiceToken = token;
        return token.ToString("X16");
    }

    public void Unregister(NativeSession session)
    {
        if (session.VoiceToken != 0) _byToken.TryRemove(session.VoiceToken, out _);
        session.VoiceEndpoint = null;
    }

    private async Task ReceiveLoopAsync()
    {
        var outBuf = new byte[HeaderOut + MaxPayload];
        while (!_stop.IsCancellationRequested)
        {
            UdpReceiveResult r;
            try { r = await _udp.ReceiveAsync(_stop.Token); }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException) { break; }
            catch (SocketException) { continue; }

            try { Handle(r.Buffer, r.RemoteEndPoint, outBuf); }
            catch (Exception ex) { _log("голос: " + ex.Message); }
        }
    }

    private void Handle(byte[] data, IPEndPoint from, byte[] outBuf)
    {
        if (data.Length < HeaderIn || data.Length > HeaderIn + MaxPayload) return;
        var token = BinaryPrimitives.ReadUInt64LittleEndian(data);
        if (!_byToken.TryGetValue(token, out var sender) || !sender.Joined || sender.Closing) return;
        // Адрес — только с того же IP, что TCP-сессия: токен, утёкший в чужую сеть, бесполезен.
        if (from.Address.MapToIPv4().ToString() != sender.Ip && !IPAddress.IsLoopback(from.Address)) return;
        sender.VoiceEndpoint = from;

        var payload = data.Length - HeaderIn;
        if (payload == 0 || sender.VoiceMuted || !sender.HasState) return; // «я здесь»

        var now = Environment.TickCount64;
        if (now - sender.VoiceWindowStart > 1000) { sender.VoiceWindowStart = now; sender.VoicePackets = 0; }
        if (++sender.VoicePackets > MaxPacketsPerSecond) return;
        sender.LastVoiceMs = now;

        BinaryPrimitives.WriteUInt32LittleEndian(outBuf, sender.Id);
        outBuf[4] = data[TokenBytes];
        outBuf[5] = data[TokenBytes + 1];
        Buffer.BlockCopy(data, HeaderIn, outBuf, HeaderOut, payload);
        var packet = new ReadOnlySpan<byte>(outBuf, 0, HeaderOut + payload);

        var s = sender.State;
        var r2 = Radius * Radius;
        RebuildCells(now);
        var cell = Radius > 1f ? Radius : 1f;
        var cx = (int)MathF.Floor(s.X / cell);
        var cy = (int)MathF.Floor(s.Y / cell);
        for (var ox = -1; ox <= 1; ox++)
        for (var oy = -1; oy <= 1; oy++)
        {
            if (!_cells.TryGetValue((cx + ox, cy + oy), out var near)) continue;
            foreach (var listener in near)
            {
                if (listener.Id == sender.Id || listener.VoiceEndpoint is null || !listener.HasState) continue;
                if (listener.Dimension != sender.Dimension || !listener.Joined) continue;
                var l = listener.State;
                var dx = l.X - s.X; var dy = l.Y - s.Y; var dz = l.Z - s.Z;
                if (dx * dx + dy * dy + dz * dz > r2) continue;
                try { _udp.Send(packet, listener.VoiceEndpoint); } catch (SocketException) { }
            }
        }
    }

    /// <summary>Раскладка слушателей по клеткам; зовётся из потока приёма, поэтому без блокировок.</summary>
    private void RebuildCells(long now)
    {
        if (now - _cellsBuiltMs < CellRebuildMs) return;
        _cellsBuiltMs = now;
        foreach (var list in _cells.Values) list.Clear();
        var cell = Radius > 1f ? Radius : 1f;
        foreach (var listener in _sessions())
        {
            if (listener.VoiceEndpoint is null || !listener.HasState || !listener.Joined) continue;
            var st = listener.State;
            var key = ((int)MathF.Floor(st.X / cell), (int)MathF.Floor(st.Y / cell));
            if (!_cells.TryGetValue(key, out var bucket)) _cells[key] = bucket = new List<NativeSession>();
            bucket.Add(listener);
        }
    }

    public void Dispose()
    {
        _stop.Cancel();
        _udp.Dispose();
        try { _loop?.Wait(TimeSpan.FromSeconds(1)); } catch { }
    }
}
