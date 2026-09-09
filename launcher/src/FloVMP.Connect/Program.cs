using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using FloVMP.Connect;
using FloVMP.Launcher.Services;

Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;
EnableDebugPrivilege();

// FloV:MP connector — автономный запуск alt:V на наш сервер без внешнего бэкенда alt:V.
//
//   FloVMP.Connect.exe -connect <ip:port> [--client <dir>] [--gta <dir>]
//                      [--port <n>] [--platform <egs|steam|rgl>] [--no-debug] [--keep-open]

if (args.Contains("--cdn-only"))
{
    var cd = ResolveClientDir() ?? @"C:\FloV-MP\runtime\client";
    var p = 9988;
    for (var i = 0; i < args.Length - 1; i++) if (args[i] == "--port") int.TryParse(args[i + 1], out p);
    using var only = new LocalCdn(cd, p, Directory.Exists(Path.Combine(cd, "ui")) ? Path.Combine(cd, "ui") : null);
    only.Start();
    Console.WriteLine("[cdn-only] Ctrl+C для выхода");
    await Task.Delay(Timeout.Infinite);
    return 0;
}

var opts = ParseArgs(args);
if (opts is null) return 1;

var (connect, clientDir, gtaDir, port, debug, keepOpen, noDirectLaunch, platformOverride, nickname, gameArgs, priority, fpsLimit, gfxPreset) = opts.Value;

var flovmpExe = Path.Combine(clientDir, "flovmp.exe");
var clientExe = File.Exists(flovmpExe) ? flovmpExe : Path.Combine(clientDir, "altv.exe");
if (!File.Exists(clientExe))
{
    Console.Error.WriteLine($"[err] Не найден исполняемый файл клиента в папке {clientDir} (ожидался flovmp.exe или altv.exe)");
    return 2;
}

var gameExe = FindGameExecutable(gtaDir);
if (gameExe is null)
{
    Console.Error.WriteLine($"[err] Не найден исполняемый файл GTA V в папке {gtaDir} (ожидался GTA5.exe или GTA5_Enhanced.exe)");
    return 2;
}

var detectedPlatform = AltvToml.DetectPlatform(gtaDir);

Console.WriteLine($"[connect] Сервер    : {connect}");
Console.WriteLine($"[connect] Клиент    : {clientDir}");
Console.WriteLine($"[connect] GTA V     : {gtaDir}");
Console.WriteLine($"[connect] Файл игры : {gameExe}");
Console.WriteLine($"[connect] Платформа : {detectedPlatform.ToUpperInvariant()}");

