using FloVMP.Connect;
using Xunit;

namespace FloVMP.Launcher.Tests;

public sealed class GtaLaunchGuardTests
{
    [Fact]
    public void MissingExecutable_IsRejected()
    {
        var result = GtaLaunchGuard.Evaluate(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "GTA5.exe"));
        Assert.False(result.Allowed);
        Assert.Equal("game-executable-missing", result.Code);
    }

    [Fact]
    public void EnhancedName_IsRejectedBeforeVersionLookup()
    {
        var dir = Path.Combine(Path.GetTempPath(), "flovmp-guard", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var exe = Path.Combine(dir, "GTA5_Enhanced.exe");
        File.WriteAllBytes(exe, new byte[] { 0x4d, 0x5a });
        try
        {
            var result = GtaLaunchGuard.Evaluate(exe);
            Assert.False(result.Allowed);
            Assert.Equal("enhanced-native-client-missing", result.Code);
        }
        finally { Directory.Delete(dir, true); }
    }
}
