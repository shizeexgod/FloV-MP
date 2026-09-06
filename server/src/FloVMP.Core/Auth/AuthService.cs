namespace FloVMP.Core.Auth;

public enum AuthOutcome
{
    Ok,
    BadUsername,
    BadPassword,
    UserExists,
    UserNotFound,
    WrongPassword,
    RateLimited,
    Banned,
    /// <summary>Аккаунт с 2FA — при входе не передан или передан неверный код.</summary>
    TwoFaRequired,
    /// <summary>Неверный одноразовый код (2FA).</summary>
    WrongCode,
    /// <summary>Некорректный адрес почты.</summary>
    EmailInvalid,
}

public sealed record AuthResult(AuthOutcome Outcome, string Message, Account? Account = null)
{
    public bool Ok => Outcome == AuthOutcome.Ok;
}

/// <summary>
/// Регистрация и вход. Чистая логика поверх <see cref="IAccountStore"/> —
/// без alt:V. Простая защита от подбора: не более N неудачных попыток за
/// окно на ключ (обычно socialClubId или ip игрока).
/// </summary>
public sealed class AuthService
{
    private readonly IAccountStore _store;
    private readonly Func<DateTime> _now;
    private readonly int _maxAttempts;
    private readonly TimeSpan _window;

    private readonly Dictionary<string, (int count, DateTime first)> _attempts = new();
    private readonly object _lock = new();

    public AuthService(IAccountStore store, Func<DateTime>? now = null,
        int maxAttempts = 5, TimeSpan? window = null)
    {
        _store = store;
        _now = now ?? (() => DateTime.UtcNow);
        _maxAttempts = maxAttempts;
        _window = window ?? TimeSpan.FromMinutes(5);
    }

    public AuthResult Register(string username, string password)
    {
        if (!Account.IsValidUsername(username))
            return new AuthResult(AuthOutcome.BadUsername, "имя: 3-20 символов, буквы/цифры/_");
        if (!Account.IsValidPassword(password))
            return new AuthResult(AuthOutcome.BadPassword, "пароль: 6-100 символов");
        if (_store.Exists(username))
            return new AuthResult(AuthOutcome.UserExists, "имя уже занято");

        var acc = _store.Create(username, PasswordHasher.Hash(password));
        return new AuthResult(AuthOutcome.Ok, "регистрация успешна", acc);
    }

    public AuthResult Login(string username, string password, string throttleKey, string? totpCode = null)
    {
        PruneAttempts();

        if (IsRateLimited(throttleKey))
            return new AuthResult(AuthOutcome.RateLimited, "слишком много попыток, подождите");

        if (!Account.IsValidUsername(username))
            return Fail(throttleKey, AuthOutcome.BadUsername, "неверное имя");

        var acc = _store.FindByUsername(username);
        if (acc is null)
            return Fail(throttleKey, AuthOutcome.UserNotFound, "нет такого игрока");

        if (!PasswordHasher.Verify(password, acc.PasswordHash))
            return Fail(throttleKey, AuthOutcome.WrongPassword, "неверный пароль");

        if (acc.IsBanned)
        {
            var reason = string.IsNullOrEmpty(acc.BanReason) ? "нарушение правил сервера" : acc.BanReason;
            return new AuthResult(AuthOutcome.Banned, $"Аккаунт заблокирован: {reason}", acc);
        }

        // Второй фактор: пароль верный, но аккаунт под 2FA — нужен код из
        // приложения. Пустой код → просим ввести; неверный → считаем попыткой.
        if (acc.TwoFaEnabled && !string.IsNullOrEmpty(acc.TotpSecret))
        {
            if (string.IsNullOrWhiteSpace(totpCode))
                return new AuthResult(AuthOutcome.TwoFaRequired, "введите код из приложения-аутентификатора", acc);
            if (!Totp.Verify(acc.TotpSecret, totpCode, _now()))
                return Fail(throttleKey, AuthOutcome.WrongCode, "неверный код из приложения");
        }

        ClearAttempts(throttleKey);
        acc.LastLoginUtc = _now().ToString("O");
        _store.Update(acc);
        return new AuthResult(AuthOutcome.Ok, "вход выполнен", acc);
    }

    // --- смена данных / 2FA -----------------------------------------------

