using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FloVMP.Core.Licensing;
using Xunit;

namespace FloVMP.Core.Tests;

public sealed class LicenseRemoteVerifierTests
{
    private const string Key = "FLV-AAAA-BBBB-CCCC";

    private sealed class Handler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _send;
        public Handler(Func<HttpRequestMessage, HttpResponseMessage> send) => _send = send;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(_send(request));
    }

    private static LicenseConfig Config => new()
    {
        LicenseKey = Key,
        LicenseVerifyUrl = "https://portal.invalid/api/v1/license/verify",
        OfflineGraceHours = 24,
    };

    [Fact]
    public async Task Forbidden_response_is_immediate_revoke()
    {
        using var http = new HttpClient(new Handler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent("{\"reason\":\"refunded\"}")
        }));
        var result = await new LicenseRemoteVerifier(http).VerifyAsync(Config);
        Assert.True(result.Reachable);
        Assert.True(result.Revoked);
        Assert.False(result.Valid);
        Assert.Contains("refunded", result.Message);
    }

    [Fact]
    public async Task Network_failure_is_not_reported_as_revoke()
    {
        using var http = new HttpClient(new Handler(_ => throw new HttpRequestException("offline")));
        var result = await new LicenseRemoteVerifier(http).VerifyAsync(Config);
        Assert.False(result.Reachable);
        Assert.False(result.Revoked);
        Assert.False(result.Valid);
    }

    [Fact]
    public async Task Signed_lease_is_accepted_and_tampered_signature_is_rejected()
    {
        using var key = RSA.Create(2048);
        var lease = new
        {
            licenseKey = Key,
            valid = true,
            issuedAt = DateTime.UtcNow.AddMinutes(-1),
            validUntil = DateTime.UtcNow.AddHours(12),
            reason = "",
        };
        var payload = JsonSerializer.SerializeToUtf8Bytes(lease);
        var signature = key.SignData(payload, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var response = JsonSerializer.Serialize(new
        {
            valid = true,
            licenseKey = Key,
            leasePayloadB64 = Convert.ToBase64String(payload),
            leaseSignature = Convert.ToBase64String(signature),
        });
        using var http = new HttpClient(new Handler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(response, Encoding.UTF8, "application/json")
        }));
        var verifier = new LicenseRemoteVerifier(http, key.ExportSubjectPublicKeyInfoPem());
        var accepted = await verifier.VerifyAsync(Config);
        Assert.True(accepted.Valid);
        Assert.False(accepted.Revoked);
        Assert.True(accepted.LeaseUntilUtc > DateTime.UtcNow);

        using var responseDoc = JsonDocument.Parse(response);
        var tamperedSignature = Convert.FromBase64String(
            responseDoc.RootElement.GetProperty("leaseSignature").GetString()!);
        tamperedSignature[0] ^= 0x01;
        var tampered = JsonSerializer.Serialize(new
        {
            valid = true,
            licenseKey = Key,
            leasePayloadB64 = responseDoc.RootElement.GetProperty("leasePayloadB64").GetString(),
            leaseSignature = Convert.ToBase64String(tamperedSignature),
        });
        using var badHttp = new HttpClient(new Handler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(tampered, Encoding.UTF8, "application/json")
        }));
        var rejected = await new LicenseRemoteVerifier(badHttp, key.ExportSubjectPublicKeyInfoPem()).VerifyAsync(Config);
        Assert.False(rejected.Valid);
        Assert.False(rejected.Revoked);
    }

    [Fact]
    public void Signed_lease_cache_rejects_wrong_key()
    {
        using var key = RSA.Create(2048);
        var payload = JsonSerializer.SerializeToUtf8Bytes(new
        {
            licenseKey = Key,
            valid = true,
            issuedAt = DateTime.UtcNow.AddMinutes(-1),
            validUntil = DateTime.UtcNow.AddHours(12),
            reason = "",
        });
        var signature = key.SignData(payload, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var path = Path.Combine(Path.GetTempPath(), "flovmp-license-lease-" + Guid.NewGuid().ToString("N"));
        try
        {
            LicenseLeaseCache.Save(path, Convert.ToBase64String(payload), Convert.ToBase64String(signature));
            Assert.True(LicenseLeaseCache.TryRead(path, Key, out var until, key.ExportSubjectPublicKeyInfoPem()));
            Assert.True(until > DateTime.UtcNow);
            Assert.False(LicenseLeaseCache.TryRead(path, "FLV-OTHER-KEY", out _, key.ExportSubjectPublicKeyInfoPem()));
        }
        finally
        {
            try { File.Delete(path); } catch { }
        }
    }
}
