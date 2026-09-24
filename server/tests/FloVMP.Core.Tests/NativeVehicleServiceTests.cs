using System;
using System.Collections.Generic;
using System.Linq;
using FloVMP.Core.Native;
using FloVMP.Core.Vehicles;
using Xunit;

namespace FloVMP.Core.Tests;

public class NativeVehicleServiceTests
{
    private const uint Adder = 0xB779A091;

    private sealed class Host : IVehicleHost
    {
        public readonly Dictionary<uint, VehiclePlayer> Players = new();
        public readonly List<(uint To, string Line)> Sent = new();
        public readonly List<string> Warnings = new();

        public void Add(uint id, float x, float y, bool registry = true, int dim = 0) =>
            Players[id] = new VehiclePlayer(id, x, y, 30, dim, registry, true);

        public bool TryGetPlayer(uint id, out VehiclePlayer player) => Players.TryGetValue(id, out player);
        public void Send(uint playerId, string line) => Sent.Add((playerId, line));
        public void Warn(string message) => Warnings.Add(message);

        public List<string[]> To(uint id) => Sent.Where(s => s.To == id).Select(s => NativeProtocol.Parse(s.Line)).ToList();
        public List<string[]> To(uint id, string type) => To(id).Where(p => p[0] == type).ToList();
    }

    private static (NativeVehicleService Svc, Host Host) Make(int max = 1000)
    {
        var host = new Host();
        return (new NativeVehicleService(host, new VehicleRegistry(max)), host);
    }

    private static void Replicate(NativeVehicleService svc, Host host, long now = 0, long ttl = 300_000) =>
        svc.Replicate(now, host.Players.Values.ToList(), radius: 400, maxStreamed: 150, abandonedTtlMs: ttl);

    private static NativeVehicleSync Sync(uint id, float x, float y = 0, float body = 1000) =>
        new(id, x, y, 30, 0, 0, 90, 10, 0, 0, true, false, body, 1000);

    [Fact]
    public void Request_RegistersAndAnswersWithVReg_WithoutVAddToDriver()
    {
        var (svc, host) = Make();
        host.Add(1, 0, 0);
        svc.HandleRequest(1, new[] { "VREQ", "77", Adder.ToString() }, 0);

        var reg = Assert.Single(host.To(1, "VREG"));
        Assert.Equal("77", reg[1]);
        var id = reg[2];
        Assert.Contains(host.To(1, "VOWN"), p => p[1] == id && p[2] == "1" && p[3] == "-1");

        host.Sent.Clear();
        Replicate(svc, host);
        Assert.Empty(host.To(1, "VADD")); // машина уже есть в игре водителя
    }

    [Fact]
    public void Request_TooOftenAnswersVRej()
    {
        var (svc, host) = Make();
        host.Add(1, 0, 0);
        svc.HandleRequest(1, new[] { "VREQ", "1", Adder.ToString() }, 0);
        svc.HandleRequest(1, new[] { "VREQ", "2", Adder.ToString() }, 500);
        var rej = Assert.Single(host.To(1, "VREJ"));
        Assert.Equal("2", rej[1]);
    }

    [Fact]
    public void Request_FromOldClientIsIgnored()
    {
        var (svc, host) = Make();
        host.Add(1, 0, 0, registry: false);
        svc.HandleRequest(1, new[] { "VREQ", "1", Adder.ToString() }, 0);
        Assert.Empty(host.Sent);
        Assert.Equal(0, svc.Registry.Count);
    }

    [Fact]
    public void StreamIn_SendsFullSnapshot_StreamOutSendsVDel_ReturnSendsSnapshotAgain()
    {
        var (svc, host) = Make();
        host.Add(1, 0, 0);
        host.Add(2, 50, 0);
        svc.HandleRequest(1, new[] { "VREQ", "1", Adder.ToString() }, 0);
        var id = uint.Parse(host.To(1, "VREG")[0][2]);

        host.Sent.Clear();
        Replicate(svc, host);
        var toB = host.To(2).Select(p => p[0]).ToList();
        Assert.Equal(new[] { "VADD", "VSTATE", "VOWN" }, toB);
        Assert.Equal("1", host.To(2, "VOWN")[0][2]);

        // Второй уехал за радиус — VDEL.
        host.Add(2, 5000, 0);
        host.Sent.Clear();
        Replicate(svc, host);
        Assert.Equal(id.ToString(), Assert.Single(host.To(2, "VDEL"))[1]);

        // Вернулся — снова полный снимок, а не дельта.
        host.Add(2, 50, 0);
        host.Sent.Clear();
        Replicate(svc, host);
        Assert.Equal(new[] { "VADD", "VSTATE", "VOWN" }, host.To(2).Select(p => p[0]));
    }

