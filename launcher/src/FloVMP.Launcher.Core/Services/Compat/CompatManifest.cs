using System.Text.Json.Serialization;

namespace FloVMP.Launcher.Services.Compat;

/// <summary>
/// Манифест совместимости с версиями GTA5.exe. Тянется лаунчером с CDN.
///
/// Вариант 1 (Native Mode): для актуальной версии игры отдаём набор
/// сигнатур/offset'ов памяти, которые нужны хуку клиента. Клиент работает
/// на настоящем GTA5.exe из Steam/Epic, без подмены.
///
/// Вариант 2 (Fallback Mode): если версия неизвестна/сломана — лаунчер
/// временно подменяет GTA5.exe кэшированным стабильным билдом
/// (см. GameExeManager) и указывает, какой билд брать.
/// </summary>
public sealed class CompatManifest
{
    [JsonPropertyName("schema")]
    public int Schema { get; set; } = 1;

    [JsonPropertyName("updated")]
    public string Updated { get; set; } = "";

    /// <summary>
    /// Кэшированные стабильные билды GTA5.exe для Варианта 2 (по ключу —
    /// «эталонная» версия, к которой откатываемся).
    /// </summary>
    [JsonPropertyName("cachedBuilds")]
    public List<CachedBuild> CachedBuilds { get; set; } = new();

    [JsonPropertyName("versions")]
    public List<CompatEntry> Versions { get; set; } = new();
}

public enum CompatStatus { Unknown, Supported, Untested, Broken }

public sealed class CompatEntry
{
    /// <summary>Человекочитаемая версия игры, напр. "1.0.3570.0".</summary>
    [JsonPropertyName("gtaFileVersion")]
    public string GtaFileVersion { get; set; } = "";

    /// <summary>Размер GTA5.exe в байтах (часть ключа сопоставления).</summary>
    [JsonPropertyName("gtaSize")]
    public long GtaSize { get; set; }

    /// <summary>Необязательный SHA-256 GTA5.exe для точного совпадения.</summary>
    [JsonPropertyName("gtaSha256")]
    public string GtaSha256 { get; set; } = "";

    [JsonPropertyName("status")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public CompatStatus Status { get; set; } = CompatStatus.Unknown;

    /// <summary>
    /// Сигнатуры/offset'ы для этой версии (Вариант 1). Формат намеренно
    /// свободный (string→string): конкретную схему задаём, когда появится
    /// собственный хук; сейчас — транспорт и валидация «известна ли версия».
    /// </summary>
    [JsonPropertyName("offsets")]
    public Dictionary<string, string> Offsets { get; set; } = new();

    /// <summary>Если Broken/Untested — какой cachedBuild брать для Варианта 2.</summary>
    [JsonPropertyName("fallbackBuildId")]
    public string FallbackBuildId { get; set; } = "";

    [JsonPropertyName("note")]
    public string Note { get; set; } = "";
}

public sealed class CachedBuild
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("gtaFileVersion")]
    public string GtaFileVersion { get; set; } = "";

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; } = "";

    /// <summary>URL кэшированного GTA5.exe (Вариант 2). Копирайт-чувствительно.</summary>
    [JsonPropertyName("url")]
    public string Url { get; set; } = "";
}
