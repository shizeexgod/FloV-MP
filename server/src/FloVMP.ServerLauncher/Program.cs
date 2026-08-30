using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("== Starting FloV:MP Server ==");
Console.ResetColor();
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

_ = Task.Run(() =>
{
    while (true)
    {
        try
        {
            var ctx = listener.GetContext();
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
        listener.Stop();
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
        return 2;
    }
    serverDir = found;
}

Console.WriteLine($"[server] dir: {serverDir}");

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
