using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FloVMP.Launcher.Services.Compat;
using FloVMP.Launcher.Services.Profiles;
using Xunit;

namespace FloVMP.Launcher.Tests;

public sealed class ProfileServiceTests : IDisposable
{
    private readonly string _dir;

    public ProfileServiceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "flovmp-profile-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    private static string Hash(string content) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();

    [Fact]
    public void LoadFromDirectory_loads_valid_profiles()
    {
        var profDir = Path.Combine(_dir, "profiles");
        Directory.CreateDirectory(profDir);

        var p1 = new ClientProfile
        {
            Id = "legacy-test",
            Edition = GtaEdition.Legacy,
            GameExecutable = "GTA5.exe",
            GameFileVersion = "1.0.3889.0"
        };
        var p2 = new ClientProfile
        {
            Id = "enhanced-test",
            Edition = GtaEdition.Enhanced,
            GameExecutable = "GTA5_Enhanced.exe",
            GameFileVersion = "1.0.1158.13"
        };

        File.WriteAllText(Path.Combine(profDir, "legacy.json"), JsonSerializer.Serialize(p1));
        File.WriteAllText(Path.Combine(profDir, "enhanced.json"), JsonSerializer.Serialize(p2));

        var svc = new ProfileService();
        var loaded = svc.LoadFromDirectory(profDir);

        Assert.Equal(2, loaded.Count);
        Assert.Contains(loaded, p => p.Id == "legacy-test" && p.Edition == GtaEdition.Legacy);
        Assert.Contains(loaded, p => p.Id == "enhanced-test" && p.Edition == GtaEdition.Enhanced);
    }

    [Fact]
    public async Task EvaluateFolder_matches_legacy_and_verifies_hashes()
    {
        var gtaDir = Path.Combine(_dir, "gta-legacy");
        var updateDir = Path.Combine(gtaDir, "update");
        Directory.CreateDirectory(updateDir);

        var exePath = Path.Combine(gtaDir, "GTA5.exe");
        var rpf1 = Path.Combine(updateDir, "update.rpf");
        var rpf2 = Path.Combine(updateDir, "update2.rpf");

        File.WriteAllText(exePath, "fake-exe-content");
        File.WriteAllText(rpf1, "fake-rpf1-content");
        File.WriteAllText(rpf2, "fake-rpf2-content");

        var profile = new ClientProfile
        {
            Id = "legacy-match",
            Edition = GtaEdition.Legacy,
            GameExecutable = "GTA5.exe",
            GameSha256 = Hash("fake-exe-content"),
            UpdateRpfSha256 = Hash("fake-rpf1-content"),
            Update2RpfSha256 = Hash("fake-rpf2-content"),
            SupportStatus = "supported"
        };

        var svc = new ProfileService();
        var result = await svc.EvaluateFolderAsync(new[] { profile }, gtaDir);

        Assert.True(result.IsMatch);
        Assert.True(result.AllHashesValid);
        Assert.Empty(result.Issues);
        Assert.Equal("legacy-match", result.MatchedProfile?.Id);
    }

    [Fact]
    public async Task EvaluateFolder_matches_enhanced_executable()
    {
        var gtaDir = Path.Combine(_dir, "gta-enhanced");
        Directory.CreateDirectory(gtaDir);

        var exePath = Path.Combine(gtaDir, "GTA5_Enhanced.exe");
        File.WriteAllText(exePath, "enhanced-exe-content");

        var profile = new ClientProfile
        {
            Id = "enhanced-match",
            Edition = GtaEdition.Enhanced,
            GameExecutable = "GTA5_Enhanced.exe",
            SupportStatus = "supported"
        };

        var svc = new ProfileService();
        var result = await svc.EvaluateFolderAsync(new[] { profile }, gtaDir);

        Assert.True(result.IsMatch);
        Assert.Equal(GtaEdition.Enhanced, result.MatchedProfile?.Edition);
    }

    [Fact]
    public async Task EvaluateFolder_detects_hash_mismatch()
    {
        var gtaDir = Path.Combine(_dir, "gta-mismatch");
        Directory.CreateDirectory(gtaDir);

        var exePath = Path.Combine(gtaDir, "GTA5.exe");
        File.WriteAllText(exePath, "modified-exe-content");

        var profile = new ClientProfile
        {
            Id = "legacy-mismatch",
            Edition = GtaEdition.Legacy,
            GameExecutable = "GTA5.exe",
            GameSha256 = Hash("original-exe-content")
        };

        var svc = new ProfileService();
        var result = await svc.EvaluateFolderAsync(new[] { profile }, gtaDir);

        Assert.True(result.IsMatch);
        Assert.False(result.AllHashesValid);
        Assert.Single(result.Issues);
        Assert.Contains("Не совпадает SHA-256", result.Issues[0]);
    }
}
