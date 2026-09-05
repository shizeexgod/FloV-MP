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
    [JsonPropertyName("compactMode")] public bool CompactMode { get; set; } = false;
    [JsonPropertyName("rememberTab")] public bool RememberTab { get; set; } = true;
    [JsonPropertyName("lastSettingsTab")] public string LastSettingsTab { get; set; } = "general";
    [JsonPropertyName("autostart")] public bool Autostart { get; set; } = false;
    [JsonPropertyName("minimizeOnPlay")] public bool MinimizeOnPlay { get; set; } = true;
    [JsonPropertyName("updateChannel")] public string UpdateChannel { get; set; } = "stable";
    [JsonPropertyName("region")] public string Region { get; set; } = "auto";
    [JsonPropertyName("anonStats")] public bool AnonStats { get; set; } = false;

    // ── Игра / запуск ──
    [JsonPropertyName("procPriority")] public string ProcPriority { get; set; } = "normal";
    [JsonPropertyName("launchArgs")] public string LaunchArgs { get; set; } = "";
    [JsonPropertyName("graphicsPreset")] public string GraphicsPreset { get; set; } = "untouched";
    [JsonPropertyName("fpsLimit")] public int FpsLimit { get; set; } = 0;
    [JsonPropertyName("disableAmbient")] public bool DisableAmbient { get; set; } = true;

    // ── Загрузка ──
    [JsonPropertyName("dlSpeed")] public int DlSpeed { get; set; } = 0;
    [JsonPropertyName("dlThreads")] public int DlThreads { get; set; } = 4;
    [JsonPropertyName("cacheDir")] public string CacheDir { get; set; } = "";

    // ── Голос ──
    [JsonPropertyName("voiceInput")] public string VoiceInput { get; set; } = "";
    [JsonPropertyName("voiceOutput")] public string VoiceOutput { get; set; } = "";
    [JsonPropertyName("voiceMode")] public string VoiceMode { get; set; } = "ptt";
    [JsonPropertyName("voiceThreshold")] public int VoiceThreshold { get; set; } = 50;

    [JsonPropertyName("notifNews")] public bool NotifNews { get; set; } = true;
    [JsonPropertyName("notifStatus")] public bool NotifStatus { get; set; } = true;
    [JsonPropertyName("notifEvents")] public bool NotifEvents { get; set; } = true;
    [JsonPropertyName("notifSound")] public bool NotifSound { get; set; } = false;

    /// <summary>Дата регистрации аккаунта (ISO, из ответа /api/auth/*) — для «Личного кабинета».</summary>
    [JsonPropertyName("accountCreatedUtc")] public string AccountCreatedUtc { get; set; } = "";
}
