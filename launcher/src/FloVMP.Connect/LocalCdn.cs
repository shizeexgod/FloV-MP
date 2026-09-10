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
                // Читаем HTTP-запрос до конца первой строки (GET /path HTTP/1.1\r\n)
                // или до 64 КБ — чтобы длинные заголовки alt:V клиента не обрезали URL.
                var buffer = new byte[65536];
                int totalRead = 0;
                bool foundFirstLine = false;
                while (totalRead < buffer.Length)
                {
                    var bytesRead = await stream.ReadAsync(buffer, totalRead, buffer.Length - totalRead, _cts.Token);
                    if (bytesRead == 0) break;
                    totalRead += bytesRead;
                    // Нам достаточно первой строки — ищем \n
                    for (int j = totalRead - bytesRead; j < totalRead; j++)
                    {
                        if (buffer[j] == (byte)'\n') { foundFirstLine = true; break; }
                    }
                    if (foundFirstLine) break;
                }
                if (totalRead == 0) return;

                var requestText = Encoding.ASCII.GetString(buffer, 0, totalRead);
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

                var statusReason = status switch
                {
                    200 => "OK",
                    404 => "Not Found",
                    500 => "Internal Server Error",
                    _ => "OK"
                };

                var headers = $"HTTP/1.1 {status} {statusReason}\r\n" +
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
        if (lower.Contains("/rpc_update"))
        {
            return (200, "text/plain", "OK"u8.ToArray());
        }
        if (lower.Contains("/backup/") && lower.EndsWith("update.json"))
        {
            return (200, "application/json", Enc(ManifestFor("backup_update.json", BuildBackupManifest())));
        }
        if (lower.Contains("/backup/"))
        {
            var name = Path.GetFileName(path);
            var f = Path.Combine(_clientDir, "cdn", "backup", name);
            if (!File.Exists(f)) f = Path.Combine(_clientDir, "backup", name);
            return File.Exists(f)
                ? (200, "application/octet-stream", File.ReadAllBytes(f))
                : (404, "text/plain", "Not Found"u8.ToArray());
        }
        if (lower.Contains("update.json") && lower.Contains("launcher"))
        {
            return (200, "application/json", Enc(ManifestFor("launcher_update.json", LauncherManifest())));
        }
        if (lower.Contains("update.json") && (lower.Contains("/client") || lower.Contains("update_release")))
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

        // По умолчанию возвращаем валидный JSON, чтобы C++ JSON-парсер alt:V никогда не падал
        return (200, "application/json", "{}"u8.ToArray());
    }

    private string ManifestFor(string name, string generated)
    {
        var f = Path.Combine(_clientDir, "cdn", name);
        return File.Exists(f) ? File.ReadAllText(f) : generated;
    }

    private string LauncherManifest()
    {
        var f = Path.Combine(_clientDir, "cdn", "launcher_update.json");
        if (File.Exists(f)) return File.ReadAllText(f);

        var altv = Path.Combine(_clientDir, "altv.exe");
        if (!File.Exists(altv)) altv = Path.Combine(_clientDir, "altv.exe.bak");
        var hash = File.Exists(altv) ? Sha1(altv) : "40eb9c68ba08e83c7bdd7709be768b6a93427070";
        var size = File.Exists(altv) ? new FileInfo(altv).Length : 5656576;

        return $"{{\"latestBuildNumber\":-1,\"version\":\"16.3.7\"," +
               $"\"hashList\":{{\"altv.exe\":\"{hash}\"}}," +
               $"\"sizeList\":{{\"altv.exe\":{size}}}}}";
    }

    private (int Status, string ContentType, byte[] Body) HandleSkin(string lower)
    {
        var skin = Path.Combine(_clientDir, "cache", "skin.bin");
        if (!File.Exists(skin)) skin = Path.Combine(_clientDir, "skin.bin");

        if (lower.EndsWith("/hash"))
        {
            return File.Exists(skin)
                ? (200, "text/plain", Enc(Sha1(skin)))
                : (200, "text/plain", "0000000000000000000000000000000000000000"u8.ToArray());
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
        if (!Directory.Exists(_clientDir)) return (404, "text/plain", "Not Found"u8.ToArray());

        var marker = "x64_win32/";
        var i = path.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        string rel = i >= 0 ? path[(i + marker.Length)..] : Path.GetFileName(path);
        rel = rel.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);

        // Никогда не отдаём и не перезаписываем конфиги или кэш клиента через CDN
        if (rel.Equals("altv.toml", StringComparison.OrdinalIgnoreCase) ||
            rel.Equals("flovmp.toml", StringComparison.OrdinalIgnoreCase) ||
            rel.Contains("cache", StringComparison.OrdinalIgnoreCase))
        {
            return (404, "text/plain", "Not Found"u8.ToArray());
        }

        // De-race: клиент качает altv-client.dll в СВОЮ же папку (_clientDir),
        // откуда CDN её и отдаёт → самоперезапись во время скачивания обнуляет
        // файл. Патченую версию (обход WRONG_STABLE_BUILD) держим ОТДЕЛЬНО в
        // patched/altv-client.dll и отдаём её — CDN читает staging, клиент
        // пишет живой файл, пересечения нет. Живой altv-client.dll остаётся
        // оригиналом (проходит любые проверки целостности до скачивания).
        var staged = StagedClientDll(rel);
        if (staged != null)
        {
            try
            {
                using var sfs = new FileStream(staged, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var sms = new MemoryStream();
                sfs.CopyTo(sms);
                Console.WriteLine("[cdn] altv-client.dll: отдаю патченую копию из staging (de-raced)");
                return (200, "application/octet-stream", sms.ToArray());
            }
            catch (Exception ex) { Console.WriteLine($"[cdn] staged altv-client.dll read failed: {ex.Message}"); }
        }

        var file = Path.GetFullPath(Path.Combine(_clientDir, rel));
        if (!file.StartsWith(Path.GetFullPath(_clientDir), StringComparison.OrdinalIgnoreCase) || !File.Exists(file))
        {
            var byName = Directory.EnumerateFiles(_clientDir, Path.GetFileName(rel), SearchOption.AllDirectories).FirstOrDefault();
            if (byName is null) return (404, "text/plain", "Not Found"u8.ToArray());
            file = byName;
        }

        try
        {
            using var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var ms = new MemoryStream();
            fs.CopyTo(ms);
            return (200, "application/octet-stream", ms.ToArray());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[cdn] error reading {file}: {ex.Message}");
            return (500, "text/plain", Encoding.UTF8.GetBytes($"Error: {ex.Message}"));
        }
    }

    private string BuildClientManifest()
    {
        if (!Directory.Exists(_clientDir))
        {
            return $"{{\"latestBuildNumber\":-1,\"version\":\"{Version}\",\"sdkVersion\":\"{SdkVersion}\"," +
                   $"\"hashList\":{{}},\"sizeList\":{{}}}}";
        }

        var hashes = new StringBuilder();
        var sizes = new StringBuilder();
        var first = true;
        foreach (var f in Directory.EnumerateFiles(_clientDir, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(_clientDir, f).Replace('\\', '/');
            if (rel is "update.json" or "manifest.json" or "altv.toml" or "flovmp.toml" or "commandline.txt" or "MISSING.txt") continue;
            if (rel.StartsWith("cache/") || rel.StartsWith("logs/") || rel.StartsWith("backup/")
                || rel.StartsWith("cdn/") || rel.StartsWith("patched/") || rel.StartsWith("ui/")
                || rel.Contains("/cache/") || rel.Contains("cache/")
                || rel.EndsWith(".log", StringComparison.OrdinalIgnoreCase)
                || rel.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)
                || rel.EndsWith(".pma", StringComparison.OrdinalIgnoreCase)
                || rel.EndsWith(".LOCK", StringComparison.OrdinalIgnoreCase)
                || rel.EndsWith(".bak", StringComparison.OrdinalIgnoreCase)) continue;

            // Для altv-client.dll хэш/размер берём из patched-копии (её же
            // отдаёт CDN) — чтобы клиент увидел совпадение манифеста с тем,
            // что скачает, и не крутил цикл перекачки.
            var eff = f;
            if (rel.Equals("altv-client.dll", StringComparison.OrdinalIgnoreCase))
            {
                var st = Path.Combine(_clientDir, "patched", "altv-client.dll");
                if (File.Exists(st)) eff = st;
            }

            if (!first) { hashes.Append(','); sizes.Append(','); }
            first = false;
            hashes.Append('"').Append(rel).Append("\":\"").Append(Sha1(eff)).Append('"');
            sizes.Append('"').Append(rel).Append("\":").Append(new FileInfo(eff).Length);
        }
        return $"{{\"latestBuildNumber\":-1,\"version\":\"{Version}\",\"sdkVersion\":\"{SdkVersion}\"," +
               $"\"hashList\":{{{hashes}}},\"sizeList\":{{{sizes}}}}}";
    }

    private string BuildBackupManifest()
    {
        var backupDir = Path.Combine(_clientDir, "cdn", "backup");
        if (!Directory.Exists(backupDir)) backupDir = Path.Combine(_clientDir, "backup");
        if (!Directory.Exists(backupDir)) return "{\"files\":[]}";

        var files = new StringBuilder();
        var first = true;
        foreach (var f in Directory.EnumerateFiles(backupDir, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(backupDir, f).Replace('\\', '/');
            if (rel is "backup_update.json" or "README.md") continue;

            if (!first) files.Append(',');
            first = false;

            files.Append($"{{\"name\":\"{rel}\",\"hash\":\"{Sha1(f)}\",\"size\":{new FileInfo(f).Length}}}");
        }
        return $"{{\"files\":[{files}]}}";
    }

    // Возвращает путь к патченой altv-client.dll (staging), если запрошена
    // именно она и файл patched/altv-client.dll существует; иначе null.
    private string? StagedClientDll(string rel)
    {
        if (!Path.GetFileName(rel).Equals("altv-client.dll", StringComparison.OrdinalIgnoreCase))
            return null;
        var staged = Path.Combine(_clientDir, "patched", "altv-client.dll");
        return File.Exists(staged) ? staged : null;
    }

    private static byte[] Enc(string s) => Encoding.UTF8.GetBytes(s);

    private static string Sha1(string file)
    {
        using var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return Convert.ToHexString(SHA1.HashData(fs)).ToLowerInvariant();
    }

    private static string ContentType(string file) => Path.GetExtension(file).ToLowerInvariant() switch
    {
        ".html" or ".htm" => "text/html; charset=utf-8",
        ".js" or ".mjs" => "application/javascript",
        ".css" => "text/css; charset=utf-8",
        ".svg" => "image/svg+xml",
        ".json" => "application/json; charset=utf-8",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".ico" => "image/x-icon",
        ".woff" => "font/woff",
        ".woff2" => "font/woff2",
        ".ttf" => "font/ttf",
        ".otf" => "font/otf",
        ".txt" => "text/plain; charset=utf-8",
        _ => "application/octet-stream",
    };

    public void Dispose()
    {
        _cts.Cancel();
        try { _listener.Stop(); } catch { }
        _cts.Dispose();
    }
}
