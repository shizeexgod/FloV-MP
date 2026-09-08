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
            _listener = new HttpListener();
            // http://+:port/ требует прав (root под systemd — ок). Фолбэк на loopback.
            _listener.Prefixes.Add($"http://+:{_port}/");
            try { _listener.Start(); }
            catch (HttpListenerException)
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://127.0.0.1:{_port}/");
                _listener.Prefixes.Add($"http://localhost:{_port}/");
                _listener.Start();
                _log($"[FloV:MP] http-api: bound to loopback:{_port} (no privilege for +:{_port})");
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
        while (_running && _listener is { IsListening: true })
        {
            HttpListenerContext ctx;
            try { ctx = _listener.GetContext(); }
            catch { break; }

            // каждый запрос — в пуле, чтобы медленный клиент не держал цикл
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try { Handle(ctx); }
                catch (Exception ex)
                {
                    try { WriteJson(ctx, 500, new { ok = false, message = ex.Message }); } catch { }
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
        string body;
        using (var r = new StreamReader(ctx.Request.InputStream, Encoding.UTF8)) body = r.ReadToEnd();

        var req = JsonSerializer.Deserialize<AuthRequestDto>(body, Json)
                  ?? new AuthRequestDto(null, null, null, null, null, null);
        var username = req.Username?.Trim() ?? "";
        var password = req.Password ?? "";
        var throttleKey = ctx.Request.RemoteEndPoint?.Address.ToString() ?? username;

        var auth = new AuthService(_store);
        var route = path["/api/auth/".Length..].TrimEnd('/');

        var result = route switch
        {
            "register" => auth.Register(username, password),
            "login" => auth.Login(username, password, throttleKey, req.Code),
            "change-password" => auth.ChangePassword(username, password, req.NewPassword ?? ""),
            "change-email" => auth.ChangeEmail(username, password, req.Email ?? ""),
            "2fa/enable" => auth.Enable2fa(username, req.Secret ?? "", req.Code ?? ""),
            "2fa/disable" => auth.Disable2fa(username, req.Code ?? ""),
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
