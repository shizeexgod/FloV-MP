using System.Diagnostics;

namespace FloVMP.Launcher.Native.Services;

/// <summary>
/// Запускает игру через FloVMP.Connect.exe — весь BattlEye-safe / exe-подмена
/// код остаётся нетронутым в FloVMP.Connect, здесь только его поиск и запуск.
/// </summary>
public static class PlayService
{
    public record LaunchResult(bool Success, string? Error = null);

    public static LaunchResult Launch(string gtaPath, string serverHost, int serverPort, string nickname)
    {
        if (!GtaLocatorService.IsValidGtaFolder(gtaPath))
        {
            var detected = GtaLocatorService.TryLocate();
            if (detected != null && GtaLocatorService.IsValidGtaFolder(detected.Value.Path))
            {
                gtaPath = detected.Value.Path;
            }
            else
            {
                return new LaunchResult(false, "Папка GTA V не найдена. Укажите верный путь к игре в настройках лаунчера.");
            }
        }

        // Подготовка и развертывание профиля масштабирования (DLSS / FSR 3 / Neural DLSS 5) перед стартом
        try
        {
            var settings = SettingsService.Load();
            if (settings != null && settings.UpscalerMode != "none")
            {
                UpscalerDeploymentService.Deploy(gtaPath, settings);
            }
            else
            {
                UpscalerDeploymentService.Cleanup(gtaPath);
            }
        }
        catch { }

        var connectExe = FindConnectExe();
        if (connectExe == null)
            return new LaunchResult(false, "Не найден FloVMP.Connect.exe. Убедитесь что движок FloV:MP установлен.");

        var safeGtaPath = gtaPath.Trim().Trim('"', '\'').TrimEnd('\\');
        var safeNick = (nickname ?? "Player").Replace("\"", "").Trim();
        var safeHost = (serverHost ?? "127.0.0.1").Trim();
        var safePort = serverPort <= 0 ? 7788 : serverPort;

        var clientDir = FindClientDir();
        var clientArg = !string.IsNullOrWhiteSpace(clientDir) ? " --client " + QuoteArg(clientDir) : "";

        var detectedPlatform = GtaLocatorService.DetectPlatform(safeGtaPath);
        // Каждый аргумент экранируется по правилам CommandLineToArgvW. Раньше
        // значения просто оборачивались в кавычки: путь или ник, кончающийся
        // на «\», экранировал закрывающую кавычку, и все следующие аргументы
        // съезжали — игра не запускалась с непонятной ошибкой коннектора.
        var args = "-connect " + QuoteArg($"{safeHost}:{safePort}") + " --gta " + QuoteArg(safeGtaPath) +
                   " --nick " + QuoteArg(safeNick) + " --platform " + QuoteArg(detectedPlatform) + clientArg;

        if (detectedPlatform == "egs")
        {
            try
            {
                var egsRunning = Process.GetProcessesByName("EpicGamesLauncher").Length > 0;
                var webRunning = Process.GetProcessesByName("EpicWebHelper").Length > 0;
                if (!egsRunning || !webRunning)
                {
                    var epicCandidates = new[]
                    {
                        @"C:\Program Files\Epic Games\Launcher\Portal\Binaries\Win64\EpicGamesLauncher.exe",
                        @"C:\Program Files (x86)\Epic Games\Launcher\Portal\Binaries\Win64\EpicGamesLauncher.exe",
                    };
                    var foundEpic = epicCandidates.FirstOrDefault(File.Exists);
                    if (foundEpic != null)
                    {
                        Process.Start(new ProcessStartInfo(foundEpic, "-Silent") { UseShellExecute = true, WindowStyle = ProcessWindowStyle.Minimized });
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
                            Thread.Sleep(2500);
                            break;
                        }
                        else if (i > 10 && Process.GetProcessesByName("EpicGamesLauncher").Length > 0)
                        {
                            Thread.Sleep(2000);
                            break;
                        }
                    }
                }
            }
            catch { }
        }

