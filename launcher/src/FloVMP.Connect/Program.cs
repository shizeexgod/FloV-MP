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

// WinExe без консоли: весь вывод коннектора уходит в файл-лог, чтобы игрок
// не видел техническую консоль, но диагностика сохранялась для поддержки.
try
{
    var _logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FloVMP");
    Directory.CreateDirectory(_logDir);
    var _sw = new StreamWriter(Path.Combine(_logDir, "connect.log"), append: false, new UTF8Encoding(false)) { AutoFlush = true };
    Console.SetOut(_sw);
    Console.SetError(_sw);
}
catch { /* если лог недоступен — просто работаем без него, окна всё равно нет */ }
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

// Настраиваем прямой маршрут к серверу в обход VPN/TUN (исключает таймауты загрузки ресурсов)
EnsureDirectRouteToHost(connect);

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
        Console.WriteLine($"[connect] Сервер не отвечает на порту {portTarget}. Запускаю локальный сервер FloV:MP...");
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

// EnsureRockstarLauncherRunning(); // Отключено: запуск RGL с -silent без токенов Epic Games вызывает ошибку 00000009 (отсутствие entitlement). GTA V сама корректно инициализирует RGL через Epic Games.

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
    // Локальная altv-client.dll теперь — ПОДЛИННЫЙ клиент 16.4.39, совпадающий
    // с билдом сервера (см. scripts/fix-client-build.cmd). Патч и де-рейс через
    // CDN больше не нужны, поэтому ПО УМОЛЧАНИЮ передаём -noupdate: клиент грузит
    // локальную altv-client.dll напрямую и НЕ идёт в update-флоу. Это убирает
    // петлю перекачки altv-client.dll (update.json -> dll -> update.json ...),
    // из-за которой клиент зависал на этапе лаунчера и GTA не стартовала.
    // Вернуть старый де-рейс (докачку патченой копии из patched/) можно флагом
    // --force-update.
    var noUpd = args.Contains("--force-update") ? "" : " -noupdate";
    var argLine = $"-connecturl \"{url}\"{direct} -customui {customUi}{noUpd}";
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
    var lastRglCheck = Stopwatch.StartNew();
    const string windowTitle = "FloV:MP Standalone Client";
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

        if (!gtaSeen && lastRglCheck.ElapsedMilliseconds >= 1000)
        {
            lastRglCheck.Restart();
            DismissRockstarCloudSyncDialog();
        }

        if (gtaSeen && !gtaUp)
        {
            Console.WriteLine("[connect] Процесс GTA V завершен.");
            break;
        }

        if (!gtaSeen)
        {
            // Ждем до 240 секунд, пока Rockstar Launcher и Epic Games проводят авторизацию и запускают игру.
            // Первый запуск: компиляция шейдеров + RGL/EGS авторизация может занять 2-3 минуты.
            // Если alt:V уже запущен — не применяем таймаут (игра грузится, просто ждём).
            if (waitStopwatch.Elapsed > TimeSpan.FromSeconds(240) && !altvUp)
            {
                Console.WriteLine("[connect] Время ожидания старта игры истекло (240 сек).");
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

[DllImport("user32.dll", SetLastError = true)]
static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

[DllImport("user32.dll", SetLastError = true)]
static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

[DllImport("user32.dll")]
static extern bool IsWindowVisible(IntPtr hWnd);

[DllImport("user32.dll", EntryPoint = "GetClassNameW", CharSet = CharSet.Unicode, SetLastError = true)]
static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);


static void DismissRockstarCloudSyncDialog()
{
    try
    {
        EnumWindows((hWnd, lParam) =>
        {
            if (!IsWindowVisible(hWnd)) return true;
            var sb = new StringBuilder(256);
            GetClassName(hWnd, sb, 256);
            var cls = sb.ToString();
            if (cls == "Rockstar Games Launcher")
            {
                if (GetWindowRect(hWnd, out var rect))
                {
                    int w = rect.Right - rect.Left;
                    int h = rect.Bottom - rect.Top;
                    if (w >= 400 && h >= 300)
                    {
                        int btnX = (int)(w * 0.35);
                        int btnY = (int)(h * 0.675);
                        IntPtr btnLParam = (IntPtr)((btnY << 16) | (btnX & 0xFFFF));
                        const uint WM_LBUTTONDOWN = 0x0201;
                        const uint WM_LBUTTONUP = 0x0202;
                        PostMessage(hWnd, WM_LBUTTONDOWN, (IntPtr)1, btnLParam);
                        Thread.Sleep(30);
                        PostMessage(hWnd, WM_LBUTTONUP, IntPtr.Zero, btnLParam);
                    }
                }
            }
            return true;
        }, IntPtr.Zero);
    }
    catch { }
}

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

static string? ResolveEpicLauncherPath()
{
    var candidates = new[]
    {
        @"C:\Program Files\Epic Games\Launcher\Portal\Binaries\Win64\EpicGamesLauncher.exe",
        @"C:\Program Files (x86)\Epic Games\Launcher\Portal\Binaries\Win64\EpicGamesLauncher.exe",
    };
    var found = candidates.FirstOrDefault(File.Exists);
    if (found != null) return found;

    // Реестр: HKLM\...\Epic Games\EpicGamesLauncher AppPath (папка Launcher/Portal/Binaries/Win64)
    foreach (var root in new[] { @"SOFTWARE\WOW6432Node\Epic Games\EpicGamesLauncher", @"SOFTWARE\Epic Games\EpicGamesLauncher" })
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(root);
            var appPath = key?.GetValue("AppPath") as string;
            if (!string.IsNullOrWhiteSpace(appPath) && File.Exists(appPath)) return appPath;
        }
        catch { }
    }
    return null;
}

