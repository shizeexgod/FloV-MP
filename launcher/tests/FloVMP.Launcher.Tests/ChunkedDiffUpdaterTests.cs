using System;
using System.IO;
using System.Threading.Tasks;
using FloVMP.Launcher.Services.Cdn;
using Xunit;

namespace FloVMP.Launcher.Tests;

public class ChunkedDiffUpdaterTests : IDisposable
{
    private readonly string _tempDir;

    public ChunkedDiffUpdaterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "flovmp_diff_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    [Fact]
    public async Task BuildManifest_SplitsFileIntoCorrectChunks()
    {
        var testFile = Path.Combine(_tempDir, "modpack.rpf");
        // Создаем файл 250 KB с чанками по 100 KB -> 3 чанка (100KB, 100KB, 50KB)
        var data = new byte[250 * 1024];
        new Random(42).NextBytes(data);
        await File.WriteAllBytesAsync(testFile, data);

        int chunkSize = 100 * 1024;
        var manifest = await ChunkedDiffUpdater.BuildManifestAsync(testFile, chunkSize);

        Assert.Equal("modpack.rpf", manifest.FileName);
        Assert.Equal(250 * 1024, manifest.TotalSize);
        Assert.Equal(chunkSize, manifest.ChunkSize);
        Assert.Equal(3, manifest.Chunks.Count);

        Assert.Equal(0, manifest.Chunks[0].Offset);
        Assert.Equal(100 * 1024, manifest.Chunks[0].Length);

        Assert.Equal(100 * 1024, manifest.Chunks[1].Offset);
        Assert.Equal(100 * 1024, manifest.Chunks[1].Length);

        Assert.Equal(200 * 1024, manifest.Chunks[2].Offset);
        Assert.Equal(50 * 1024, manifest.Chunks[2].Length);
    }

    [Fact]
    public async Task ComputeDiff_IdentifiesOnlyModifiedChunk()
    {
        var serverFile = Path.Combine(_tempDir, "server_map.rpf");
        var localFile = Path.Combine(_tempDir, "local_map.rpf");

        // 3 чанка по 64 KB
        int chunkSize = 64 * 1024;
        var data = new byte[3 * chunkSize];
        for (int i = 0; i < data.Length; i++) data[i] = (byte)(i % 256);
        await File.WriteAllBytesAsync(serverFile, data);

        var serverManifest = await ChunkedDiffUpdater.BuildManifestAsync(serverFile, chunkSize);

        // Копируем на клиент, но модифицируем только второй чанк (смещение 64KB)
        var clientData = (byte[])data.Clone();
        clientData[chunkSize + 10] ^= 0xFF; // Меняем 1 байт во втором чанке
        await File.WriteAllBytesAsync(localFile, clientData);

        var diff = await ChunkedDiffUpdater.ComputeDiffAsync(localFile, serverManifest);

        Assert.Equal(3, diff.TotalChunks);
        Assert.Single(diff.ChunksToDownload); // Только 1 чанк изменился!
        Assert.Equal(1, diff.ChunksToDownload[0].Index);
        Assert.Equal(chunkSize, diff.BytesToDownload);
        Assert.True(diff.TrafficSavedPercent >= 66.0f, "Должно быть сэкономлено ~66.7% трафика");
    }

    [Fact]
    public async Task ApplyChunk_PatchesFileInPlaceCorrectly()
    {
        var targetFile = Path.Combine(_tempDir, "patched.bin");
        var initial = new byte[300];
        Array.Fill(initial, (byte)1);
        await File.WriteAllBytesAsync(targetFile, initial);

        // Патчим второй чанк (индекс 1, смещение 100, длина 100) байтами со значением 99
        var replacement = new byte[100];
        Array.Fill(replacement, (byte)99);
        var chunkInfo = new FileChunkInfo(1, 100, 100, "hash");

        await ChunkedDiffUpdater.ApplyChunkAsync(targetFile, chunkInfo, replacement);

        var result = await File.ReadAllBytesAsync(targetFile);
        Assert.Equal(300, result.Length);
        Assert.Equal(1, result[0]);      // Чанк 0 остался прежним
        Assert.Equal(99, result[150]);   // Чанк 1 пропатчен
        Assert.Equal(1, result[250]);    // Чанк 2 остался прежним
    }
}
