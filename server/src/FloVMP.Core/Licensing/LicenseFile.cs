using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FloVMP.Core.Licensing;

/// <summary>Данные лицензии из подписанного файла license.flv.</summary>
public sealed record LicenseInfo(
    string LicenseKey,
    string Project,
    string IssuedTo,
    string Plan,
    int MaxPlayers,
    int MaxServers,
    DateTime IssuedAtUtc,
    DateTime ExpiresAtUtc);

public enum LicenseState
{
    /// <summary>Подпись верна, срок не истёк.</summary>
    Valid,
    /// <summary>Историческое состояние; production-проверка больше его не создаёт.</summary>
    Grace,
    /// <summary>Файла license.flv нет.</summary>
    Missing,
    /// <summary>Файл повреждён, подделан или выдан на другой ключ.</summary>
    Invalid,
    /// <summary>Срок и отсрочка истекли.</summary>
    Expired,
    /// <summary>Backend подтвердил отзыв или блокировку ключа.</summary>
    Revoked,
    /// <summary>Backend недоступен дольше разрешённого lease/grace.</summary>
    RemoteUnavailable,
}

/// <summary>Итог проверки: состояние, данные и действующий лимит игроков.</summary>
public sealed record LicenseStatus(LicenseState State, LicenseInfo? Info, string Message, int PlayerLimit)
{
    public bool IsLicensed => State is LicenseState.Valid;
}

/// <summary>
/// Проверка license.flv — файла, который выдаёт портал
/// (GET /api/v1/licenses/download-by-key): <c>{ "payload_b64", "signature" }</c>,
/// подпись RSA-2048 PKCS#1 v1.5 / SHA-256 над байтами payload.
///
/// Проверяется полностью локально: подпись открытым ключом портала, водяной
/// знак, срок. Online lease и bounded offline grace добавляют управляемый
/// отзыв; подделать файл или продлить срок без закрытого ключа портала нельзя.
///
/// Без действующей лицензии игровой вход запрещается. Ограниченный
/// <see cref="UnlicensedPlayerLimit"/> оставлен только для совместимости с
/// диагностикой и старым API; production-путь не должен использовать его как
/// разрешение на вход.
/// </summary>
public static class LicenseFile
{
    public const string FileName = "license.flv";

    /// <summary>Сколько игроков пускает сервер без действующей лицензии.</summary>
    public const int UnlicensedPlayerLimit = 32;

    /// <summary>Историческое значение grace-периода; production-вход после expiry запрещён.</summary>
    public static readonly TimeSpan GracePeriod = TimeSpan.FromDays(7);

    /// <summary>
    /// Открытый ключ портала лицензий. Пара к FLOVMP_AUTHORITY_PRIVATE_KEY
    /// портала: при смене ключа на портале заменить здесь и пересобрать.
    /// </summary>
    public const string AuthorityPublicKeyPem = """
        -----BEGIN PUBLIC KEY-----
        MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAj78Lp8wdYha6Ra87S9xT
        NYir0eJKRz6ymm6UQnyFRkrPwwMHrNpQ9bYT5Bg9CCXVBvEUlhjh/A4zvudfAlyn
        PkWeUM5U+9+ddulC8VaR5JKEmBK90i+w98n4lwmYsWNWIoyQH6mMa9zpY1eSYXDh
        AGWH8siHMmJF8v86DWIGLgtL4YH0H4dv+9E65eegixpEEY+83vuWu3YEFXQzmqky
        d6hAh78AULXdeaT0gW1Wp9de7hwTFTSpotmdMmoTI/56SZ3pj5sGPfeOMJahw6de
        8alPzuTTY/xGpzsNC4I2tILtdIqHCWy2i4MCGpWeXNs5kUfHf1qL7abhX0zQMpos
        LQIDAQAB
        -----END PUBLIC KEY-----
        """;

