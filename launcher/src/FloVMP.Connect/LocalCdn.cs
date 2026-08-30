using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace FloVMP.Connect;

/// <summary>
/// Локальная заглушка бэкенда alt:V. Перехватывает запросы клиента (манифесты,
/// skin.bin, branch-access, UI) через TcpListener с поддержкой SO_REUSEADDR,
/// что полностью исключает блокировки HTTP.sys и не требует прав администратора.
/// </summary>
public sealed class LocalCdn : IDisposable
{
    private const string Version = "16.4.39";
    private const string SdkVersion = "c150769";

    private readonly string _clientDir;
    private readonly string? _uiDir;
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private string _clientManifestJson = "{}";

    public int Port { get; }
    public string BaseUrl => $"http://127.0.0.1:{Port}";

    public LocalCdn(string clientDir, int port, string? uiDir = null)
    {
        _clientDir = clientDir;
        _uiDir = uiDir;
        Port = port;

        _listener = new TcpListener(IPAddress.Loopback, port);
        _listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
    }

    public void Start()
    {
        _clientManifestJson = BuildClientManifest();
        _listener.Start();
        _ = Task.Run(AcceptLoopAsync);
        Console.WriteLine($"[cdn] listening on {BaseUrl}");
    }

    private async Task AcceptLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync(_cts.Token);
                _ = Task.Run(() => HandleClientAsync(client));
            }
            catch when (_cts.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[cdn] accept error: {ex.Message}");
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        using (client)
        using (var stream = client.GetStream())
        {
            try
            {
                var buffer = new byte[8192];
                var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, _cts.Token);
                if (bytesRead == 0) return;

                var requestText = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                var firstLine = requestText.Split('\n')[0].Trim();
                var parts = firstLine.Split(' ');
                if (parts.Length < 2) return;

                var method = parts[0];
                var rawPath = parts[1];
                var queryIdx = rawPath.IndexOf('?');
                var path = queryIdx >= 0 ? rawPath.Substring(0, queryIdx) : rawPath;
                var lower = path.ToLowerInvariant();

                var (status, contentType, body) = ProcessRequest(path, lower);
                Console.WriteLine($"[cdn] {method} {path} -> {status}");

                var headers = $"HTTP/1.1 {status} OK\r\n" +
                              $"Content-Type: {contentType}\r\n" +
                              $"Content-Length: {body.Length}\r\n" +
                              $"Access-Control-Allow-Origin: *\r\n" +
                              $"Connection: close\r\n\r\n";

                var headerBytes = Encoding.UTF8.GetBytes(headers);
                await stream.WriteAsync(headerBytes, 0, headerBytes.Length);
                await stream.WriteAsync(body, 0, body.Length);
                await stream.FlushAsync();
            }
            catch
            {
                // client disconnected or aborted request
            }
        }
    }

    private (int Status, string ContentType, byte[] Body) ProcessRequest(string path, string lower)
    {
        if (lower.Contains("/backup/") && lower.EndsWith("update.json"))
        {
            return (200, "application/json", Enc(ManifestFor("backup_update.json", BuildBackupManifest())));
        }
        if (lower.Contains("/backup/"))
        {
            var name = Path.GetFileName(path);
            var f = Path.Combine(_clientDir, "cdn", "backup", name);
            return File.Exists(f)
                ? (200, "application/octet-stream", File.ReadAllBytes(f))
                : (404, "text/plain", "Not Found"u8.ToArray());
        }
        if (lower.Contains("update.json") && lower.Contains("/launcher"))
        {
            return (200, "application/json", Enc(ManifestFor("launcher_update.json", LauncherManifest())));
        }
        if (lower.Contains("update.json") && lower.Contains("/client"))
        {
            return (200, "application/json", Enc(ManifestFor("client_update.json", _clientManifestJson)));
        }
        if (lower.Contains("/skin"))
        {
            return HandleSkin(lower);
        }
        if (lower.StartsWith("/ui/") || lower == "/ui")
        {
            return HandleUi(path);
        }
        if (lower.Contains("branch-access") || lower.Contains("/auth") || lower.Contains("token"))
        {
            return (200, "application/json", Enc("{\"access\": true, \"branches\": [\"release\"]}"));
        }
        if (lower.Contains("client-branches") || lower is "/" or "/w" or "/q")
        {
            return (200, "application/json", Enc($"{{\"release\":\"{Version}\",\"rc\":\"{Version}\",\"dev\":\"{Version}\"}}"));
        }
        if ((lower.Contains("/client/") || lower.Contains("/launcher/")) && !lower.EndsWith(".json"))
        {
            return HandleClientFile(path);
        }

        return (200, "text/plain", "ok"u8.ToArray());
    }

    private string ManifestFor(string name, string generated)
    {
        var f = Path.Combine(_clientDir, "cdn", name);
        return File.Exists(f) ? File.ReadAllText(f) : generated;
    }

    private static string LauncherManifest() =>
        "{\"latestBuildNumber\":-1,\"version\":\"16.3.7\"," +
        "\"hashList\":{\"altv.exe\":\"2800e0d6665cdfa9c02419360db44b5cccf64147\"}," +
        "\"sizeList\":{\"altv.exe\":9267200}}";

    private (int Status, string ContentType, byte[] Body) HandleSkin(string lower)
    {
        var skin = Path.Combine(_clientDir, "cache", "skin.bin");
        if (!File.Exists(skin)) skin = Path.Combine(_clientDir, "skin.bin");

        if (lower.EndsWith("/hash"))
        {
            return File.Exists(skin)
                ? (200, "text/plain", Enc(Sha1(skin)))
                : (404, "text/plain", "Not Found"u8.ToArray());
        }
        if (lower.EndsWith(".json"))
        {
            if (!File.Exists(skin)) return (404, "text/plain", "Not Found"u8.ToArray());
            var json = $"{{\"version\":\"{Version}\",\"hashList\":{{\"skin.bin\":\"{Sha1(skin)}\"}}," +
                       $"\"sizeList\":{{\"skin.bin\":{new FileInfo(skin).Length}}}}}";
            return (200, "application/json", Enc(json));
        }
        return File.Exists(skin)
            ? (200, "application/octet-stream", File.ReadAllBytes(skin))
            : (404, "text/plain", "Not Found"u8.ToArray());
    }

    private (int Status, string ContentType, byte[] Body) HandleUi(string path)
    {
        if (_uiDir is null) return (200, "text/html", Enc("<!doctype html><title>FloV:MP</title>"));
        var rel = path.Length > 4 ? path[4..] : "index.html";
        if (string.IsNullOrEmpty(rel)) rel = "index.html";
        var file = Path.GetFullPath(Path.Combine(_uiDir, rel));
        if (!file.StartsWith(Path.GetFullPath(_uiDir), StringComparison.OrdinalIgnoreCase) || !File.Exists(file))
        {
            return (404, "text/plain", "Not Found"u8.ToArray());
        }
        return (200, ContentType(file), File.ReadAllBytes(file));
    }

    private (int Status, string ContentType, byte[] Body) HandleClientFile(string path)
    {
        var marker = "x64_win32/";
        var i = path.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        string rel = i >= 0 ? path[(i + marker.Length)..] : Path.GetFileName(path);
        rel = rel.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);

        var file = Path.GetFullPath(Path.Combine(_clientDir, rel));
        if (!file.StartsWith(Path.GetFullPath(_clientDir), StringComparison.OrdinalIgnoreCase) || !File.Exists(file))
        {
            var byName = Directory.EnumerateFiles(_clientDir, Path.GetFileName(rel), SearchOption.AllDirectories).FirstOrDefault();
            if (byName is null) return (404, "text/plain", "Not Found"u8.ToArray());
            file = byName;
        }
        return (200, "application/octet-stream", File.ReadAllBytes(file));
    }

    private string BuildClientManifest()
    {
        var hashes = new StringBuilder();
        var sizes = new StringBuilder();
        var first = true;
        foreach (var f in Directory.EnumerateFiles(_clientDir, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(_clientDir, f).Replace('\\', '/');
            if (rel is "update.json" or "manifest.json") continue;
            if (rel.StartsWith("cache/") || rel.StartsWith("logs/") || rel.StartsWith("backup/") || rel.StartsWith("cdn/")) continue;

            if (!first) { hashes.Append(','); sizes.Append(','); }
            first = false;
            hashes.Append('"').Append(rel).Append("\":\"").Append(Sha1(f)).Append('"');
            sizes.Append('"').Append(rel).Append("\":").Append(new FileInfo(f).Length);
        }
        return $"{{\"latestBuildNumber\":-1,\"version\":\"{Version}\",\"sdkVersion\":\"{SdkVersion}\"," +
               $"\"hashList\":{{{hashes}}},\"sizeList\":{{{sizes}}}}}";
    }

    private string BuildBackupManifest()
    {
        var backupDir = Path.Combine(_clientDir, "cdn", "backup");
        if (!Directory.Exists(backupDir)) return "{\"files\":[]}";

        var files = new StringBuilder();
        var first = true;
        foreach (var f in Directory.EnumerateFiles(backupDir, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(backupDir, f).Replace('\\', '/');
            if (rel is "backup_update.json") continue;

            if (!first) files.Append(',');
            first = false;

            // Формат backup_update.json у alt:V немного другой, это массив объектов.
            files.Append($"{{\"name\":\"{rel}\",\"hash\":\"{Sha1(f)}\",\"size\":{new FileInfo(f).Length}}}");
        }
        return $"{{\"files\":[{files}]}}";
    }

    private static byte[] Enc(string s) => Encoding.UTF8.GetBytes(s);

    private static string Sha1(string file)
    {
        using var fs = File.OpenRead(file);
        return Convert.ToHexString(SHA1.HashData(fs)).ToLowerInvariant();
    }

    private static string ContentType(string file) => Path.GetExtension(file).ToLowerInvariant() switch
    {
        ".html" => "text/html; charset=utf-8",
        ".js" => "application/javascript",
        ".css" => "text/css",
        ".svg" => "image/svg+xml",
        ".json" => "application/json",
        _ => "application/octet-stream",
    };

    public void Dispose()
    {
        _cts.Cancel();
        try { _listener.Stop(); } catch { }
        _cts.Dispose();
    }
}
