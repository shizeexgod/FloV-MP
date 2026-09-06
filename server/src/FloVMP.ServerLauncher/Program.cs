using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using FloVMP.Core.Auth;

Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("== Starting FloV:MP Server ==");
Console.ResetColor();

string serverDir;
var envDir = Environment.GetEnvironmentVariable("FLOVMP_SERVER_DIR");
if (!string.IsNullOrEmpty(envDir) && Directory.Exists(envDir))
{
    serverDir = Path.GetFullPath(envDir);
}
else
{
    var walkDir = AppContext.BaseDirectory;
    string? found = null;
    for (var i = 0; i < 10; i++)
    {
        var candidate = Path.Combine(walkDir, "runtime", "server");
        if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "altv-server.exe")))
        {
            found = candidate;
            break;
        }
        var parent = Directory.GetParent(walkDir);
        if (parent is null) break;
        walkDir = parent.FullName;
    }
    if (found is null)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("[error] Не нашёл runtime\\server с altv-server.exe.");
        Console.WriteLine("        Задай переменную окружения FLOVMP_SERVER_DIR.");
        Console.ResetColor();
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
        return 2;
    }
    serverDir = found;
}

Console.WriteLine($"[server] dir: {serverDir}");

var accountsPath = Path.Combine(serverDir, "flovmp-data", "accounts.json");

Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine("Auth proxy starting on http://127.0.0.1:7799 ...");
Console.ResetColor();

var listener = new HttpListener();
listener.Prefixes.Add("http://127.0.0.1:7799/");
try
{
    listener.Start();
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("[proxy] Auth proxy started OK.");
    Console.ResetColor();
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"[proxy] FAILED to start auth proxy: {ex.Message}");
    Console.ResetColor();
    Console.WriteLine("Press any key to exit...");
    Console.ReadKey();
    return 1;
}

// accounts.json меняет и этот процесс, и отдельный процесс alt:V-геймода —
// общего in-memory состояния между процессами быть не может. Поэтому ниже
// на КАЖДЫЙ запрос создаётся свежий JsonAccountStore (перечитывает файл),
// а не один держится на всё время жизни ServerLauncher. Плата за это —
// анти-брутфорс AuthService (лимит попыток в окне) не переживает между
// запросами HTTP API — некритично для редких login/register, встроенный
// rate-limit самого alt:V-подключения (не этого API) всё равно на месте.
var jsonOpts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

_ = Task.Run(() =>
{
    while (true)
    {
        try
        {
            var ctx = listener.GetContext();
            var path = ctx.Request.Url?.AbsolutePath ?? "";

            ctx.Response.AddHeader("Access-Control-Allow-Origin", "*");
            ctx.Response.AddHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            ctx.Response.AddHeader("Access-Control-Allow-Headers", "Content-Type");
            if (ctx.Request.HttpMethod == "OPTIONS")
            {
                ctx.Response.StatusCode = 204;
                ctx.Response.Close();
                continue;
            }

            if (path.StartsWith("/api/auth/") && ctx.Request.HttpMethod == "POST")
            {
                HandleAuthRequest(ctx, path, accountsPath, jsonOpts);
                continue;
            }

            using var reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8);
            var body = reader.ReadToEnd();
            if (body.Contains("clientTokenHashes"))
            {
                var count = 1;
                var match = Regex.Match(body, "\"clientTokenHashes\"\\s*:\\s*\\[(.*?)\\]");
                if (match.Success && !string.IsNullOrWhiteSpace(match.Groups[1].Value))
                {
                    var items = match.Groups[1].Value.Trim();
                    count = items.Length > 0 ? items.Split(',').Length : 0;
                }
                var responseJson = count > 0
                    ? "[" + string.Join(", ", Enumerable.Repeat("true", count)) + "]"
                    : "[]";
                var bytes = Encoding.UTF8.GetBytes(responseJson);
                ctx.Response.ContentType = "application/json";
                ctx.Response.StatusCode = 200;
                ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
            }
            else
            {
                var bytes = Encoding.UTF8.GetBytes("ok");
                ctx.Response.ContentType = "text/plain";
                ctx.Response.StatusCode = 200;
                ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
            }
            ctx.Response.Close();
        }
        catch (ObjectDisposedException) { break; }
        catch { }
    }
});

var psi = new ProcessStartInfo
{
    FileName = Path.Combine(serverDir, "altv-server.exe"),
    WorkingDirectory = serverDir,
    UseShellExecute = false,
};

Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("[server] Launching altv-server.exe...");
Console.ResetColor();

Process? proc;
try { proc = Process.Start(psi); }
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"[error] Failed to start altv-server.exe: {ex.Message}");
    Console.ResetColor();
    listener.Stop();
    Console.WriteLine("Press any key to exit...");
    Console.ReadKey();
    return 3;
}

if (proc is null)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("[error] Process.Start returned null.");
    Console.ResetColor();
    listener.Stop();
    return 4;
}

Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine($"[server] altv-server.exe PID {proc.Id}. Ctrl+C to stop.");
Console.ResetColor();

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    if (!proc.HasExited) proc.Kill(true);
};

proc.WaitForExit();
Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine($"[server] altv-server.exe exited with code {proc.ExitCode}.");
Console.ResetColor();
listener.Stop();
return 0;

// ─── /api/auth/* — тот же AuthService/JsonAccountStore, что и в игре
// (FloVMP.Gamemode/Systems/Auth/AuthSystem.cs), тот же accounts.json. Один
// аккаунт для лаунчера и игры — решено с владельцем 2026-08-30. Эндпоинты:
//   register, login            — вход/регистрация (login принимает code для 2FA);
//   change-password, change-email — смена данных (нужен текущий пароль);
//   2fa/enable, 2fa/disable    — Google Authenticator.
static void HandleAuthRequest(HttpListenerContext ctx, string path, string accountsPath, JsonSerializerOptions jsonOpts)
{
    try
    {
        using var reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8);
        var body = reader.ReadToEnd();
        var req = JsonSerializer.Deserialize<AuthRequestDto>(body, jsonOpts) ?? new AuthRequestDto(null, null, null, null, null, null);
        var username = req.Username?.Trim() ?? "";
        var password = req.Password ?? "";
        var throttleKey = ctx.Request.RemoteEndPoint?.Address.ToString() ?? username;

        var auth = new AuthService(new JsonAccountStore(accountsPath));
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

        WriteJson(ctx, result.Ok ? 200 : 400, resp, jsonOpts);
    }
    catch (Exception ex)
    {
        WriteJson(ctx, 500, new AuthResponseDto(false, $"внутренняя ошибка: {ex.Message}", null, null, "", false, false), jsonOpts);
    }
}

static void WriteJson(HttpListenerContext ctx, int status, object payload, JsonSerializerOptions jsonOpts)
{
    var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, jsonOpts));
    ctx.Response.ContentType = "application/json";
    ctx.Response.StatusCode = status;
    ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
    ctx.Response.Close();
}

record AuthRequestDto(
    string? Username,
    string? Password,
    string? NewPassword,
    string? Email,
    string? Secret,
    string? Code);

record AuthResponseDto(
    bool Ok,
    string Message,
    string? Username,
    string? CreatedUtc,
    string Email,
    bool TwoFa,
    bool TwoFaRequired);
