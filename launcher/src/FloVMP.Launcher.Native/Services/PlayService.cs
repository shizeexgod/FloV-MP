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
            return new LaunchResult(false, "Папка GTA V не найдена или некорректна.");

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

        var args = $"-connect {safeHost}:{safePort} --gta \"{safeGtaPath}\" --nick \"{safeNick}\"";

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = connectExe,
                Arguments = args,
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(connectExe),
            };
            Process.Start(psi);
            return new LaunchResult(true);
        }
        catch (Exception ex)
        {
            return new LaunchResult(false, $"Ошибка запуска: {ex.Message}");
        }
    }

    private static string? FindConnectExe()
    {
        var baseDir = AppContext.BaseDirectory;
        var candidate1 = Path.Combine(baseDir, "FloVMP.Connect.exe");
        if (File.Exists(candidate1)) return candidate1;

        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FloridaV", "engine", "FloVMP.Connect.exe");
        if (File.Exists(appData)) return appData;

        var dir = baseDir;
        for (var i = 0; i < 8; i++)
        {
            var devRelease = Path.Combine(dir, "launcher", "src", "FloVMP.Connect",
                "bin", "Release", "net8.0-windows", "FloVMP.Connect.exe");
            if (File.Exists(devRelease)) return devRelease;

            var devDebug = Path.Combine(dir, "launcher", "src", "FloVMP.Connect",
                "bin", "Debug", "net8.0-windows", "FloVMP.Connect.exe");
            if (File.Exists(devDebug)) return devDebug;

            var parent = Directory.GetParent(dir);
            if (parent == null) break;
            dir = parent.FullName;
        }

        return null;
    }
}
