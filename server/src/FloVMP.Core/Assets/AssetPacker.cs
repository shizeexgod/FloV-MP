using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;

namespace FloVMP.Core.Assets;

/// <summary>
/// Движок упаковки, хеширования (SHA-256 / SHA-1), сжатия и проверки целостности ассетов FastDL.
/// </summary>
public static class AssetPacker
{
    private static readonly HashSet<string> DefaultIgnoredNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".gitignore", ".gitattributes", "manifest.json", "update.json", ".DS_Store", "Thumbs.db"
    };

    public static AssetManifest ScanDirectory(string sourceDir, IEnumerable<string>? additionalIgnores = null)
    {
        if (!Directory.Exists(sourceDir))
        {
            throw new DirectoryNotFoundException($"Каталог не найден: {sourceDir}");
        }

        var ignores = new HashSet<string>(DefaultIgnoredNames, StringComparer.OrdinalIgnoreCase);
        if (additionalIgnores != null)
        {
            foreach (var ign in additionalIgnores) ignores.Add(ign);
        }

        var manifest = new AssetManifest();
        long totalBytes = 0;

        foreach (var file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var fileName = Path.GetFileName(file);
            if (ignores.Contains(fileName)) continue;

            var rel = Path.GetRelativePath(sourceDir, file).Replace('\\', '/');
            if (ignores.Any(ig => rel.StartsWith(ig + "/", StringComparison.OrdinalIgnoreCase))) continue;

            var info = new FileInfo(file);
            var sha256 = ComputeSha256(file);
            var sha1 = ComputeSha1(file);

            manifest.Files.Add(new AssetFileEntry(rel, info.Length, sha256, sha1, info.LastWriteTimeUtc));
            totalBytes += info.Length;
        }

        manifest.TotalBytes = totalBytes;
        return manifest;
    }

    public static AssetVerificationResult VerifyDirectory(string sourceDir, AssetManifest manifest)
    {
        var result = new AssetVerificationResult();
        if (!Directory.Exists(sourceDir))
        {
            result.MissingFiles.AddRange(manifest.Files.Select(f => f.Path));
            return result;
        }

        var diskFiles = new HashSet<string>(
            Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(sourceDir, f).Replace('\\', '/')),
            StringComparer.OrdinalIgnoreCase);

        foreach (var entry in manifest.Files)
        {
            var localPath = Path.Combine(sourceDir, entry.Path.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(localPath))
            {
                result.MissingFiles.Add(entry.Path);
                continue;
            }

            diskFiles.Remove(entry.Path);

            var actualSha256 = ComputeSha256(localPath);
            if (!string.Equals(actualSha256, entry.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                result.HashMismatches.Add(new HashMismatch(entry.Path, entry.Sha256, actualSha256));
            }
        }

        // Остальные файлы на диске считаются не отслеживаемыми манифестом
        foreach (var extra in diskFiles)
        {
            var fileName = Path.GetFileName(extra);
            if (!DefaultIgnoredNames.Contains(fileName))
            {
                result.UntrackedFiles.Add(extra);
            }
        }

        return result;
    }

    public static void GzipCompressFile(string sourceFile, string targetFile)
    {
        var targetDir = Path.GetDirectoryName(targetFile);
        if (!string.IsNullOrEmpty(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        using var sourceStream = File.OpenRead(sourceFile);
        using var targetStream = File.Create(targetFile);
        using var gzipStream = new GZipStream(targetStream, CompressionLevel.Optimal);
        sourceStream.CopyTo(gzipStream);
    }

    public static void GzipDecompressFile(string compressedFile, string targetFile)
    {
        var targetDir = Path.GetDirectoryName(targetFile);
        if (!string.IsNullOrEmpty(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        using var sourceStream = File.OpenRead(compressedFile);
        using var gzipStream = new GZipStream(sourceStream, CompressionMode.Decompress);
        using var targetStream = File.Create(targetFile);
        gzipStream.CopyTo(targetStream);
    }

    public static string ComputeSha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string ComputeSha1(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var hash = SHA1.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
