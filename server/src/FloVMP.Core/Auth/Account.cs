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

    /// <summary>Правила имени пользователя (общие для клиента и сервера).</summary>
    public static bool IsValidUsername(string? name) =>
        !string.IsNullOrWhiteSpace(name)
        && name.Length is >= 3 and <= 20
        && name.All(c => char.IsLetterOrDigit(c) || c is '_');

    public static bool IsValidPassword(string? pw) =>
        !string.IsNullOrEmpty(pw) && pw.Length is >= 6 and <= 100;
}