/// <summary>
/// Гарантирует, что Epic Games Launcher реально готов выдать игру. «Закрытый»
/// EGL часто оставляет фоновые процессы в трее, но игра через них НЕ
/// запускается (flovmp.exe -directlaunch зависает в ожидании entitlement).
/// Поэтому не доверяем простому наличию процессов — всегда будим EGL
/// (идемпотентно, он single-instance) и ждём готовности EpicWebHelper.
/// </summary>
static void EnsureEpicGamesLauncherRunning()
{
    bool WebReady() => Process.GetProcessesByName("EpicWebHelper").Length > 0;
    bool MainRunning() => Process.GetProcessesByName("EpicGamesLauncher").Length > 0;

    var found = ResolveEpicLauncherPath();
    Console.WriteLine(MainRunning()
        ? "[connect] Epic Games Launcher: есть фоновые процессы — пробуждаю до рабочего состояния..."
        : "[connect] Epic Games Launcher не запущен — запускаю и жду готовности...");

    try
    {
        if (found != null)
            Process.Start(new ProcessStartInfo(found, "-Silent") { UseShellExecute = true, WindowStyle = ProcessWindowStyle.Minimized });
        else
            Process.Start(new ProcessStartInfo("com.epicgames.launcher://") { UseShellExecute = true });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[connect] Предупреждение EGS (запуск): {ex.Message}");
    }

    // Ждём EpicWebHelper — это признак, что EGS/EOS поднялись и готовы
    // авторизовать запуск игры. До 40 сек.
    for (int i = 0; i < 80; i++)
    {
        Thread.Sleep(500);
        if (WebReady())
        {
            Console.WriteLine("[connect] Epic Games Launcher готов (EpicWebHelper активен). Добиваю EOS-аутентификацию...");
            Thread.Sleep(3000);
            return;
        }
    }

    if (MainRunning())
        Console.WriteLine("[connect] EGL запущен, но EpicWebHelper не поднялся. Вероятно, нужен вход в аккаунт Epic — открой EGL, войди и повтори запуск.");
    else
        Console.WriteLine("[connect] ВНИМАНИЕ: не удалось запустить Epic Games Launcher. Открой его вручную, войди в аккаунт и повтори.");
}