        // Настройки запуска из settings.json (те же, что редактируются на
        // вкладке «Игра»). Раньше сохранялись, но никуда не передавались —
        // отсюда жалобы «не применяются».
        try
        {
            var s = SettingsService.Load();
            if (s != null)
            {
                var gameArgs = BuildGameArgs(s);
                if (gameArgs.Length > 0)
                    args += " --game-args " + QuoteArg(gameArgs);
                if (!string.IsNullOrWhiteSpace(s.ProcPriority) && s.ProcPriority != "normal")
                    args += " --priority " + QuoteArg(s.ProcPriority);
                if (s.FpsLimit > 0)
                    args += $" --fps-limit {s.FpsLimit}";
                if (!string.IsNullOrWhiteSpace(s.GraphicsPreset) && s.GraphicsPreset != "untouched")
                    args += " --gfx-preset " + QuoteArg(s.GraphicsPreset);
            }
        }
        catch { }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = connectExe,
                Arguments = args,
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(connectExe),
            };
            var proc = Process.Start(psi);
            if (proc == null)
            {
                return new LaunchResult(false, "Не удалось запустить процесс FloVMP.Connect.exe");
            }
            return new LaunchResult(true);
        }
        catch (Exception ex)
        {
            return new LaunchResult(false, $"Ошибка запуска: {ex.Message}");
        }
    }

    /// <summary>
    /// Экранирует один аргумент командной строки Windows (правила
    /// CommandLineToArgvW): обратные слеши перед кавычкой и в конце удваиваются,
    /// кавычки экранируются.
    /// </summary>
    public static string QuoteArg(string? value)
    {
        value ??= "";
        if (value.Length > 0 && value.IndexOfAny(new[] { ' ', '\t', '"' }) < 0) return value;

        var sb = new System.Text.StringBuilder("\"");
        var backslashes = 0;
        foreach (var ch in value)
        {
            if (ch == '\\') { backslashes++; continue; }
            if (ch == '"')
            {
                sb.Append('\\', backslashes * 2 + 1).Append('"');
            }
            else
            {
                sb.Append('\\', backslashes).Append(ch);
            }
            backslashes = 0;
        }
        sb.Append('\\', backslashes * 2).Append('"');
        return sb.ToString();
    }

    /// <summary>
    /// Находит папку рантайма клиента alt:V (runtime/client).
    /// </summary>
    private static string? FindClientDir()
    {
        var baseDir = AppContext.BaseDirectory;
        var dir = baseDir;
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

        var appDataFlovmp = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FloVMP", "runtime", "client");
        if (Directory.Exists(appDataFlovmp)) return appDataFlovmp;

        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FloridaV", "runtime", "client");
        if (Directory.Exists(appData)) return appData;

        var appDataFlovmpEngine = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FloVMP", "engine");
        if (Directory.Exists(appDataFlovmpEngine) &&
            (File.Exists(Path.Combine(appDataFlovmpEngine, "altv.exe")) || File.Exists(Path.Combine(appDataFlovmpEngine, "flovmp.exe"))))
        {
            return appDataFlovmpEngine;
        }

        var appDataEngine = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FloridaV", "engine");
        if (Directory.Exists(appDataEngine) &&
            (File.Exists(Path.Combine(appDataEngine, "altv.exe")) || File.Exists(Path.Combine(appDataEngine, "flovmp.exe"))))
        {
            return appDataEngine;
        }

        return null;
    }

    /// <summary>
    /// Аргументы командной строки GTA V из настроек: режим окна + свои
    /// аргументы владельца («Доп. аргументы запуска», передаются как есть).
    /// </summary>
    private static string BuildGameArgs(Models.LauncherSettings s)
    {
        var parts = new List<string>();
        switch (s.GtaWindowMode)
        {
            case "windowed": parts.Add("-windowed"); break;
            case "borderless": parts.Add("-windowed"); parts.Add("-borderless"); break;
            case "fullscreen": parts.Add("-fullscreen"); break;
        }
        if (!string.IsNullOrWhiteSpace(s.LaunchArgs))
            parts.Add(s.LaunchArgs.Trim());
        return string.Join(' ', parts);
    }

    private static string? FindConnectExe()
    {
        var baseDir = AppContext.BaseDirectory;
        var candidate1 = Path.Combine(baseDir, "FloVMP.Connect.exe");
        if (File.Exists(candidate1)) return candidate1;

        var appDataFlovmp = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FloVMP", "engine", "FloVMP.Connect.exe");
        if (File.Exists(appDataFlovmp)) return appDataFlovmp;

        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FloridaV", "engine", "FloVMP.Connect.exe");
        if (File.Exists(appData)) return appData;

        var runtimeClient = Path.Combine(baseDir, "runtime", "client", "FloVMP.Connect.exe");
        if (File.Exists(runtimeClient)) return runtimeClient;

        var hardcodedClient = @"C:\FloV-MP\runtime\client\FloVMP.Connect.exe";
        if (File.Exists(hardcodedClient)) return hardcodedClient;

        var dir = baseDir;
        for (var i = 0; i < 8; i++)
        {
            var devRelease = Path.Combine(dir, "launcher", "src", "FloVMP.Connect",
                "bin", "Release", "net8.0-windows", "FloVMP.Connect.exe");
            if (File.Exists(devRelease)) return devRelease;

            var devDist = Path.Combine(dir, "launcher", "electron", "native-dist", "FloVMP.Connect.exe");
            if (File.Exists(devDist)) return devDist;

            var devDebug = Path.Combine(dir, "launcher", "src", "FloVMP.Connect",
                "bin", "Debug", "net8.0-windows", "FloVMP.Connect.exe");
            if (File.Exists(devDebug)) return devDebug;

            var runtimeSub = Path.Combine(dir, "runtime", "client", "FloVMP.Connect.exe");
            if (File.Exists(runtimeSub)) return runtimeSub;

            var parent = Directory.GetParent(dir);
            if (parent == null) break;
            dir = parent.FullName;
        }

        return null;
    }
}
