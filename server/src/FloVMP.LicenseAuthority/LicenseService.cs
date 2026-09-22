using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FloVMP.LicenseAuthority;

/// <summary>Ответ службы: HTTP-код и JSON (или файл лицензии).</summary>
public sealed record ServiceReply(int Status, string Body, string ContentType = "application/json; charset=utf-8");

/// <summary>
/// Решения по ключам: проверка, активация сервера, подпись license.flv и
/// короткого подтверждения (lease). Протокол — тот, что понимает сервер FloV:MP
/// (FloVMP.Core.Licensing: LicenseFile, LicenseRemoteVerifier).
/// </summary>
public sealed class LicenseService
{
    public static readonly Regex KeyShape = new(
        @"^FLV-(?:[A-Z0-9]{4}-[A-Z0-9]{4}-[A-Z0-9]{4}|[A-Z0-9]{8}-[A-Z0-9]{8}-[A-Z0-9]{8}-[A-Z0-9]{8})$",
        RegexOptions.CultureInvariant);
    private static readonly Regex ServerIdShape = new(@"^[a-z0-9:.\-]{1,64}$", RegexOptions.CultureInvariant);

    /// <summary>Подтверждение действует сутки: столько сервер живёт без связи с VDS.</summary>
    public static readonly TimeSpan LeaseLifetime = TimeSpan.FromHours(24);
    /// <summary>«Бессрочно» в файле лицензии: у сервера поле срока обязательное.</summary>
    public static readonly DateTime LifetimeExpiry = new(2099, 12, 31, 0, 0, 0, DateTimeKind.Utc);

    private readonly ILicenseStore _store;
    private readonly RSA _signer;
    private readonly Func<DateTime> _now;
    private readonly object _activationGate = new();

    public LicenseService(ILicenseStore store, RSA signer, Func<DateTime>? now = null)
    {
        _store = store;
        _signer = signer;
        _now = now ?? (() => DateTime.UtcNow);
    }

    public static string NewKey()
    {
        var hex = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        return $"FLV-{hex[..8]}-{hex[8..16]}-{hex[16..24]}-{hex[24..32]}";
    }

    public static string? NormalizeKey(string? raw)
    {
        var key = (raw ?? "").Trim().ToUpperInvariant();
        return KeyShape.IsMatch(key) ? key : null;
    }

