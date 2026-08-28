using System.Diagnostics;
using FloVMP.Connect;

// FloV:MP connector — запуск клиента alt:V на наш сервер без бэкенда alt:V.
//
//   FloVMP.Connect.exe -connect <ip:port> [--client <dir>] [--gta <dir>]
//                      [--port <n>] [--no-debug] [--keep-open]
//
// По умолчанию:
//   --client  = <exeDir>\..\..\..\..\..\runtime\client  (или CWD\runtime\client)
//   --gta     = автодетект из altv.toml рядом с клиентом, иначе спросить
//   --port    = 39987

// Диагностический режим: поднять только локальный бэкенд и ждать (для проверки роутов).
if (args.Contains("--cdn-only"))
{
    var cd = ResolveClientDir() ?? @"C:\FloV-MP\runtime\client";
    var p = 39987;
    for (var i = 0; i < args.Length - 1; i++) if (args[i] == "--port") int.TryParse(args[i + 1], out p);
    using var only = new LocalCdn(cd, p, Directory.Exists(Path.Combine(cd, "ui")) ? Path.Combine(cd, "ui") : null);
    only.Start();
    Console.WriteLine("[cdn-only] Ctrl+C для выхода");
    await Task.Delay(Timeout.Infinite);
    return 0;
}

var opts = ParseArgs(args);
if (opts is null) return 1;

var (connect, clientDir, gtaDir, port, debug, keepOpen) = opts.Value;

if (!File.Exists(Path.Combine(clientDir, "altv.exe")))
{
    Console.Error.WriteLine($"[err] не найден altv.exe в {clientDir}");
    return 2;
}
if (!File.Exists(Path.Combine(gtaDir, "GTA5.exe")))
{
    Console.Error.WriteLine($"[err] не найден GTA5.exe в {gtaDir}");
    return 2;
}

Console.WriteLine($"[connect] server : {connect}");
Console.WriteLine($"[connect] client : {clientDir}");
Console.WriteLine($"[connect] gta    : {gtaDir}");

// 1) локальный бэкенд
var uiDir = Directory.Exists(Path.Combine(clientDir, "ui")) ? Path.Combine(clientDir, "ui") : null;
using var cdn = new LocalCdn(clientDir, port, uiDir);
cdn.Start();

// 2) altv.toml (настоящий gtapath, без подмены GTA5.exe)
AltvToml.Write(clientDir, gtaDir, debug);
Console.WriteLine("[connect] altv.toml записан");

// 3) запуск клиента
var altv = Path.Combine(clientDir, "altv.exe");
var url = $"altv://connect/{connect}";
var argLine = $"-connecturl \"{url}\" -directlaunch -customui {cdn.BaseUrl}/ui/index.html";
Console.WriteLine($"[connect] запуск: altv.exe {argLine}");

var psi = new ProcessStartInfo(altv, argLine)
{
    WorkingDirectory = clientDir,
    UseShellExecute = false,
};
psi.Environment["SteamAppId"] = "271590";

using var proc = Process.Start(psi);
if (proc is null) { Console.Error.WriteLine("[err] не удалось запустить altv.exe"); return 3; }
Console.WriteLine($"[connect] altv.exe PID {proc.Id}. Жду завершения игры…");

// 4) ждём: пока жив altv.exe или GTA5.exe
var gtaSeen = false;
while (true)
{
    var altvUp = IsUp("altv.exe");
    var gtaUp = IsUp("GTA5.exe");
    if (gtaUp) gtaSeen = true;
    if (gtaSeen && !gtaUp) break;
    if (!gtaSeen && !altvUp)
    {
        await Task.Delay(3000);
        if (!IsUp("altv.exe") && !IsUp("GTA5.exe")) break;
    }
    await Task.Delay(500);
}

Console.WriteLine("[connect] игра закрыта, останавливаю локальный бэкенд");
if (keepOpen) { Console.WriteLine("нажмите Enter"); Console.ReadLine(); }
return 0;

// --- helpers ---------------------------------------------------------