    [Fact]
    public void Changes_GoAsVStateToOthers_ButNotToDriver()
    {
        var (svc, host) = Make();
        host.Add(1, 0, 0);
        host.Add(2, 10, 0);
        svc.HandleRequest(1, new[] { "VREQ", "1", Adder.ToString() }, 0);
        var id = uint.Parse(host.To(1, "VREG")[0][2]);
        Replicate(svc, host);

        host.Sent.Clear();
        svc.HandleSync(1, Sync(id, x: 3), DateTime.UtcNow);
        Replicate(svc, host);
        var st = Assert.Single(host.To(2, "VSTATE"));
        Assert.Equal("3", st[2]);
        Assert.Empty(host.To(1, "VSTATE"));

        // Ничего не изменилось — ничего не шлём.
        host.Sent.Clear();
        Replicate(svc, host);
        Assert.Empty(host.Sent);
    }

    [Fact]
    public void Sync_FromNonDriverIsDroppedAndLogged()
    {
        var (svc, host) = Make();
        host.Add(1, 0, 0);
        host.Add(2, 1, 0);
        svc.HandleRequest(1, new[] { "VREQ", "1", Adder.ToString() }, 0);
        var id = uint.Parse(host.To(1, "VREG")[0][2]);

        svc.HandleSync(2, Sync(id, x: 200), DateTime.UtcNow);
        Assert.Equal(0f, svc.Registry.Get(id)!.X);
        Assert.Contains(host.Warnings, w => w.Contains("Античит") && w.Contains("VSYNC"));
    }

    [Fact]
    public void Sync_HealthUpFromClientIsIgnored_RepairGoesToDriverAsVSet()
    {
        var (svc, host) = Make();
        host.Add(1, 0, 0);
        svc.HandleRequest(1, new[] { "VREQ", "1", Adder.ToString() }, 0);
        var id = uint.Parse(host.To(1, "VREG")[0][2]);

        svc.HandleSync(1, Sync(id, x: 1, body: 400), DateTime.UtcNow);
        svc.HandleSync(1, Sync(id, x: 2, body: 1000), DateTime.UtcNow);
        Assert.Equal(400f, svc.Registry.Get(id)!.BodyHealth);

        host.Sent.Clear();
        Assert.True(svc.Repair(id));
        var set = Assert.Single(host.To(1, "VSET"));
        Assert.Equal("1000", set[5]);
    }

    [Fact]
    public void Enter_RefusedGetsTruthAsVOwn_OnlyToRequester()
    {
        var (svc, host) = Make();
        host.Add(1, 0, 0);
        host.Add(2, 2, 0);
        host.Add(3, 3, 0);
        svc.HandleRequest(1, new[] { "VREQ", "1", Adder.ToString() }, 0);
        var id = host.To(1, "VREG")[0][2];
        Replicate(svc, host);

        host.Sent.Clear();
        svc.HandleEnter(2, new[] { "VENTER", id, "-1" }, 0); // место водителя занято
        var own = Assert.Single(host.To(2, "VOWN"));
        Assert.Equal(new[] { "VOWN", id, "1", "-1" }, own);
        Assert.Empty(host.To(3));

        // Пассажиром — можно; VOWN видят все, кому машина показана.
        host.Sent.Clear();
        svc.HandleEnter(2, new[] { "VENTER", id, "0" }, 0);
        foreach (var who in new uint[] { 1, 2, 3 })
            Assert.Contains(host.To(who, "VOWN"), p => p[2] == "2" && p[3] == "0");
    }

