using FloVMP.Core.Auth;
using Xunit;

namespace FloVMP.Core.Tests;

public sealed class PasswordHasherTests
{
    [Fact]
    public void Hash_then_Verify_roundtrips()
    {
        var h = PasswordHasher.Hash("correct horse battery");
        Assert.True(PasswordHasher.Verify("correct horse battery", h));
        Assert.False(PasswordHasher.Verify("wrong", h));
    }

    [Fact]
    public void Hash_is_salted_each_time()
    {
        Assert.NotEqual(PasswordHasher.Hash("same"), PasswordHasher.Hash("same"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not$the$right$format")]
    [InlineData("pbkdf2$sha256$abc$xx$yy")]
    public void Verify_rejects_malformed_stored(string stored)
    {
        Assert.False(PasswordHasher.Verify("whatever", stored));
    }
}

public sealed class AuthServiceTests : IDisposable
{
    private readonly string _dir;
    private readonly AuthService _svc;
    private DateTime _now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public AuthServiceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "flovmp-auth-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        var store = new JsonAccountStore(Path.Combine(_dir, "accounts.json"));
        _svc = new AuthService(store, () => _now, maxAttempts: 3, window: TimeSpan.FromMinutes(5));
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    [Fact]
    public void Register_then_Login_succeeds()
    {
        Assert.True(_svc.Register("Player_1", "secret6").Ok);
        var login = _svc.Login("Player_1", "secret6", "ip:1");
        Assert.True(login.Ok);
        Assert.Equal("Player_1", login.Account!.Username);
    }

    [Fact]
    public void Register_rejects_bad_username_and_password()
    {
        Assert.Equal(AuthOutcome.BadUsername, _svc.Register("ab", "secret6").Outcome);
        Assert.Equal(AuthOutcome.BadPassword, _svc.Register("Player_1", "12345").Outcome);
    }

    [Fact]
    public void Register_rejects_duplicate()
    {
        Assert.True(_svc.Register("Dup", "secret6").Ok);
        Assert.Equal(AuthOutcome.UserExists, _svc.Register("dup", "secret6").Outcome); // case-insensitive
    }

    [Fact]
    public void Login_unknown_user_and_wrong_password()
    {
        Assert.Equal(AuthOutcome.UserNotFound, _svc.Login("Ghost", "secret6", "ip:2").Outcome);
        _svc.Register("Real", "secret6");
        Assert.Equal(AuthOutcome.WrongPassword, _svc.Login("Real", "nope66", "ip:2").Outcome);
    }

    [Fact]
    public void Login_rate_limits_after_maxAttempts()
    {
        _svc.Register("Target", "secret6");
        for (var i = 0; i < 3; i++)
            Assert.Equal(AuthOutcome.WrongPassword, _svc.Login("Target", "bad", "ip:3").Outcome);

        Assert.Equal(AuthOutcome.RateLimited, _svc.Login("Target", "secret6", "ip:3").Outcome);

        // окно прошло — снова можно
        _now = _now.AddMinutes(6);
        Assert.True(_svc.Login("Target", "secret6", "ip:3").Ok);
    }

    [Fact]
    public void Successful_login_clears_attempts()
    {
        _svc.Register("Clr", "secret6");
        _svc.Login("Clr", "bad", "ip:4");
        _svc.Login("Clr", "bad", "ip:4");
        Assert.True(_svc.Login("Clr", "secret6", "ip:4").Ok);
        // счётчик сброшен — ещё две неудачи не должны залочить
        _svc.Login("Clr", "bad", "ip:4");
        _svc.Login("Clr", "bad", "ip:4");
        Assert.NotEqual(AuthOutcome.RateLimited, _svc.Login("Clr", "bad", "ip:4").Outcome);
    }

    [Fact]
    public void Stale_throttle_entries_are_pruned()
    {
        _svc.Register("V", "secret6");
        // много разных ключей с неудачами
        for (var i = 0; i < 50; i++)
            _svc.Login("V", "bad", $"ip:{i}");

        // окно прошло — следующий Login должен всё вычистить, и старый ключ
        // больше не залочен
        _now = _now.AddMinutes(6);
        Assert.NotEqual(AuthOutcome.RateLimited, _svc.Login("V", "bad", "ip:0").Outcome);
    }

    [Fact]
    public void Accounts_persist_across_store_instances()
    {
        var path = Path.Combine(_dir, "persist.json");
        new AuthService(new JsonAccountStore(path), () => _now).Register("Persist", "secret6");

        var reopened = new AuthService(new JsonAccountStore(path), () => _now);
        Assert.True(reopened.Login("Persist", "secret6", "ip:5").Ok);
    }
}
