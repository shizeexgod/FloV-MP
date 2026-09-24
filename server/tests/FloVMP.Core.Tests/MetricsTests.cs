using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FloVMP.Core.Diagnostics;
using Xunit;

namespace FloVMP.Core.Tests;

public class MetricsTests
{
    private static MetricsSnapshot Snap(double maxTick = 5, double rate = 60, int online = 3, int errors = 0, long mem = 200) =>
        new(DateTime.UtcNow, 3700, online, 32, rate, 1.5, maxTick, 12.5, 80, errors, mem, new Dictionary<string, double> { ["jobs"] = 4 });

    [Fact]
    public void Collector_AveragesAndMax_ThenStartsNewWindow()
    {
        var m = new ServerMetrics();
        var ms = Stopwatch.Frequency / 1000;
        m.RecordTick(2 * ms);
        m.RecordTick(4 * ms);
        m.RecordTick(30 * ms);
        m.RecordError();
        var s = m.Collect(5, 32, memoryBytes: 300L * 1024 * 1024);
        Assert.Equal(12, s.AvgTickMs, 1);
        Assert.Equal(30, s.MaxTickMs, 1);
        Assert.Equal(1, s.Errors);
        Assert.Equal(300, s.MemoryMb);
        Assert.Equal(5, s.Online);

        var next = m.Collect(5, 32);
        Assert.Equal(0, next.MaxTickMs);
        Assert.Equal(0, next.Errors);
    }

    [Fact]
    public void Collector_CustomMetricNamesAreChecked()
    {
        var m = new ServerMetrics();
        Assert.True(m.SetCustom("jobs_active", 3));
        Assert.False(m.SetCustom("Bad Name", 1));
        Assert.False(m.SetCustom("x", double.NaN));
        Assert.Equal(3, m.Collect(0, 0).Custom["jobs_active"]);
    }

    [Fact]
    public void Formats_JsonAndPrometheus()
    {
        var s = Snap();
        using var doc = JsonDocument.Parse(s.ToJson());
        Assert.Equal(3, doc.RootElement.GetProperty("online").GetInt32());
        Assert.Equal(4, doc.RootElement.GetProperty("custom").GetProperty("jobs").GetDouble());
        var prom = s.ToPrometheus();
        Assert.Contains("\nflovmp_online 3\n", prom);
        Assert.Contains("flovmp_custom_jobs 4", prom);
        Assert.Contains("# TYPE flovmp_tick_max_ms gauge", prom);
        Assert.Contains("онлайн 3/32", s.ToLogLine());
    }

    [Fact]
    public void Alerts_ThresholdsAndCooldown()
    {
        var p = new AlertPolicy { MaxTickMs = 250, MinTickRate = 30, MaxErrorsPerWindow = 5, MaxMemoryMb = 1000, CooldownMs = 60_000 };
        Assert.Empty(p.Evaluate(Snap(), 0));
        var alerts = p.Evaluate(Snap(maxTick: 900, rate: 10, errors: 20, mem: 4000), 1000);
        Assert.Equal(new[] { "tick", "tickrate", "errors", "memory" }, alerts.Select(a => a.Kind));
        Assert.Empty(p.Evaluate(Snap(maxTick: 900), 30_000));          // повтор раньше перерыва — молчим
        Assert.Single(p.Evaluate(Snap(maxTick: 900), 70_000));
    }

    [Fact]
    public void Alerts_TickRateOnlyJudgedWithPlayers_ZeroDisables()
    {
        var p = new AlertPolicy { MinTickRate = 30, MaxTickMs = 0, MaxErrorsPerWindow = 0 };
        Assert.Empty(p.Evaluate(Snap(rate: 1, online: 0), 0));
        Assert.Empty(p.Evaluate(Snap(maxTick: 100000, rate: 100, errors: 1000), 0));
    }

