using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace FloridaV.Launcher.Services;

/// <summary>
/// Автоматически находит установку GTA V.
/// Порядок поиска: Epic Games → Steam → Rockstar → ручной выбор.
/// </summary>
public static class GtaLocatorService
{
    private static readonly string[] EpicManifestDirs = {
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Epic", "EpicGamesLauncher", "Data", "Manifests"),
    };

    private static readonly string[] SteamAppsLocations = {
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Steam", "steamapps", "common", "Grand Theft Auto V"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Steam", "steamapps", "common", "Grand Theft Auto V"),
    };

    private static readonly string[] RockstarLocations = {
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Rockstar Games", "Grand Theft Auto V"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Rockstar Games", "Grand Theft Auto V"),
    };

    /// <summary>
    /// Пытается найти папку GTA V автоматически.
    /// Возвращает (путь, источник) или null.
    /// </summary>
    public static (string Path, string Source)? TryLocate()
    {
        // 1. Epic Games — через реестр (Majestic хранит так же)
        var epic = TryEpicRegistry();
        if (epic != null) return (epic, "Epic Games");

        // 2. Epic Games — через манифест-файлы
        var epicManifest = TryEpicManifest();
        if (epicManifest != null) return (epicManifest, "Epic Games");

        // 3. Steam — реестр
        var steam = TrySteamRegistry();
        if (steam != null) return (steam, "Steam");

        // 4. Steam — типичные папки
        foreach (var loc in SteamAppsLocations)
        {
            if (IsValidGtaFolder(loc))
                return (loc, "Steam");
        }

        // 5. Rockstar Games Launcher — реестр
        var rockstar = TryRockstarRegistry();
        if (rockstar != null) return (rockstar, "Rockstar");

        // 6. Rockstar — типичные папки
        foreach (var loc in RockstarLocations)
        {
            if (IsValidGtaFolder(loc))
                return (loc, "Rockstar");
        }

        return null;
    }

    /// <summary>
    /// Проверяет, что папка содержит GTA5.exe (или GTA5_Enhanced.exe).
    /// </summary>
    public static bool IsValidGtaFolder(string path)
        => Directory.Exists(path) &&
           (File.Exists(Path.Combine(path, "GTA5.exe")) ||
            File.Exists(Path.Combine(path, "GTA5_Enhanced.exe")) ||
            File.Exists(Path.Combine(path, "PlayGTAV.exe")));

    /// <summary>
    /// Определяет тип установки (Legacy / Enhanced).
    /// </summary>
    public static string DetectVersion(string path)
    {
        if (File.Exists(Path.Combine(path, "GTA5_Enhanced.exe")))
            return "Enhanced";
        if (File.Exists(Path.Combine(path, "GTA5.exe")))
            return "Legacy";
        return "Неизвестная";
    }

    private static string? TryEpicRegistry()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\WOW6432Node\Epic Games\EpicGamesLauncher");
            var appDataPath = key?.GetValue("AppDataPath") as string;
            if (appDataPath == null) return null;

            // Сканируем manifest-файлы
            var manifestsDir = Path.Combine(appDataPath, "Data", "Manifests");
            return ScanEpicManifests(manifestsDir);
        }
        catch { return null; }
    }

    private static string? TryEpicManifest()
    {
        foreach (var dir in EpicManifestDirs)
        {
            var result = ScanEpicManifests(dir);
            if (result != null) return result;
        }
        return null;
    }

    private static string? ScanEpicManifests(string manifestsDir)
    {
        if (!Directory.Exists(manifestsDir)) return null;
        foreach (var file in Directory.GetFiles(manifestsDir, "*.item"))
        {
            try
            {
                var json = File.ReadAllText(file);
                if (!json.Contains("\"GTAV\"") && !json.Contains("9d2d0eb64d5c44529cece33fe2a46482") &&
                    !json.Contains("Grand Theft Auto")) continue;

                // Ищем InstallLocation
                var match = System.Text.RegularExpressions.Regex.Match(
                    json, "\"InstallLocation\"\\s*:\\s*\"([^\"]+)\"");
                if (!match.Success) continue;

                var path = match.Groups[1].Value.Replace("\\\\", "\\");
                if (IsValidGtaFolder(path)) return path;
            }
            catch { }
        }
        return null;
    }

    private static string? TrySteamRegistry()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\WOW6432Node\Valve\Steam");
            var steamPath = key?.GetValue("InstallPath") as string;
            if (steamPath == null) return null;

            // libraryfolders.vdf содержит все пути библиотек
            var libraryFile = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(libraryFile)) return null;

            var vdf = File.ReadAllText(libraryFile);
            var paths = System.Text.RegularExpressions.Regex.Matches(
                vdf, "\"path\"\\s*\"([^\"]+)\"");

            foreach (System.Text.RegularExpressions.Match m in paths)
            {
                var libPath = m.Groups[1].Value.Replace("\\\\", "\\");
                var gtaPath = Path.Combine(libPath, "steamapps", "common", "Grand Theft Auto V");
                if (IsValidGtaFolder(gtaPath)) return gtaPath;
            }
        }
        catch { }
        return null;
    }

    private static string? TryRockstarRegistry()
    {
        try
        {
            string[] keys = {
                @"SOFTWARE\WOW6432Node\Rockstar Games\Grand Theft Auto V",
                @"SOFTWARE\Rockstar Games\Grand Theft Auto V",
            };
            foreach (var keyPath in keys)
            {
                using var key = Registry.LocalMachine.OpenSubKey(keyPath);
                var installFolder = key?.GetValue("InstallFolder") as string;
                if (installFolder != null && IsValidGtaFolder(installFolder))
                    return installFolder;
            }
        }
        catch { }
        return null;
    }
}
