using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FloVMP.Core.Auth;

namespace FloVMP.Gamemode.Systems.Api;

/// <summary>
/// Встроенный HTTP-API сервера (Linux: отдельного FloVMP.ServerLauncher нет,
/// alt:V-сервер живёт напрямую под systemd). Один <see cref="IAccountStore"/>
/// на игру, лаунчер и API — тот же аккаунт везде.
///
///   POST /api/auth/{register,login,change-password,change-email,2fa/enable,2fa/disable}
///   GET  /info   — живой статус (реальный онлайн), плюс дублируется в
///                  /var/www/cdn/info.json для nginx на :80.
///
/// Слушает в фоновом потоке, не блокирует главный поток alt:V.
/// </summary>
public sealed class HttpApiSystem
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly IAccountStore _store;
    private readonly AuthService _auth;
    private readonly DateTime _startTime = DateTime.UtcNow;
    private readonly Func<int> _playerCount;
    private readonly int _maxPlayers;
    private readonly string _serverName;
    private readonly string _gamemode;
    private readonly int _port;
    private readonly string? _infoJsonMirror;

    private HttpListener? _listener;
    private Thread? _thread;
    private Timer? _infoTimer;
    private volatile bool _running;
    private readonly Action<string> _log;

    private const int MaxBodyBytes = 16 * 1024;
    private const int MaxInFlight = 64;
    private const int RateWindowSec = 10;
    private const int RateMaxPerWindow = 30;
    private int _inFlight;
    private readonly ConcurrentDictionary<string, (int count, DateTime start)> _rate = new();

    public HttpApiSystem(
        IAccountStore store,
        Func<int> playerCount,
        int maxPlayers,
        string serverName,
        string gamemode,
        Action<string> log,
        int port = 7799,
        string? infoJsonMirror = "/var/www/cdn/info.json")
    {
        _store = store;
        _auth = new AuthService(store);
        _playerCount = playerCount;
        _maxPlayers = maxPlayers;
        _serverName = serverName;
        _gamemode = gamemode;
        _log = log;
        _port = port;
        _infoJsonMirror = infoJsonMirror;
    }

    public void Start()
    {
        try
        {
            // По умолчанию слушаем ТОЛЬКО loopback — публичная точка входа это
            // nginx на :80 (proxy_pass → 127.0.0.1:7799), он же режет флуд и
            // размер тела. FLOVMP_API_BIND=public открывает все интерфейсы —
            // только если nginx перед сервером нет.
            var bindPublic = string.Equals(
                Environment.GetEnvironmentVariable("FLOVMP_API_BIND"), "public",
                StringComparison.OrdinalIgnoreCase);

            _listener = new HttpListener();
            if (bindPublic)
            {
                _listener.Prefixes.Add($"http://+:{_port}/");
                try { _listener.Start(); }
                catch (HttpListenerException)
                {
                    _listener = new HttpListener();
                    _listener.Prefixes.Add($"http://127.0.0.1:{_port}/");
                    _listener.Start();
                    _log($"[FloV:MP] http-api: no privilege for +:{_port}, loopback only");
                }
            }
            else
            {
                _listener.Prefixes.Add($"http://127.0.0.1:{_port}/");
                _listener.Prefixes.Add($"http://localhost:{_port}/");
                _listener.Start();
            }

            _running = true;
            _thread = new Thread(Loop) { IsBackground = true, Name = "flovmp-http-api" };
            _thread.Start();
            _log($"[FloV:MP] http-api: listening on :{_port} (/api/auth/*, /info)");

            WriteInfoMirror();
            _infoTimer = new Timer(_ => WriteInfoMirror(), null, 10_000, 10_000);
        }
        catch (Exception ex)
        {
            _log($"[FloV:MP] http-api: FAILED to start on :{_port}: {ex.Message}");
        }
    }

    public void Stop()
    {
        _running = false;
        try { _infoTimer?.Dispose(); } catch { }
        try { _listener?.Stop(); _listener?.Close(); } catch { }
        try { _thread?.Join(1000); } catch { }
    }

    private void Loop()
    {
        while (_running)
        {
            HttpListenerContext ctx;
            try { ctx = _listener!.GetContext(); }
            catch (ObjectDisposedException) { break; }
            catch (HttpListenerException) { if (!_running) break; Thread.Sleep(50); continue; }
            catch { if (!_running) break; Thread.Sleep(50); continue; }

            // словарь rate-limit не должен расти без конца
            if (_rate.Count > 8192)
            {
                var cutoff = DateTime.UtcNow.AddSeconds(-RateWindowSec * 6);
                foreach (var kv in _rate)
                    if (kv.Value.start < cutoff) _rate.TryRemove(kv.Key, out _);
            }

            // каждый запрос — в пуле, чтобы медленный клиент не держал цикл
            ThreadPool.QueueUserWorkItem(_ =>
            {
                if (Interlocked.Increment(ref _inFlight) > MaxInFlight)
                {
                    Interlocked.Decrement(ref _inFlight);
                    try { WriteJson(ctx, 429, new { ok = false, message = "server busy" }); } catch { }
                    return;
                }

                try
                {
                    var ip = ctx.Request.RemoteEndPoint?.Address.ToString() ?? "unknown";
                    var now = DateTime.UtcNow;
                    var window = _rate.AddOrUpdate(ip,
                        _ => (1, now),
                        (_, cur) => (now - cur.start).TotalSeconds > RateWindowSec ? (1, now) : (cur.count + 1, cur.start));

                    if (window.count > RateMaxPerWindow)
                    {
                        WriteJson(ctx, 429, new { ok = false, message = "too many requests" });
                        return;
                    }

                    Handle(ctx);
                }
                catch (Exception ex)
                {
                    // деталь исключения — только в лог сервера, не клиенту
                    _log($"[FloV:MP] http-api: handler error: {ex.Message}");
                    try { WriteJson(ctx, 500, new { ok = false, message = "внутренняя ошибка" }); } catch { }
                }
                finally
                {
                    Interlocked.Decrement(ref _inFlight);
                }
            });
        }
    }

    private void Handle(HttpListenerContext ctx)
    {
        var path = (ctx.Request.Url?.AbsolutePath ?? "/").TrimEnd('/');
        if (path.Length == 0) path = "/";
        ctx.Response.AddHeader("Access-Control-Allow-Origin", "*");
        ctx.Response.AddHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
        ctx.Response.AddHeader("Access-Control-Allow-Headers", "Content-Type");

        if (ctx.Request.HttpMethod == "OPTIONS") { ctx.Response.StatusCode = 204; ctx.Response.Close(); return; }

        if (path is "/info" or "/status")
        {
            WriteJson(ctx, 200, BuildInfo());
            return;
        }

        if (path.StartsWith("/api/auth/") && ctx.Request.HttpMethod == "POST")
        {
            HandleAuth(ctx, path);
            return;
        }

        WriteJson(ctx, 404, new { ok = false, message = "not found" });
    }

    private object BuildInfo() => new
    {
        online = true,
        players = SafeCount(),
        maxPlayers = _maxPlayers,
        name = _serverName,
        gamemode = _gamemode,
        uptimeSeconds = (long)(DateTime.UtcNow - _startTime).TotalSeconds,
        memoryMb = Math.Round(GC.GetTotalMemory(false) / (1024.0 * 1024.0), 2),
        updatedUtc = DateTime.UtcNow.ToString("O"),
    };

    private int SafeCount()
    {
        try { return _playerCount(); } catch { return 0; }
    }

    private void WriteInfoMirror()
    {
        if (string.IsNullOrEmpty(_infoJsonMirror)) return;
        try
        {
            var dir = Path.GetDirectoryName(_infoJsonMirror);
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
            {
                var tmp = _infoJsonMirror + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(BuildInfo(), Json));
                File.Move(tmp, _infoJsonMirror, overwrite: true);
            }
        }
        catch { /* зеркало не критично */ }
    }

    private void HandleAuth(HttpListenerContext ctx, string path)
    {
        if (ctx.Request.ContentLength64 > 65536)
        {
            WriteJson(ctx, 413, new AuthResponseDto(false, "payload too large", null, null, "", false, false));
            return;
        }

        string body;
        using (var r = new StreamReader(ctx.Request.InputStream, Encoding.UTF8)) body = r.ReadToEnd();

        AuthRequestDto? req;
        try { req = JsonSerializer.Deserialize<AuthRequestDto>(body, Json); }
        catch
        {
            WriteJson(ctx, 400, new AuthResponseDto(false, "некорректный JSON", null, null, "", false, false));
            return;
        }
        req ??= new AuthRequestDto(null, null, null, null, null, null);
        var username = req.Username?.Trim() ?? "";
        var password = req.Password ?? "";
        // за nginx реальный адрес — в X-Real-IP / X-Forwarded-For; RemoteEndPoint
        // это всегда 127.0.0.1 (прокси), по нему троттлинг был бы глобальным.
        var throttleKey = FirstNonEmpty(
            ctx.Request.Headers["X-Real-IP"],
            ctx.Request.Headers["X-Forwarded-For"]?.Split(',')[0],
            ctx.Request.RemoteEndPoint?.Address.ToString(),
            username);

        var route = path["/api/auth/".Length..].TrimEnd('/');

        var result = route switch
        {
            "register" => _auth.Register(username, password, throttleKey),
            "login" => _auth.Login(username, password, throttleKey, req.Code),
            "change-password" => _auth.ChangePassword(username, password, req.NewPassword ?? "", throttleKey),
            "change-email" => _auth.ChangeEmail(username, password, req.Email ?? "", throttleKey),
            "2fa/enable" => _auth.Enable2fa(username, req.Secret ?? "", req.Code ?? "", throttleKey),
            "2fa/disable" => _auth.Disable2fa(username, req.Code ?? "", throttleKey),
            _ => new AuthResult(AuthOutcome.BadUsername, "неизвестная операция"),
        };

        var resp = new AuthResponseDto(
            result.Ok,
            result.Message,
            result.Account?.Username,
            result.Account?.CreatedUtc,
            result.Account?.Email ?? "",
            result.Account?.TwoFaEnabled ?? false,
            result.Outcome == AuthOutcome.TwoFaRequired);

        WriteJson(ctx, result.Ok ? 200 : 400, resp);
    }

    private static string FirstNonEmpty(params string?[] vals)
    {
        foreach (var v in vals)
            if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
        return "?";
    }

    private static void WriteJson(HttpListenerContext ctx, int status, object payload)
    {
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, Json));
        ctx.Response.ContentType = "application/json; charset=utf-8";
        ctx.Response.StatusCode = status;
        ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
        ctx.Response.Close();
    }

    private sealed record AuthRequestDto(
        string? Username, string? Password, string? NewPassword,
        string? Email, string? Secret, string? Code);

    private sealed record AuthResponseDto(
        bool Ok, string Message, string? Username, string? CreatedUtc,
        string Email, bool TwoFa, bool TwoFaRequired);
}