    [Fact]
    public void Enter_LockedVehicleIsRefused()
    {
        var (svc, host) = Make();
        host.Add(1, 0, 0);
        var v = svc.SpawnForPlayer(1, Adder, 0, 0)!;
        svc.HandleLeave(1, new[] { "VLEAVE", v.Id.ToString() }, 0);
        svc.SetLocked(v.Id, true);
        host.Sent.Clear();
        svc.HandleEnter(1, new[] { "VENTER", v.Id.ToString(), "-1" }, 0);
        Assert.Equal(new[] { "VOWN", v.Id.ToString(), "0", "-1" }, Assert.Single(host.To(1, "VOWN")));
        Assert.Equal(0u, v.Driver);
    }

    [Fact]
    public void DriverLeaves_VehicleStaysForOthersAndStops_NextDriverTakesOver()
    {
        var (svc, host) = Make();
        host.Add(1, 0, 0);
        host.Add(2, 5, 0);
        svc.HandleRequest(1, new[] { "VREQ", "1", Adder.ToString() }, 0);
        var id = uint.Parse(host.To(1, "VREG")[0][2]);
        svc.HandleSync(1, Sync(id, x: 4), DateTime.UtcNow);
        Replicate(svc, host);

        host.Sent.Clear();
        svc.HandleLeave(1, new[] { "VLEAVE", id.ToString() }, 1000);
        Replicate(svc, host);
        Assert.Contains(host.To(2, "VOWN"), p => p[2] == "0" && p[3] == "-1");
        var stop = Assert.Single(host.To(2, "VSTATE"));
        Assert.Equal("0", stop[8]); // скорость обнулена
        Assert.Empty(host.To(2, "VDEL")); // машина осталась

        svc.HandleEnter(2, new[] { "VENTER", id.ToString(), "-1" }, 2000);
        Assert.Equal(2u, svc.Registry.Get(id)!.Driver);
        svc.HandleSync(2, Sync(id, x: 6), DateTime.UtcNow);
        Assert.Equal(6f, svc.Registry.Get(id)!.X);
    }

    [Fact]
    public void ServerVehicle_StaysAfterExit_NoMatterHowLong()
    {
        // Вышел из своей машины (/car) — она стоит, пока её не уберут командой.
        var (svc, host) = Make();
        host.Add(1, 0, 0);
        host.Add(2, 5, 0);
        var v = svc.SpawnForPlayer(1, Adder, 0, 0)!;
        Replicate(svc, host, now: 0);
        svc.HandleLeave(1, new[] { "VLEAVE", v.Id.ToString() }, 1_000);
        host.Players.Clear(); // все разошлись
        host.Sent.Clear();
        Replicate(svc, host, now: 100_000_000);
        Assert.NotNull(svc.Registry.Get(v.Id));
    }

    private static uint TrafficCar(NativeVehicleService svc, Host host, uint driver)
    {
        svc.HandleRequest(driver, new[] { "VREQ", "1", Adder.ToString() }, 0);
        return uint.Parse(host.To(driver, "VREG")[0][2]);
    }

    [Fact]
    public void AbandonedTraffic_StaysWhileAnyoneIsNear_EvenOldClient()
    {
        var (svc, host) = Make();
        host.Add(1, 0, 0);
        host.Add(3, 50, 0, registry: false); // старый клиент рядом
        var id = TrafficCar(svc, host, 1);
        svc.HandleLeave(1, new[] { "VLEAVE", id.ToString() }, 1_000);
        host.Players.Remove(1);
        for (long t = 0; t <= 1_000_000; t += 100_000) Replicate(svc, host, now: t);
        Assert.NotNull(svc.Registry.Get(id));
    }

    [Fact]
    public void AbandonedTraffic_IsRemovedWhenNobodyNearForTtl_WithVDel()
    {
        var (svc, host) = Make();
        host.Add(1, 0, 0);
        host.Add(2, 5000, 0);
        var id = TrafficCar(svc, host, 1);
        svc.HandleLeave(1, new[] { "VLEAVE", id.ToString() }, 1_000);
        Replicate(svc, host, now: 2_000);          // водитель ещё рядом
        host.Add(1, 5000, 10);                       // ушёл далеко
        host.Sent.Clear();
        Replicate(svc, host, now: 301_999);
        Assert.NotNull(svc.Registry.Get(id));
        Replicate(svc, host, now: 302_000);
        Assert.Null(svc.Registry.Get(id));
    }

