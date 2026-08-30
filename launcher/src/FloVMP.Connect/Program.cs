using System.Diagnostics;
using System.Windows.Forms;
using FloVMP.Connect;
using FloVMP.Launcher.Services;

// FloV:MP connector — запуск клиента alt:V на наш сервер без бэкенда alt:V.
//
//   FloVMP.Connect.exe -connect <ip:port> [--client <dir>] [--gta <dir>]
//                      [--port <n>] [--platform <steam|rgl|rockstar>] [--no-debug] [--keep-open]
//
// По умолчанию:
//   --client  = <exeDir>\..\..\..\..\..\runtime\client  (или CWD\runtime\client)
//   --gta     = автодетект из altv.toml рядом с клиентом, иначе спросить
//   --port    = 39987

// Диагностический режим: поднять только локальный бэкенд и ждать (для проверки роутов).
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

var (connect, clientDir, gtaDir, port, debug, keepOpen, noDirectLaunch, platformOverride) = opts.Value;

if (!File.Exists(Path.Combine(clientDir, "altv.exe")))
{
    Console.Error.WriteLine($"[err] не найден altv.exe в {clientDir}");
    return 2;
}
var gameExe = FindGameExecutable(gtaDir);
if (gameExe is null)
{
    Console.Error.WriteLine($"[err] не найден GTA V executable в {gtaDir} (ожидался GTA5.exe или GTA5_Enhanced.exe)");
    return 2;
}

Console.WriteLine($"[connect] server : {connect}");
Console.WriteLine($"[connect] client : {clientDir}");
Console.WriteLine($"[connect] gta    : {gtaDir}");
Console.WriteLine($"[connect] exe    : {gameExe}");

// 0) alt:V должен САМ запустить GTA5.exe (suspended). Если игра/клиент/старый коннектор уже
//    запущены — закрываем их для чистого старта.
var currentPid = Environment.ProcessId;
foreach (var stale in new[] { "FloVMP.Connect", "GTA5", "GTA5_Enhanced", "altv", "altv-webengine", "PlayGTAV", "GTA5_BE" })
{
    foreach (var pr in Process.GetProcessesByName(stale))
    {
        if (pr.Id == currentPid) continue;
        try { Console.WriteLine($"[connect] закрываю уже запущенный {stale} (PID {pr.Id})"); pr.Kill(true); pr.WaitForExit(3000); }
        catch { }
    }
}

// 1) локальный бэкенд
var uiDir = Directory.Exists(Path.Combine(clientDir, "ui")) ? Path.Combine(clientDir, "ui") : null;
using var cdn = new LocalCdn(clientDir, port, uiDir);
cdn.Start();

// 2) altv.toml (настоящий gtapath, без подмены GTA5.exe)
AltvToml.Write(clientDir, gtaDir, debug, platformOverride);
Console.WriteLine("[connect] altv.toml записан");

// 2.5) BattlEye: ничего не ломаем. Чиним службу, если её испортил прошлый
// заход, и советуем отключить BE в Rockstar Launcher.
if (OperatingSystem.IsWindows()) BattlEye.RepairServiceIfBroken();
BattlEye.Advise(gtaDir);

// 2.6) Epic: GTA5.exe (Epic-копия) при прямом запуске без запущенного
// Epic Games Launcher не получает auth -> падает с "Не удалось запустить
// Steam". Лаунчер Epic должен быть ЗАПУЩЕН и залогинен.
if (AltvToml.DetectPlatform(gtaDir) == "rgl" && !IsUp("EpicGamesLauncher.exe"))
{
    Console.WriteLine();
    Console.WriteLine("  == ВНИМАНИЕ: Epic Games Launcher не запущен ==================");
    Console.WriteLine("  GTA V куплена в Epic. Запусти Epic Games Launcher, залогинься,");
    Console.WriteLine("  оставь его открытым — иначе GTA5.exe упадёт с ошибкой Steam.");
    Console.WriteLine("  (Rockstar Games Launcher закрой.) Затем повтори этот запуск.");
    Console.WriteLine("  ===========================================================");
    Console.WriteLine();
}

// 2.7) skin.bin — внедряем SHA-256 хэш customUiUrl для надежной загрузки NUI-оболочки
var customUi = $"{cdn.BaseUrl}/ui/index.html";
foreach (var skinPath in new[] { Path.Combine(clientDir, "cache", "skin.bin"), Path.Combine(clientDir, "skin.bin") })
{
    if (SkinPatcher.PatchSkinBin(skinPath, customUi))
        Console.WriteLine($"[connect] skin.bin успешно пропатчен ({Path.GetFileName(skinPath)})");
}

