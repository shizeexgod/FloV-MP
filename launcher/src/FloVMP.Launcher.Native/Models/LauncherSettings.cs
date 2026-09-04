using System.Text.Json.Serialization;

namespace FloVMP.Launcher.Native.Models;

public class LauncherSettings
{
    [JsonPropertyName("nickname")] public string Nickname { get; set; } = "Игрок";
    [JsonPropertyName("gtaPath")] public string GtaPath { get; set; } = "";
    [JsonPropertyName("serverHost")] public string ServerHost { get; set; } = "127.0.0.1";
    [JsonPropertyName("serverPort")] public int ServerPort { get; set; } = 7788;
    [JsonPropertyName("autoUpdate")] public bool AutoUpdate { get; set; } = true;
    /// <summary>"Legacy" или "Enhanced".</summary>
    [JsonPropertyName("clientEdition")] public string ClientEdition { get; set; } = "Legacy";
}