    [Fact]
    public void RunMarker_DetectsUncleanStop()
    {
        var path = Path.Combine(Path.GetTempPath(), "flovmp-run-" + Guid.NewGuid().ToString("N"), "running.json");
        try
        {
            Assert.Null(RunMarker.Begin(path));                 // первый запуск
            RunMarker.Update(path, DateTime.UtcNow, "онлайн 7/32");
            var crashed = RunMarker.Begin(path);                // метка осталась — прошлый упал
            Assert.NotNull(crashed);
            Assert.Equal("онлайн 7/32", crashed!.LastMetrics);
            RunMarker.End(path);
            Assert.Null(RunMarker.Begin(path));                 // штатная остановка — не падение
        }
        finally { try { Directory.Delete(Path.GetDirectoryName(path)!, true); } catch { } }
    }

    [Fact]
    public void Http_RoutesAndToken()
    {
        using var open = new MetricsHttpServer(IPAddress.Loopback, 0, "", () => Snap());
        Assert.StartsWith("200", open.Route("GET /metrics HTTP/1.1\r\n\r\n").Status);
        Assert.Contains("flovmp_online", open.Route("GET /metrics.prom HTTP/1.1\r\n\r\n").Body);
        Assert.StartsWith("404", open.Route("GET /x HTTP/1.1\r\n\r\n").Status);
        Assert.StartsWith("405", open.Route("POST /metrics HTTP/1.1\r\n\r\n").Status);

        using var locked = new MetricsHttpServer(IPAddress.Loopback, 0, "s3cr et", () => Snap());
        Assert.StartsWith("401", locked.Route("GET /metrics HTTP/1.1\r\n\r\n").Status);
        Assert.StartsWith("401", locked.Route("GET /metrics?token=wrong HTTP/1.1\r\n\r\n").Status);
        Assert.StartsWith("200", locked.Route("GET /metrics?token=s3cr%20et HTTP/1.1\r\n\r\n").Status);
        Assert.StartsWith("200", locked.Route("GET /metrics HTTP/1.1\r\nAuthorization: Bearer s3cr et\r\n\r\n").Status);

        using var empty = new MetricsHttpServer(IPAddress.Loopback, 0, "", () => null);
        Assert.StartsWith("503", empty.Route("GET /metrics HTTP/1.1\r\n\r\n").Status);
    }

    [Fact]
    public void Http_RefusesPublicBindWithoutToken()
    {
        Assert.Throws<InvalidOperationException>(() => new MetricsHttpServer(IPAddress.Any, 0, "", () => null));
    }

    [Fact]
    public async Task Http_ServesOverRealSocket()
    {
        using var server = new MetricsHttpServer(IPAddress.Loopback, 0, "", () => Snap(online: 9));
        server.Start();
        using var http = new HttpClient();
        var json = await http.GetStringAsync($"http://127.0.0.1:{server.Port}/metrics");
        Assert.Contains("\"online\":9", json);
    }

    [Fact]
    public void Webhook_PostsContentAndText()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        string? body = null;
        var got = new ManualResetEventSlim();
        _ = Task.Run(async () =>
        {
            using var c = await listener.AcceptTcpClientAsync();
            var s = c.GetStream();
            var buf = new byte[8192];
            var sb = new StringBuilder();
            while (!sb.ToString().Contains("}"))
            {
                var n = await s.ReadAsync(buf);
                if (n == 0) break;
                sb.Append(Encoding.UTF8.GetString(buf, 0, n));
            }
            body = sb.ToString();
            var ok = Encoding.ASCII.GetBytes("HTTP/1.1 204 No Content\r\nContent-Length: 0\r\n\r\n");
            await s.WriteAsync(ok);
            got.Set();
        });
        using (var w = new WebhookNotifier(_ => { }) { Url = $"http://127.0.0.1:{port}/hook", ServerName = "Test" })
        {
            w.Send("сервер завис");
            Assert.True(got.Wait(5000));
        }
        listener.Stop();
        Assert.Contains("\"content\":\"[Test] сервер завис\"", Regex(body!));
        Assert.Contains("\"text\":", body);
    }

    // JSON экранирует кириллицу — разэкранируем для сравнения.
    private static string Regex(string raw)
    {
        var i = raw.IndexOf('{');
        using var doc = JsonDocument.Parse(raw[i..]);
        return "\"content\":\"" + doc.RootElement.GetProperty("content").GetString() + "\"";
    }
}
