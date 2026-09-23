using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using FloVMP.Core.Licensing;
using FloVMP.LicenseAuthority;
using Xunit;
using AuthorityStatus = FloVMP.LicenseAuthority.LicenseStatus;
using CoreState = FloVMP.Core.Licensing.LicenseState;

namespace FloVMP.Core.Tests;

/// <summary>
/// Сервер лицензий (flovmp-license) и сервер FloV:MP вместе: то, что выдаёт
/// служба, принимает код сервера клиента, а отказы доходят как отказы.
/// </summary>
public sealed class LicenseAuthorityTests
{
    private sealed class MemoryStore : ILicenseStore
    {
        public readonly List<LicenseRecord> Licenses = new();
        public readonly List<ActivationRecord> Acts = new();
        public readonly List<EventRecord> Log = new();
        private long _id;
        public void EnsureSchema() { }
        public LicenseRecord? FindByKey(string key) => Licenses.FirstOrDefault(l => l.Key == key);
        public List<LicenseRecord> List(string? status) => Licenses.Where(l => status is null || l.Status == status).ToList();
        public void Insert(LicenseRecord l) { l.Id = ++_id; Licenses.Add(l); }
        public void Update(LicenseRecord l) { }
        public List<ActivationRecord> Activations(long id) => Acts.Where(a => a.LicenseId == id).ToList();
        public ActivationRecord? FindActivation(long id, string s) => Acts.FirstOrDefault(a => a.LicenseId == id && a.ServerId == s);
        public void InsertActivation(ActivationRecord a) { a.Id = ++_id; Acts.Add(a); }
        public void TouchActivation(long id, string ip, string v, int slots, DateTime at)
        {
            var a = Acts.First(a => a.Id == id);
            a.Ip = ip;
            if (v.Length > 0) a.Version = v;
            if (slots > 0) a.Slots = slots;
            a.LastSeen = at;
        }
        public int RemoveActivations(long id, string? s) => Acts.RemoveAll(a => a.LicenseId == id && (s is null || a.ServerId == s));
        public void AddEvent(EventRecord e) => Log.Add(e);
        public List<EventRecord> Events(long? id, int limit) => Log.Where(e => id is null || e.LicenseId == id).Take(limit).ToList();
    }

    private readonly RSA _key = RSA.Create(2048);
    private readonly MemoryStore _store = new();
    // Часы службы — рядом с настоящими: LicenseRemoteVerifier сверяет срок lease
    // с DateTime.UtcNow, и фиксированная дата ломала бы проверку на следующий день.
    private DateTime _now = DateTime.UtcNow;
    private string Pub => _key.ExportSubjectPublicKeyInfoPem();

    private LicenseService Service() => new(_store, _key, () => _now);

    private LicenseRecord Issue(int servers = 1, int? days = 365)
    {
        var l = new LicenseRecord
        {
            Key = LicenseService.NewKey(), Project = "Держава RP", Owner = "Иван", Plan = "business",
            MaxPlayers = 500, MaxServers = servers, CreatedAt = _now, ExpiresAt = days is { } d ? _now.AddDays(d) : null,
        };
        _store.Insert(l);
        return l;
    }

    [Fact]
    public void Generated_keys_have_the_strong_format_the_server_accepts()
    {
        var key = LicenseService.NewKey();
        Assert.Matches(@"^FLV-[0-9A-F]{8}-[0-9A-F]{8}-[0-9A-F]{8}-[0-9A-F]{8}$", key);
        Assert.NotEqual(key, LicenseService.NewKey());
    }

    [Fact]
    public void Activation_returns_a_license_file_the_server_accepts_and_marks_the_key_active()
    {
        var l = Issue();
        var reply = Service().Download(l.Key, "srv-a", "1.2.3.4");
        Assert.Equal(200, reply.Status);

        var status = LicenseFile.EvaluateContent(reply.Body, _now, l.Key, Pub);
        Assert.Equal(CoreState.Valid, status.State);
        Assert.Equal(500, status.PlayerLimit);
        Assert.Equal("business", status.Info!.Plan);
        Assert.Equal(AuthorityStatus.Active, l.Status);
        Assert.NotNull(l.ActivatedAt);
        Assert.Single(_store.Acts);
    }

    [Fact]
    public void Lifetime_key_gets_a_far_expiry_in_the_file()
    {
        var l = Issue(days: null);
        var status = LicenseFile.EvaluateContent(Service().Download(l.Key, "srv", "ip").Body, _now.AddYears(50), l.Key, Pub);
        Assert.Equal(CoreState.Valid, status.State);
    }

