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
    /// <summary>Id пресета акцентного цвета лаунчера (см. ACCENTS в renderer.js). По умолчанию — фирменный золотой.</summary>
    [JsonPropertyName("accentColor")] public string AccentColor { get; set; } = "gold";

    [JsonPropertyName("language")] public string Language { get; set; } = "ru";
    [JsonPropertyName("animations")] public bool Animations { get; set; } = true;
    [JsonPropertyName("autostart")] public bool Autostart { get; set; } = false;
    [JsonPropertyName("minimizeOnPlay")] public bool MinimizeOnPlay { get; set; } = true;
    [JsonPropertyName("notifNews")] public bool NotifNews { get; set; } = true;
    [JsonPropertyName("notifStatus")] public bool NotifStatus { get; set; } = true;
    [JsonPropertyName("notifSound")] public bool NotifSound { get; set; } = false;

    /// <summary>Дата регистрации аккаунта (ISO, из ответа /api/auth/*) — для «Личного кабинета».</summary>
    [JsonPropertyName("accountCreatedUtc")] public string AccountCreatedUtc { get; set; } = "";
}
