using System;
using System.IO;
using System.Text;
using FloVMP.Core.Assets;
using Xunit;

namespace FloVMP.Core.Tests;

public class AssetPackerTests : IDisposable
{
    private readonly string _tempDir;

    public AssetPackerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "flovmp_packer_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }
        catch { }
    }

    [Fact]
    public void ScanDirectory_ProducesValidManifest()
    {
        // Arrange
        var subDir = Path.Combine(_tempDir, "client", "scripts");
        Directory.CreateDirectory(subDir);

        File.WriteAllText(Path.Combine(subDir, "main.js"), "console.log('hello');");
        File.WriteAllText(Path.Combine(_tempDir, "meta.xml"), "<meta><script src='client/scripts/main.js'/></meta>");

        // Act
        var manifest = AssetPacker.ScanDirectory(_tempDir);

        // Assert
        Assert.Equal(2, manifest.TotalFiles);
        Assert.Contains(manifest.Files, f => f.Path == "client/scripts/main.js");
        Assert.Contains(manifest.Files, f => f.Path == "meta.xml");

        var json = manifest.ToJson();
        Assert.NotEmpty(json);

        var deserialized = AssetManifest.FromJson(json);
        Assert.NotNull(deserialized);
        Assert.Equal(2, deserialized.TotalFiles);
    }

    [Fact]
    public void VerifyDirectory_DetectsModificationAndMissingFiles()
    {
        // Arrange
        var fileA = Path.Combine(_tempDir, "fileA.txt");
        var fileB = Path.Combine(_tempDir, "fileB.txt");
        File.WriteAllText(fileA, "Content A");
        File.WriteAllText(fileB, "Content B");

        var manifest = AssetPacker.ScanDirectory(_tempDir);
        var initialCheck = AssetPacker.VerifyDirectory(_tempDir, manifest);
        Assert.True(initialCheck.IsValid);

        // Act 1: Modify File A
        File.WriteAllText(fileA, "Tampered Content");
        var tamperedCheck = AssetPacker.VerifyDirectory(_tempDir, manifest);
        Assert.False(tamperedCheck.IsValid);
        Assert.Single(tamperedCheck.HashMismatches);
        Assert.Equal("fileA.txt", tamperedCheck.HashMismatches[0].Path);

        // Act 2: Delete File B
        File.Delete(fileB);
        var missingCheck = AssetPacker.VerifyDirectory(_tempDir, manifest);
        Assert.False(missingCheck.IsValid);
        Assert.Contains("fileB.txt", missingCheck.MissingFiles);

        // Act 3: Add untracked File C
        var fileC = Path.Combine(_tempDir, "fileC.txt");
        File.WriteAllText(fileC, "Extra File");
        var untrackedCheck = AssetPacker.VerifyDirectory(_tempDir, manifest);
        Assert.Contains("fileC.txt", untrackedCheck.UntrackedFiles);
    }

    [Fact]
    public void GzipCompressAndDecompress_BitExact()
    {
        // Arrange
        var originalFile = Path.Combine(_tempDir, "bundle.js");
        var gzFile = Path.Combine(_tempDir, "bundle.js.gz");
        var restoredFile = Path.Combine(_tempDir, "bundle_restored.js");

        var originalText = "function run() { return Array.from({length: 1000}, (_, i) => i * 2); } run();";
        File.WriteAllText(originalFile, originalText, Encoding.UTF8);

        // Act
        AssetPacker.GzipCompressFile(originalFile, gzFile);
        Assert.True(File.Exists(gzFile));

        AssetPacker.GzipDecompressFile(gzFile, restoredFile);
        Assert.True(File.Exists(restoredFile));

        // Assert
        var restoredText = File.ReadAllText(restoredFile, Encoding.UTF8);
        Assert.Equal(originalText, restoredText);
    }
}
