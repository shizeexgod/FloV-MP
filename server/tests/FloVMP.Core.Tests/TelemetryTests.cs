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

public class TelemetryTests
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
    public async Task TelemetryReporter_Reports_Metrics_Successfully()
    {
        var config = new LicenseConfig
        {
            LicenseKey = "FLV-ENTERPRISE-2026-DEV",
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