    [Fact]
    public void SpawnForPlayer_SendsSnapshotAndSeatsDriverImmediately()
    {
        var (svc, host) = Make();
        host.Add(1, 0, 0);
        var v = svc.SpawnForPlayer(1, Adder, 45, 0)!;
        var types = host.To(1).Select(p => p[0]).ToList();
        Assert.Equal(new[] { "VADD", "VSTATE", "VOWN" }, types);
        Assert.Equal(new[] { "VOWN", v.Id.ToString(), "1", "-1" }, host.To(1, "VOWN")[0]);
        Assert.True(v.EngineOn);
    }

    [Fact]
    public void OtherDimension_DoesNotSeeVehicle()
    {
        var (svc, host) = Make();
        host.Add(1, 0, 0);
        host.Add(2, 5, 0, dim: 7);
        svc.SpawnForPlayer(1, Adder, 0, 0);
        host.Sent.Clear();
        Replicate(svc, host);
        Assert.Empty(host.To(2));
    }

    [Fact]
    public void PlayerLeft_FreesSeatForEveryone()
    {
        var (svc, host) = Make();
        host.Add(1, 0, 0);
        host.Add(2, 5, 0);
        var v = svc.SpawnForPlayer(1, Adder, 0, 0)!;
        Replicate(svc, host);
        host.Players.Remove(1);
        host.Sent.Clear();
        svc.PlayerLeft(1, 0);
        Assert.Contains(host.To(2, "VOWN"), p => p[1] == v.Id.ToString() && p[2] == "0");
        Assert.Equal(0u, v.Driver);
    }

    [Fact]
    public void LegacyFields_OnlyWhileDriverPresent()
    {
        var (svc, host) = Make();
        host.Add(1, 0, 0);
        host.Add(2, 1, 0);
        var v = svc.SpawnForPlayer(1, Adder, 0, 0)!;
        svc.HandleEnter(2, new[] { "VENTER", v.Id.ToString(), "0" }, 0);

        Assert.True(svc.TryLegacyFields(2, out var model, out var driver, out var seat, out _, out _, out _));
        Assert.Equal((Adder, 1, 0), (model, driver, seat));

        svc.HandleLeave(1, new[] { "VLEAVE", v.Id.ToString() }, 0);
        Assert.False(svc.TryLegacyFields(2, out _, out _, out _, out _, out _, out _));
    }

    [Fact]
    public void ManyVehicles_RespectMaxStreamed_ButOwnVehicleAlwaysVisible()
    {
        var (svc, host) = Make();
        host.Add(1, 0, 0);
        var own = svc.SpawnForPlayer(1, Adder, 0, 0)!;
        // Своя машина отъехала «дальше» остальных — всё равно видна.
        svc.HandleSync(1, Sync(own.Id, x: 390), DateTime.UtcNow);
        host.Add(1, 300, 0);
        for (var i = 0; i < 20; i++) svc.Registry.Create(Adder, 300 + i, 1, 30, 0, 0, 0, 0, 0);
        host.Sent.Clear();
        svc.Replicate(0, host.Players.Values.ToList(), radius: 400, maxStreamed: 5, abandonedTtlMs: 0);
        Assert.DoesNotContain(host.To(1, "VDEL"), p => p[1] == own.Id.ToString());
        Assert.Equal(5, host.To(1, "VADD").Count);
    }

    // ------------------------------------------------------------ 8c: API и события

    private static (NativeVehicleService Svc, Host Host, List<string> Events) MakeWithEvents()
    {
        var (svc, host) = Make();
        var ev = new List<string>();
        svc.PlayerEnteredVehicle += (p, v, seat) => ev.Add($"enter {p} {v} {seat}");
        svc.PlayerLeftVehicle += (p, v, seat) => ev.Add($"leave {p} {v} {seat}");
        svc.Damaged += (v, b, e, d) => ev.Add($"damage {v} {b} {e} {d}");
        svc.Destroyed += v => ev.Add($"destroyed {v}");
        svc.Removed += (v, why) => ev.Add($"removed {v} {why}");
        return (svc, host, ev);
    }