try
{
    // 3) запуск клиента
    var altv = Path.Combine(clientDir, "altv.exe");
    var url = $"altv://connect/{connect}";
    var direct = noDirectLaunch ? "" : " -directlaunch";
    var argLine = $"-connecturl \"{url}\"{direct} -customui {customUi}";
    Console.WriteLine($"[connect] запуск: altv.exe {argLine}");

    var psi = new ProcessStartInfo(altv, argLine)
    {
        WorkingDirectory = clientDir,
        UseShellExecute = false,
    };
    // SteamAppId ставим ТОЛЬКО для Steam-установки. Для Epic (egs) эта
    // переменная заставляет GTA5.exe искать Steam Client -> мгновенный вылет
    // ("Не удалось запустить Steam").
    if (AltvToml.DetectPlatform(gtaDir) == "steam")
        psi.Environment["SteamAppId"] = "271590";

    using var proc = Process.Start(psi);
    if (proc is null) { Console.Error.WriteLine("[err] не удалось запустить altv.exe"); return 3; }
    Console.WriteLine($"[connect] altv.exe PID {proc.Id}. Жду завершения игры…");

    // 4) ждём: пока жив altv.exe или GTA5.exe
    var gtaSeen = false;
    while (true)
    {
        var altvUp = IsUp("altv.exe");
        var gtaUp = IsUp("GTA5.exe") || IsUp("GTA5_Enhanced.exe");
        if (gtaUp) gtaSeen = true;
        if (gtaSeen && !gtaUp) break;
        if (!gtaSeen && !altvUp)
        {
            await Task.Delay(3000);
            if (!IsUp("altv.exe") && !IsUp("GTA5.exe") && !IsUp("GTA5_Enhanced.exe")) break;
        }
        await Task.Delay(500);
    }
}
finally
{
    Console.WriteLine("[connect] игра закрыта, останавливаю локальный бэкенд");
}

if (keepOpen) { Console.WriteLine("нажмите Enter"); Console.ReadLine(); }
return 0;

// --- helpers ---------------------------------------------------------

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

static (string connect, string clientDir, string gtaDir, int port, bool debug, bool keepOpen, bool noDirectLaunch, string? platformOverride)? ParseArgs(string[] a)
{
    string? connect = null, client = null, gta = null, platformOverride = null;
    // alt:V-клиент ходит на бэкенд по ЖЁСТКО зашитому 127.0.0.1:9988
    // (флаг -customui только включает local-backend режим, порт не читает).
    var port = 9988;
    var debug = true;
    var keepOpen = false;
    var noDirectLaunch = false;

    for (var i = 0; i < a.Length; i++)
    {
        switch (a[i])
        {
            case "-connect" or "--connect" when i + 1 < a.Length: connect = a[++i]; break;
            case "--client" when i + 1 < a.Length: client = a[++i]; break;
            case "--gta" when i + 1 < a.Length: gta = a[++i]; break;
            case "--platform" when i + 1 < a.Length: platformOverride = a[++i]; break;
            case "--port" when i + 1 < a.Length && int.TryParse(a[i + 1], out var p): port = p; i++; break;
            case "--no-debug": debug = false; break;
            case "--keep-open": keepOpen = true; break;
            case "--no-directlaunch": noDirectLaunch = true; break;
        }
    }

    if (connect is null)
    {
        Console.Error.WriteLine("использование: FloVMP.Connect.exe -connect <ip:port> [--client <dir>] [--gta <dir>] [--port <n>] [--platform <steam|rgl|rockstar>] [--no-debug] [--keep-open] [--no-directlaunch]");
        return null;
    }
    if (connect.StartsWith("altv://connect/", StringComparison.OrdinalIgnoreCase))
        connect = connect["altv://connect/".Length..];

    client ??= ResolveClientDir();
    if (client is null) { Console.Error.WriteLine("[err] не нашёл папку клиента, задайте --client <dir>"); return null; }

    gta ??= ResolveGtaDir(client);
    if (gta is null) { Console.Error.WriteLine("[err] не нашёл папку GTA V, задайте --gta <dir>"); return null; }

    if (!string.IsNullOrWhiteSpace(platformOverride) &&
        !new[] { "steam", "rgl", "rockstar" }.Contains(platformOverride.Trim().ToLowerInvariant()))
    {
        Console.Error.WriteLine("[err] --platform допускает только steam, rgl или rockstar");
        return null;
    }

    return (connect, Path.GetFullPath(client), Path.GetFullPath(gta), port, debug, keepOpen, noDirectLaunch, platformOverride);
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
    // 1) Полноценный поиск через GtaLocator (Epic Manifests, реестр Rockstar, Steam)
    var candidates = GtaLocator.Detect();
    if (candidates.Count > 0)
    {
        // Приоритет: реально скачанные папки (наличие RPF архивов)
        var best = candidates.FirstOrDefault(c => c.IsComplete) ?? candidates[0];
        Console.WriteLine($"[connect] автоопределена GTA V: {best.Path} [{best.Source}]");
        return best.Path;
    }

    // 2) Если ничего не найдено автоматически — открываем диалоговое окно выбора папки
    Console.WriteLine();
    Console.WriteLine("[connect] GTA V не найдена автоматически в реестре/магазинах.");
    Console.WriteLine("[connect] Открываю окно выбора папки GTA V (Legacy или Enhanced)...");
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
