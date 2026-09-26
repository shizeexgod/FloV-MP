using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FloVMP.LicenseAuthority;

/// <summary>
/// HTTP службы за nginx (слушает только 127.0.0.1). nginx передаёт адрес
/// клиента в X-Real-IP и сам отдаёт файлы пакетов (X-Accel-Redirect).
///
///   POST /api/v1/license/verify                      — проверка сервера клиента
///   GET  /api/v1/licenses/download-by-key?key=&server= — активация, license.flv
///   GET  /api/v1/distribution/release|release.sig|download?os=&key= — пакеты
///   GET  /api/v1/distribution/health, /api/v1/license/health
/// </summary>
public sealed class HttpApi
{
    // Подбор ключей: 20 неверных за 10 минут с одного IP — пауза до конца окна.
    private const int FailLimit = 20;
    private static readonly TimeSpan FailWindow = TimeSpan.FromMinutes(10);
    private const string AccelPrefix = "/_flovmp_dist_files/";
    private static readonly Regex FileName = new(@"^[A-Za-z0-9._-]+$", RegexOptions.CultureInvariant);

    private readonly LicenseService _service;
    private readonly string _releases;
    private readonly ConcurrentDictionary<string, List<DateTime>> _fails = new();
    private readonly object _lock = new();

    public HttpApi(LicenseService service, string releasesDir)
    {
        _service = service;
        _releases = releasesDir;
    }

    public async Task RunAsync(string prefix, CancellationToken stop)
    {
        using var listener = new HttpListener();
        listener.Prefixes.Add(prefix);
        listener.Start();
        Console.WriteLine($"flovmp-license: слушаю {prefix}");
        using var reg = stop.Register(() => { try { listener.Stop(); } catch { } });
        while (!stop.IsCancellationRequested)
        {
            HttpListenerContext ctx;
            try { ctx = await listener.GetContextAsync(); }
            catch (Exception) when (stop.IsCancellationRequested) { break; }
            catch (HttpListenerException) { continue; }
            _ = Task.Run(() => Handle(ctx));
        }
    }