    [Fact]
    public void Server_limit_blocks_an_extra_server_until_one_is_unbound()
    {
        var l = Issue(servers: 1);
        Assert.Equal(200, Service().Download(l.Key, "srv-a", "ip").Status);
        Assert.Equal(200, Service().Download(l.Key, "srv-a", "ip").Status); // тот же сервер — не считается дважды

        var second = Service().Download(l.Key, "srv-b", "ip");
        Assert.Equal(403, second.Status);
        Assert.Contains("1 из 1", second.Body);

        _store.RemoveActivations(l.Id, "srv-a");
        Assert.Equal(200, Service().Download(l.Key, "srv-b", "ip").Status);
    }

    [Fact]
    public void Reusing_a_server_id_from_another_ip_is_refused_until_unbound()
    {
        var l = Issue();
        Assert.Equal(200, Service().Verify(l.Key, "srv-a", "1.2.3.4", "1.0", 100).Status);

        var moved = Service().Verify(l.Key, "srv-a", "5.6.7.8", "1.0", 100);
        Assert.Equal(403, moved.Status);
        Assert.Contains("другому IP", moved.Body);

        _store.RemoveActivations(l.Id, "srv-a");
        Assert.Equal(200, Service().Verify(l.Key, "srv-a", "5.6.7.8", "1.0", 100).Status);
    }

    [Fact]
    public void Configured_slots_cannot_exceed_the_license_limit()
    {
        var l = Issue();
        var reply = Service().Verify(l.Key, "srv", "ip", "1.0", 501);
        Assert.Equal(403, reply.Status);
        Assert.Contains("не более 500", reply.Body);
        Assert.Empty(_store.Acts);
    }

    [Fact]
    public async Task Parallel_first_activations_cannot_overrun_the_server_limit()
    {
        var l = Issue(servers: 1);
        var service = Service();
        var gate = new ManualResetEventSlim(false);
        var calls = Enumerable.Range(0, 16).Select(i => Task.Run(() =>
        {
            gate.Wait();
            return service.Verify(l.Key, $"srv-{i}", $"10.0.0.{i + 1}", "1.0", 100).Status;
        })).ToArray();

        gate.Set();
        var statuses = await Task.WhenAll(calls);
        Assert.Equal(1, statuses.Count(s => s == 200));
        Assert.Equal(15, statuses.Count(s => s == 403));
        Assert.Single(_store.Acts);
    }

    [Theory]
    [InlineData(AuthorityStatus.Suspended, "приостановлен")]
    [InlineData(AuthorityStatus.Revoked, "отозван")]
    public void Suspended_or_revoked_key_is_refused_with_the_reason(string status, string word)
    {
        var l = Issue();
        l.Status = status;
        l.StatusReason = "неоплата";
        var reply = Service().Verify(l.Key, "srv", "ip", "1.0.0", 100);
        Assert.Equal(403, reply.Status);
        Assert.Contains(word, reply.Body);
        Assert.Contains("неоплата", reply.Body);
    }

    [Fact]
    public void Expired_and_unknown_keys_are_refused()
    {
        var l = Issue(days: 10);
        _now = _now.AddDays(11);
        Assert.Equal(403, Service().Verify(l.Key, "srv", "ip", "", 0).Status);
        Assert.Equal(403, Service().Verify(LicenseService.NewKey(), "srv", "ip", "", 0).Status);
        Assert.Equal(400, Service().Verify("not-a-key", "srv", "ip", "", 0).Status);
    }

    [Fact]
    public async Task Server_verifier_accepts_the_lease_and_gets_the_refreshed_file()
    {
        var l = Issue();
        var service = Service();
        using var http = new HttpClient(new Handler(req =>
        {
            var body = req.Content!.ReadAsStringAsync().Result;
            Assert.Contains("\"serverId\":\"srv-a\"", body);
            var r = service.Verify(l.Key, "srv-a", "1.2.3.4", "1.0.0", 500);
            return new HttpResponseMessage((HttpStatusCode)r.Status) { Content = new StringContent(r.Body, Encoding.UTF8, "application/json") };
        }));
        var config = new LicenseConfig { LicenseKey = l.Key, LicenseVerifyUrl = "http://authority.invalid/api/v1/license/verify", ServerId = "srv-a" };
        var result = await new LicenseRemoteVerifier(http, Pub).VerifyAsync(config, slots: 500);

        Assert.True(result.Valid);
        Assert.False(result.Revoked);
        Assert.Equal(_now + LicenseService.LeaseLifetime, result.LeaseUntilUtc);
        Assert.Equal(CoreState.Valid, LicenseFile.EvaluateContent(result.LicenseFlv!, _now, l.Key, Pub).State);

        // Продление: следующий ответ несёт файл с новым сроком.
        l.ExpiresAt = _now.AddDays(1000);
        var renewed = await new LicenseRemoteVerifier(http, Pub).VerifyAsync(config, slots: 500);
        Assert.Contains(_now.AddDays(1000).ToString("dd.MM.yyyy"), LicenseFile.EvaluateContent(renewed.LicenseFlv!, _now, l.Key, Pub).Message);
    }