// Прогрев Rockstar Games Launcher / Social Club — чтобы GTA V не зависла на
// "RGL initialization". Стартуем в тихом свёрнутом режиме и ждём готовности.
static void EnsureRockstarLauncherRunning()
{
    if (IsRockstarLauncherReady())
    {
        Console.WriteLine("[connect] Rockstar Games Launcher активен и готов.");
        return;
    }

    var path = FindRockstarLauncher();
    if (path == null)
    {
        Console.WriteLine("[connect] Rockstar Games Launcher не найден — пропускаю прогрев (игра запустит его сама).");
        return;
    }

    Console.WriteLine("[connect] Rockstar Games Launcher не готов. Выполняю предварительный прогрев...");
    try
    {
        Process.Start(new ProcessStartInfo(path, "-silent")
        {
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Minimized,
        });
        for (int i = 0; i < 50; i++) // до ~25 секунд
        {
            Thread.Sleep(500);
            if (IsRockstarLauncherReady())
            {
                Console.WriteLine("[connect] Rockstar Games Launcher инициализирован. Ожидаю готовность Social Club (2 сек)...");
                Thread.Sleep(2000);
                return;
            }
        }
        Console.WriteLine("[connect] Rockstar Games Launcher не подтвердил готовность за 25с — продолжаю запуск.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[connect] Предупреждение RGL: {ex.Message}");
    }
}

static bool IsRockstarLauncherReady()
{
    // Rockstar-овский Launcher.exe (не Epic'овский) ИЛИ SocialClubHelper —
    // признаки, что Social Club/RGL поднялся и готов обслуживать GTA5.
    if (Process.GetProcessesByName("SocialClubHelper").Length > 0) return true;
    foreach (var p in Process.GetProcessesByName("Launcher"))
    {
        try
        {
            var m = p.MainModule?.FileName;
            if (m != null && m.Contains("Rockstar", StringComparison.OrdinalIgnoreCase)) return true;
        }
        catch { /* доступ к MainModule может кинуть — игнорируем */ }
    }
    return false;
}

static string? FindRockstarLauncher()
{
    try
    {
        using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Rockstar Games\Launcher")
                     ?? Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Rockstar Games\Launcher");
        if (key?.GetValue("InstallFolder") is string folder && !string.IsNullOrWhiteSpace(folder))
        {
            var exe = Path.Combine(folder, "Launcher.exe");
            if (File.Exists(exe)) return exe;
        }
    }
    catch { /* нет прав к реестру — падаем на дефолтные пути */ }

    foreach (var c in new[]
    {
        @"C:\Program Files\Rockstar Games\Launcher\Launcher.exe",
        @"C:\Program Files (x86)\Rockstar Games\Launcher\Launcher.exe",
    })
        if (File.Exists(c)) return c;

    return null;
}

