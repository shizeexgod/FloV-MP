namespace FloVMP.Launcher.Models;

/// <summary>
/// Настройки лаунчера. Сохраняются в
/// %LOCALAPPDATA%\FloVMP\launcher.settings.json.
/// </summary>
public sealed class LauncherSettings
{
    /// <summary>Адрес нашего сервера (Phase 1 — локальный).</summary>
    public string ServerHost { get; set; } = "127.0.0.1";

    public int ServerPort { get; set; } = 7788;

    /// <summary>Никнейм игрока для direct-connect.</summary>
    public string Nickname { get; set; } = "";

    /// <summary>
    /// Путь к папке GTA V (содержит GTA5.exe). Пусто = автодетект при старте.
    /// </summary>
    public string GtaPath { get; set; } = "";

    /// <summary>
    /// Папка ядра клиента alt:V (содержит altv.exe + libs\ + cef\).
    /// В будущем — приватная папка лаунчера (%LOCALAPPDATA%\FloVMP\core),
    /// наполняемая через CDN. Сейчас указывается вручную.
    /// </summary>
    public string AltvCoreDir { get; set; } = "";

    /// <summary>Ветка клиента alt:V: release | rc | dev.</summary>
    public string Branch { get; set; } = "release";

    /// <summary>
    /// URL (или локальный путь) манифеста CDN для ядра клиента. Если задан —
    /// лаунчер может проверить/докачать ядро в AltvCoreDir по этому манифесту.
    /// Пусто = ручной режим (AltvCoreDir указывается вручную).
    /// </summary>
    public string CoreManifestUrl { get; set; } = "";

    /// <summary>
    /// Разрешить лаунчеру синхронизировать глобальный
    /// %LOCALAPPDATA%\altv\altv.toml (gtapath / branch) перед запуском.
    /// По умолчанию выключено — глобальный файл общий с обычным alt:V.
    /// </summary>
    public bool SyncAltvToml { get; set; }

    /// <summary>
    /// Передавать -skipprocesscheck (нужно, чтобы запустить второй клиент
    /// на той же машине для локального теста).
    /// </summary>
    public bool AllowMultipleInstances { get; set; } = true;
}
