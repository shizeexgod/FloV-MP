using System.Diagnostics;
using System.IO;

namespace FloridaV.Launcher.Services;

/// <summary>
/// Запускает игру через FloVMP.Connect.
/// Находит connect.exe рядом с лаунчером или по стандартному пути.
/// </summary>
public static class PlayService
{
    /// <summary>Результат попытки запуска.</summary>
    public record LaunchResult(bool Success, string? Error = null);

    public static LaunchResult Launch(string gtaPath, string serverHost, int serverPort, string nickname)
    {
        if (!GtaLocatorService.IsValidGtaFolder(gtaPath))
            return new LaunchResult(false, "Папка GTA V не найдена или некорректна.");

        // Ищем FloVMP.Connect.exe: рядом с exe, или в папке runtime
        var connectExe = FindConnectExe();
        if (connectExe == null)
            return new LaunchResult(false, "Не найден FloVMP.Connect.exe. Убедитесь что движок FloV:MP установлен.");

        var args = $"--gta \"{gtaPath}\" --host {serverHost} --port {serverPort} --nick \"{nickname}\"";

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
        // 1. Рядом с текущим exe
        var launcherDir = AppContext.BaseDirectory;
        var candidate1 = Path.Combine(launcherDir, "FloVMP.Connect.exe");
        if (File.Exists(candidate1)) return candidate1;

        // 2. В AppData\FloridaV\engine\
        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FloridaV", "engine", "FloVMP.Connect.exe");
        if (File.Exists(appData)) return appData;

        // 3. Идём вверх по дереву ищем dev-сборку (для разработки)
        var dir = launcherDir;
        for (var i = 0; i < 8; i++)
        {
            var dev = Path.Combine(dir, "launcher", "src", "FloVMP.Connect",
                "bin", "Release", "net8.0-windows", "FloVMP.Connect.exe");
            if (File.Exists(dev)) return dev;
            var parent = Directory.GetParent(dir);
            if (parent == null) break;
            dir = parent.FullName;
        }

        return null;
    }
}