static void PrepareGameCommandLine(string gtaDir, string? gameArgs, int fpsLimit)
{
    var cmdFile = Path.Combine(gtaDir, "commandline.txt");
    var parts = new List<string> { "-scDisableCloudSaves", "-noCloud" };
    if (!string.IsNullOrWhiteSpace(gameArgs)) parts.Add(gameArgs.Trim());
    if (fpsLimit > 0) parts.Add($"-FPSLimit {fpsLimit}");
    try
    {
        File.WriteAllLines(cmdFile, parts);
        Console.WriteLine($"[connect] Применены параметры в commandline.txt: {string.Join(' ', parts)}");
    }
    catch { }
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

static bool IsRunningAsAdmin()
{
    try
    {
        using var id = System.Security.Principal.WindowsIdentity.GetCurrent();
        var principal = new System.Security.Principal.WindowsPrincipal(id);
        return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
    }
    catch { return false; }
}

/// <summary>
/// Добавляет прямой /32-маршрут к серверу через ФИЗИЧЕСКИЙ шлюз в обход
/// активного VPN/TUN (Happ и т.п.) — иначе загрузка ресурсов уходит в
/// таймаут (curl error 28), т.к. TUN перехватывает 0.0.0.0/0. route add
/// требует прав администратора: если их нет, поднимаем ТОЛЬКО эту команду
/// через UAC (один запрос), не элевируя саму игру.
/// </summary>
static void EnsureDirectRouteToHost(string connectTarget)
{
    try
    {
        var host = connectTarget.Split(':')[0].Trim();
        if (host is "127.0.0.1" or "localhost" || string.IsNullOrWhiteSpace(host)) return;
        if (!System.Net.IPAddress.TryParse(host, out var hostIp)
            || hostIp.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) return;

        // Шлюз ТОЛЬКО физического интерфейса (Ethernet/Wi-Fi) и ТОЛЬКО IPv4 —
        // иначе route add с IPv6-шлюзом (fe80::…) невалиден и падает.
        var physicalGateway = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
            .Where(ni => ni.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up
                     && ni.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback
                     && ni.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Tunnel
                     && !ni.Description.Contains("tun", StringComparison.OrdinalIgnoreCase)
                     && !ni.Description.Contains("tap", StringComparison.OrdinalIgnoreCase)
                     && !ni.Description.Contains("vpn", StringComparison.OrdinalIgnoreCase)
                     && !ni.Description.Contains("wintun", StringComparison.OrdinalIgnoreCase)
                     && !ni.Description.Contains("sing-box", StringComparison.OrdinalIgnoreCase)
                     && !ni.Name.Contains("happ", StringComparison.OrdinalIgnoreCase))
            .SelectMany(ni => ni.GetIPProperties().GatewayAddresses)
            .Where(g => g?.Address != null
                     && g.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            .Select(g => g.Address.ToString())
            .FirstOrDefault(g => !string.IsNullOrEmpty(g) && g != "0.0.0.0");

        if (string.IsNullOrEmpty(physicalGateway))
        {
            Console.WriteLine("[connect] Прямой маршрут: физический IPv4-шлюз не найден — пропускаю (при активном VPN возможен таймаут загрузки).");
            return;
        }

        Console.WriteLine($"[connect] Прямой маршрут к {host} через физический шлюз {physicalGateway} (в обход VPN/TUN)...");
        var admin = IsRunningAsAdmin();

        // Снимаем возможный устаревший маршрут (с другим шлюзом) — молча.
        RunRoute($"delete {host}", admin, silent: true);

        var ok = RunRoute($"add {host} mask 255.255.255.255 {physicalGateway} metric 1", admin, silent: false);
        if (ok)
            Console.WriteLine("[connect] Прямой маршрут добавлен — трафик к серверу идёт мимо VPN.");
        else if (!admin)
            Console.WriteLine("[connect] ВНИМАНИЕ: маршрут не добавлен (нет прав администратора). Запустите лаунчер/коннектор от имени администратора, иначе при активном VPN загрузка ресурсов уйдёт в таймаут.");
        else
            Console.WriteLine("[connect] ВНИМАНИЕ: команда route add вернула ошибку — проверьте активные VPN/маршруты.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[connect] Предупреждение: не удалось добавить прямой маршрут: {ex.Message}");
    }
}

/// <summary>Запускает route.exe; если нет прав админа — поднимает через UAC (runas).</summary>
static bool RunRoute(string args, bool admin, bool silent)
{
    try
    {
        var psi = new ProcessStartInfo("route", args) { CreateNoWindow = true };
        if (admin)
        {
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
        }
        else
        {
            // route add требует элевации — поднимаем ТОЛЬКО эту команду (один UAC)
            psi.UseShellExecute = true;
            psi.Verb = "runas";
            psi.WindowStyle = ProcessWindowStyle.Hidden;
        }
        using var p = Process.Start(psi);
        if (p == null) return false;
        p.WaitForExit(4000);
        return p.HasExited && p.ExitCode == 0;
    }
    catch (System.ComponentModel.Win32Exception)
    {
        // пользователь отклонил UAC — не критично для delete, критично для add (залогируем выше)
        if (!silent) Console.WriteLine("[connect] UAC-запрос на добавление маршрута отклонён.");
        return false;
    }
    catch { return false; }
}

delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

[StructLayout(LayoutKind.Sequential)]
struct LUID { public uint LowPart; public int HighPart; }

[StructLayout(LayoutKind.Sequential, Pack = 1)]
struct TOKEN_PRIVILEGES { public uint PrivilegeCount; public LUID Luid; public uint Attributes; }

[StructLayout(LayoutKind.Sequential)]
struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

