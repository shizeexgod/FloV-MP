using System.Text.Json;
using FloVMP.Launcher.Services.Compat;
using Xunit;

namespace FloVMP.Launcher.Tests;

public sealed class CompatServiceTests : IDisposable
{
    private readonly string _dir;

    public CompatServiceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "flovmp-compat-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    private string WriteManifest(CompatManifest m)
    {
        var p = Path.Combine(_dir, "compat.json");
        File.WriteAllText(p, JsonSerializer.Serialize(m));
        return p;
    }

    private static GameVersion Game(string fv, long size) =>
        new(fv, size, @"X:\gta\GTA5.exe");

    [Fact]
    public async Task Supported_version_returns_Ok()
    {
        var m = new CompatManifest
        {
            Versions =
            {
                new CompatEntry { GtaFileVersion = "1.0.3570.0", GtaSize = 12345, Status = CompatStatus.Supported },
            },
        };
        var res = await new CompatService().EvaluateAsync(WriteManifest(m), Game("1.0.3570.0", 12345));
        Assert.Equal(CompatVerdict.Ok, res.Verdict);
        Assert.NotNull(res.Entry);
    }

    [Fact]
    public async Task Matches_by_fileversion_when_size_differs()
    {
        var m = new CompatManifest
        {
            Versions = { new CompatEntry { GtaFileVersion = "1.0.3570.0", GtaSize = 999, Status = CompatStatus.Supported } },
        };
        var res = await new CompatService().EvaluateAsync(WriteManifest(m), Game("1.0.3570.0", 12345));
        Assert.Equal(CompatVerdict.Ok, res.Verdict);
    }

    [Fact]
    public async Task Unknown_version_returns_Unknown()
    {
        var m = new CompatManifest
        {
            Versions = { new CompatEntry { GtaFileVersion = "1.0.3570.0", GtaSize = 12345, Status = CompatStatus.Supported } },
        };
        var res = await new CompatService().EvaluateAsync(WriteManifest(m), Game("1.0.9999.0", 42));
        Assert.Equal(CompatVerdict.Unknown, res.Verdict);
    }

    [Fact]
    public async Task Broken_version_returns_NeedsFallback_with_build()
    {
        var m = new CompatManifest
        {
            CachedBuilds = { new CachedBuild { Id = "stable-3570", GtaFileVersion = "1.0.3570.0", Size = 100, Url = "https://x/y" } },
            Versions =
            {
                new CompatEntry
                {
                    GtaFileVersion = "1.0.3600.0", GtaSize = 200, Status = CompatStatus.Broken,
                    FallbackBuildId = "stable-3570", Note = "hook сломан",
                },
            },
        };
        var res = await new CompatService().EvaluateAsync(WriteManifest(m), Game("1.0.3600.0", 200));
        Assert.Equal(CompatVerdict.NeedsFallback, res.Verdict);
        Assert.NotNull(res.FallbackBuild);
        Assert.Equal("stable-3570", res.FallbackBuild!.Id);
    }

    [Fact]
    public async Task Missing_manifest_returns_NoManifest()
    {
        var res = await new CompatService().EvaluateAsync(
            Path.Combine(_dir, "does-not-exist.json"), Game("1.0.1.0", 1));
        Assert.Equal(CompatVerdict.NoManifest, res.Verdict);
    }
}
