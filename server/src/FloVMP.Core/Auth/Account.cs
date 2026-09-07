namespace FloVMP.Core.Auth;

/// <summary>Учётная запись игрока (то, что хранится).</summary>
public sealed class Account
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string CreatedUtc { get; set; } = "";
    public string LastLoginUtc { get; set; } = "";

    /// <summary>Наличные на руках. Стартовый баланс — <see cref="StartingCash"/>.</summary>
    public long Cash { get; set; } = StartingCash;

    public const long StartingCash = 5000;

    /// <summary>Банковский счёт. Стартовый баланс — <see cref="StartingBank"/>.</summary>
    public long Bank { get; set; } = StartingBank;

    public const long StartingBank = 15000;

    /// <summary>Номер банковского счёта (например, 4276...).</summary>
    public string BankAccountNumber { get; set; } = "";

    /// <summary>Почта для восстановления доступа (пустая — не указана).</summary>
    public string Email { get; set; } = "";

    /// <summary>Секрет TOTP (Base32) для двухфакторной аутентификации.</summary>
    public string TotpSecret { get; set; } = "";

    /// <summary>Включена ли двухфакторная аутентификация (Google Authenticator).</summary>
    public bool TwoFaEnabled { get; set; } = false;

    /// <summary>Уровень администратора от 0 (игрок) до 8 (Руководитель проекта).</summary>
    public int AdminLevel { get; set; } = 0;

    /// <summary>Флаг блокировки аккаунта.</summary>
    public bool IsBanned { get; set; } = false;
    public string BanReason { get; set; } = "";
    public string BanUntilUtc { get; set; } = "";

    /// <summary>Время окончания блокировки чата (ISO 8601 string, пустая если мута нет).</summary>
    public string MuteUntilUtc { get; set; } = "";

    public bool IsMuted(DateTime nowUtc)
    {
        if (string.IsNullOrEmpty(MuteUntilUtc)) return false;
        if (DateTime.TryParse(MuteUntilUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out var until))
            return nowUtc < until;
        return false;
    }

    /// <summary>Количество активных предупреждений (варнов). При 3/3 — автоматический бан.</summary>
    public int Warns { get; set; } = 0;

    /// <summary>Время окончания заключения в деморгане / КПЗ (ISO 8601 string, пустая если не заключён).</summary>
    public string JailUntilUtc { get; set; } = "";

    public bool IsJailed(DateTime nowUtc)
    {
        if (string.IsNullOrEmpty(JailUntilUtc)) return false;
        if (DateTime.TryParse(JailUntilUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out var until))
            return nowUtc < until;
        return false;
    }

    /// <summary>Правила имени пользователя (общие для клиента и сервера).</summary>
    public static bool IsValidUsername(string? name) =>
        !string.IsNullOrWhiteSpace(name)
        && name.Length is >= 3 and <= 20
        && name.All(c => char.IsLetterOrDigit(c) || c is '_');

    public static bool IsValidPassword(string? pw) =>
        !string.IsNullOrEmpty(pw) && pw.Length is >= 6 and <= 100;

    /// <summary>Нестрогая проверка адреса почты (есть "@", точка после, без пробелов).</summary>
    public static bool IsValidEmail(string? email) =>
        !string.IsNullOrWhiteSpace(email)
        && email.Length <= 128
        && System.Text.RegularExpressions.Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$");
}
