using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace FloVMP.Connect;

/// <summary>
/// Локальная заглушка бэкенда alt:V. alt:V-клиент, запущенный с
/// <c>-customui http://127.0.0.1:PORT/...</c>, шлёт сюда весь бэкенд-трафик:
/// манифесты обновления, проверку веток/токена, скин лаунчера.
///
/// Отдаём:
///  - манифест клиента, собранный из РЕАЛЬНЫХ файлов папки клиента (значит
///    хэши всегда сходятся, клиент ничего не докачивает);
///  - сами файлы (если вдруг попросит) — из папки клиента;
///  - auth/branch — «всё разрешено, версия 16.4.39»;
///  - /backup/* — 404, чтобы НЕ включалась подмена GTA5.exe (играем на
///    настоящем legacy-экзе игрока).
/// </summary>
public sealed class LocalCdn : IDisposable
{
    private const string Version = "16.4.39";
    private const string SdkVersion = "c150769";

    private readonly string _clientDir;
    private readonly string? _uiDir;
    private readonly HttpListener _listener = new();
    private readonly CancellationTokenSource _cts = new();
    private string _clientManifestJson = "{}";

    public int Port { get; }
    public string BaseUrl => $"http://127.0.0.1:{Port}";

    public LocalCdn(string clientDir, int port, string? uiDir = null)
    {
        _clientDir = clientDir;
        _uiDir = uiDir;
        Port = port;
        _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
    }

    public void Start()
    {
        _clientManifestJson = BuildClientManifest();
        _listener.Start();
        _ = Task.Run(AcceptLoop);
        Console.WriteLine($"[cdn] listening on {BaseUrl}");
    }

    private async Task AcceptLoop()
    {
        while (!_cts.IsCancellationRequested)
        {
            HttpListenerContext ctx;
            try { ctx = await _listener.GetContextAsync(); }
            catch { break; }
            _ = Task.Run(() => Handle(ctx));
        }
    }

    private void Handle(HttpListenerContext ctx)
    {
        var path = ctx.Request.Url?.AbsolutePath ?? "/";
        var lower = path.ToLowerInvariant();
        try
        {
            ctx.Response.AddHeader("Access-Control-Allow-Origin", "*");

            if (lower.Contains("/backup/"))
            {
                // никакой подмены GTA5.exe
                Write(ctx, 404, "text/plain", "Not Found"u8.ToArray());
            }
            else if (lower.Contains("update.json") && lower.Contains("/launcher"))
            {
                Write(ctx, 200, "application/json", Enc(ManifestFor("launcher_update.json", LauncherManifest())));
            }
            else if (lower.Contains("update.json") && lower.Contains("/client"))
            {
                Write(ctx, 200, "application/json", Enc(ManifestFor("client_update.json", _clientManifestJson)));
            }
            else if (lower.Contains("/skin"))
            {
                HandleSkin(ctx, lower);
            }
            else if (lower.StartsWith("/ui/") || lower == "/ui")
            {
                HandleUi(ctx, path);
            }
            else if (lower.Contains("branch-access") || lower.Contains("/auth") || lower.Contains("token"))
            {
                Write(ctx, 200, "application/json", Enc("{\"access\": true, \"branches\": [\"release\"]}"));
            }
            else if (lower.Contains("client-branches") || lower is "/" or "/w" or "/q")
            {
                Write(ctx, 200, "application/json",
                    Enc($"{{\"release\":\"{Version}\",\"rc\":\"{Version}\",\"dev\":\"{Version}\"}}"));
            }
            else if ((lower.Contains("/client/") || lower.Contains("/launcher/")) && !lower.EndsWith(".json"))
            {
                HandleClientFile(ctx, path);
            }
            else
            {
                Write(ctx, 200, "application/json", "{}"u8.ToArray());
            }
        }
        catch
        {
            try { Write(ctx, 500, "text/plain", "err"u8.ToArray()); } catch { }
        }
        finally
        {
            Console.WriteLine($"[cdn] {ctx.Request.HttpMethod} {path} -> {ctx.Response.StatusCode}");
        }
    }

    // --- routes ------------------------------------------------------

