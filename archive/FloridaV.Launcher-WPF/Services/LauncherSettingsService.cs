using System.Text.Json;
using System.Text.Json.Serialization;

namespace FloridaV.Launcher.Services;

public class LauncherSettings
{
    [JsonPropertyName("nickname")] public string Nickname { get; set; } = "Игрок";
    [JsonPropertyName("gtaPath")] public string GtaPath { get; set; } = "";
    [JsonPropertyName("enginePath")] public string EnginePath { get; set; } = "";
    [JsonPropertyName("serverHost")] public string ServerHost { get; set; } = "127.0.0.1";
    [JsonPropertyName("serverPort")] public int ServerPort { get; set; } = 7788;
    [JsonPropertyName("autoUpdate")] public bool AutoUpdate { get; set; } = true;
    /// <summary>"Legacy" или "Enhanced" — какой профиль клиента использовать при запуске.</summary>
    [JsonPropertyName("clientEdition")] public string ClientEdition { get; set; } = "Legacy";
}

public static class LauncherSettingsService
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
        try
        {
            Directory.CreateDirectory(SettingsDir);
            File.WriteAllText(SettingsFile, JsonSerializer.Serialize(settings, Opts));
        }
        catch { }
    }
}
