using System;

namespace FloVMP.Core.Assets;

/// <summary>
/// Описание файла ресурса в манифесте CDN/FastDL.
/// </summary>
public sealed class AssetFileEntry
{
    public string Path { get; set; } = string.Empty;
    public long Size { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public string Sha1 { get; set; } = string.Empty;
    public DateTime LastModifiedUtc { get; set; } = DateTime.UtcNow;

    public AssetFileEntry() { }

    public AssetFileEntry(string path, long size, string sha256, string sha1, DateTime lastModifiedUtc)
    {
        Path = path.Replace('\\', '/').TrimStart('/');
        Size = size;
        Sha256 = sha256.ToLowerInvariant();
        Sha1 = sha1.ToLowerInvariant();
        LastModifiedUtc = lastModifiedUtc;
    }
}
