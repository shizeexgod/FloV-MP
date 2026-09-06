using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FloVMP.Core.Licensing;
using Xunit;

namespace FloVMP.Core.Tests;

public class LicenseTests
{
    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_responder(request));
        }
    }

    [Fact]
    public async Task Verify_Rejects_Invalid_Key_Format()
    {
        var config = new LicenseConfig { LicenseKey = "INVALID-KEY-NO-FLV-PREFIX" };
        using var client = new LicenseClient(config);

        var result = await client.VerifyAsync();

        Assert.False(result.IsValid);
        Assert.Contains("FLV-", result.ErrorMessage);
    }

    [Fact]
    public async Task Verify_Parses_Successful_Response()
    {
        var config = new LicenseConfig
        {
            LicenseKey = "FLV-ENTERPRISE-2026-DERZHAVA",
            ServerIp = "188.127.229.224",
            VerifyUrl = "http://mock-license/verify"
        };

        var handler = new MockHttpMessageHandler(req =>
        {
            var json = JsonSerializer.Serialize(new
            {
                valid = true,
                licenseKey = "FLV-ENTERPRISE-2026-DERZHAVA",
                serverName = "Держава Онлайн",
                plan = "enterprise",
                maxPlayers = 1500,
                boundIp = "188.127.229.224",
                expiresAt = DateTime.UtcNow.AddDays(365).ToString("o"),
                signature = "mock-sha256-signature"
            });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json)
            };
        });

        using var http = new HttpClient(handler);
        var tempDir = Path.Combine(Path.GetTempPath(), "flov_test_" + Guid.NewGuid());
        using var client = new LicenseClient(config, http, tempDir);

        var result = await client.VerifyAsync();

        Assert.True(result.IsValid);
        Assert.Equal("enterprise", result.Plan);
        Assert.Equal(1500, result.MaxPlayers);
        Assert.Equal("188.127.229.224", result.BoundIp);
        Assert.True(result.SignatureValid);

        if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
    }

    [Fact]
    public async Task Verify_Falls_Back_To_Cache_On_Network_Error()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "flov_test_cache_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);

        var config = new LicenseConfig
        {
            LicenseKey = "FLV-ENTERPRISE-2026-DERZHAVA",
            ServerIp = "188.127.229.224",
            VerifyUrl = "http://mock-license/verify"
        };

        // First call succeeds and caches
        var handlerSuccess = new MockHttpMessageHandler(_ =>
        {
            var json = JsonSerializer.Serialize(new
            {
                valid = true,
                licenseKey = "FLV-ENTERPRISE-2026-DERZHAVA",
                serverName = "Держава Онлайн",
                plan = "enterprise",
                maxPlayers = 1500,
                boundIp = "188.127.229.224",
                expiresAt = DateTime.UtcNow.AddDays(30).ToString("o"),
                signature = "sig"
            });
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) };
        });

        using var httpSuccess = new HttpClient(handlerSuccess);
        using var clientSuccess = new LicenseClient(config, httpSuccess, tempDir);
        var res1 = await clientSuccess.VerifyAsync();
        Assert.True(res1.IsValid);

        // Second call has network failure -> uses offline cache
        var handlerFailure = new MockHttpMessageHandler(_ => throw new HttpRequestException("Portal down"));
        using var httpFail = new HttpClient(handlerFailure);
        using var clientFail = new LicenseClient(config, httpFail, tempDir);

        var res2 = await clientFail.VerifyAsync();

        Assert.True(res2.IsValid);
        Assert.True(res2.IsCachedOffline);
        Assert.Equal(1500, res2.MaxPlayers);

        if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
    }

    [Fact]
    public async Task TelemetryReporter_Reports_Metrics_Successfully()
    {
        var config = new LicenseConfig
        {
            LicenseKey = "FLV-ENTERPRISE-2026-DERZHAVA",
            TelemetryUrl = "http://mock-telemetry/heartbeat"
        };

        bool reported = false;
        var handler = new MockHttpMessageHandler(req =>
        {
            reported = true;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"acknowledged\":true}")
            };
        });

        using var http = new HttpClient(handler);
        using var reporter = new TelemetryReporter(config, http);

        var success = await reporter.ReportTickAsync(players: 42, maxPlayers: 1500, tickRate: 60, memoryMb: 256, fps: 60);

        Assert.True(success);
        Assert.True(reported);
    }
}
