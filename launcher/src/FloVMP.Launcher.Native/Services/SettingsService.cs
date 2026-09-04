using System.Text.Json;
using FloVMP.Launcher.Native.Models;

namespace FloVMP.Launcher.Native.Services;

/// <summary>
/// Тот же файл настроек (%LOCALAPPDATA%\FloridaV\settings.json), что использовал
/// старый WPF-лаунчер — общее хранилище, без миграции.
/// </summary>
public static class SettingsService
{
    private static readonly string SettingsDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FloridaV");
    private static readonly string SettingsFile = Path.Combine(SettingsDir, "settings.json");

    private static readonly JsonSerializerOptions Opts = new() { WriteIndented = true };

    public static LauncherSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsFile)) return new LauncherSettings();
            var json = File.ReadAllText(SettingsFile);
            return JsonSerializer.Deserialize<LauncherSettings>(json, Opts) ?? new LauncherSettings();
        }
        catch { return new LauncherSettings(); }
    }

    public static void Save(LauncherSettings settings)
    {
        Directory.CreateDirectory(SettingsDir);
        File.WriteAllText(SettingsFile, JsonSerializer.Serialize(settings, Opts));
    }
}