// Автоматически проверяем и поднимаем сервер, если подключаемся к локальному хосту
if (connect.StartsWith("127.0.0.1") || connect.StartsWith("localhost"))
{
    var portTarget = 7788;
    if (connect.Contains(':') && int.TryParse(connect.Split(':')[1], out var parsedPort))
        portTarget = parsedPort;

    bool isListening = false;
    for (int retry = 0; retry < 2; retry++)
    {
        try
        {
            using var tcpProbe = new System.Net.Sockets.TcpClient();
            var connectTask = tcpProbe.ConnectAsync("127.0.0.1", portTarget);
            if (await Task.WhenAny(connectTask, Task.Delay(500)) == connectTask && tcpProbe.Connected)
            {
                isListening = true;
                break;
            }
        }
        catch { }
    }

    if (!isListening)
    {
        Console.WriteLine($"[connect] Сервер не отвечает на порту {portTarget}. Запускаю сервер Держава Онлайн...");
        var serverCandidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "server", "src", "FloVMP.ServerLauncher", "bin", "Release", "net8.0", "FloVMP.ServerLauncher.exe"),
            Path.Combine(Environment.CurrentDirectory, "server", "src", "FloVMP.ServerLauncher", "bin", "Release", "net8.0", "FloVMP.ServerLauncher.exe"),
            @"C:\FloV-MP\server\src\FloVMP.ServerLauncher\bin\Release\net8.0\FloVMP.ServerLauncher.exe"
        };

        var foundServer = serverCandidates.FirstOrDefault(File.Exists);
        if (foundServer is not null)
        {
            try
            {
                Process.Start(new ProcessStartInfo(foundServer) { UseShellExecute = true, WindowStyle = ProcessWindowStyle.Minimized });
                Console.WriteLine("[connect] Ожидаю инициализации сетевого интерфейса сервера...");
                for (int i = 0; i < 15; i++)
                {
                    await Task.Delay(1000);
                    try
                    {
                        using var tcpProbe = new System.Net.Sockets.TcpClient();
                        var connectTask = tcpProbe.ConnectAsync("127.0.0.1", portTarget);
                        if (await Task.WhenAny(connectTask, Task.Delay(500)) == connectTask && tcpProbe.Connected)
                        {
                            Console.WriteLine($"[connect] Сервер успешно запущен и принимает соединения (порт {portTarget}).");
                            isListening = true;
                            break;
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[connect] Не удалось автоматически запустить сервер: {ex.Message}");
            }
        }
    }
}

// Закрываем зависшие прошлые процессы игры/клиента и служб
var currentPid = Environment.ProcessId;
foreach (var stale in new[] { "FloVMP.Connect", "GTA5", "GTA5_Enhanced", "altv", "altv-webengine", "PlayGTAV", "GTA5_BE", "SocialClubHelper", "RockstarErrorHandler" })
{
    foreach (var pr in Process.GetProcessesByName(stale))
    {
        if (pr.Id == currentPid) continue;
        try 
        { 
            Console.WriteLine($"[connect] Закрываю старый процесс {stale} (PID {pr.Id})"); 
            pr.Kill(true); 
            pr.WaitForExit(3000); 
        }
        catch { }
    }
}

// Если GTA5 не запущена, сбрасываем зависшие процессы Rockstar Launcher, чтобы сбросить вечный статус «Запуск» в Epic Games
if (Process.GetProcessesByName("GTA5").Length == 0 && Process.GetProcessesByName("GTA5_Enhanced").Length == 0)
{
    foreach (var rglName in new[] { "Launcher", "LauncherPatcher" })
    {
        foreach (var pr in Process.GetProcessesByName(rglName))
        {
            try
            {
                var mod = pr.MainModule?.FileName;
                if (mod != null && (mod.Contains("Rockstar", StringComparison.OrdinalIgnoreCase) || mod.Contains("Social Club", StringComparison.OrdinalIgnoreCase)))
                {
                    Console.WriteLine($"[connect] Сбрасываю зависший Rockstar {rglName} (PID {pr.Id}) для чистого запуска");
                    pr.Kill(true);
                    pr.WaitForExit(2000);
                }
            }
            catch { }
        }
    }
}

// Прогрев платформы Epic Games, если игра куплена в Epic Games
if (detectedPlatform == "egs")
{
    EnsureEpicGamesLauncherRunning();
}

// Подготовка параметров запуска через commandline.txt в папке GTA V
PrepareGameCommandLine(gtaDir, gameArgs, fpsLimit);

// Очищаем старый кэш ресурсов клиента (кроме skin.bin), чтобы обновления скриптов применялись мгновенно
var cacheDir = Path.Combine(clientDir, "cache");
if (Directory.Exists(cacheDir))
{
    foreach (var subDir in Directory.GetDirectories(cacheDir))
    {
        try { Directory.Delete(subDir, true); } catch { }
    }
}

// 1) Локальный CDN бэкенд
var uiDir = Directory.Exists(Path.Combine(clientDir, "ui")) ? Path.Combine(clientDir, "ui") : null;
using var cdn = new LocalCdn(clientDir, port, uiDir);
cdn.Start();

// 2) altv.toml
AltvToml.Write(clientDir, gtaDir, debug, platformOverride, nickname);
Console.WriteLine("[connect] flovmp.toml записан");

// 2.7) skin.bin — патчим customUiUrl для загрузки NUI
var customUi = $"{cdn.BaseUrl}/ui/index.html";
foreach (var skinPath in new[] { Path.Combine(clientDir, "cache", "skin.bin"), Path.Combine(clientDir, "skin.bin") })
{
    if (SkinPatcher.PatchSkinBin(skinPath, customUi))
        Console.WriteLine($"[connect] skin.bin успешно пропатчен ({Path.GetFileName(skinPath)})");
}

try
{
    // 3) Запуск игрового клиента FloV:MP
    var url = $"altv://connect/{connect}";
    var direct = noDirectLaunch ? "" : " -directlaunch";
    var argLine = $"-connecturl \"{url}\"{direct} -customui {customUi} -noupdate";
    var exeName = Path.GetFileName(clientExe);
    Console.WriteLine($"[connect] Запуск: {exeName} {argLine}");

    var psi = new ProcessStartInfo(clientExe, argLine)
    {
        WorkingDirectory = clientDir,
        UseShellExecute = false,
    };

    if (detectedPlatform == "steam")
        psi.Environment["SteamAppId"] = "271590";
    else
        psi.Environment.Remove("SteamAppId");

    using var proc = Process.Start(psi);
    if (proc is null)
    {
        Console.Error.WriteLine($"[err] Не удалось запустить {exeName}");
        return 3;
    }
    Console.WriteLine($"[connect] {exeName} запущен (PID {proc.Id}). Ожидаю запуска и завершения игры...");

    // 4) Ждём завершения
    var gtaSeen = false;
    var waitStopwatch = Stopwatch.StartNew();
    const string windowTitle = "Держава Онлайн (FloV:MP)";
    while (true)
    {
        var altvUp = IsUp("altv.exe") || IsUp("flovmp.exe");
        var gtaUp = IsUp("GTA5.exe") || IsUp("GTA5_Enhanced.exe");
        if (gtaUp)
        {
            if (!gtaSeen)
            {
                Console.WriteLine("[connect] GTA5.exe обнаружен в процессах! Игра успешно запущена.");
                gtaSeen = true;
                ApplyGamePriority(priority);
            }
            UpdateGameWindowTitle(windowTitle);
        }
        else if (altvUp)
        {
            UpdateGameWindowTitle(windowTitle);
        }

        if (gtaSeen && !gtaUp)
        {
            Console.WriteLine("[connect] Процесс GTA V завершен.");
            break;
        }

        if (!gtaSeen)
        {
            // Ждем до 75 секунд, пока Rockstar Launcher и Epic Games проводят авторизацию и запускают игру
            if (waitStopwatch.Elapsed > TimeSpan.FromSeconds(75) && !altvUp)
            {
                Console.WriteLine("[connect] Время ожидания старта игры истекло (75 сек).");
                break;
            }
        }
        await Task.Delay(500);
    }
}
finally
{
    CleanupGameCommandLine(gtaDir);
    Console.WriteLine("[connect] Игра закрыта, останавливаю локальный бэкенд.");
}

if (keepOpen) { Console.WriteLine("Нажмите Enter для выхода..."); Console.ReadLine(); }
return 0;

// --- helpers ---------------------------------------------------------

[DllImport("user32.dll", EntryPoint = "SetWindowTextW", CharSet = CharSet.Unicode, SetLastError = true)]
static extern bool SetWindowText(IntPtr hWnd, string lpString);

[DllImport("user32.dll", SetLastError = true)]
static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

[DllImport("user32.dll")]
static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

static void UpdateGameWindowTitle(string newTitle)
{
    try
    {
        var targetPids = new HashSet<int>();
        foreach (var name in new[] { "GTA5", "GTA5_Enhanced", "flovmp", "altv" })
        {
            foreach (var p in Process.GetProcessesByName(name))
            {
                targetPids.Add(p.Id);
                if (p.MainWindowHandle != IntPtr.Zero)
                {
                    SetWindowText(p.MainWindowHandle, newTitle);
                }
            }
        }

        if (targetPids.Count > 0)
        {
            EnumWindows((hWnd, lParam) =>
            {
                if (GetWindowThreadProcessId(hWnd, out var pid) != 0 && targetPids.Contains((int)pid))
                {
                    SetWindowText(hWnd, newTitle);
                }
                return true;
            }, IntPtr.Zero);
        }
    }
    catch { }
}

static bool IsUp(string name)
{
    try { return Process.GetProcessesByName(Path.GetFileNameWithoutExtension(name)).Length > 0; }
    catch { return false; }
}

static string? FindGameExecutable(string gtaDir)
{
    foreach (var name in new[] { "GTA5.exe", "GTA5_Enhanced.exe" })
        if (File.Exists(Path.Combine(gtaDir, name))) return name;
    return null;
}

static (string connect, string clientDir, string gtaDir, int port, bool debug, bool keepOpen, bool noDirectLaunch, string? platformOverride, string? nickname, string? gameArgs, string? priority, int fpsLimit, string? gfxPreset)? ParseArgs(string[] a)
{
    string? connect = null, client = null, gta = null, platformOverride = null, nickname = null, host = null;
    string? gameArgs = null, priority = null, gfxPreset = null;
    int fpsLimit = 0;
    int? sPort = null;
    var port = 9988;
    var debug = true;
    var keepOpen = false;
    var noDirectLaunch = false;

    for (var i = 0; i < a.Length; i++)
    {
        switch (a[i])
        {
            case "-connect" or "--connect" when i + 1 < a.Length: connect = a[++i]; break;
            case "--host" or "-host" when i + 1 < a.Length: host = a[++i]; break;
            case "--server-port" when i + 1 < a.Length && int.TryParse(a[i + 1], out var sp): sPort = sp; i++; break;
            case "--nick" or "-nick" or "--nickname" when i + 1 < a.Length: nickname = a[++i]; break;
            case "--client" when i + 1 < a.Length: client = a[++i]; break;
            case "--gta" when i + 1 < a.Length: gta = a[++i]; break;
            case "--platform" when i + 1 < a.Length: platformOverride = a[++i]; break;
            case "--port" when i + 1 < a.Length && int.TryParse(a[i + 1], out var p): port = p; i++; break;
            case "--no-debug": debug = false; break;
            case "--keep-open": keepOpen = true; break;
            case "--no-directlaunch": noDirectLaunch = true; break;
            case "--game-args" when i + 1 < a.Length: gameArgs = a[++i]; break;
            case "--priority" when i + 1 < a.Length: priority = a[++i]; break;
            case "--fps-limit" when i + 1 < a.Length && int.TryParse(a[i + 1], out var fl): fpsLimit = fl; i++; break;
            case "--gfx-preset" when i + 1 < a.Length: gfxPreset = a[++i]; break;
        }
    }

    if (connect is null && !string.IsNullOrWhiteSpace(host))
    {
        connect = $"{host}:{sPort ?? 7788}";
    }

    if (connect is null)
    {
        Console.Error.WriteLine("Использование: FloVMP.Connect.exe -connect <ip:port> [--client <dir>] [--gta <dir>] [--nick <name>] [--port <n>] [--platform <egs|steam|rgl>] [--no-debug] [--keep-open] [--no-directlaunch]");
        return null;
    }
    if (connect.StartsWith("altv://connect/", StringComparison.OrdinalIgnoreCase))
        connect = connect["altv://connect/".Length..];

    client ??= ResolveClientDir();
    if (client is null)
    {
        Console.Error.WriteLine("[err] Не найдена папка клиента FloV:MP (runtime/client). Задайте путь через --client <dir>");
        return null;
    }

    gta ??= ResolveGtaDir(client);
    if (gta is null)
    {
        Console.Error.WriteLine("[err] Не найдена папка с установленной GTA V. Задайте путь через --gta <dir>");
        return null;
    }

    if (!string.IsNullOrWhiteSpace(platformOverride) &&
        !new[] { "egs", "steam", "rgl", "rockstar", "epic" }.Contains(platformOverride.Trim().ToLowerInvariant()))
    {
        Console.Error.WriteLine("[err] --platform допускает только egs, steam или rgl");
        return null;
    }

    return (connect, Path.GetFullPath(client), Path.GetFullPath(gta), port, debug, keepOpen, noDirectLaunch, platformOverride, nickname, gameArgs, priority, fpsLimit, gfxPreset);
}

static string? ResolveClientDir()
{
    var dir = AppContext.BaseDirectory;
    for (var i = 0; i < 8; i++)
    {
        var candidate = Path.Combine(dir, "runtime", "client");
        if (Directory.Exists(candidate) &&
            (File.Exists(Path.Combine(candidate, "altv.exe")) || File.Exists(Path.Combine(candidate, "flovmp.exe"))))
        {
            return Path.GetFullPath(candidate);
        }

        var parent = Directory.GetParent(dir);
        if (parent == null) break;
        dir = parent.FullName;
    }

    var hardcoded = @"C:\FloV-MP\runtime\client";
    if (Directory.Exists(hardcoded) &&
        (File.Exists(Path.Combine(hardcoded, "altv.exe")) || File.Exists(Path.Combine(hardcoded, "flovmp.exe"))))
    {
        return hardcoded;
    }

    var appData = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FloridaV", "runtime", "client");
    if (Directory.Exists(appData)) return appData;

    return null;
}

static string? ResolveGtaDir(string clientDir)
{
    var candidates = GtaLocator.Detect();
    if (candidates.Count > 0)
    {
        var best = candidates.FirstOrDefault(c => c.IsComplete) ?? candidates[0];
        Console.WriteLine($"[connect] Автоопределена GTA V: {best.Path} [{best.Source}]");
        return best.Path;
    }

    Console.WriteLine();
    Console.WriteLine("[connect] GTA V не найдена автоматически.");
    Console.WriteLine("[connect] Открываю окно выбора папки GTA V...");
    var selected = PromptUserForGtaFolder();
    if (!string.IsNullOrWhiteSpace(selected))
    {
        Console.WriteLine($"[connect] Выбрана папка: {selected}");
        return selected;
    }

    return null;
}

static string? PromptUserForGtaFolder()
{
    string? selected = null;
    var thread = new Thread(() =>
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = "FloV:MP — Выберите папку с установленной GTA V (Legacy или Enhanced)",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };
        if (dlg.ShowDialog() == DialogResult.OK && Directory.Exists(dlg.SelectedPath))
        {
            if (FindGameExecutable(dlg.SelectedPath) is not null)
                selected = dlg.SelectedPath;
        }
    });
    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();
    return selected;
}

