using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FloVMP.Core.Licensing;
using Xunit;

namespace FloVMP.Core.Tests;

public sealed class LicenseFileTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "flovmp-license-tests", Guid.NewGuid().ToString("N"));
    private readonly RSA _key = RSA.Create(2048);
    private readonly string _publicPem;
    private static readonly DateTime Now = new(2026, 9, 17, 12, 0, 0, DateTimeKind.Utc);

    public LicenseFileTests()
    {
        Directory.CreateDirectory(_dir);
        _publicPem = _key.ExportSubjectPublicKeyInfoPem();
    }

    public void Dispose()
    {
        _key.Dispose();
        try { Directory.Delete(_dir, true); } catch { }
    }

    /// <summary>Файл в формате портала (createSignedLicenseFlv).</summary>
    private string MakeFlv(string key = "FLV-AAAA-BBBB-CCCC", string issuedTo = "Owner", int maxPlayers = 500,
                           DateTime? expires = null, string? watermark = null, Action<byte[]>? tamper = null,
                           DateTime? issuedAt = null)
    {
        var payload = new
        {
            licenseKey = key,
            project = "Mason RP",
            issuedTo,
            plan = "business",
            maxPlayers,
            maxServers = 3,
            issuedAt = (issuedAt ?? Now.AddDays(-10)).ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            expiresAt = (expires ?? Now.AddDays(30)).ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            watermark = watermark ?? Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(issuedTo + "|" + key)))[..16].ToLowerInvariant(),
        };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload);
        var sig = _key.SignData(bytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        tamper?.Invoke(bytes);
        return JsonSerializer.Serialize(new { payload_b64 = Convert.ToBase64String(bytes), signature = Convert.ToBase64String(sig) });
    }

    private LicenseStatus Check(string content, DateTime? now = null, string? expectedKey = null) =>
        LicenseFile.EvaluateContent(content, now ?? Now, expectedKey, _publicPem);

    [Fact]
    public void Valid_file_gives_license_limit()
    {
        var s = Check(MakeFlv(maxPlayers: 500));
        Assert.Equal(LicenseState.Valid, s.State);
        Assert.Equal(500, s.PlayerLimit);
        Assert.Equal("Mason RP", s.Info!.Project);
    }

    [Fact]
    public void Licence_from_the_future_works_but_warns_about_server_clock()
    {
        // Сбитые часы на машине владельца: лицензия «выдана завтра». Подпись
        // настоящая, поэтому слоты не отнимаем — но причину называем, иначе
        // владелец ищет проблему в лицензии, а она во времени сервера.
        var s = Check(MakeFlv(issuedAt: Now.AddDays(30)));

        Assert.Equal(LicenseState.Valid, s.State);
        Assert.Equal(500, s.PlayerLimit);
        Assert.Contains("часы сервера", s.Message);
    }

    [Fact]
    public void Tampered_payload_is_rejected()
    {
        // Попытка поднять слоты правкой файла без закрытого ключа портала.
        var s = Check(MakeFlv(tamper: b => b[^5] ^= 0x01));
        Assert.Equal(LicenseState.Invalid, s.State);
        Assert.Equal(LicenseFile.UnlicensedPlayerLimit, s.PlayerLimit);
    }

    [Fact]
    public void File_signed_by_other_key_is_rejected()
    {
        using var other = RSA.Create(2048);
        var content = MakeFlv();
        Assert.Equal(LicenseState.Invalid,
            LicenseFile.EvaluateContent(content, Now, null, other.ExportSubjectPublicKeyInfoPem()).State);
    }

    [Fact]
    public void Wrong_watermark_is_rejected()
    {
        Assert.Equal(LicenseState.Invalid, Check(MakeFlv(watermark: "0000000000000000")).State);
    }

    [Fact]
    public void Key_mismatch_with_settings_is_rejected()
    {
        var s = Check(MakeFlv(key: "FLV-AAAA-BBBB-CCCC"), expectedKey: "FLV-OTHER-KEY");
        Assert.Equal(LicenseState.Invalid, s.State);
        Assert.Equal(LicenseState.Valid, Check(MakeFlv(key: "FLV-AAAA-BBBB-CCCC"), expectedKey: "flv-aaaa-bbbb-cccc").State);
    }

    [Fact]
    public void Expired_license_is_blocked_immediately()
    {
        var content = MakeFlv(maxPlayers: 800, expires: Now.AddDays(-3));
        var expired = Check(content);
        Assert.Equal(LicenseState.Expired, expired.State);
        Assert.False(expired.IsLicensed);
        Assert.Contains("вход на сервер запрещён", expired.Message);
        Assert.Equal(LicenseFile.UnlicensedPlayerLimit, expired.PlayerLimit);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{\"payload_b64\":\"!!!\",\"signature\":\"x\"}")]
    [InlineData("{}")]
    public void Garbage_is_invalid_not_crash(string content)
    {
        Assert.Equal(LicenseState.Invalid, Check(content).State);
    }

    [Fact]
    public void Missing_file_blocks_login()
    {
        var s = LicenseFile.Evaluate(Path.Combine(_dir, "nope.flv"), Now);
        Assert.Equal(LicenseState.Missing, s.State);
        Assert.False(s.IsLicensed);
        Assert.Contains("вход на сервер запрещён", s.Message);
        Assert.Equal(LicenseFile.UnlicensedPlayerLimit, s.PlayerLimit);
    }

    [Fact]
    public void Locate_finds_file_in_install_root_when_server_runs_from_subfolder()
    {
        var serverDir = Path.Combine(_dir, "server");
        Directory.CreateDirectory(serverDir);
        File.WriteAllText(Path.Combine(_dir, LicenseFile.FileName), "{}");
        Assert.Equal(Path.Combine(_dir, LicenseFile.FileName), LicenseFile.Locate(serverDir));
    }

    [Fact]
    public void Real_portal_file_verifies_with_embedded_authority_key()
    {
        // Файл сгенерирован кодом портала (createSignedLicenseFlv) его ключом:
        // проверка совместимости формата и встроенного открытого ключа.
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "portal-license.flv");
        var s = LicenseFile.Evaluate(path, Now);
        Assert.Equal(LicenseState.Valid, s.State);
        Assert.Equal("FLV-TEST-0001-0001", s.Info!.LicenseKey);
        Assert.Equal(500, s.PlayerLimit);
    }

    [Fact]
    public void License_from_live_portal_with_previous_key_is_accepted_during_transition()
    {
        // Лицензия, выданная действующим порталом (ещё старым ключом): без
        // переходного периода ни один проданный сервер не пустил бы игроков.
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "portal-license-legacy-key.flv");
        var s = LicenseFile.Evaluate(path, Now);
        Assert.Equal(LicenseState.Valid, s.State);
        Assert.Equal("FLV-34C6-24FA-1E9F", s.Info!.LicenseKey);
    }

    [Fact]
    public void Previous_key_can_be_switched_off_after_portal_rotation()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "portal-license-legacy-key.flv");
        var before = Environment.GetEnvironmentVariable("FLOVMP_LICENSE_LEGACY_KEY");
        try
        {
            Environment.SetEnvironmentVariable("FLOVMP_LICENSE_LEGACY_KEY", "off");
            Assert.Equal(LicenseState.Invalid, LicenseFile.Evaluate(path, Now).State);
            // Новым ключом подписанная — по-прежнему действует.
            var fresh = Path.Combine(AppContext.BaseDirectory, "Fixtures", "portal-license.flv");
            Assert.Equal(LicenseState.Valid, LicenseFile.Evaluate(fresh, Now).State);
        }
        finally
        {
            Environment.SetEnvironmentVariable("FLOVMP_LICENSE_LEGACY_KEY", before);
        }
    }
}
