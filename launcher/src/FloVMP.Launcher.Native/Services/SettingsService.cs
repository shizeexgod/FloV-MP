using System.Text.Json;
using FloVMP.Launcher.Native.Models;

namespace FloVMP.Launcher.Native.Services;

/// <summary>
/// Хранилище настроек лаунчера в %LOCALAPPDATA%\FloVMP\settings.json.
/// Поддерживает автоматическую миграцию со старых путей.
/// </summary>
public static class SettingsService
{
    private static readonly string SettingsDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FloVMP");
    private static readonly string SettingsFile = Path.Combine(SettingsDir, "settings.json");

    private static readonly string LegacyDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FloridaV");
    private static readonly string LegacySettingsFile = Path.Combine(LegacyDir, "settings.json");

    private static readonly JsonSerializerOptions Opts = new() { WriteIndented = true };

    public static LauncherSettings Load()
    {
        try
        {
            if (File.Exists(SettingsFile))
            {
                var json = File.ReadAllText(SettingsFile);
                return JsonSerializer.Deserialize<LauncherSettings>(json, Opts) ?? new LauncherSettings();
            }

            if (File.Exists(LegacySettingsFile))
            {
                var legacyJson = File.ReadAllText(LegacySettingsFile);
                var settings = JsonSerializer.Deserialize<LauncherSettings>(legacyJson, Opts) ?? new LauncherSettings();
                Save(settings);
                return settings;
            }

            return new LauncherSettings();
        }
        catch { return new LauncherSettings(); }
    }

    public static void Save(LauncherSettings settings)
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            File.WriteAllText(SettingsFile, JsonSerializer.Serialize(settings, Opts));
        }
        catch { }
    }
}
