using System.IO;
using System.Text.Json;
using FloVMP.Launcher.Models;

namespace FloVMP.Launcher.Services;

/// <summary>Загрузка/сохранение <see cref="LauncherSettings"/> в JSON.</summary>
public static class SettingsStore
{
    public static string Dir { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FloVMP");

    public static string FilePath { get; } = Path.Combine(Dir, "launcher.settings.json");

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public static LauncherSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<LauncherSettings>(json) ?? new LauncherSettings();
            }
        }
        catch
        {
            // Битый файл — не падаем, стартуем с дефолтов.
        }

        return new LauncherSettings();
    }

    public static void Save(LauncherSettings settings)
    {
        Directory.CreateDirectory(Dir);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, JsonOpts));
    }
}
