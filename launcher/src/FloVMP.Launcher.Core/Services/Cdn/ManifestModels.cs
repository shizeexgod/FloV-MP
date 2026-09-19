using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

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

/// <summary>Проверки входных путей манифеста до любых операций с диском.</summary>
public static class ManifestValidation
{
    private static readonly Regex Sha256 = new("^[0-9a-fA-F]{64}$", RegexOptions.CultureInvariant);

    public static string NormalizeRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.IndexOf('\0') >= 0)
            throw new InvalidDataException("манифест: пустой или недопустимый путь");

        var normalized = path.Replace('\\', '/');
        if (normalized.StartsWith('/') || normalized.StartsWith("//", StringComparison.Ordinal) ||
            (normalized.Length >= 2 && normalized[1] == ':'))
            throw new InvalidDataException($"манифест: абсолютный путь запрещён: {path}");

        var parts = normalized.Split('/');
        if (parts.Any(p => p.Length == 0 || p == "." || p == ".."))
            throw new InvalidDataException($"манифест: небезопасный путь: {path}");
        return string.Join('/', parts);
    }

    public static void Validate(Manifest manifest)
    {
        if (manifest is null) throw new InvalidDataException("манифест пуст");
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in manifest.Files ?? new List<ManifestEntry>())
        {
            ValidateEntry(entry);
            if (!seen.Add(entry.Path))
                throw new InvalidDataException($"манифест: повторный путь: {entry.Path}");
        }
    }

    public static void ValidateEntry(ManifestEntry entry)
    {
        entry.Path = NormalizeRelativePath(entry.Path);
        if (entry.Size < 0) throw new InvalidDataException($"манифест: отрицательный размер: {entry.Path}");
        if (!Sha256.IsMatch(entry.Sha256 ?? ""))
            throw new InvalidDataException($"манифест: неверный SHA-256: {entry.Path}");
    }
}