    /// <summary>Смена пароля: нужен текущий пароль и новый (6-100 символов).</summary>
    public AuthResult ChangePassword(string username, string currentPassword, string newPassword)
    {
        var acc = _store.FindByUsername(username);
        if (acc is null) return new AuthResult(AuthOutcome.UserNotFound, "нет такого игрока");
        if (!PasswordHasher.Verify(currentPassword, acc.PasswordHash))
            return new AuthResult(AuthOutcome.WrongPassword, "текущий пароль неверный");
        if (!Account.IsValidPassword(newPassword))
            return new AuthResult(AuthOutcome.BadPassword, "новый пароль: 6-100 символов");

        acc.PasswordHash = PasswordHasher.Hash(newPassword);
        _store.Update(acc);
        return new AuthResult(AuthOutcome.Ok, "пароль изменён", acc);
    }

    /// <summary>Смена/установка почты: нужен пароль от аккаунта.</summary>
    public AuthResult ChangeEmail(string username, string password, string email)
    {
        var acc = _store.FindByUsername(username);
        if (acc is null) return new AuthResult(AuthOutcome.UserNotFound, "нет такого игрока");
        if (!PasswordHasher.Verify(password, acc.PasswordHash))
            return new AuthResult(AuthOutcome.WrongPassword, "пароль неверный");
        if (!Account.IsValidEmail(email))
            return new AuthResult(AuthOutcome.EmailInvalid, "некорректный адрес почты");

        acc.Email = email.Trim();
        _store.Update(acc);
        return new AuthResult(AuthOutcome.Ok, "почта сохранена", acc);
    }

    /// <summary>
    /// Включение 2FA: клиент сгенерировал секрет и показал QR, игрок ввёл
    /// код — проверяем и, если сходится, сохраняем секрет и включаем флаг.
    /// </summary>
    public AuthResult Enable2fa(string username, string secretBase32, string code)
    {
        var acc = _store.FindByUsername(username);
        if (acc is null) return new AuthResult(AuthOutcome.UserNotFound, "нет такого игрока");
        if (acc.TwoFaEnabled)
            return new AuthResult(AuthOutcome.Ok, "двухфакторная защита уже включена", acc);
        if (string.IsNullOrWhiteSpace(secretBase32) || Totp.FromBase32Safe(secretBase32).Length < 10)
            return new AuthResult(AuthOutcome.WrongCode, "некорректный секрет");
        if (!Totp.Verify(secretBase32, code, _now()))
            return new AuthResult(AuthOutcome.WrongCode, "неверный код — проверьте время на устройстве");

        acc.TotpSecret = secretBase32.Trim();
        acc.TwoFaEnabled = true;
        _store.Update(acc);
        return new AuthResult(AuthOutcome.Ok, "двухфакторная защита включена", acc);
    }

    /// <summary>Отключение 2FA: принимаем либо текущий код из приложения, либо пароль.</summary>
    public AuthResult Disable2fa(string username, string codeOrPassword)
    {
        var acc = _store.FindByUsername(username);
        if (acc is null) return new AuthResult(AuthOutcome.UserNotFound, "нет такого игрока");
        if (!acc.TwoFaEnabled)
            return new AuthResult(AuthOutcome.Ok, "двухфакторная защита уже выключена", acc);

        var byCode = Totp.Verify(acc.TotpSecret, codeOrPassword, _now());
        var byPassword = PasswordHasher.Verify(codeOrPassword, acc.PasswordHash);
        if (!byCode && !byPassword)
            return new AuthResult(AuthOutcome.WrongCode, "неверный код или пароль");

        acc.TotpSecret = "";
        acc.TwoFaEnabled = false;
        _store.Update(acc);
        return new AuthResult(AuthOutcome.Ok, "двухфакторная защита отключена", acc);
    }

    // --- throttle -----------------------------------------------------

    /// <summary>Выкинуть протухшие записи, чтобы словарь не рос без конца.</summary>
    private void PruneAttempts()
    {
        lock (_lock)
        {
            if (_attempts.Count == 0) return;
            var now = _now();
            var stale = _attempts
                .Where(kv => now - kv.Value.first > _window)
                .Select(kv => kv.Key)
                .ToList();
            foreach (var k in stale) _attempts.Remove(k);
        }
    }

    private bool IsRateLimited(string key)
    {
        lock (_lock)
        {
            if (!_attempts.TryGetValue(key, out var e)) return false;
            if (_now() - e.first > _window) { _attempts.Remove(key); return false; }
            return e.count >= _maxAttempts;
        }
    }

    private AuthResult Fail(string key, AuthOutcome outcome, string message)
    {
        lock (_lock)
        {
            if (_attempts.TryGetValue(key, out var e) && _now() - e.first <= _window)
                _attempts[key] = (e.count + 1, e.first);
            else
                _attempts[key] = (1, _now());
        }
        return new AuthResult(outcome, message);
    }

    private void ClearAttempts(string key)
    {
        lock (_lock) { _attempts.Remove(key); }
    }
}
