using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;

namespace FloVMP.Core.Licensing;

public sealed record LicenseRemoteResult(
    bool Reachable,
    bool Valid,
    bool Revoked,
    string Message,
    DateTime CheckedAtUtc,
    DateTime? LeaseUntilUtc,
    string? LeasePayloadB64 = null,
    string? LeaseSignatureB64 = null,
    string? LicenseFlv = null);

/// <summary>
/// Проверяет короткий RSA-подписанный lease сервера лицензий. Отзыв приходит отдельным
/// 403 и блокирует сервер сразу; сетевой сбой не превращается в ложный отзыв,
/// пока локальный lease не истёк.
/// </summary>
public sealed class LicenseRemoteVerifier
{
    private static readonly HttpClient DefaultHttp = new() { Timeout = TimeSpan.FromSeconds(5) };
    private readonly HttpClient _http;
    private readonly string? _publicKeyPem;

    public LicenseRemoteVerifier(HttpClient? http = null, string? publicKeyPem = null)
    {
        _http = http ?? DefaultHttp;
        _publicKeyPem = publicKeyPem;
    }

    public async Task<LicenseRemoteResult> VerifyAsync(
        LicenseConfig config,
        string? version = null,
        int slots = 0,
        CancellationToken cancellationToken = default)
    {
        var checkedAt = DateTime.UtcNow;
        if (string.IsNullOrWhiteSpace(config.LicenseKey) || string.IsNullOrWhiteSpace(config.LicenseVerifyUrl))
            return new(false, false, false, "online-проверка лицензии не настроена", checkedAt, null);

        try
        {
            using var response = await _http.PostAsJsonAsync(config.LicenseVerifyUrl, new
            {
                licenseKey = config.LicenseKey.Trim(),
                serverIp = config.ServerIp,
                serverId = config.ServerId,
                version,
                slots,
            }, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var reason = TryReason(body) ?? (response.StatusCode == HttpStatusCode.Forbidden
                ? "портал отклонил лицензию"
                : $"портал вернул HTTP {(int)response.StatusCode}");

            if (response.StatusCode == HttpStatusCode.Forbidden)
                return new(true, false, true, reason, checkedAt, null);
            if (!response.IsSuccessStatusCode)
                return new(true, false, false, reason, checkedAt, null);

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (!root.TryGetProperty("valid", out var valid) || !valid.GetBoolean())
                return new(true, false, true, reason, checkedAt, null);
            var key = root.TryGetProperty("licenseKey", out var keyValue) ? keyValue.GetString() : null;
            if (!string.Equals(key?.Trim(), config.LicenseKey.Trim(), StringComparison.OrdinalIgnoreCase))
                return new(true, false, false, "портал вернул lease для другого ключа", checkedAt, null);

            var payloadB64 = root.GetProperty("leasePayloadB64").GetString() ?? "";
            var signatureB64 = root.GetProperty("leaseSignature").GetString() ?? "";
            var payload = Convert.FromBase64String(payloadB64);
            var signature = Convert.FromBase64String(signatureB64);
            if (!LicenseFile.VerifyAuthoritySignature(payload, signature, _publicKeyPem))
                return new(true, false, false, "подпись online lease не сошлась", checkedAt, null);

            using var leaseDoc = JsonDocument.Parse(payload);
            var lease = leaseDoc.RootElement;
            if (!lease.GetProperty("valid").GetBoolean() ||
                !string.Equals(lease.GetProperty("licenseKey").GetString()?.Trim(), config.LicenseKey.Trim(), StringComparison.OrdinalIgnoreCase))
                return new(true, false, true, "online lease отозван", checkedAt, null);
            var until = lease.GetProperty("validUntil").GetDateTime().ToUniversalTime();
            if (until <= checkedAt)
                return new(true, false, false, "online lease уже истёк", checkedAt, until);

            // Свежий license.flv (продлили срок, сменили тариф) — сервер заменит свой,
            // если подпись и ключ сойдутся (проверяет StarterResource).
            var flv = root.TryGetProperty("licenseFlv", out var f) && f.ValueKind == JsonValueKind.String ? f.GetString() : null;
            return new(true, true, false, "online lease действителен", checkedAt, until, payloadB64, signatureB64, flv);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new(false, false, false, "портал недоступен: " + ex.Message, checkedAt, null);
        }
    }

    private static string? TryReason(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.TryGetProperty("reason", out var reason) ? reason.GetString() : null;
        }
        catch
        {
            return null;
        }
    }
}
