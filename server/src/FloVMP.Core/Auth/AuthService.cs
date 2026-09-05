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

    public AuthResult Login(string username, string password, string throttleKey)
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

        ClearAttempts(throttleKey);
        acc.LastLoginUtc = _now().ToString("O");
        _store.Update(acc);
        return new AuthResult(AuthOutcome.Ok, "вход выполнен", acc);
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
