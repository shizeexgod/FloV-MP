using System.Net;
using System.Net.Sockets;
using System.Text;

namespace FloVMP.Core.Diagnostics;

/// <summary>
/// Страница метрик для внешнего мониторинга (metrics.http_port): /metrics —
/// JSON, /metrics.prom — текст Prometheus (Grafana, Uptime Kuma, Zabbix).
///
/// Свой маленький HTTP на TcpListener, а не HttpListener: тот на Windows
/// требует прав администратора на любой адрес, кроме localhost. Отвечает
/// только на GET, запрос не длиннее 4 КБ, ответ — последний готовый снимок
/// (главный поток не трогается). Наружу (не 127.0.0.1) без токена не
/// слушает: метрики выдают онлайн и нагрузку сервера кому угодно.
/// </summary>
public sealed class MetricsHttpServer : IDisposable
{
    private readonly TcpListener _listener;
    private readonly Func<MetricsSnapshot?> _snapshot;
    private readonly string _token;
    private readonly CancellationTokenSource _stop = new();

    public MetricsHttpServer(IPAddress address, int port, string token, Func<MetricsSnapshot?> snapshot)
    {
        if (!IPAddress.IsLoopback(address) && string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("страница метрик наружу без metrics.token не запускается");
        _listener = new TcpListener(address, port);
        _token = token ?? "";
        _snapshot = snapshot;
    }

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    public void Start()
    {
        _listener.Start();
        _ = Task.Run(AcceptLoop);
    }

    private async Task AcceptLoop()
    {
        while (!_stop.IsCancellationRequested)
        {
            TcpClient client;
            try { client = await _listener.AcceptTcpClientAsync(_stop.Token); }
            catch { break; }
            _ = Task.Run(() => Serve(client));
        }
    }

    private async Task Serve(TcpClient client)
    {
        using (client)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
                cts.CancelAfter(TimeSpan.FromSeconds(5));
                var stream = client.GetStream();
                var buffer = new byte[4096];
                var read = 0;
                while (read < buffer.Length)
                {
                    var n = await stream.ReadAsync(buffer.AsMemory(read), cts.Token);
                    if (n == 0) break;
                    read += n;
                    if (Encoding.ASCII.GetString(buffer, 0, read).Contains("\r\n\r\n")) break;
                }
                var request = Encoding.ASCII.GetString(buffer, 0, read);
                var (status, type, body) = Route(request);
                var bytes = Encoding.UTF8.GetBytes(body);
                var head = $"HTTP/1.1 {status}\r\nContent-Type: {type}\r\nContent-Length: {bytes.Length}\r\n" +
                           "Cache-Control: no-store\r\nConnection: close\r\n\r\n";
                await stream.WriteAsync(Encoding.ASCII.GetBytes(head), cts.Token);
                await stream.WriteAsync(bytes, cts.Token);
            }
            catch { /* оборванный запрос мониторинга — не наша забота */ }
        }
    }

    /// <summary>Разбор запроса отдельно от сети — чтобы его проверяли тесты.</summary>
    public (string Status, string ContentType, string Body) Route(string request)
    {
        var line = request.Split("\r\n", 2)[0].Split(' ');
        if (line.Length < 2 || line[0] != "GET") return ("405 Method Not Allowed", "text/plain; charset=utf-8", "только GET\n");
        var target = line[1];
        var path = target.Split('?', 2)[0];
        if (_token.Length > 0 && !Authorized(request, target)) return ("401 Unauthorized", "text/plain; charset=utf-8", "нужен токен\n");
        var s = _snapshot();
        if (s is null) return ("503 Service Unavailable", "text/plain; charset=utf-8", "метрики ещё не собраны\n");
        return path switch
        {
            "/metrics" or "/metrics.json" => ("200 OK", "application/json; charset=utf-8", s.ToJson()),
            "/metrics.prom" => ("200 OK", "text/plain; version=0.0.4; charset=utf-8", s.ToPrometheus()),
            "/" or "/health" => ("200 OK", "text/plain; charset=utf-8", "ok " + s.ToLogLine() + "\n"),
            _ => ("404 Not Found", "text/plain; charset=utf-8", "есть: /metrics, /metrics.prom, /health\n"),
        };
    }

    private bool Authorized(string request, string target)
    {
        var q = target.Split('?', 2);
        if (q.Length == 2)
            foreach (var pair in q[1].Split('&'))
                if (pair.StartsWith("token=", StringComparison.Ordinal) &&
                    FixedEquals(Uri.UnescapeDataString(pair["token=".Length..]), _token)) return true;
        foreach (var header in request.Split("\r\n"))
            if (header.StartsWith("Authorization:", StringComparison.OrdinalIgnoreCase) &&
                FixedEquals(header["Authorization:".Length..].Trim(), "Bearer " + _token)) return true;
        return false;
    }

    // Сравнение без раннего выхода: токен не подбирается по времени ответа.
    private static bool FixedEquals(string a, string b) =>
        System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));

    public void Dispose()
    {
        _stop.Cancel();
        try { _listener.Stop(); } catch { }
    }
}