    [Fact]
    public async Task Lease_cannot_be_replayed_by_another_server()
    {
        var l = Issue(servers: 2);
        var service = Service();
        string? responseBody = null;
        using var firstHttp = new HttpClient(new Handler(_ =>
        {
            var r = service.Verify(l.Key, "srv-a", "1.2.3.4", "1.0", 100);
            responseBody = r.Body;
            return new HttpResponseMessage((HttpStatusCode)r.Status) { Content = new StringContent(r.Body) };
        }));
        var first = await new LicenseRemoteVerifier(firstHttp, Pub).VerifyAsync(new LicenseConfig
        {
            LicenseKey = l.Key, LicenseVerifyUrl = "http://authority.invalid/v", ServerId = "srv-a",
        });
        Assert.True(first.Valid);

        using var replayHttp = new HttpClient(new Handler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseBody!),
        }));
        var replay = await new LicenseRemoteVerifier(replayHttp, Pub).VerifyAsync(new LicenseConfig
        {
            LicenseKey = l.Key, LicenseVerifyUrl = "http://authority.invalid/v", ServerId = "srv-b",
        });
        Assert.False(replay.Valid);
        Assert.Contains("другому серверу", replay.Message);
    }

    [Fact]
    public async Task Suspension_reaches_the_server_as_an_immediate_revoke()
    {
        var l = Issue();
        l.Status = AuthorityStatus.Suspended;
        var service = Service();
        using var http = new HttpClient(new Handler(_ =>
        {
            var r = service.Verify(l.Key, "srv", "ip", "", 0);
            return new HttpResponseMessage((HttpStatusCode)r.Status) { Content = new StringContent(r.Body) };
        }));
        var config = new LicenseConfig { LicenseKey = l.Key, LicenseVerifyUrl = "http://authority.invalid/v", ServerId = "srv" };
        var result = await new LicenseRemoteVerifier(http, Pub).VerifyAsync(config);
        Assert.True(result.Revoked);
        Assert.Contains("приостановлен", result.Message);
    }

    [Fact]
    public void File_signed_by_another_key_is_rejected_by_the_server()
    {
        var l = Issue();
        using var other = RSA.Create(2048);
        var forged = new LicenseService(_store, other, () => _now).Download(l.Key, "srv", "ip").Body;
        Assert.Equal(CoreState.Invalid, LicenseFile.EvaluateContent(forged, _now, l.Key, Pub).State);
    }

    [Fact]
    public void Journal_records_activation_and_refusals_without_the_key_itself()
    {
        var l = Issue(servers: 1);
        Service().Download(l.Key, "srv-a", "ip");
        Service().Download(l.Key, "srv-b", "ip");
        Assert.Contains(_store.Log, e => e.Type == "активация");
        Assert.Contains(_store.Log, e => e.Type == "download:лимит");
        Assert.DoesNotContain(_store.Log, e => e.Key.Contains(l.Key));
    }

    [Fact]
    public void Server_identity_is_stable_and_depends_on_machine_and_folder()
    {
        var a = ServerIdentity.Compute("machine-1", @"C:\FloVMP");
        Assert.Equal(a, ServerIdentity.Compute("MACHINE-1", @"c:\flovmp\"));
        Assert.NotEqual(a, ServerIdentity.Compute("machine-2", @"C:\FloVMP"));
        Assert.NotEqual(a, ServerIdentity.Compute("machine-1", @"D:\FloVMP"));
        Assert.Matches("^[0-9a-f]{20}$", a);
    }

    private sealed class Handler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _send;
        public Handler(Func<HttpRequestMessage, HttpResponseMessage> send) => _send = send;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken c) => Task.FromResult(_send(r));
    }
}