    private void Handle(HttpListenerContext ctx)
    {
        var ip = ctx.Request.Headers["X-Real-IP"] ?? ctx.Request.RemoteEndPoint?.Address.ToString() ?? "0.0.0.0";
        // Игровой сервер на той же машине, что и сервер лицензий, обращается к
        // нему через 127.0.0.1 (к своему внешнему адресу контейнер-VDS
        // обратиться не может). Такой запрос приходит только с этой машины,
        // поэтому для него — её внешний адрес из FLOVMP_AUTHORITY_PUBLIC_IP:
        // этот IP попадёт в аренду, и клиенты игроков сверят его.
        if (ip is "127.0.0.1" or "::1" &&
            Environment.GetEnvironmentVariable("FLOVMP_AUTHORITY_PUBLIC_IP") is { Length: > 0 } publicIp &&
            System.Net.IPAddress.TryParse(publicIp, out _))
            ip = publicIp;
        try
        {
            var reply = Route(ctx, ip);
            if (reply is not null) Send(ctx, reply);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"{ip} {ctx.Request.HttpMethod} {ctx.Request.Url?.AbsolutePath}: {ex.Message}");
            try { Send(ctx, new ServiceReply(500, JsonSerializer.Serialize(new { valid = false, reason = "внутренняя ошибка сервера лицензий" }, LicenseService.JsonOut))); }
            catch { }
        }
    }

    internal ServiceReply? Route(HttpListenerContext ctx, string ip)
    {
        var path = ctx.Request.Url?.AbsolutePath.TrimEnd('/') ?? "";
        var q = ctx.Request.QueryString;
        switch (path)
        {
            case "/api/v1/license/health":
            case "/api/v1/distribution/health":
                return new ServiceReply(200, JsonSerializer.Serialize(new { ok = true, version = ReleaseInfo("linux")?.GetValueOrDefault("version") }));

            case "/api/v1/license/verify":
            {
                if (ctx.Request.HttpMethod != "POST") return Error(405, "нужен POST");
                if (Blocked(ip)) return Error(429, "слишком много неверных запросов — подождите 10 минут");
                if (ctx.Request.ContentLength64 > 8192) return Error(413, "слишком большой запрос");
                string? key = null, serverId = null, version = null;
                var slots = 0;
                try
                {
                    var body = ReadBoundedBody(ctx.Request.InputStream);
                    if (body is null) return Error(413, "слишком большой запрос");
                    using var doc = JsonDocument.Parse(body);
                    var r = doc.RootElement;
                    key = Str(r, "licenseKey");
                    serverId = Str(r, "serverId");
                    version = Str(r, "version");
                    if (r.TryGetProperty("slots", out var s) && s.ValueKind == JsonValueKind.Number) s.TryGetInt32(out slots);
                }
                catch (JsonException) { return Error(400, "неверный JSON"); }
                catch (DecoderFallbackException) { return Error(400, "неверная кодировка JSON"); }
                return Count(ip, _service.Verify(key, serverId, ip, version, slots));
            }

            case "/api/v1/telemetry/heartbeat":
                // Телеметрия серверов пока не хранится: состояние видно по проверкам лицензии.
                return new ServiceReply(204, "");

            case "/api/v1/licenses/download-by-key":
                if (Blocked(ip)) return Error(429, "слишком много неверных запросов — подождите 10 минут");
                return Count(ip, _service.Download(q["key"], q["server"], ip));

            case "/api/v1/distribution/release":
            case "/api/v1/distribution/release.sig":
            case "/api/v1/distribution/download":
            case "/api/v1/distribution/download-latest":
                return Distribution(ctx, path, q["os"] ?? "linux", q["key"], ip);
        }
        return Error(404, "нет такого адреса");
    }

    private ServiceReply? Distribution(HttpListenerContext ctx, string path, string os, string? key, string ip)
    {
        if (os is not ("linux" or "windows")) return Error(400, "os: linux или windows");
        if (Blocked(ip)) return Error(429, "слишком много неверных запросов — подождите 10 минут");
        var refusal = _service.DownloadRefusal(key);
        if (refusal is not null)
        {
            NoteFail(ip);
            return Error(403, "доступ запрещён: " + refusal);
        }
        var info = ReleaseInfo(os);
        if (info is null) return Error(503, "релиз ещё не опубликован");
        var txt = Path.Combine(_releases, "current", $"release-{os}.txt");
        if (path.EndsWith("/release")) return new ServiceReply(200, File.ReadAllText(txt), "text/plain; charset=utf-8");
        if (path.EndsWith("/release.sig")) return new ServiceReply(200, File.ReadAllText(txt + ".sig"), "text/plain; charset=utf-8");

        var name = info.GetValueOrDefault("file") ?? "";
        if (!FileName.IsMatch(name) || !File.Exists(Path.Combine(_releases, "current", name)))
            return Error(503, "файл релиза не найден");
        // Файл отдаёт nginx: докачка и sendfile без участия службы.
        ctx.Response.StatusCode = 200;
        ctx.Response.AddHeader("X-Accel-Redirect", AccelPrefix + name);
        ctx.Response.AddHeader("Content-Disposition", $"attachment; filename=\"{name}\"");
        ctx.Response.AddHeader("X-Checksum-Sha256", info.GetValueOrDefault("sha256") ?? "");
        ctx.Response.ContentType = "application/octet-stream";
        ctx.Response.Close();
        return null;
    }

    private Dictionary<string, string>? ReleaseInfo(string os)
    {
        var txt = Path.Combine(_releases, "current", $"release-{os}.txt");
        if (!File.Exists(txt)) return null;
        var d = new Dictionary<string, string>();
        foreach (var line in File.ReadAllLines(txt))
        {
            var i = line.IndexOf('=');
            if (i > 0) d[line[..i]] = line[(i + 1)..].Trim();
        }
        return d;
    }

    private static string? Str(JsonElement r, string name) =>
        r.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    internal static string? ReadBoundedBody(Stream input)
    {
        const int limit = 8192;
        var bytes = new byte[limit + 1];
        var count = 0;
        while (count < bytes.Length)
        {
            var read = input.Read(bytes, count, bytes.Length - count);
            if (read == 0) break;
            count += read;
        }
        if (count > limit) return null;
        return new UTF8Encoding(false, true).GetString(bytes, 0, count);
    }

    /// <summary>Отказ «ключ не найден / неверный формат» считается попыткой подбора.</summary>
    private ServiceReply Count(string ip, ServiceReply reply)
    {
        if (reply.Status is 400 || (reply.Status == 403 && reply.Body.Contains("не найден"))) NoteFail(ip);
        return reply;
    }

    private bool Blocked(string ip)
    {
        lock (_lock)
        {
            if (!_fails.TryGetValue(ip, out var list)) return false;
            list.RemoveAll(t => DateTime.UtcNow - t > FailWindow);
            return list.Count >= FailLimit;
        }
    }

    private void NoteFail(string ip)
    {
        lock (_lock) _fails.GetOrAdd(ip, _ => new List<DateTime>()).Add(DateTime.UtcNow);
    }

    private static ServiceReply Error(int status, string reason) =>
        new(status, JsonSerializer.Serialize(new { valid = false, reason }, LicenseService.JsonOut));

    private static void Send(HttpListenerContext ctx, ServiceReply reply)
    {
        var bytes = Encoding.UTF8.GetBytes(reply.Body);
        ctx.Response.StatusCode = reply.Status;
        ctx.Response.ContentType = reply.ContentType;
        ctx.Response.AddHeader("Cache-Control", "no-store");
        ctx.Response.ContentLength64 = bytes.Length;
        ctx.Response.OutputStream.Write(bytes);
        ctx.Response.Close();
    }
}