    [Fact]
    public void Api_Create_StandsEmptyAndIsReplicated()
    {
        var (svc, host, _) = MakeWithEvents();
        host.Add(1, 0, 0);
        var v = svc.Create(Adder, 5, 0, 30, 90, 0, "RP 001", persistent: true, 0)!;
        Assert.Equal("RP 001", v.Plate);
        Assert.True(v.Persistent);
        Assert.True(v.IsEmpty);
        Replicate(svc, host);
        Assert.Contains(host.To(1, "VADD"), p => p[1] == v.Id.ToString() && p[11] == "RP 001");
    }

    [Fact]
    public void Events_EnterLeaveAndSeatSwitch()
    {
        var (svc, host, ev) = MakeWithEvents();
        host.Add(1, 0, 0);
        var v = svc.Create(Adder, 1, 0, 30, 0, 0, null, false, 0)!;
        svc.HandleEnter(1, new[] { "VENTER", v.Id.ToString(), "0" }, 0);
        svc.HandleEnter(1, new[] { "VENTER", v.Id.ToString(), "-1" }, 0);
        svc.HandleLeave(1, new[] { "VLEAVE", v.Id.ToString() }, 0);
        Assert.Equal(new[]
        {
            $"enter 1 {v.Id} 0",
            $"leave 1 {v.Id} 0", $"enter 1 {v.Id} -1",   // пересел = вышел + сел
            $"leave 1 {v.Id} -1",
        }, ev);
    }

    [Fact]
    public void Api_PutInto_SendsSnapshotFirst_AndBumpsOccupant()
    {
        var (svc, host, ev) = MakeWithEvents();
        host.Add(1, 0, 0);
        host.Add(2, 3000, 0);   // далеко: машина ему не показана
        var v = svc.Create(Adder, 1, 0, 30, 0, 0, null, false, 0)!;
        svc.HandleEnter(1, new[] { "VENTER", v.Id.ToString(), "-1" }, 0);
        host.Sent.Clear();
        ev.Clear();

        Assert.Null(svc.PutInto(2, v.Id, -1, 0));
        Assert.Equal(new[] { "VADD", "VSTATE", "VOWN", "VOWN" }, host.To(2).Select(p => p[0]).Take(4));
        Assert.Contains(host.To(2, "VOWN"), p => p[2] == "2" && p[3] == "-1");
        // Прежний водитель узнаёт, что место не его, — его клиент выйдет.
        Assert.Contains(host.To(1, "VOWN"), p => p[2] != "1" && p[3] == "-1");
        Assert.Equal(new[] { $"leave 1 {v.Id} -1", $"enter 2 {v.Id} -1" }, ev);
    }

    [Fact]
    public void Api_PutInto_Refusals()
    {
        var (svc, host, _) = MakeWithEvents();
        host.Add(1, 0, 0);
        host.Add(2, 0, 0, registry: false);
        host.Add(3, 0, 0, dim: 5);
        var v = svc.Create(Adder, 1, 0, 30, 0, 0, null, false, 0)!;
        Assert.Equal("игрока нет на сервере", svc.PutInto(9, v.Id, -1, 0));
        Assert.Equal("у игрока клиент старше 1.0.6", svc.PutInto(2, v.Id, -1, 0));
        Assert.Equal("машины нет", svc.PutInto(1, 999, -1, 0));
        Assert.Equal("нет такого места", svc.PutInto(1, v.Id, 16, 0));
        Assert.Equal("машина в другом измерении", svc.PutInto(3, v.Id, -1, 0));
    }

