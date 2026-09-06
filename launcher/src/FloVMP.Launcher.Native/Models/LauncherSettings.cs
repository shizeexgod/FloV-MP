using System.Text.Json.Serialization;

namespace FloVMP.Launcher.Native.Models;

public class LauncherSettings
{
    [JsonPropertyName("nickname")] public string Nickname { get; set; } = "Игрок";
    [JsonPropertyName("gtaPath")] public string GtaPath { get; set; } = "";
    [JsonPropertyName("serverHost")] public string ServerHost { get; set; } = "188.127.229.224";
    [JsonPropertyName("serverPort")] public int ServerPort { get; set; } = 7788;
    [JsonPropertyName("autoUpdate")] public bool AutoUpdate { get; set; } = true;
    /// <summary>"Legacy" или "Enhanced".</summary>
    [JsonPropertyName("clientEdition")] public string ClientEdition { get; set; } = "Legacy";
    /// <summary>Id пресета акцентного цвета лаунчера (см. ACCENTS в renderer.js) или "custom".</summary>
    [JsonPropertyName("accentColor")] public string AccentColor { get; set; } = "gold";
    /// <summary>Свой акцентный цвет (#RRGGBB), когда accentColor == "custom".</summary>
    [JsonPropertyName("accentCustom")] public string AccentCustom { get; set; } = "#8b5cf6";

    [JsonPropertyName("language")] public string Language { get; set; } = "ru";
    [JsonPropertyName("animations")] public bool Animations { get; set; } = true;
    [JsonPropertyName("compactMode")] public bool CompactMode { get; set; } = false;
    [JsonPropertyName("rememberTab")] public bool RememberTab { get; set; } = true;
    /// <summary>Масштаб интерфейса лаунчера, % (80–140).</summary>
    [JsonPropertyName("uiScale")] public int UiScale { get; set; } = 100;
    /// <summary>Короткий звук клика в интерфейсе лаунчера.</summary>
    [JsonPropertyName("uiSounds")] public bool UiSounds { get; set; } = false;
    /// <summary>Крестик сворачивает лаунчер в трей вместо закрытия.</summary>
    [JsonPropertyName("trayOnClose")] public bool TrayOnClose { get; set; } = false;
    [JsonPropertyName("lastSettingsTab")] public string LastSettingsTab { get; set; } = "general";
    [JsonPropertyName("autostart")] public bool Autostart { get; set; } = false;
    [JsonPropertyName("minimizeOnPlay")] public bool MinimizeOnPlay { get; set; } = true;
    [JsonPropertyName("updateChannel")] public string UpdateChannel { get; set; } = "stable";
    [JsonPropertyName("region")] public string Region { get; set; } = "auto";
    [JsonPropertyName("anonStats")] public bool AnonStats { get; set; } = false;

    // ── Игра / запуск ──
    [JsonPropertyName("procPriority")] public string ProcPriority { get; set; } = "normal";
    [JsonPropertyName("launchArgs")] public string LaunchArgs { get; set; } = "";
    /// <summary>"keep" | "windowed" | "borderless" | "fullscreen".</summary>
    [JsonPropertyName("gtaWindowMode")] public string GtaWindowMode { get; set; } = "keep";
    [JsonPropertyName("graphicsPreset")] public string GraphicsPreset { get; set; } = "untouched";
    [JsonPropertyName("fpsLimit")] public int FpsLimit { get; set; } = 0;
    [JsonPropertyName("disableAmbient")] public bool DisableAmbient { get; set; } = true;

    // ── FloV:Graphics & Upscaler (DLSS 5 / FSR 3 / Frame Gen) ──
    /// <summary>"none" | "fsr3_framegen" | "dlss_framegen" | "dlss5_neural"</summary>
    [JsonPropertyName("upscalerMode")] public string UpscalerMode { get; set; } = "none";
    /// <summary>"quality" | "balanced" | "performance" | "ultra_performance"</summary>
    [JsonPropertyName("upscalerQuality")] public string UpscalerQuality { get; set; } = "quality";
    /// <summary>Резкость масштабирования (0 - 100)</summary>
    [JsonPropertyName("upscalerSharpness")] public int UpscalerSharpness { get; set; } = 50;
    /// <summary>Включить аппаратную генерацию кадров (Frame Generation)</summary>
    [JsonPropertyName("upscalerFrameGen")] public bool UpscalerFrameGen { get; set; } = true;
    /// <summary>Защита NUI HUD / инвентаря от размытия и артефактов апскейлинга</summary>
    [JsonPropertyName("upscalerNuiProtection")] public bool UpscalerNuiProtection { get; set; } = true;

    // ── Загрузка ──
    [JsonPropertyName("dlSpeed")] public int DlSpeed { get; set; } = 0;
    [JsonPropertyName("dlThreads")] public int DlThreads { get; set; } = 4;
    [JsonPropertyName("verifyAfterDl")] public bool VerifyAfterDl { get; set; } = true;
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

    /// <summary>
    /// Аккаунт, под которым выполнен вход в лаунчере (null — гость «Игрок»).
    /// Тот же аккаунт, что и в игре; может прийти из session.json (хэндофф).
    /// </summary>
    [JsonPropertyName("account")] public LauncherAccount? Account { get; set; }
}

/// <summary>Данные вошедшего аккаунта, хранимые в settings.json.</summary>
public sealed class LauncherAccount
{
    [JsonPropertyName("username")] public string Username { get; set; } = "";
    [JsonPropertyName("createdUtc")] public string CreatedUtc { get; set; } = "";
    [JsonPropertyName("email")] public string Email { get; set; } = "";
    [JsonPropertyName("twoFa")] public bool TwoFa { get; set; } = false;
}
