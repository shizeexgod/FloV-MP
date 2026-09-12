using System.Security.Cryptography;
using System.Text;

namespace FloVMP.Core.Auth;

/// <summary>
/// TOTP (RFC 6238) — одноразовые коды для Google Authenticator и совместимых.
/// HMAC-SHA1, 6 цифр, шаг 30 секунд. Секрет хранится/передаётся в Base32
/// (RFC 4648, без паддинга) — так его понимают приложения-аутентификаторы.
/// Чистая крипта, без alt:V — тестируется отдельно.
/// </summary>
public static class Totp
{
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
    private const int Digits = 6;
    private const int PeriodSeconds = 30;

    /// <summary>Новый случайный секрет (по умолчанию 20 байт = 160 бит) в Base32.</summary>
    public static string GenerateSecret(int bytes = 20)
    {
        var raw = RandomNumberGenerator.GetBytes(bytes);
        return ToBase32(raw);
    }

    /// <summary>otpauth://-ссылка для QR-кода в приложении.</summary>
    public static string BuildUri(string secretBase32, string account, string issuer = "FloV:MP")
    {
        var label = Uri.EscapeDataString($"{issuer}:{account}");
        var iss = Uri.EscapeDataString(issuer);
        return $"otpauth://totp/{label}?secret={secretBase32}&issuer={iss}&digits={Digits}&period={PeriodSeconds}&algorithm=SHA1";
    }

    /// <summary>
    /// Проверяет код против секрета с допуском ±<paramref name="window"/> шагов
    /// (по умолчанию ±1 = принимаем предыдущий/текущий/следующий 30-сек интервал,
    /// компенсирует рассинхрон часов).
    /// </summary>
    public static bool Verify(string? secretBase32, string? code, DateTime utcNow, int window = 1)
    {
        if (string.IsNullOrWhiteSpace(secretBase32) || string.IsNullOrWhiteSpace(code)) return false;
        code = code.Trim();
        if (code.Length != Digits || !code.All(char.IsDigit)) return false;

        byte[] key;
        try { key = FromBase32(secretBase32); }
        catch { return false; }
        if (key.Length == 0) return false;

        var counter = ToUnixSeconds(utcNow) / PeriodSeconds;
        for (var offset = -window; offset <= window; offset++)
        {
            if (CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(Compute(key, counter + offset)),
                    Encoding.ASCII.GetBytes(code)))
                return true;
        }
        return false;
    }

    /// <summary>Код для конкретного счётчика (для тестов и Verify).</summary>
    public static string Compute(byte[] key, long counter)
    {
        Span<byte> msg = stackalloc byte[8];
        for (var i = 7; i >= 0; i--) { msg[i] = (byte)(counter & 0xff); counter >>= 8; }

        Span<byte> hash = stackalloc byte[20];
        HMACSHA1.HashData(key, msg, hash);

        var truncOffset = hash[19] & 0x0f;
        var binCode = ((hash[truncOffset] & 0x7f) << 24)
                    | ((hash[truncOffset + 1] & 0xff) << 16)
                    | ((hash[truncOffset + 2] & 0xff) << 8)
                    | (hash[truncOffset + 3] & 0xff);

        var mod = (int)Math.Pow(10, Digits);
        return (binCode % mod).ToString().PadLeft(Digits, '0');
    }

    private static long ToUnixSeconds(DateTime utc) =>
        (long)(DateTime.SpecifyKind(utc, DateTimeKind.Utc) - DateTime.UnixEpoch).TotalSeconds;

    // --- Base32 ---------------------------------------------------------

    public static string ToBase32(ReadOnlySpan<byte> data)
    {
        if (data.Length == 0) return "";
        var sb = new StringBuilder((data.Length * 8 + 4) / 5);
        int buffer = data[0], next = 1, bitsLeft = 8;
        while (bitsLeft > 0 || next < data.Length)
        {
            if (bitsLeft < 5)
            {
                if (next < data.Length) { buffer = (buffer << 8) | (data[next++] & 0xff); bitsLeft += 8; }
                else { buffer <<= 5 - bitsLeft; bitsLeft = 5; }
            }
            var index = 0x1f & (buffer >> (bitsLeft - 5));
            bitsLeft -= 5;
            sb.Append(Base32Alphabet[index]);
        }
        return sb.ToString();
    }

    /// <summary>Как <see cref="FromBase32"/>, но на кривой вход возвращает пустой массив.</summary>
    public static byte[] FromBase32Safe(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return Array.Empty<byte>();
        try { return FromBase32(s); } catch { return Array.Empty<byte>(); }
    }

    public static byte[] FromBase32(string s)
    {
        s = s.Trim().Replace(" ", "").Replace("-", "").TrimEnd('=').ToUpperInvariant();
        if (s.Length == 0) return Array.Empty<byte>();

        var outLen = s.Length * 5 / 8;
        var result = new byte[outLen];
        int buffer = 0, bitsLeft = 0, outPos = 0;
        foreach (var c in s)
        {
            var val = Base32Alphabet.IndexOf(c);
            if (val < 0) throw new FormatException($"недопустимый символ Base32: {c}");
            buffer = (buffer << 5) | val;
            bitsLeft += 5;
            if (bitsLeft >= 8)
            {
                result[outPos++] = (byte)((buffer >> (bitsLeft - 8)) & 0xff);
                bitsLeft -= 8;
            }
        }
        return result;
    }
}
