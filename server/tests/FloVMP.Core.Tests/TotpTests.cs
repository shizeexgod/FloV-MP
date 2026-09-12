using System.Text;
using FloVMP.Core.Auth;
using Xunit;

namespace FloVMP.Core.Tests;

public sealed class TotpTests
{
    // Контрольные векторы RFC 6238 (Appendix B) — секрет "12345678901234567890"
    // (ASCII) в Base32 = GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ, алгоритм SHA1, 8 цифр.
    // Мы используем 6 цифр — берём последние 6 знаков ожидаемого 8-значного кода.
    private static readonly byte[] RfcKey = Encoding.ASCII.GetBytes("12345678901234567890");

    [Theory]
    [InlineData(59L, "287082")]           // 94287082 → последние 6
    [InlineData(1111111109L, "081804")]   // 07081804
    [InlineData(1111111111L, "050471")]   // 14050471
    [InlineData(1234567890L, "005924")]   // 89005924
    [InlineData(2000000000L, "279037")]   // 69279037
    public void Compute_matches_rfc6238_vectors(long unixSeconds, string expected6)
    {
        var counter = unixSeconds / 30;
        Assert.Equal(expected6, Totp.Compute(RfcKey, counter));
    }

    [Fact]
    public void Base32_roundtrips()
    {
        var secret = Totp.GenerateSecret();
        var bytes = Totp.FromBase32(secret);
        Assert.Equal(20, bytes.Length);
        Assert.Equal(secret, Totp.ToBase32(bytes));
    }

    private static string CodeAt(string secret, DateTime utc)
    {
        var counter = ((long)(DateTime.SpecifyKind(utc, DateTimeKind.Utc) - DateTime.UnixEpoch).TotalSeconds) / 30;
        return Totp.Compute(Totp.FromBase32(secret), counter);
    }

    [Fact]
    public void Verify_accepts_current_code_and_rejects_wrong()
    {
        var now = new DateTime(2026, 6, 1, 12, 0, 15, DateTimeKind.Utc);
        var secret = Totp.GenerateSecret();
        var code = CodeAt(secret, now);

        Assert.True(Totp.Verify(secret, code, now));
        Assert.False(Totp.Verify(secret, "000000", now));
        Assert.False(Totp.Verify(secret, "notnum", now));
        Assert.False(Totp.Verify(secret, "12345", now));   // 5 цифр
        Assert.False(Totp.Verify("", code, now));
    }

    [Fact]
    public void Verify_tolerates_clock_skew_within_window()
    {
        var secret = Totp.GenerateSecret();
        var t0 = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var codeNow = CodeAt(secret, t0);

        // код, сгенерированный "сейчас", принимается и через 25 секунд (тот же шаг),
        // и на следующем шаге (±1 окно)
        Assert.True(Totp.Verify(secret, codeNow, t0.AddSeconds(25)));
        Assert.True(Totp.Verify(secret, codeNow, t0.AddSeconds(35)));
        // но не через две минуты
        Assert.False(Totp.Verify(secret, codeNow, t0.AddMinutes(2)));
    }

    [Fact]
    public void BuildUri_is_valid_otpauth()
    {
        var uri = Totp.BuildUri("JBSWY3DPEHPK3PXP", "Игрок123", "FloV:MP Server");
        Assert.StartsWith("otpauth://totp/", uri);
        Assert.Contains("secret=JBSWY3DPEHPK3PXP", uri);
        Assert.Contains("issuer=FloV", uri);
        Assert.Contains("period=30", uri);
    }

    [Fact]
    public void FromBase32Safe_never_throws()
    {
        Assert.Empty(Totp.FromBase32Safe(null));
        Assert.Empty(Totp.FromBase32Safe(""));
        Assert.Empty(Totp.FromBase32Safe("!!!not base32!!!"));
        Assert.NotEmpty(Totp.FromBase32Safe("JBSWY3DPEHPK3PXP"));
    }
}
