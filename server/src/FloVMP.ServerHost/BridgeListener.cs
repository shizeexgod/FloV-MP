using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace FloVMP.ServerHost;

/// <summary>
/// Minimal transport for the native Legacy 3889 client. This is deliberately
/// separate from alt:V's wire protocol: the native client is an independent
/// game-thread adapter and must not pretend to be an alt:V client.
/// </summary>
public sealed class BridgeListener : IDisposable
{
    private readonly TcpListener _listener;
    private readonly Action<string> _log;
    private readonly CancellationTokenSource _stop = new();
    private readonly ConcurrentDictionary<int, BridgeSession> _sessions = new();
    private Task? _acceptLoop;
    private int _nextId;

    public BridgeListener(int port, Action<string> log)
    {
        _listener = new TcpListener(IPAddress.Any, port);
        _log = log;
    }

    public void Start()
    {
        _listener.Start();
        _acceptLoop = Task.Run(AcceptLoopAsync);
        _log("Native bridge endpoint запущен на TCP 7798 (Legacy 3889).");
    }

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    private async Task AcceptLoopAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(_stop.Token);
            }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException) { break; }
            catch (SocketException) when (_stop.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                _log("Native bridge accept error: " + ex.Message);
                continue;
            }

            _ = Task.Run(() => HandleAsync(client), _stop.Token);
        }
    }

    private async Task HandleAsync(TcpClient client)
    {
        using (client)
        {
            client.ReceiveTimeout = 5000;
            client.SendTimeout = 5000;
            BridgeSession? session = null;
            try
            {
                await using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.UTF8, false, 256, leaveOpen: true);
                var hello = await reader.ReadLineAsync(_stop.Token);
                if (hello is null || !hello.StartsWith("FLOVMP-BRIDGE/1 hello build=3889 ", StringComparison.Ordinal))
                {
                    await WriteAsync(stream, "FLOVMP-BRIDGE/1 REJECT reason=unsupported-hello\n");
                    _log("Отклонён native bridge с неподдерживаемым hello.");
                    return;
                }

                if (!hello.Contains("version=1.0.3889.0", StringComparison.Ordinal))
                {
                    await WriteAsync(stream, "FLOVMP-BRIDGE/1 REJECT reason=unsupported-game-version\n");
                    _log("Отклонён native bridge: GTA имеет не Legacy 1.0.3889.0.");
                    return;
                }

                session = new BridgeSession(Interlocked.Increment(ref _nextId), stream);
                _sessions[session.Id] = session;
                await session.SendAsync($"FLOVMP-BRIDGE/1 WELCOME id={session.Id} build=3889\n", _stop.Token);
                foreach (var existing in _sessions.Values.Where(item => item.Id != session.Id))
                {
                    await session.SendAsync(FormatState(existing.Id, existing.LastState), _stop.Token);
                    await existing.SendAsync(FormatState(session.Id, session.LastState), _stop.Token);
                }

                _log($"Native bridge Legacy 3889 подключён: id={session.Id}, {hello}");
                while (!_stop.IsCancellationRequested)
                {
                    var message = await reader.ReadLineAsync(_stop.Token);
                    if (message is null) break;
                    if (message.Equals("FLOVMP-BRIDGE/1 heartbeat", StringComparison.Ordinal))
                    {
                        await session.SendAsync("FLOVMP-BRIDGE/1 heartbeat-ack\n", _stop.Token);
                        continue;
                    }

                    if (!TryParseState(message, out var state))
                    {
                        await session.SendAsync("FLOVMP-BRIDGE/1 REJECT reason=unknown-message\n", _stop.Token);
                        _log($"Native bridge id={session.Id} прислал неизвестное сообщение: {message}");
                        continue;
                    }

                    session.LastState = state;
                    var payload = FormatState(session.Id, state);
                    foreach (var recipient in _sessions.Values.Where(item => item.Id != session.Id))
                        await recipient.SendAsync(payload, _stop.Token);
                }
            }
            catch (OperationCanceledException) { }
            catch (IOException) { }
            catch (SocketException) { }
            catch (Exception ex)
            {
                _log("Native bridge connection error: " + ex.Message);
            }
            finally
            {
                if (session is not null && _sessions.TryRemove(session.Id, out _))
                {
                    foreach (var recipient in _sessions.Values)
                        _ = recipient.SendAsync($"FLOVMP-BRIDGE/1 despawn id={session.Id}\n", CancellationToken.None);
                    session.Dispose();
                    _log($"Native bridge Legacy 3889 отключён: id={session.Id}.");
                }
            }
        }
    }

    private static bool TryParseState(string message, out BridgeState state)
    {
        state = default;
        var parts = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3 || parts[0] != "FLOVMP-BRIDGE/1" || parts[1] != "state") return false;
        var values = new Dictionary<string, float>(StringComparer.Ordinal);
        foreach (var part in parts.Skip(2))
        {
            var separator = part.IndexOf('=');
            if (separator <= 0 || !float.TryParse(part[(separator + 1)..], NumberStyles.Float,
                CultureInfo.InvariantCulture, out var value)) return false;
            values[part[..separator]] = value;
        }
        if (!values.TryGetValue("x", out var x) || !values.TryGetValue("y", out var y) ||
            !values.TryGetValue("z", out var z) || !values.TryGetValue("heading", out var heading)) return false;
        state = new BridgeState(x, y, z, heading);
        return true;
    }

    private static string FormatState(int id, BridgeState state) =>
        string.Create(CultureInfo.InvariantCulture,
            $"FLOVMP-BRIDGE/1 state id={id} x={state.X:0.###} y={state.Y:0.###} z={state.Z:0.###} heading={state.Heading:0.###}\n");

    private static async Task WriteAsync(NetworkStream stream, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        await stream.WriteAsync(bytes);
        await stream.FlushAsync();
    }

    public void Dispose()
    {
        _stop.Cancel();
        _listener.Stop();
        foreach (var session in _sessions.Values) session.Dispose();
        try { _acceptLoop?.Wait(TimeSpan.FromSeconds(2)); } catch { }
        _stop.Dispose();
    }

    private readonly record struct BridgeState(float X, float Y, float Z, float Heading);

    private sealed class BridgeSession : IDisposable
    {
        private readonly NetworkStream _stream;
        private readonly SemaphoreSlim _writeLock = new(1, 1);

        public BridgeSession(int id, NetworkStream stream)
        {
            Id = id;
            _stream = stream;
        }

        public int Id { get; }
        public BridgeState LastState { get; set; }

        public async Task SendAsync(string message, CancellationToken cancellationToken)
        {
            await _writeLock.WaitAsync(cancellationToken);
            try { await WriteAsync(_stream, message); }
            finally { _writeLock.Release(); }
        }

        public void Dispose()
        {
            _writeLock.Dispose();
            _stream.Dispose();
        }
    }
}
