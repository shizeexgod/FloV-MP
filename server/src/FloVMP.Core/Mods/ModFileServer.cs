using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace FloVMP.Core.Mods;

/// <summary>
/// Раздача модов игрокам с самого игрового сервера (mods.http_port).
///
///   GET /mods/manifest.json        — список (<see cref="ModManifest"/>)
///   GET /mods/files/&lt;путь&gt;         — файл; Range: bytes=N- для докачки
///
/// Свой HTTP на TcpListener, как у страницы метрик: HttpListener на Windows
/// требует прав администратора. Отдаётся только то, что есть в текущем
/// списке, — путь из запроса ищется в словаре, а не склеивается с папкой,
/// поэтому «../» выйти за server/mods не может. Ограничения: подключений на
/// адрес и всего, размер заголовков, простой соединения.
///
/// Для больших карт и сотен игроков лучше отдельный CDN (mods.public_url):
/// тогда этот сервер можно не включать вовсе.
/// </summary>
public sealed class ModFileServer : IDisposable
{
    public const int MaxConnectionsPerIp = 8;
    public const int MaxConnections = 256;
    private const int MaxHeaderBytes = 8192;
    private const int MaxRequestsPerConnection = 1000;

    private readonly TcpListener _listener;
    private readonly Func<ModManifest> _manifest;
    private readonly string _root;
    private readonly Action<string> _log;
    private readonly CancellationTokenSource _stop = new();
    private readonly ConcurrentDictionary<IPAddress, int> _perIp = new();
    private int _total;

    /// <summary>Отдано байт с запуска — для журнала и метрик.</summary>
    public long BytesServed => Interlocked.Read(ref _bytesServed);
    private long _bytesServed;

