using FloVMP.Launcher.Services;
using Xunit;

namespace FloVMP.Launcher.Tests;

public sealed class AltvClientCoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "flovmp-core-tests", Guid.NewGuid().ToString("N"));

    public AltvClientCoreTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    [Fact]
    public void Epic_folder_uses_rgl_platform()
    {
        Directory.CreateDirectory(Path.Combine(_dir, ".egstore"));
        Assert.Equal("rgl", AltvClientCore.DetectPlatform(_dir));
    }

    [Fact]
    public void Steam_folder_uses_steam_platform()
    {
        File.WriteAllText(Path.Combine(_dir, "steam_api64.dll"), "fixture");
        Assert.Equal("steam", AltvClientCore.DetectPlatform(_dir));
    }
}