    [Fact]
    public void Api_RemoveFrom_TellsPlayerSeatIsNotTheirs()
    {
        var (svc, host, ev) = MakeWithEvents();
        host.Add(1, 0, 0);
        var v = svc.SpawnForPlayer(1, Adder, 0, 0)!;
        host.Sent.Clear();
        ev.Clear();
        Assert.True(svc.RemoveFrom(1, 0));
        Assert.Contains(host.To(1, "VOWN"), p => p[1] == v.Id.ToString() && p[2] == "0" && p[3] == "-1");
        Assert.Equal(new[] { $"leave 1 {v.Id} -1" }, ev);
        Assert.False(svc.RemoveFrom(1, 0));
    }

    [Fact]
    public void Events_DamageAndDestroyedOnce_RepairRearms()
    {
        var (svc, host, ev) = MakeWithEvents();
        host.Add(1, 0, 0);
        var v = svc.SpawnForPlayer(1, Adder, 0, 0)!;
        ev.Clear();
        svc.HandleSync(1, new NativeVehicleSync(v.Id, 1, 0, 30, 0, 0, 0, 0, 0, 0, true, false, 900, 700), DateTime.UtcNow);
        svc.HandleSync(1, new NativeVehicleSync(v.Id, 2, 0, 30, 0, 0, 0, 0, 0, 0, true, false, 900, -4000), DateTime.UtcNow);
        svc.HandleSync(1, new NativeVehicleSync(v.Id, 3, 0, 30, 0, 0, 0, 0, 0, 0, true, false, 900, -4000), DateTime.UtcNow);
        Assert.Equal(new[] { $"damage {v.Id} 100 300 1", $"damage {v.Id} 0 4700 1", $"destroyed {v.Id}" }, ev);

        svc.Repair(v.Id);
        Assert.False(v.Destroyed);
        ev.Clear();
        svc.HandleSync(1, new NativeVehicleSync(v.Id, 4, 0, 30, 0, 0, 0, 0, 0, 0, true, false, 1000, -4000), DateTime.UtcNow);
        Assert.Contains($"destroyed {v.Id}", ev);
    }

    [Fact]
    public void Events_RemoveReportsOccupantsAndReason()
    {
        var (svc, host, ev) = MakeWithEvents();
        host.Add(1, 0, 0);
        var v = svc.SpawnForPlayer(1, Adder, 0, 0)!;
        ev.Clear();
        svc.Remove(v.Id, "command");
        Assert.Equal(new[] { $"leave 1 {v.Id} -1", $"removed {v.Id} command" }, ev);
    }

    [Fact]
    public void Api_SetPlate_ResendsSnapshotToViewers()
    {
        var (svc, host, _) = MakeWithEvents();
        host.Add(1, 0, 0);
        var v = svc.Create(Adder, 1, 0, 30, 0, 0, null, false, 0)!;
        Replicate(svc, host);
        host.Sent.Clear();
        Assert.True(svc.SetPlate(v.Id, "police 1"));
        Assert.Equal("POLICE 1", Assert.Single(host.To(1, "VADD"))[11]);
    }

    [Fact]
    public void AntiCheat_BlacklistedTrafficModelIsRefusedAndSuspected()
    {
        var (svc, host) = Make();
        host.Add(1, 0, 0);
        var rhino = FloVMP.Core.AntiCheat.GameHash.Joaat("rhino");
        var suspects = new List<string>();
        svc.Suspicious += (p, d) => suspects.Add($"{p}: {d}");
        svc.TrafficModelAllowed = m => m != rhino;
        svc.HandleRequest(1, new[] { "VREQ", "4", rhino.ToString() }, 0);
        Assert.Equal("4", Assert.Single(host.To(1, "VREJ"))[1]);
        Assert.Single(suspects);
        Assert.Equal(0, svc.Registry.Count);
    }

    [Fact]
    public void AntiCheat_ForeignVSyncIsSuspected()
    {
        var (svc, host) = Make();
        host.Add(1, 0, 0);
        host.Add(2, 1, 0);
        var suspects = new List<uint>();
        svc.Suspicious += (p, _) => suspects.Add(p);
        var v = svc.SpawnForPlayer(1, Adder, 0, 0)!;
        svc.HandleSync(2, Sync(v.Id, x: 50), DateTime.UtcNow);
        Assert.Equal(new[] { 2u }, suspects);
    }
}