static bool IsUp(string name)
{
    try { return Process.GetProcessesByName(Path.GetFileNameWithoutExtension(name)).Length > 0; }
    catch { return false; }
}

static (string connect, string clientDir, string gtaDir, int port, bool debug, bool keepOpen)? ParseArgs(string[] a)
{
    string? connect = null, client = null, gta = null;
    var port = 39987;
    var debug = true;
    var keepOpen = false;

    for (var i = 0; i < a.Length; i++)
    {
        switch (a[i])
        {
            case "-connect" or "--connect" when i + 1 < a.Length: connect = a[++i]; break;
            case "--client" when i + 1 < a.Length: client = a[++i]; break;
            case "--gta" when i + 1 < a.Length: gta = a[++i]; break;
            case "--port" when i + 1 < a.Length && int.TryParse(a[i + 1], out var p): port = p; i++; break;
            case "--no-debug": debug = false; break;
            case "--keep-open": keepOpen = true; break;
        }
    }

    if (connect is null)
    {
        Console.Error.WriteLine("использование: FloVMP.Connect.exe -connect <ip:port> [--client <dir>] [--gta <dir>] [--port <n>] [--no-debug] [--keep-open]");
        return null;
    }
    if (connect.StartsWith("altv://connect/", StringComparison.OrdinalIgnoreCase))
        connect = connect["altv://connect/".Length..];

    client ??= ResolveClientDir();
    if (client is null) { Console.Error.WriteLine("[err] не нашёл папку клиента, задайте --client <dir>"); return null; }

    gta ??= ResolveGtaDir(client);
    if (gta is null) { Console.Error.WriteLine("[err] не нашёл папку GTA V, задайте --gta <dir>"); return null; }

    return (connect, Path.GetFullPath(client), Path.GetFullPath(gta), port, debug, keepOpen);
}

static string? ResolveClientDir()
{
    foreach (var c in new[]
             {
                 Path.Combine(Environment.CurrentDirectory, "runtime", "client"),
                 Path.Combine(AppContext.BaseDirectory, "client"),
                 @"C:\FloV-MP\runtime\client",
             })
    {
        if (File.Exists(Path.Combine(c, "altv.exe"))) return c;
    }
    return null;
}

static string? ResolveGtaDir(string clientDir)
{
    // 1) из существующего altv.toml
    var toml = Path.Combine(clientDir, "altv.toml");
    if (File.Exists(toml))
    {
        foreach (var line in File.ReadAllLines(toml))
        {
            var t = line.Trim();
            if (t.StartsWith("gtapath", StringComparison.Ordinal))
            {
                var v = t[(t.IndexOf('=') + 1)..].Trim().Trim('\'', '"');
                if (Directory.Exists(v)) return v;
            }
        }
    }
    // 2) реестр Rockstar (пишется и Epic-, и RGL-установкой)
    if (OperatingSystem.IsWindows())
    {
        foreach (var view in new[] { Microsoft.Win32.RegistryView.Registry64, Microsoft.Win32.RegistryView.Registry32 })
        {
            try
            {
                using var hklm = Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, view);
                using var k = hklm.OpenSubKey(@"SOFTWARE\Rockstar Games\Grand Theft Auto V")
                            ?? hklm.OpenSubKey(@"SOFTWARE\WOW6432Node\Rockstar Games\Grand Theft Auto V");
                foreach (var name in new[] { "InstallFolderEpic", "InstallFolder", "InstallFolderSteam" })
                {
                    if (k?.GetValue(name) is string p && File.Exists(Path.Combine(p, "GTA5.exe"))) return p;
                }
            }
            catch { /* ignore */ }
        }
    }

    // 3) частые пути
    foreach (var c in new[]
             {
                 @"C:\Program Files\Rockstar Games\Grand Theft Auto V",
                 @"C:\Program Files (x86)\Steam\steamapps\common\Grand Theft Auto V",
                 @"C:\Program Files\Epic Games\GTAV",
             })
    {
        if (File.Exists(Path.Combine(c, "GTA5.exe"))) return c;
    }
    return null;
}
