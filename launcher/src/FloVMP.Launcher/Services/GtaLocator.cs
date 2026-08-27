using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace FloVMP.Launcher.Services;

public sealed record GtaCandidate(string Path, string Source)
{
    public bool HasExe => File.Exists(System.IO.Path.Combine(Path, "GTA5.exe"));
}

/// <summary>
/// Поиск установленной GTA V по реестру Windows: Rockstar/Epic, Steam,
/// Rockstar Games Launcher. Стандартная документированная практика —
/// реальные ключи реестра существуют (проверено на этой машине: GTA V
/// стоит из Epic по пути с GUID-именем папки).
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
            if (Directory.Exists(path)) found.Add(new GtaCandidate(path, source));
        }

        // --- Rockstar\Grand Theft Auto V (пишется и Epic-, и RGL-установкой)
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
            using var gta = hklm.OpenSubKey(@"SOFTWARE\Rockstar Games\Grand Theft Auto V")
                          ?? hklm.OpenSubKey(@"SOFTWARE\WOW6432Node\Rockstar Games\Grand Theft Auto V");
            if (gta is null) continue;

            Add(gta.GetValue("InstallFolderEpic") as string, "Epic (реестр Rockstar)");
            Add(gta.GetValue("InstallFolder") as string, "Rockstar Launcher (реестр)");
            Add(gta.GetValue("InstallFolderSteam") as string, "Steam (реестр Rockstar)");
        }

        // --- Steam: libraryfolders.vdf → <lib>\steamapps\common\Grand Theft Auto V
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
            using var steam = hklm.OpenSubKey(@"SOFTWARE\Valve\Steam")
                            ?? hklm.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam");
            var steamPath = steam?.GetValue("InstallPath") as string;
            if (string.IsNullOrWhiteSpace(steamPath)) continue;

            var vdf = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(vdf)) continue;

            foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
            {
                var lib = m.Groups[1].Value.Replace(@"\\", @"\");
                Add(Path.Combine(lib, "steamapps", "common", "Grand Theft Auto V"), "Steam (библиотека)");
            }
        }

        return found.Where(c => c.HasExe).ToList();
    }

    public static bool LooksLikeGtaFolder(string? path) =>
        !string.IsNullOrWhiteSpace(path) && File.Exists(Path.Combine(path, "GTA5.exe"));
}
