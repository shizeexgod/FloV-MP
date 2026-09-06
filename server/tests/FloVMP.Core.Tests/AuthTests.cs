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

    [Fact]
    public void Store_preserves_admin_level_and_punishment_flags()
    {
        var path = Path.Combine(_dir, "admin_store.json");
        var store1 = new JsonAccountStore(path);
        var acc = store1.Create("AdminHero", "secret6");
        acc.AdminLevel = 8;
        acc.Cash = 100_000;
        acc.IsBanned = true;
        acc.BanReason = "Testing";
        acc.MuteUntilUtc = "2026-12-31T23:59:59.0000000Z";
        store1.Update(acc);

        var store2 = new JsonAccountStore(path);
        var loaded = store2.FindByUsername("AdminHero");
        Assert.NotNull(loaded);
        Assert.Equal(8, loaded.AdminLevel);
        Assert.Equal(100_000, loaded.Cash);
        Assert.True(loaded.IsBanned);
        Assert.Equal("Testing", loaded.BanReason);
        Assert.Equal("2026-12-31T23:59:59.0000000Z", loaded.MuteUntilUtc);
    }

    // ── смена пароля / почты ──────────────────────────────────────────

    [Fact]
    public void ChangePassword_requires_current_and_applies_new()
    {
        _svc.Register("Chpw", "oldpass1");
        Assert.Equal(AuthOutcome.WrongPassword, _svc.ChangePassword("Chpw", "nope", "newpass1").Outcome);
        Assert.Equal(AuthOutcome.BadPassword, _svc.ChangePassword("Chpw", "oldpass1", "12345").Outcome);

        Assert.True(_svc.ChangePassword("Chpw", "oldpass1", "newpass1").Ok);
        Assert.Equal(AuthOutcome.WrongPassword, _svc.Login("Chpw", "oldpass1", "ip:p").Outcome);
        Assert.True(_svc.Login("Chpw", "newpass1", "ip:p").Ok);
    }

    [Fact]
    public void ChangeEmail_validates_and_persists()
    {
        _svc.Register("Chem", "secret6");
        Assert.Equal(AuthOutcome.WrongPassword, _svc.ChangeEmail("Chem", "bad", "a@b.ru").Outcome);
        Assert.Equal(AuthOutcome.EmailInvalid, _svc.ChangeEmail("Chem", "secret6", "not-an-email").Outcome);

        var ok = _svc.ChangeEmail("Chem", "secret6", "player@mail.ru");
        Assert.True(ok.Ok);
        Assert.Equal("player@mail.ru", ok.Account!.Email);
        // переживает перечитывание из файла
        var reloaded = _svc.Login("Chem", "secret6", "ip:e");
        Assert.Equal("player@mail.ru", reloaded.Account!.Email);
    }

    // ── двухфакторная аутентификация ─────────────────────────────────

    [Fact]
    public void Enable2fa_rejects_wrong_code_accepts_valid_then_login_needs_code()
    {
        _svc.Register("Tfa", "secret6");
        var secret = Totp.GenerateSecret();
        var counter = ((long)(_now - DateTime.UnixEpoch).TotalSeconds) / 30;
        var goodCode = Totp.Compute(Totp.FromBase32(secret), counter);

        Assert.Equal(AuthOutcome.WrongCode, _svc.Enable2fa("Tfa", secret, "000000").Outcome);
        Assert.True(_svc.Enable2fa("Tfa", secret, goodCode).Ok);

        // теперь вход без кода — TwoFaRequired, с неверным — WrongCode, с верным — Ok
        Assert.Equal(AuthOutcome.TwoFaRequired, _svc.Login("Tfa", "secret6", "ip:t").Outcome);
        Assert.Equal(AuthOutcome.WrongCode, _svc.Login("Tfa", "secret6", "ip:t", "111111").Outcome);
        Assert.True(_svc.Login("Tfa", "secret6", "ip:t", goodCode).Ok);
    }

    [Fact]
    public void Disable2fa_accepts_password_or_code_then_login_plain_again()
    {
        _svc.Register("Tfa2", "secret6");
        var secret = Totp.GenerateSecret();
        var counter = ((long)(_now - DateTime.UnixEpoch).TotalSeconds) / 30;
        var goodCode = Totp.Compute(Totp.FromBase32(secret), counter);
        _svc.Enable2fa("Tfa2", secret, goodCode);

        Assert.Equal(AuthOutcome.WrongCode, _svc.Disable2fa("Tfa2", "garbage").Outcome);
        Assert.True(_svc.Disable2fa("Tfa2", "secret6").Ok);          // по паролю
        Assert.True(_svc.Login("Tfa2", "secret6", "ip:t2").Ok);       // код больше не нужен
    }

    [Fact]
    public void TwoFa_state_persists_across_store_instances()
    {
        var path = Path.Combine(_dir, "tfa_persist.json");
        var secret = Totp.GenerateSecret();
        var counter = ((long)(_now - DateTime.UnixEpoch).TotalSeconds) / 30;
        var goodCode = Totp.Compute(Totp.FromBase32(secret), counter);

        var s1 = new AuthService(new JsonAccountStore(path), () => _now);
        s1.Register("Persist2fa", "secret6");
        Assert.True(s1.Enable2fa("Persist2fa", secret, goodCode).Ok);

        var s2 = new AuthService(new JsonAccountStore(path), () => _now);
        Assert.Equal(AuthOutcome.TwoFaRequired, s2.Login("Persist2fa", "secret6", "ip:x").Outcome);
        Assert.True(s2.Login("Persist2fa", "secret6", "ip:x", goodCode).Ok);
    }
}

public sealed class SessionHandoffTests : IDisposable
{
    private readonly string _dir;
    private readonly string _path;

    public SessionHandoffTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "flovmp-session-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _path = Path.Combine(_dir, "session.json");
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    [Fact]
    public void Write_then_Read_roundtrips_camelCase()
    {
        var acc = new Account { Username = "InGameGuy", CreatedUtc = "2026-01-02T03:04:05Z", Email = "g@h.ru", TwoFaEnabled = true };
        SessionHandoff.Write(acc, _path);

        var raw = File.ReadAllText(_path);
        Assert.Contains("\"username\"", raw);   // camelCase — так читает лаунчер
        Assert.Contains("\"twoFa\"", raw);

        var read = SessionHandoff.Read(_path);
        Assert.NotNull(read);
        Assert.Equal("InGameGuy", read!.Username);
        Assert.Equal("g@h.ru", read.Email);
        Assert.True(read.TwoFa);
    }

    [Fact]
    public void Read_missing_or_empty_returns_null()
    {
        Assert.Null(SessionHandoff.Read(_path));
        File.WriteAllText(_path, "{ \"username\": \"\" }");
        Assert.Null(SessionHandoff.Read(_path));
        File.WriteAllText(_path, "not json");
        Assert.Null(SessionHandoff.Read(_path));
    }

    [Fact]
    public void Clear_removes_file_and_is_idempotent()
    {
        SessionHandoff.Write(new Account { Username = "X" }, _path);
        Assert.True(File.Exists(_path));
        SessionHandoff.Clear(_path);
        Assert.False(File.Exists(_path));
        SessionHandoff.Clear(_path); // повторно — не бросает
    }
}

