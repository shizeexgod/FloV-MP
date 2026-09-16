using FloVMP.Core.Admin;
using Xunit;

namespace FloVMP.Core.Tests;

/// <summary>
/// Защита токена владельца от перебора.
///
/// Токен даёт уровень 8 — полный контроль над сервером. Подсказка о
/// /claimowner показывается каждому, кто зашёл на свежий сервер, так что
/// перебор открыто предлагается любому посетителю. Раньше токен был 32-битным
/// и ни одной блокировки после неудач не было.
/// </summary>
[Collection("AdminEnvironment")]
public sealed class OwnerClaimBruteForceTests : IDisposable
{
    private readonly string _dir;
    private DateTime _now = new(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc);

    public OwnerClaimBruteForceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "flovmp-claim", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { /* временный каталог */ }
    }

    private AdminBootstrapManager NewManager()
    {
        Environment.SetEnvironmentVariable("FLOVMP_SETUP_TOKEN", null);
        return new AdminBootstrapManager(Path.Combine(_dir, "admins.json"), () => _now);
    }

    [Fact]
    public void GeneratedToken_Has64BitsOfEntropy()
    {
        // FLV-XXXX-XXXX-XXXX-XXXX: 16 шестнадцатеричных знаков = 64 бита.
        var token = NewManager().CurrentSetupToken;
        var hex = token.Replace("FLV-", "").Replace("-", "");
        Assert.Equal(16, hex.Length);
        Assert.All(hex, ch => Assert.True(Uri.IsHexDigit(ch), $"не hex: {ch}"));
    }

    [Fact]
    public void GeneratedTokens_AreNotRepeated()
    {
        var seen = new HashSet<string>();
        for (var i = 0; i < 50; i++)
        {
            var dir = Path.Combine(_dir, "t" + i);
            Directory.CreateDirectory(dir);
            var mgr = new AdminBootstrapManager(Path.Combine(dir, "admins.json"), () => _now);
            Assert.True(seen.Add(mgr.CurrentSetupToken), "токен повторился");
        }
    }

    [Fact]
    public void CorrectToken_StillWorks()
    {
        var mgr = NewManager();
        var token = mgr.CurrentSetupToken;
        Assert.True(mgr.TryClaimOwner(token, "Owner", 111UL, out _));
    }

    [Fact]
    public void CorrectToken_IsCaseInsensitive()
    {
        // Владелец набирает токен руками из консоли — регистр не должен мешать.
        var mgr = NewManager();
        var token = mgr.CurrentSetupToken.ToLowerInvariant();
        Assert.True(mgr.TryClaimOwner(token, "Owner", 111UL, out _));
    }

    [Fact]
    public void AfterMaxFailures_ClaimIsLockedForEveryone()
    {
        var mgr = NewManager();
        var real = mgr.CurrentSetupToken;

        for (var i = 0; i < AdminBootstrapManager.ClaimMaxFailures; i++)
            Assert.False(mgr.TryClaimOwner("FLV-0000-0000-0000-000" + (i % 10), "Attacker" + i, (ulong)i, out _));

        Assert.True(mgr.IsClaimLocked);

        // Даже ВЕРНЫЙ токен сейчас не принимается: блокировка общая, иначе
        // перебор продолжился бы, просто угадав в момент блокировки.
        Assert.False(mgr.TryClaimOwner(real, "Owner", 111UL, out var msg));
        Assert.Contains("закрыт", msg);
    }

    [Fact]
    public void Lockout_IsServerWide_NotPerPlayer()
    {
        // Каждая попытка от «разного» игрока — так выглядит перебор с сотни
        // подключений. Блокировка обязана сработать всё равно.
        var mgr = NewManager();
        for (var i = 0; i < AdminBootstrapManager.ClaimMaxFailures; i++)
            mgr.TryClaimOwner("wrong-" + i, "Bot" + i, (ulong)(1000 + i), out _);

        Assert.True(mgr.IsClaimLocked);
    }

    [Fact]
    public void Lockout_ExpiresAfterCooldown()
    {
        var mgr = NewManager();
        var real = mgr.CurrentSetupToken;

        for (var i = 0; i < AdminBootstrapManager.ClaimMaxFailures; i++)
            mgr.TryClaimOwner("wrong", "Attacker", 1UL, out _);
        Assert.True(mgr.IsClaimLocked);

        _now = _now + AdminBootstrapManager.ClaimLockout + TimeSpan.FromSeconds(1);

        // Владелец не должен остаться без сервера навсегда из-за чужого перебора.
        Assert.False(mgr.IsClaimLocked);
        Assert.True(mgr.TryClaimOwner(real, "Owner", 111UL, out _));
    }

    [Fact]
    public void FewFailures_BelowLimit_DoNotLock()
    {
        // Владелец может ошибиться при наборе пару раз — это не атака.
        var mgr = NewManager();
        var real = mgr.CurrentSetupToken;

        for (var i = 0; i < 3; i++)
            mgr.TryClaimOwner("опечатка", "Owner", 111UL, out _);

        Assert.False(mgr.IsClaimLocked);
        Assert.True(mgr.TryClaimOwner(real, "Owner", 111UL, out _));
    }

    [Fact]
    public void SuccessfulClaim_ResetsFailureCounter()
    {
        var mgr = NewManager();
        var real = mgr.CurrentSetupToken;

        for (var i = 0; i < 5; i++)
            mgr.TryClaimOwner("wrong", "Owner", 111UL, out _);
        Assert.Equal(5, mgr.ClaimFailures);

        Assert.True(mgr.TryClaimOwner(real, "Owner", 111UL, out _));
        Assert.Equal(0, mgr.ClaimFailures);
    }

    [Fact]
    public void ClaimedToken_CannotBeReused()
    {
        var mgr = NewManager();
        var real = mgr.CurrentSetupToken;

        Assert.True(mgr.TryClaimOwner(real, "Owner", 111UL, out _));
        Assert.False(mgr.TryClaimOwner(real, "Thief", 222UL, out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("FLV-")]
    public void EmptyOrPrefixOnlyToken_IsRejected(string input)
    {
        var mgr = NewManager();
        Assert.False(mgr.TryClaimOwner(input, "Attacker", 1UL, out _));
    }
}