    /// <summary>
    /// Готовый манифест из &lt;clientDir&gt;\cdn\&lt;name&gt; (взят у GTAMP —
    /// alt:V-формат, проверен рабочим прогоном), иначе — сгенерированный.
    /// </summary>
    private string ManifestFor(string name, string generated)
    {
        var f = Path.Combine(_clientDir, "cdn", name);
        return File.Exists(f) ? File.ReadAllText(f) : generated;
    }

    private static string LauncherManifest() =>
        "{\"latestBuildNumber\":-1,\"version\":\"16.3.7\"," +
        "\"hashList\":{\"altv.exe\":\"2800e0d6665cdfa9c02419360db44b5cccf64147\"}," +
        "\"sizeList\":{\"altv.exe\":9267200}}";

    private void HandleSkin(HttpListenerContext ctx, string lower)
    {
        var skin = Path.Combine(_clientDir, "cache", "skin.bin");
        if (!File.Exists(skin)) skin = Path.Combine(_clientDir, "skin.bin");

        if (lower.EndsWith("/hash"))
        {
            Write(ctx, File.Exists(skin) ? 200 : 404, "text/plain",
                Enc(File.Exists(skin) ? Sha1(skin) : "Not Found"));
            return;
        }
        if (lower.EndsWith(".json"))
        {
            if (!File.Exists(skin)) { Write(ctx, 404, "text/plain", "Not Found"u8.ToArray()); return; }
            var json = $"{{\"version\":\"{Version}\",\"hashList\":{{\"skin.bin\":\"{Sha1(skin)}\"}}," +
                       $"\"sizeList\":{{\"skin.bin\":{new FileInfo(skin).Length}}}}}";
            Write(ctx, 200, "application/json", Enc(json));
            return;
        }
        if (File.Exists(skin)) Write(ctx, 200, "application/octet-stream", File.ReadAllBytes(skin));
        else Write(ctx, 404, "text/plain", "Not Found"u8.ToArray());
    }

    private void HandleUi(HttpListenerContext ctx, string path)
    {
        if (_uiDir is null) { Write(ctx, 200, "text/html", Enc("<!doctype html><title>FloV:MP</title>")); return; }
        var rel = path.Length > 4 ? path[4..] : "index.html";
        if (string.IsNullOrEmpty(rel)) rel = "index.html";
        var file = Path.GetFullPath(Path.Combine(_uiDir, rel));
        if (!file.StartsWith(Path.GetFullPath(_uiDir), StringComparison.OrdinalIgnoreCase) || !File.Exists(file))
        {
            Write(ctx, 404, "text/plain", "Not Found"u8.ToArray());
            return;
        }
        Write(ctx, 200, ContentType(file), File.ReadAllBytes(file));
    }

    private void HandleClientFile(HttpListenerContext ctx, string path)
    {
        // .../x64_win32/<rel>  ->  <clientDir>/<rel>
        var marker = "x64_win32/";
        var i = path.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        string rel = i >= 0 ? path[(i + marker.Length)..] : Path.GetFileName(path);
        rel = rel.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);

        var file = Path.GetFullPath(Path.Combine(_clientDir, rel));
        if (!file.StartsWith(Path.GetFullPath(_clientDir), StringComparison.OrdinalIgnoreCase) || !File.Exists(file))
        {
            // запасной вариант — поиск по имени
            var byName = Directory.EnumerateFiles(_clientDir, Path.GetFileName(rel), SearchOption.AllDirectories).FirstOrDefault();
            if (byName is null) { Write(ctx, 404, "text/plain", "Not Found"u8.ToArray()); return; }
            file = byName;
        }
        Write(ctx, 200, "application/octet-stream", File.ReadAllBytes(file));
    }

    // --- manifest --------------------------------------------------

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

    // --- helpers -------------------------------------------------

    private static void Write(HttpListenerContext ctx, int status, string contentType, byte[] body)
    {
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = contentType;
        ctx.Response.ContentLength64 = body.Length;
        ctx.Response.OutputStream.Write(body, 0, body.Length);
        ctx.Response.OutputStream.Close();
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
        try { _listener.Stop(); _listener.Close(); } catch { }
        _cts.Dispose();
    }
}
