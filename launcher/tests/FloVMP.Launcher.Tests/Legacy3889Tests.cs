using FloVMP.Connect;
using Xunit;

namespace FloVMP.Launcher.Tests;

public sealed class Legacy3889Tests
{
    [Theory]
    [InlineData("1.2.3.4:7788", null, "1.2.3.4:7798")]
    [InlineData("1.2.3.4:30120", null, "1.2.3.4:30130")]
    [InlineData("play.example.ru", null, "play.example.ru:7798")]
    [InlineData("1.2.3.4:7788", 9000, "1.2.3.4:9000")]
    public void NativeAddress_IsGamePortPlusTenUnlessOverridden(string connect, int? nativePort, string expected)
    {
        Assert.Equal(expected, Legacy3889.NativeAddress(connect, nativePort));
    }

    [Fact]
    public void MissingScriptHook_IsReportedNotCrashed()
    {
        var dir = Directory.CreateTempSubdirectory("flovmp-shv-").FullName;
        try
        {
            Assert.False(Legacy3889.HasScriptHook(dir, out var detail));
            Assert.Contains("ScriptHookV", detail);
            Assert.False(Legacy3889.IsLegacy3889(dir));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void ConnectRequest_IsFreshAndSingleLineFields()
    {
        Legacy3889.WriteConnectRequest("1.2.3.4:7798", "Bad\nName");
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FloVMP", "connect.txt");
        var lines = File.ReadAllLines(path);
        File.Delete(path);
        Assert.Contains("address=1.2.3.4:7798", lines);
        Assert.Contains("name=Bad Name", lines);
        var created = long.Parse(lines.Single(l => l.StartsWith("created=")).Split('=')[1]);
        Assert.InRange(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - created, 0, 5);
    }
}