    /// <summary>
    /// Где искать license.flv: FLOVMP_LICENSE_FILE, затем папка запуска
    /// сервера и её родитель (корень установки: сервер работает из server/).
    /// </summary>
    public static string? Locate(string? workingDirectory = null)
    {
        var explicitPath = Environment.GetEnvironmentVariable("FLOVMP_LICENSE_FILE");
        if (!string.IsNullOrWhiteSpace(explicitPath))
            return File.Exists(explicitPath) ? Path.GetFullPath(explicitPath) : null;

        var dir = workingDirectory ?? Directory.GetCurrentDirectory();
        for (var i = 0; i < 3 && dir is not null; i++)
        {
            var candidate = Path.Combine(dir, FileName);
            if (File.Exists(candidate)) return candidate;
            dir = Directory.GetParent(dir)?.FullName;
        }
        return null;
    }

    /// <param name="path">Путь к license.flv (null — файла нет).</param>
    /// <param name="expectedKey">FLOVMP_LICENSE_KEY из настроек; если задан — обязан совпасть с ключом в файле.</param>
    public static LicenseStatus Evaluate(string? path, DateTime nowUtc, string? expectedKey = null, string? publicKeyPem = null)
    {
        if (path is null || !File.Exists(path))
        {
            return new LicenseStatus(LicenseState.Missing, null,
                "license.flv не найден — вход на сервер запрещён до установки действующей лицензии",
                UnlicensedPlayerLimit);
        }

        string text;
        try { text = File.ReadAllText(path); }
        catch (Exception ex) { return Invalid("не удалось прочитать license.flv: " + ex.Message); }

        return EvaluateContent(text, nowUtc, expectedKey, publicKeyPem);
    }

    public static LicenseStatus EvaluateContent(string content, DateTime nowUtc, string? expectedKey = null, string? publicKeyPem = null)
    {
        byte[] payload, signature;
        try
        {
            using var doc = JsonDocument.Parse(content);
            payload = Convert.FromBase64String(doc.RootElement.GetProperty("payload_b64").GetString() ?? "");
            signature = Convert.FromBase64String(doc.RootElement.GetProperty("signature").GetString() ?? "");
        }
        catch
        {
            return Invalid("license.flv повреждён (ожидается JSON с payload_b64 и signature)");
        }

        try
        {
            if (!VerifyAuthoritySignature(payload, signature, publicKeyPem))
                return Invalid("подпись license.flv не сходится — файл изменён или выдан не порталом FloV:MP");
        }
        catch (Exception ex)
        {
            return Invalid("не удалось проверить подпись license.flv: " + ex.Message);
        }

        LicenseInfo info;
        string watermark;
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var r = doc.RootElement;
            info = new LicenseInfo(
                LicenseKey: r.GetProperty("licenseKey").GetString() ?? "",
                Project: r.TryGetProperty("project", out var pr) ? pr.GetString() ?? "" : "",
                IssuedTo: r.TryGetProperty("issuedTo", out var it) ? it.GetString() ?? "" : "",
                Plan: r.TryGetProperty("plan", out var pl) ? pl.GetString() ?? "" : "",
                MaxPlayers: r.TryGetProperty("maxPlayers", out var mp) && mp.TryGetInt32(out var mpv) ? mpv : 0,
                MaxServers: r.TryGetProperty("maxServers", out var ms) && ms.TryGetInt32(out var msv) ? msv : 0,
                IssuedAtUtc: r.GetProperty("issuedAt").GetDateTime().ToUniversalTime(),
                ExpiresAtUtc: r.GetProperty("expiresAt").GetDateTime().ToUniversalTime());
            watermark = r.TryGetProperty("watermark", out var wm) ? wm.GetString() ?? "" : "";
        }
        catch
        {
            return Invalid("в license.flv нет обязательных полей (licenseKey, issuedAt, expiresAt)");
        }