    public ModFileServer(IPAddress address, int port, string root, Func<ModManifest> manifest, Action<string> log)
    {
        _listener = new TcpListener(address, port);
        _root = Path.GetFullPath(root);
        _manifest = manifest;
        _log = log;
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
            var ip = (client.Client.RemoteEndPoint as IPEndPoint)?.Address ?? IPAddress.None;
            // Оба счётчика всегда увеличиваются и уменьшаются парой.
            var total = Interlocked.Increment(ref _total);
            var fromIp = _perIp.AddOrUpdate(ip, 1, (_, n) => n + 1);
            if (total > MaxConnections || fromIp > MaxConnectionsPerIp)
            {
                Release(ip);
                client.Dispose();
                continue;
            }
            _ = Task.Run(async () =>
            {
                try { await Serve(client); }
                finally { Release(ip); client.Dispose(); }
            });
        }
    }

    private void Release(IPAddress ip)
    {
        Interlocked.Decrement(ref _total);
        _perIp.AddOrUpdate(ip, 0, (_, n) => Math.Max(0, n - 1));
    }

    private async Task Serve(TcpClient client)
    {
        client.NoDelay = true;
        var stream = client.GetStream();
        var buffer = new byte[MaxHeaderBytes];
        var carry = 0;
        for (var served = 0; served < MaxRequestsPerConnection && !_stop.IsCancellationRequested; served++)
        {
            using var idle = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
            idle.CancelAfter(TimeSpan.FromSeconds(15));
            int end;
            try
            {
                // Заголовки запроса целиком (до пустой строки), не больше 8 КБ.
                while ((end = IndexOfHeaderEnd(buffer, carry)) < 0)
                {
                    if (carry == buffer.Length) return;
                    var n = await stream.ReadAsync(buffer.AsMemory(carry), idle.Token);
                    if (n == 0) return;
                    carry += n;
                }
            }
            catch { return; }

            var head = Encoding.ASCII.GetString(buffer, 0, end);
            // Тело у GET/HEAD не бывает: всё после заголовков — следующий запрос.
            var rest = carry - (end + 4);
            Buffer.BlockCopy(buffer, end + 4, buffer, 0, rest);
            carry = rest;

            var request = HttpRequestLine.Parse(head);
            var plan = Plan(request);
            try
            {
                using var send = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
                // Сервер не должен вечно держать поток ради игрока, который не читает.
                send.CancelAfter(TimeSpan.FromMinutes(30));
                await stream.WriteAsync(Encoding.ASCII.GetBytes(plan.Head), send.Token);
                if (plan.Body is { } body && !request.Head) await stream.WriteAsync(body, send.Token);
                if (plan.FilePath is { } path && !request.Head && plan.Length > 0)
                {
                    await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16, FileOptions.Asynchronous | FileOptions.SequentialScan);
                    fs.Seek(plan.Offset, SeekOrigin.Begin);
                    var chunk = new byte[1 << 16];
                    var left = plan.Length;
                    while (left > 0)
                    {
                        var n = await fs.ReadAsync(chunk.AsMemory(0, (int)Math.Min(chunk.Length, left)), send.Token);
                        if (n == 0) return;   // файл укоротили под ногами — рвём, клиент докачает
                        await stream.WriteAsync(chunk.AsMemory(0, n), send.Token);
                        left -= n;
                        Interlocked.Add(ref _bytesServed, n);
                    }
                }
            }
            catch { return; }
            if (!plan.KeepAlive || !request.KeepAlive) return;
        }
    }

    private static int IndexOfHeaderEnd(byte[] b, int count)
    {
        for (var i = 3; i < count; i++)
            if (b[i - 3] == '\r' && b[i - 2] == '\n' && b[i - 1] == '\r' && b[i] == '\n') return i - 3;
        return -1;
    }

    /// <summary>Что ответить — без сети, чтобы проверяли тесты.</summary>
    public ResponsePlan Plan(HttpRequestLine r)
    {
        if (!r.Valid) return ResponsePlan.Text("400 Bad Request", "неверный запрос\n", keepAlive: false);
        if (r.Method != "GET" && r.Method != "HEAD") return ResponsePlan.Text("405 Method Not Allowed", "только GET и HEAD\n", keepAlive: false);
        var m = _manifest();
        if (r.Path == "/mods/manifest.json")
            return ResponsePlan.Bytes("200 OK", "application/json; charset=utf-8", Encoding.UTF8.GetBytes(m.ToJson()), "no-cache");
        const string prefix = "/mods/files/";
        if (!r.Path.StartsWith(prefix, StringComparison.Ordinal))
            return ResponsePlan.Text("404 Not Found", "есть: /mods/manifest.json, /mods/files/<путь>\n");
        string rel;
        try { rel = Uri.UnescapeDataString(r.Path[prefix.Length..]); }
        catch { return ResponsePlan.Text("400 Bad Request", "неверный путь\n"); }
        if (!m.TryGet(rel, out var file)) return ResponsePlan.Text("404 Not Found", "такого файла в модах сервера нет\n");
        var full = Path.Combine(_root, rel.Replace('/', Path.DirectorySeparatorChar));
        long size;
        try { size = new FileInfo(full).Length; }
        catch { return ResponsePlan.Text("404 Not Found", "файл пропал — владелец обновляет моды\n"); }
        if (size != file.Size) return ResponsePlan.Text("503 Service Unavailable", "файл меняется — повторите позже\n");

        // ETag — SHA-256 из списка: если файл поменялся между кусками докачки,
        // клиент увидит другой ETag и начнёт его заново.
        var etag = "\"" + file.Sha256 + "\"";
        if (r.RangeStart is { } start)
        {
            if (r.IfRange is { } ifRange && ifRange != etag)
                return ResponsePlan.FromFile("200 OK", full, 0, size, size, etag, partial: false);
            if (start >= size) return ResponsePlan.Text("416 Range Not Satisfiable", "", extra: $"Content-Range: bytes */{size}\r\n");
            var last = r.RangeEnd is { } e && e < size ? e : size - 1;
            if (last < start) return ResponsePlan.Text("416 Range Not Satisfiable", "", extra: $"Content-Range: bytes */{size}\r\n");
            return ResponsePlan.FromFile("206 Partial Content", full, start, last - start + 1, size, etag, partial: true);
        }
        return ResponsePlan.FromFile("200 OK", full, 0, size, size, etag, partial: false);
    }

    public void Dispose()
    {
        _stop.Cancel();
        try { _listener.Stop(); } catch { }
    }
}