static void EnsureEpicGamesLauncherRunning()
{
    var egsProc = Process.GetProcessesByName("EpicGamesLauncher");
    var egsWeb = Process.GetProcessesByName("EpicWebHelper");
    if (egsProc.Length > 0 && egsWeb.Length > 0)
    {
        Console.WriteLine("[connect] Epic Games Launcher активен и готов.");
        return;
    }

    Console.WriteLine("[connect] Epic Games Launcher не готов. Выполняю предварительный прогрев EGS...");
    var epicCandidates = new[]
    {
        @"C:\Program Files\Epic Games\Launcher\Portal\Binaries\Win64\EpicGamesLauncher.exe",
        @"C:\Program Files (x86)\Epic Games\Launcher\Portal\Binaries\Win64\EpicGamesLauncher.exe",
    };
    var found = epicCandidates.FirstOrDefault(File.Exists);
    try
    {
        if (found != null)
        {
            Process.Start(new ProcessStartInfo(found, "-Silent") { UseShellExecute = true, WindowStyle = ProcessWindowStyle.Minimized });
        }
        else
        {
            Process.Start(new ProcessStartInfo("com.epicgames.launcher://") { UseShellExecute = true });
        }
        for (int i = 0; i < 40; i++)
        {
            Thread.Sleep(500);
            if (Process.GetProcessesByName("EpicWebHelper").Length > 0)
            {
                Console.WriteLine("[connect] Epic Games Launcher инициализирован. Ожидаю EOS аутентификацию (2.5 сек)...");
                Thread.Sleep(2500);
                break;
            }
            else if (i > 10 && Process.GetProcessesByName("EpicGamesLauncher").Length > 0)
            {
                Console.WriteLine("[connect] Epic Games Launcher обнаружен. Ожидаю готовность...");
                Thread.Sleep(2000);
                break;
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[connect] Предупреждение EGS: {ex.Message}");
    }
}

static void PrepareGameCommandLine(string gtaDir, string? gameArgs, int fpsLimit)
{
    var cmdFile = Path.Combine(gtaDir, "commandline.txt");
    var parts = new List<string>();
    if (!string.IsNullOrWhiteSpace(gameArgs)) parts.Add(gameArgs.Trim());
    if (fpsLimit > 0) parts.Add($"-FPSLimit {fpsLimit}");
    if (parts.Count > 0)
    {
        try
        {
            File.WriteAllLines(cmdFile, parts);
            Console.WriteLine($"[connect] Применены параметры в commandline.txt: {string.Join(' ', parts)}");
        }
        catch { }
    }
}

static void CleanupGameCommandLine(string gtaDir)
{
    try
    {
        var cmdFile = Path.Combine(gtaDir, "commandline.txt");
        if (File.Exists(cmdFile)) File.Delete(cmdFile);
    }
    catch { }
}

static void ApplyGamePriority(string? priority)
{
    if (string.IsNullOrWhiteSpace(priority) || priority == "normal") return;
    try
    {
        foreach (var name in new[] { "GTA5", "GTA5_Enhanced" })
        {
            foreach (var pr in Process.GetProcessesByName(name))
            {
                if (pr.HasExited) continue;
                if (priority == "high" && pr.PriorityClass != ProcessPriorityClass.High)
                {
                    pr.PriorityClass = ProcessPriorityClass.High;
                    Console.WriteLine($"[connect] Выставлен высокий приоритет процесса для {name} (PID {pr.Id})");
                }
            }
        }
    }
    catch { }
}

static void EnableDebugPrivilege()
{
    try
    {
        const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
        const uint TOKEN_QUERY = 0x0008;
        const uint SE_PRIVILEGE_ENABLED = 0x00000002;
        if (OpenProcessToken(Process.GetCurrentProcess().Handle, TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out var hToken))
        {
            if (LookupPrivilegeValue(null, "SeDebugPrivilege", out var luid))
            {
                var tp = new TOKEN_PRIVILEGES
                {
                    PrivilegeCount = 1,
                    Luid = luid,
                    Attributes = SE_PRIVILEGE_ENABLED
                };
                AdjustTokenPrivileges(hToken, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
            }
            CloseHandle(hToken);
        }
    }
    catch { }
}

[DllImport("advapi32.dll", SetLastError = true)]
static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

[DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
static extern bool LookupPrivilegeValue(string? lpSystemName, string lpName, out LUID lpLuid);

[DllImport("advapi32.dll", SetLastError = true)]
static extern bool AdjustTokenPrivileges(IntPtr TokenHandle, bool DisableAllPrivileges, ref TOKEN_PRIVILEGES NewState, uint BufferLength, IntPtr PreviousState, IntPtr ReturnLength);

[DllImport("kernel32.dll", SetLastError = true)]
static extern bool CloseHandle(IntPtr hObject);

delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

[StructLayout(LayoutKind.Sequential)]
struct LUID { public uint LowPart; public int HighPart; }

[StructLayout(LayoutKind.Sequential, Pack = 1)]
struct TOKEN_PRIVILEGES { public uint PrivilegeCount; public LUID Luid; public uint Attributes; }

