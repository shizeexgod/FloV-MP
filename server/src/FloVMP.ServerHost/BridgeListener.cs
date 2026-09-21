using System.Net;
using System.Net.Sockets;
using System.Text;

namespace FloVMP.ServerHost;

/// <summary>
/// Минимальная native-boundary точка для аварийного клиента Legacy 3889.
/// Она не подменяет игровой протокол alt:V: принимает только подтверждение
/// того, что native bridge реально загрузился в процессе GTA.
/// </summary>
public sealed class BridgeListener : IDisposable
{
    private readonly TcpListener _listener;
    private readonly Action<string> _log;
    private readonly CancellationTokenSource _stop = new();
    private Task? _acceptLoop;

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
        client.ReceiveTimeout = 3000;
        client.SendTimeout = 3000;
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

            await WriteAsync(stream, "FLOVMP-BRIDGE/1 WELCOME build=3889\n");
            _log("Native bridge Legacy 3889 подключён: " + hello);

            while (!_stop.IsCancellationRequested)
            {
                var message = await reader.ReadLineAsync(_stop.Token);
                if (message is null) break;
                if (message.Equals("FLOVMP-BRIDGE/1 heartbeat", StringComparison.Ordinal))
                {
                    await WriteAsync(stream, "FLOVMP-BRIDGE/1 heartbeat-ack\n");
                    continue;
                }

                await WriteAsync(stream, "FLOVMP-BRIDGE/1 REJECT reason=unknown-message\n");
                _log("Native bridge прислал неизвестное сообщение: " + message);
            }

            _log("Native bridge Legacy 3889 отключён.");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _log("Native bridge connection error: " + ex.Message);
        }
        }
    }

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
        try { _acceptLoop?.Wait(TimeSpan.FromSeconds(2)); } catch { }
        _stop.Dispose();
    }
}