/// <summary>Нужное из запроса: метод, путь, диапазон, keep-alive.</summary>
public sealed record HttpRequestLine(bool Valid, string Method, string Path, long? RangeStart, long? RangeEnd, string? IfRange, bool KeepAlive, bool Head)
{
    public static HttpRequestLine Parse(string head)
    {
        var lines = head.Split("\r\n");
        var first = lines[0].Split(' ');
        if (first.Length != 3 || !first[2].StartsWith("HTTP/1.", StringComparison.Ordinal))
            return new(false, "", "", null, null, null, false, false);
        long? start = null, end = null;
        string? ifRange = null;
        var keepAlive = first[2] == "HTTP/1.1";
        foreach (var line in lines.Skip(1))
        {
            var colon = line.IndexOf(':');
            if (colon <= 0) continue;
            var name = line[..colon].Trim();
            var value = line[(colon + 1)..].Trim();
            if (name.Equals("Range", StringComparison.OrdinalIgnoreCase) && value.StartsWith("bytes=", StringComparison.Ordinal))
            {
                // Только один диапазон «N-» или «N-M» — загрузчикам больше не нужно.
                var spec = value[6..];
                var dash = spec.IndexOf('-');
                if (dash > 0 && !spec.Contains(',') && long.TryParse(spec[..dash], out var s) && s >= 0)
                {
                    start = s;
                    if (dash < spec.Length - 1 && long.TryParse(spec[(dash + 1)..], out var e2) && e2 >= s) end = e2;
                }
            }
            else if (name.Equals("If-Range", StringComparison.OrdinalIgnoreCase)) ifRange = value;
            else if (name.Equals("Connection", StringComparison.OrdinalIgnoreCase))
                keepAlive = !value.Equals("close", StringComparison.OrdinalIgnoreCase) &&
                            (keepAlive || value.Equals("keep-alive", StringComparison.OrdinalIgnoreCase));
        }
        var path = first[1].Split('?', 2)[0];
        return new(true, first[0], path, start, end, ifRange, keepAlive, first[0] == "HEAD");
    }
}

/// <summary>Ответ: заголовки плюс тело из памяти или кусок файла.</summary>
public sealed record ResponsePlan(string Status, string Head, byte[]? Body, string? FilePath, long Offset, long Length, bool KeepAlive)
{
    private static string Common(string status, string type, long length, bool keepAlive, string extra) =>
        $"HTTP/1.1 {status}\r\nContent-Type: {type}\r\nContent-Length: {length}\r\nAccept-Ranges: bytes\r\n{extra}" +
        $"Connection: {(keepAlive ? "keep-alive" : "close")}\r\n\r\n";

    public static ResponsePlan Text(string status, string text, bool keepAlive = true, string extra = "")
    {
        var body = Encoding.UTF8.GetBytes(text);
        return new(status, Common(status, "text/plain; charset=utf-8", body.Length, keepAlive, extra), body, null, 0, 0, keepAlive);
    }

    public static ResponsePlan Bytes(string status, string type, byte[] body, string cache) =>
        new(status, Common(status, type, body.Length, true, $"Cache-Control: {cache}\r\n"), body, null, 0, 0, true);

    public static ResponsePlan FromFile(string status, string path, long offset, long length, long size, string etag, bool partial)
    {
        var extra = $"ETag: {etag}\r\n" + (partial ? $"Content-Range: bytes {offset}-{offset + length - 1}/{size}\r\n" : "");
        return new(status, Common(status, "application/octet-stream", length, true, extra), null, path, offset, length, true);
    }
}