    /// <summary>Отпечаток ключа для журнала службы (сам ключ туда не пишем).</summary>
    public static string Mark(string key) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key.Trim().ToUpperInvariant())))[..10].ToLowerInvariant();

    // --- серверы клиентов --------------------------------------------------------------

    /// <summary>POST /api/v1/license/verify: регулярная проверка сервера клиента.</summary>
    public ServiceReply Verify(string? rawKey, string? serverId, string ip, string? version, int slots)
    {
        var (license, error) = Admit(rawKey, serverId, ip, version, slots, "verify");
        if (license is null) return error!;
        var now = _now();
        var expiry = license.ExpiresAt ?? LifetimeExpiry;
        var until = now + LeaseLifetime < expiry ? now + LeaseLifetime : expiry;
        var normalizedServerId = NormalizeServerId(serverId, ip);
        var lease = Sign(new
        {
            licenseKey = license.Key,
            serverId = normalizedServerId,
            valid = true,
            issuedAt = now,
            validUntil = until,
            reason = "",
        });
        return Json(200, new
        {
            valid = true,
            licenseKey = license.Key,
            project = license.Project,
            plan = license.Plan,
            maxPlayers = license.MaxPlayers,
            maxServers = license.MaxServers,
            expiresAt = license.ExpiresAt,
            verifiedAt = now,
            leasePayloadB64 = lease.PayloadB64,
            leaseSignature = lease.SignatureB64,
            // Свежий файл: после продления или смены тарифа сервер заменит свой.
            licenseFlv = LicenseFlv(license),
        });
    }

    /// <summary>GET /api/v1/licenses/download-by-key: активация, ответ — license.flv.</summary>
    public ServiceReply Download(string? rawKey, string? serverId, string ip)
    {
        var (license, error) = Admit(rawKey, serverId, ip, "", 0, "download");
        if (license is null) return error!;
        return new ServiceReply(200, LicenseFlv(license));
    }

    /// <summary>
    /// Ключ существует, действует, а сервер уже активирован или помещается в лимит.
    /// Первый сервер переводит ключ из «выдан» в «активирован».
    /// </summary>
    private (LicenseRecord? License, ServiceReply? Error) Admit(string? rawKey, string? rawServerId, string ip,
                                                                 string? version, int slots, string what)
    {
        var key = NormalizeKey(rawKey);
        if (key is null) return (null, Deny(400, "неверный формат ключа (FLV-XXXXXXXX-XXXXXXXX-XXXXXXXX-XXXXXXXX)"));
        // Старые сборки не присылают ID установки — считаем сервер по IP.
        var serverId = NormalizeServerId(rawServerId, ip);
        if (!ServerIdShape.IsMatch(serverId)) return (null, Deny(400, "неверный ID сервера"));

        var license = _store.FindByKey(key);
        var now = _now();
        string? refusal = license is null ? "ключ не найден — проверьте, что он введён без ошибок"
            : license.Status == LicenseStatus.Revoked ? "ключ отозван" + Because(license)
            : license.Status == LicenseStatus.Suspended ? "ключ приостановлен" + Because(license)
            : license.Expired(now) ? $"срок ключа истёк {license.ExpiresAt:dd.MM.yyyy} — продлите лицензию"
            : null;
        if (refusal is not null)
        {
            Log(license, key, what + ":отказ", ip, serverId, refusal);
            return (null, Deny(403, refusal));
        }

        if (slots < 0) return (null, Deny(400, "число слотов не может быть отрицательным"));
        if (slots > license!.MaxPlayers)
        {
            var msg = $"заявлено слотов: {slots}, лицензия разрешает не более {license.MaxPlayers}";
            Log(license, key, what + ":слоты", ip, serverId, msg);
            return (null, Deny(403, msg));
        }

        // HttpApi обслуживает запросы параллельно. Без общей секции два первых
        // запроса могли одновременно увидеть свободный лимит и создать две
        // активации для ключа с maxServers=1.
        lock (_activationGate)
        {
            var activation = _store.FindActivation(license.Id, serverId);
            if (activation is null)
            {
                var used = _store.Activations(license.Id).Count;
                if (used >= license.MaxServers)
                {
                    var msg = $"по ключу уже работает серверов: {used} из {license.MaxServers} — отвяжите старый сервер или увеличьте лимит";
                    Log(license, key, what + ":лимит", ip, serverId, msg);
                    return (null, Deny(403, msg));
                }
                _store.InsertActivation(new ActivationRecord
                {
                    LicenseId = license.Id, ServerId = serverId, Ip = ip, Version = Short(version, 32),
                    Slots = slots, FirstSeen = now, LastSeen = now,
                });
                if (license.Status == LicenseStatus.Issued)
                {
                    license.Status = LicenseStatus.Active;
                    license.ActivatedAt = now;
                    _store.Update(license);
                }
                Log(license, key, "активация", ip, serverId, $"сервер {used + 1} из {license.MaxServers}");
            }
            else
            {
                if (!string.Equals(activation.Ip, ip, StringComparison.OrdinalIgnoreCase))
                {
                    var msg = $"ID сервера уже привязан к другому IP ({activation.Ip}); отвяжите сервер перед переносом";
                    Log(license, key, what + ":ip", ip, serverId, msg);
                    return (null, Deny(403, msg));
                }
                _store.TouchActivation(activation.Id, ip, Short(version, 32), slots, now);
            }
        }
        return (license, null);
    }

    private static string NormalizeServerId(string? rawServerId, string ip)
    {
        var serverId = (rawServerId ?? "").Trim().ToLowerInvariant();
        return serverId.Length == 0 ? "ip:" + ip : serverId;
    }

    private static string Because(LicenseRecord l) => l.StatusReason.Length > 0 ? ": " + l.StatusReason : "";
    private static string Short(string? s, int max) => (s ?? "").Length > max ? s![..max] : s ?? "";

    private void Log(LicenseRecord? license, string key, string type, string ip, string serverId, string detail) =>
        _store.AddEvent(new EventRecord
        {
            At = _now(), LicenseId = license?.Id, Key = Mark(key), Type = type, Ip = ip, ServerId = serverId, Detail = detail,
        });

    /// <summary>Раздача пакетов: ключ должен действовать, сервер не активируется.</summary>
    public string? DownloadRefusal(string? rawKey)
    {
        var key = NormalizeKey(rawKey);
        if (key is null) return "неверный формат ключа";
        var l = _store.FindByKey(key);
        if (l is null) return "ключ не найден";
        if (l.Status is LicenseStatus.Revoked or LicenseStatus.Suspended) return "ключ " + LicenseStatus.Russian(l.Status);
        if (l.Expired(_now())) return "срок ключа истёк";
        return null;
    }

    // --- подпись -------------------------------------------------------------------------

    public string LicenseFlv(LicenseRecord l)
    {
        var watermark = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(l.Owner + "|" + l.Key)))[..16].ToLowerInvariant();
        var signed = Sign(new
        {
            licenseKey = l.Key, project = l.Project, issuedTo = l.Owner, plan = l.Plan,
            maxPlayers = l.MaxPlayers, maxServers = l.MaxServers,
            issuedAt = _now(), expiresAt = l.ExpiresAt ?? LifetimeExpiry, watermark,
        });
        return JsonSerializer.Serialize(new { payload_b64 = signed.PayloadB64, signature = signed.SignatureB64 },
            new JsonSerializerOptions { WriteIndented = true });
    }

    private (string PayloadB64, string SignatureB64) Sign(object payload)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload);
        var sig = _signer.SignData(bytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return (Convert.ToBase64String(bytes), Convert.ToBase64String(sig));
    }

    private static ServiceReply Deny(int status, string reason) => Json(status, new { valid = false, reason });
    /// <summary>Кириллица в ответах — как есть: причину отказа читает человек.</summary>
    public static readonly JsonSerializerOptions JsonOut = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All),
    };

    private static ServiceReply Json(int status, object body) => new(status, JsonSerializer.Serialize(body, JsonOut));
}
