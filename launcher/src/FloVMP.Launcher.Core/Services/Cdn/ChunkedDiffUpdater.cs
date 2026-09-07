using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace FloVMP.Launcher.Services.Cdn;

public record FileChunkInfo(int Index, long Offset, int Length, string Hash);

public record ChunkManifest(string FileName, long TotalSize, int ChunkSize, List<FileChunkInfo> Chunks);

public record DiffPlan(
    string FileName,
    long TotalSize,
    int TotalChunks,
    List<FileChunkInfo> ChunksToDownload,
    long BytesToDownload,
    float TrafficSavedPercent
);

/// <summary>
/// Умный блочный апдейтер FastDL (Chunked Binary Diff Updater).
/// Разбивает большие модпаки и карты на 4MB блоки, сверяет локальные чанки
/// и докачивает только изменённые дельты, экономя до 95-99% трафика и времени.
/// </summary>
public static class ChunkedDiffUpdater
{
    public const int DefaultChunkSize = 4 * 1024 * 1024; // 4 MB

    /// <summary>
    /// Генерирует манифест 4MB блоков для локального или серверного файла.
    /// </summary>
    public static async Task<ChunkManifest> BuildManifestAsync(
        string filePath,
        int chunkSize = DefaultChunkSize,
        CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Файл не найден", filePath);

        var fi = new FileInfo(filePath);
        var chunks = new List<FileChunkInfo>();
        var buffer = new byte[chunkSize];

        await using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 1024 * 1024, useAsync: true);
        int chunkIndex = 0;
        long currentOffset = 0;
        int bytesRead;
        while ((bytesRead = await fs.ReadAsync(buffer.AsMemory(0, chunkSize), ct)) > 0)
        {
            var hashBytes = SHA256.HashData(buffer.AsSpan(0, bytesRead));
            string hash = Convert.ToHexString(hashBytes).ToLowerInvariant();

            chunks.Add(new FileChunkInfo(chunkIndex, currentOffset, bytesRead, hash));
            currentOffset += bytesRead;
            chunkIndex++;
        }

        return new ChunkManifest(Path.GetFileName(filePath), fi.Length, chunkSize, chunks);
    }

    /// <summary>
    /// Сравнивает удаленный манифест чанков с локальным файлом и строит план дельта-загрузки.
    /// </summary>
    public static async Task<DiffPlan> ComputeDiffAsync(
        string localFilePath,
        ChunkManifest remoteManifest,
        CancellationToken ct = default)
    {
        var neededChunks = new List<FileChunkInfo>();

        if (!File.Exists(localFilePath))
        {
            // Файла нет вовсе — скачиваем все чанки
            return new DiffPlan(
                remoteManifest.FileName,
                remoteManifest.TotalSize,
                remoteManifest.Chunks.Count,
                new List<FileChunkInfo>(remoteManifest.Chunks),
                remoteManifest.TotalSize,
                0.0f
            );
        }

        await using var fs = new FileStream(localFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 1024 * 1024, useAsync: true);
        var buffer = new byte[remoteManifest.ChunkSize];

        foreach (var chunk in remoteManifest.Chunks)
        {
            ct.ThrowIfCancellationRequested();

            if (chunk.Offset + chunk.Length > fs.Length)
            {
                // Локальный файл короче — чанк отсутствует
                neededChunks.Add(chunk);
                continue;
            }

            fs.Seek(chunk.Offset, SeekOrigin.Begin);
            int read = await fs.ReadAsync(buffer.AsMemory(0, chunk.Length), ct);
            if (read != chunk.Length)
            {
                neededChunks.Add(chunk);
                continue;
            }

            var localHash = Convert.ToHexString(SHA256.HashData(buffer.AsSpan(0, read))).ToLowerInvariant();
            if (!string.Equals(localHash, chunk.Hash, StringComparison.OrdinalIgnoreCase))
            {
                // Хэш 4MB блока не совпадает — требуется обновить только этот блок
                neededChunks.Add(chunk);
            }
        }

        long bytesToDownload = 0;
        foreach (var c in neededChunks) bytesToDownload += c.Length;

        float savedPercent = remoteManifest.TotalSize > 0
            ? Math.Max(0f, 100f * (1f - ((float)bytesToDownload / remoteManifest.TotalSize)))
            : 0f;

        return new DiffPlan(
            remoteManifest.FileName,
            remoteManifest.TotalSize,
            remoteManifest.Chunks.Count,
            neededChunks,
            bytesToDownload,
            (float)Math.Round(savedPercent, 1)
        );
    }

    /// <summary>
    /// Применяет загруженный чанк точечно (in-place patch) прямо в локальный файл.
    /// </summary>
    public static async Task ApplyChunkAsync(
        string targetFilePath,
        FileChunkInfo chunk,
        ReadOnlyMemory<byte> chunkData,
        CancellationToken ct = default)
    {
        var dir = Path.GetDirectoryName(targetFilePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        await using var fs = new FileStream(targetFilePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None, bufferSize: 1024 * 1024, useAsync: true);
        fs.Seek(chunk.Offset, SeekOrigin.Begin);
        await fs.WriteAsync(chunkData, ct);
    }

    /// <summary>
    /// Обеспечивает корректный итоговый размер файла (усечение или преаллокация).
    /// Гарантирует отсутствие остаточных байтов при уменьшении размера файла между версиями.
    /// </summary>
    public static void EnsureFileSize(string targetFilePath, long totalSize)
    {
        var dir = Path.GetDirectoryName(targetFilePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using var fs = new FileStream(targetFilePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
        if (fs.Length != totalSize)
        {
            fs.SetLength(totalSize);
        }
    }
}