        // Водяной знак портала: sha256("issuedTo|licenseKey"), первые 16 hex.
        var expectedMark = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(info.IssuedTo + "|" + info.LicenseKey)))[..16];
        if (!string.Equals(watermark, expectedMark, StringComparison.OrdinalIgnoreCase))
            return Invalid("водяной знак license.flv не совпадает с владельцем лицензии");

        if (!string.IsNullOrWhiteSpace(expectedKey) &&
            !string.Equals(expectedKey.Trim(), info.LicenseKey, StringComparison.OrdinalIgnoreCase))
            return Invalid($"license.flv выдан на ключ {info.LicenseKey}, а в настройках указан {expectedKey.Trim()}");

        var limit = info.MaxPlayers > 0 ? info.MaxPlayers : UnlicensedPlayerLimit;

        // Лицензия «выдана в будущем» — почти всегда сбитые часы сервера, а не
        // подделка (подпись уже проверена). Лицензию из-за этого не отнимаем:
        // иначе сервер с неверной датой остался бы без слотов на ровном месте.
        // Но о причине говорим прямо, иначе владелец ищет проблему в лицензии.
        var clockNote = info.IssuedAtUtc > nowUtc.AddDays(1)
            ? $" | часы сервера отстают: лицензия выдана {info.IssuedAtUtc:dd.MM.yyyy}, а сейчас {nowUtc:dd.MM.yyyy} — проверьте время на машине"
            : "";

        if (nowUtc <= info.ExpiresAtUtc)
        {
            var days = (int)Math.Floor((info.ExpiresAtUtc - nowUtc).TotalDays);
            return new LicenseStatus(LicenseState.Valid, info,
                $"лицензия {info.LicenseKey} ({info.Project}, тариф {info.Plan}): до {info.ExpiresAtUtc:dd.MM.yyyy}, осталось {days} дн., слотов {limit}{clockNote}",
                limit);
        }

        return new LicenseStatus(LicenseState.Expired, info,
            $"срок лицензии {info.LicenseKey} истёк {info.ExpiresAtUtc:dd.MM.yyyy} — вход на сервер запрещён, продлите лицензию в личном кабинете",
            UnlicensedPlayerLimit);
    }

    private static LicenseStatus Invalid(string message) =>
        new(LicenseState.Invalid, null, message + " — вход на сервер запрещён до установки действующей лицензии",
            UnlicensedPlayerLimit);

    /// <summary>
    /// Прежний ключ портала. Действующий портал flovmp.ru на 21.09.2026 всё ещё
    /// подписывает лицензии и lease им, а новый ключ (AuthorityPublicKeyPem)
    /// заведён в коде раньше, чем на портале. Без переходного периода ни один
    /// сервер не принял бы выданную порталом лицензию. Поддельная лицензия со
    /// старым ключом не проходит онлайн-проверку портала (ключа нет в базе —
    /// вход закрывается). После перевода портала на новый ключ переходный
    /// период выключается: FLOVMP_LICENSE_LEGACY_KEY=off, затем ключ удаляется.
    /// </summary>
    public const string LegacyAuthorityPublicKeyPem = """
        -----BEGIN PUBLIC KEY-----
        MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAuKirQxSjX3hcS80XVFA8
        oOZPEfa2ZzSOMj1B3QgpcmjU/lI++TOKpmvhx/JGleQth1uQygJ8prFYuD3wTNvR
        4ZTO8lrSNS4T/J+gIQQZiXZofJCXrOnf3Bzx5e/1+XCthJkyBZyQUFI0Xs/eIEtn
        gbqsdHpzsTwUIZhhjZv3Y72G9dV6vRcXXu2nZRbA1AjOMRR6BnNh5ByUjt5aUSt2
        avAWjPd5YZMFNvaVvcponjbhK4cxhV1W3a0JuXE/3gNYGBE+3/6I+lN+fylJQkKj
        5kz7MTFu7zhlIU7NfHA10hdKibdLRiDjAUXV9bhKL1mrYnWJUSkUctgunOUuxxOQ
        HQIDAQAB
        -----END PUBLIC KEY-----
        """;

    public static bool LegacyKeyAccepted =>
        !string.Equals(Environment.GetEnvironmentVariable("FLOVMP_LICENSE_LEGACY_KEY")?.Trim(), "off",
            StringComparison.OrdinalIgnoreCase);

    public static bool VerifyAuthoritySignature(byte[] payload, byte[] signature, string? publicKeyPem = null)
    {
        if (publicKeyPem is not null) return Verify(publicKeyPem, payload, signature);
        return Verify(AuthorityPublicKeyPem, payload, signature) ||
               (LegacyKeyAccepted && Verify(LegacyAuthorityPublicKeyPem, payload, signature));
    }

    private static bool Verify(string pem, byte[] payload, byte[] signature)
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(pem);
        return rsa.VerifyData(payload, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    }
}
