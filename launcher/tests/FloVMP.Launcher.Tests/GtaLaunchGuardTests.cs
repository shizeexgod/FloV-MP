using FloVMP.Connect;
using Xunit;
using System.Diagnostics;

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

    [Fact]
    public void InstalledLegacy3889_IsFingerprintCheckedAndBlockedUntilAdapter()
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "9d2d0eb64d5c44529cece33fe2a46482",
            "GTA5.exe");
        if (!File.Exists(path) || !string.Equals(
                FileVersionInfo.GetVersionInfo(path).FileVersion?.Trim(), "1.0.3889.0", StringComparison.OrdinalIgnoreCase))
            return;

        var result = GtaLaunchGuard.Evaluate(path);
        Assert.False(result.Allowed);
        Assert.Equal("legacy-3889-native-adapter-missing", result.Code);
    }

    [Fact]
    public void MissingAdapterPath_PreservesHardStopForLegacy3889()
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "9d2d0eb64d5c44529cece33fe2a46482",
            "GTA5.exe");
        if (!File.Exists(path) || !string.Equals(
                FileVersionInfo.GetVersionInfo(path).FileVersion?.Trim(), "1.0.3889.0", StringComparison.OrdinalIgnoreCase))
            return;

        var result = GtaLaunchGuard.Evaluate(path, Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.dll"));
        Assert.False(result.Allowed);
        Assert.Equal("legacy-3889-native-adapter-missing", result.Code);
    }

    [Fact]
    public void MalformedExecutable_IsRejectedBeforeFingerprintOrRpfChecks()
    {
        var root = Path.Combine(Path.GetTempPath(), "flovmp-guard", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "update"));
        var exe = Path.Combine(root, "GTA5.exe");
        File.WriteAllBytes(exe, new byte[] { 0x4d, 0x5a });

        try
        {
            var result = GtaLaunchGuard.Evaluate(exe);
            Assert.False(result.Allowed);
            Assert.Equal("game-version-unreadable", result.Code);
        }
        finally { Directory.Delete(root, true); }
    }
}
