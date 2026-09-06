using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FloVMP.Core.Assets;

/// <summary>
/// Манифест файлов FastDL/CDN для верификации целостности клиентом и лаунчером.
/// </summary>
public sealed class AssetManifest
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public string Version { get; set; } = "1.0.0";
    public DateTime GeneratedUtc { get; set; } = DateTime.UtcNow;
    public int TotalFiles => Files.Count;
    public long TotalBytes { get; set; }
    public List<AssetFileEntry> Files { get; set; } = new();

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static AssetManifest? FromJson(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<AssetManifest>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }
}
