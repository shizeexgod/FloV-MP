using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace FloVMP.Launcher.Services;

public sealed record GtaCandidate(string Path, string Source)
{
    public string? GameExecutable => GtaLocator.FindGameExecutable(Path);
    public bool HasExe => GameExecutable is not null;
    public bool IsEnhanced => string.Equals(GameExecutable, "GTA5_Enhanced.exe", StringComparison.OrdinalIgnoreCase);
    public bool IsComplete => GtaLocator.IsGameDownloaded(Path);
}

/// <summary>
/// Поиск установленной GTA V:
/// 1) Манифесты Epic Games Launcher (%ProgramData%\Epic\EpicGamesLauncher\Data\Manifests\*.item)
/// 2) Реестр Rockstar / Epic / Steam
/// 3) Библиотеки Steam (libraryfolders.vdf)
/// С приоритетом реально скачанных версий (наличие RPF архивов).
/// </summary>
public static class GtaLocator
{
    public static IReadOnlyList<GtaCandidate> Detect()
    {
        var found = new List<GtaCandidate>();

        void Add(string? path, string source)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            path = path.Trim().Trim('"');
            if (found.Any(c => string.Equals(c.Path, path, StringComparison.OrdinalIgnoreCase))) return;
            if (Directory.Exists(path) && FindGameExecutable(path) is not null)
            {
                found.Add(new GtaCandidate(path, source));
            }
        }

        // 1) Epic Games Launcher Manifests (%ProgramData%\Epic\EpicGamesLauncher\Data\Manifests\*.item)
        try
        {
            var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            var manifestsDir = Path.Combine(programData, "Epic", "EpicGamesLauncher", "Data", "Manifests");
            if (Directory.Exists(manifestsDir))
            {
                foreach (var file in Directory.EnumerateFiles(manifestsDir, "*.item"))
                {
                    try
                    {
                        var json = File.ReadAllText(file);
                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;
                        var disp = root.TryGetProperty("DisplayName", out var d) ? d.GetString() : "";
                        var loc = root.TryGetProperty("InstallLocation", out var l) ? l.GetString() : "";
                        if (!string.IsNullOrWhiteSpace(loc) && (disp?.Contains("Grand Theft Auto", StringComparison.OrdinalIgnoreCase) == true || disp?.Contains("GTA", StringComparison.OrdinalIgnoreCase) == true))
                        {
                            var editionStr = disp.Contains("Enhanced", StringComparison.OrdinalIgnoreCase) ? "Epic (Enhanced)" : "Epic (Legacy)";
                            Add(loc, editionStr);
                        }
                    }
                    catch { }
                }
            }
        }
        catch { }

        // 2) Rockstar\Grand Theft Auto V (пишется и Epic-, и RGL-установкой)
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                using var gta = hklm.OpenSubKey(@"SOFTWARE\Rockstar Games\Grand Theft Auto V")
                              ?? hklm.OpenSubKey(@"SOFTWARE\WOW6432Node\Rockstar Games\Grand Theft Auto V");
                if (gta is not null)
                {
                    Add(gta.GetValue("InstallFolderEpic") as string, "Epic (реестр Rockstar)");
                    Add(gta.GetValue("InstallFolder") as string, "Rockstar Launcher (реестр)");
                    Add(gta.GetValue("InstallFolderSteam") as string, "Steam (реестр Rockstar)");
                }
            }
            catch { }
        }

        // 3) Steam: libraryfolders.vdf → <lib>\steamapps\common\Grand Theft Auto V
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                using var steam = hklm.OpenSubKey(@"SOFTWARE\Valve\Steam")
                                ?? hklm.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam");
                var steamPath = steam?.GetValue("InstallPath") as string;
                if (!string.IsNullOrWhiteSpace(steamPath))
                {
                    var vdf = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
                    if (File.Exists(vdf))
                    {
                        foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
                        {
                            var lib = m.Groups[1].Value.Replace(@"\\", @"\");
                            Add(Path.Combine(lib, "steamapps", "common", "Grand Theft Auto V"), "Steam (библиотека)");
                        }
                    }
                }
            }
            catch { }
        }

        // Сортировка: реально скачанные (с RPF) первыми
        return found
            .Where(c => c.HasExe)
            .OrderByDescending(c => c.IsComplete)
            .ToList();
    }

    public static bool LooksLikeGtaFolder(string? path) =>
        FindGameExecutable(path) is not null && IsGameDownloaded(path);

    public static bool IsGameDownloaded(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return false;
        // Проверяем наличие ключевых архивов данных
        return File.Exists(Path.Combine(path, "common.rpf")) ||
               File.Exists(Path.Combine(path, "x64a.rpf")) ||
               File.Exists(Path.Combine(path, "update", "update.rpf"));
    }

    public static string? FindGameExecutable(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return null;
        foreach (var name in new[] { "GTA5.exe", "GTA5_Enhanced.exe" })
        {
            if (File.Exists(Path.Combine(path, name))) return name;
        }
        return null;
    }
}
