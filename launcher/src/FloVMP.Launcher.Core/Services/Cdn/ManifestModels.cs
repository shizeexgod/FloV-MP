using System.Text.Json.Serialization;

namespace FloVMP.Launcher.Services.Cdn;

/// <summary>
/// Манифест раздачи с CDN. Описывает полный набор файлов, которые должны
/// лежать локально в управляемой папке лаунчера (ядро клиента alt:V +
/// патчи + конфиги совместимости). Формат — свой, простой JSON.
///
/// Генерируется скриптом scripts/make-manifest.ps1 по содержимому папки.
/// </summary>
public sealed class Manifest
{
    /// <summary>Версия набора (произвольная строка, для отображения/логов).</summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = "";

    /// <summary>Ветка клиента alt:V, к которой относится набор.</summary>
    [JsonPropertyName("branch")]
    public string Branch { get; set; } = "release";

    /// <summary>
    /// База для относительных Url записей (если у записи Url не абсолютный).
    /// Пусто = брать за базу URL самого манифеста.
    /// </summary>
    [JsonPropertyName("baseUrl")]
    public string BaseUrl { get; set; } = "";

    [JsonPropertyName("files")]
    public List<ManifestEntry> Files { get; set; } = new();
}

public sealed class ManifestEntry
{
    /// <summary>Путь относительно корня управляемой папки, с '/'.</summary>
    [JsonPropertyName("path")]
    public string Path { get; set; } = "";

    /// <summary>SHA-256 содержимого, hex в нижнем регистре.</summary>
    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; } = "";

    [JsonPropertyName("size")]
    public long Size { get; set; }

    /// <summary>
    /// Абсолютный URL файла, либо относительный (резолвится от BaseUrl /
    /// URL манифеста). Пусто = Path от базы.
    /// </summary>
    [JsonPropertyName("url")]
    public string Url { get; set; } = "";
}
