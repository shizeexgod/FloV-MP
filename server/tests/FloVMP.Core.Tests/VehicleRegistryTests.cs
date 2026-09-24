using System;
using System.Linq;
using FloVMP.Core.Native;
using FloVMP.Core.Vehicles;
using Xunit;

namespace FloVMP.Core.Tests;

public class VehicleRegistryTests
{
    private const uint Adder = 0xB779A091;

    private static NativeVehicleSync Sync(uint id, float x = 0, float body = 1000, float engine = 1000, float vx = 0) =>
        new(id, x, 0, 0, 0, 0, 0, vx, 0, 0, true, false, body, engine);

    [Fact]
    public void Create_GivesUniqueIdsAndGamePlate()
    {
        var r = new VehicleRegistry();
        var a = r.Create(Adder, 0, 0, 0, 0, 0, 0, 0, 0)!;
        var b = r.Create(Adder, 0, 0, 0, 0, 0, 0, 0, 0)!;
        Assert.NotEqual(a.Id, b.Id);
        Assert.Matches("^[0-9]{2}[A-Z]{3}[0-9]{3}$", a.Plate);
        Assert.Equal(1000f, a.BodyHealth);
    }

    [Fact]
    public void Create_RespectsCeiling()
    {
        var r = new VehicleRegistry(maxRegistered: 2);
        Assert.NotNull(r.Create(Adder, 0, 0, 0, 0, 0, 0, 0, 0));
        Assert.NotNull(r.Create(Adder, 0, 0, 0, 0, 0, 0, 0, 0));
        Assert.Null(r.Create(Adder, 0, 0, 0, 0, 0, 0, 0, 0));
    }

    [Fact]
    public void Create_CleansCustomPlate()
    {
        var r = new VehicleRegistry();
        var v = r.Create(Adder, 0, 0, 0, 0, 0, 0, 0, 0, plate: "ab\t12<script>xyz")!;
        Assert.Equal("AB12SCRI", v.Plate);
    }

    [Fact]
    public void RegisterTraffic_MakesPlayerDriver_AndIsRateLimited()
    {
        var r = new VehicleRegistry();
        var (v, refusal) = r.RegisterTraffic(7, Adder, 1, 2, 3, 0, 0, 0, 0, nowMs: 10_000);
        Assert.Null(refusal);
        Assert.Equal(7u, v!.Driver);
        Assert.Equal(VehicleOrigin.Traffic, v.Origin);
        Assert.Equal((v.Id, VehicleRegistry.DriverSeat), r.SeatOf(7));

        var (second, why) = r.RegisterTraffic(7, Adder, 1, 2, 3, 0, 0, 0, 0, nowMs: 11_000);
        Assert.Null(second);
        Assert.Equal("слишком часто", why);

        var (third, _) = r.RegisterTraffic(7, Adder, 1, 2, 3, 0, 0, 0, 0, nowMs: 12_000);
        Assert.NotNull(third);
        // Пересел в новую — из прежней высажен.
        Assert.Equal(0u, v.Driver);
    }

    [Fact]
    public void RegisterTraffic_RespectsCeiling()
    {
        var r = new VehicleRegistry(maxRegistered: 1);
        r.Create(Adder, 0, 0, 0, 0, 0, 0, 0, 0);
        var (v, why) = r.RegisterTraffic(1, Adder, 0, 0, 0, 0, 0, 0, 0, 0);
        Assert.Null(v);
        Assert.Equal("на сервере слишком много машин", why);
    }

    [Fact]
    public void TryEnter_ChecksDistanceDimensionLockAndSeat()
    {
        var r = new VehicleRegistry();
        var v = r.Create(Adder, 100, 100, 30, 0, 0, 0, dimension: 5, nowMs: 0)!;

        Assert.Equal(EnterResult.NoVehicle, r.TryEnter(1, 999, -1, 100, 100, 30, 5, 0));
        Assert.Equal(EnterResult.BadSeat, r.TryEnter(1, v.Id, 16, 100, 100, 30, 5, 0));
        Assert.Equal(EnterResult.OtherDimension, r.TryEnter(1, v.Id, -1, 100, 100, 30, 0, 0));
        Assert.Equal(EnterResult.TooFar, r.TryEnter(1, v.Id, -1, 120, 100, 30, 5, 0));

        r.SetLocked(v.Id, true);
        Assert.Equal(EnterResult.Locked, r.TryEnter(1, v.Id, -1, 101, 100, 30, 5, 0));
        r.SetLocked(v.Id, false);

        Assert.Equal(EnterResult.Ok, r.TryEnter(1, v.Id, -1, 101, 100, 30, 5, 0));
        Assert.Equal(EnterResult.SeatTaken, r.TryEnter(2, v.Id, -1, 101, 100, 30, 5, 0));
        Assert.Equal(EnterResult.Ok, r.TryEnter(2, v.Id, 0, 101, 100, 30, 5, 0));
        Assert.Equal(1u, v.Driver);
        Assert.Equal(2, v.Occupants.Count);
    }

    [Fact]
    public void TryEnter_SeatSwitchInsideLockedVehicleIsAllowed()
    {
        var r = new VehicleRegistry();
        var v = r.Create(Adder, 0, 0, 0, 0, 0, 0, 0, 0)!;
        r.TryEnter(1, v.Id, 0, 0, 0, 0, 0, 0);
        r.SetLocked(v.Id, true);
        Assert.Equal(EnterResult.Ok, r.TryEnter(1, v.Id, -1, 0, 0, 0, 0, 0));
        Assert.Equal(1u, v.Driver);
        Assert.False(v.Occupants.ContainsKey(0));
    }

    [Fact]
    public void Leave_OfDriver_StopsVehicleAndReportsSeatChange()
    {
        var r = new VehicleRegistry();
        var (v, _) = r.RegisterTraffic(3, Adder, 0, 0, 0, 0, 0, 0, 0, 0);
        r.ApplyDriverSync(3, Sync(v!.Id, x: 5, vx: 20));
        r.DrainSeatChanges();

        Assert.True(r.Leave(3, nowMs: 500));
        Assert.Equal(0f, v.Vx);
        Assert.Equal(5f, v.X); // стоит там, где бросили
        Assert.Equal(500, v.LastOccupiedMs);
        Assert.Equal(new[] { new SeatChange(v.Id, -1, 0) }, r.DrainSeatChanges());
        Assert.Equal((0u, 0), r.SeatOf(3));
    }

    [Fact]
    public void DriverSync_HealthOnlyGoesDown()
    {
        var r = new VehicleRegistry();
        var (v, _) = r.RegisterTraffic(3, Adder, 0, 0, 0, 0, 0, 0, 0, 0);
        Assert.Equal(SyncResult.Applied, r.ApplyDriverSync(3, Sync(v!.Id, body: 600, engine: -100)));
        Assert.Equal(600f, v.BodyHealth);
        Assert.Equal(-100f, v.EngineHealth);

        // Изменённый клиент «чинит» машину — сервер не верит.
        r.ApplyDriverSync(3, Sync(v.Id, x: 1, body: 1000, engine: 1000));
        Assert.Equal(600f, v.BodyHealth);
        Assert.Equal(-100f, v.EngineHealth);

        // Ремонт — только командой сервера.
        r.Repair(v.Id);
        Assert.Equal(1000f, v.BodyHealth);
        Assert.Equal(1000f, v.EngineHealth);
    }

    [Fact]
    public void DriverSync_FromNonDriverIsRejected()
    {
        var r = new VehicleRegistry();
        var (v, _) = r.RegisterTraffic(3, Adder, 0, 0, 0, 0, 0, 0, 0, 0);
        r.TryEnter(4, v!.Id, 0, 0, 0, 0, 0, 0);
        Assert.Equal(SyncResult.NotDriver, r.ApplyDriverSync(4, Sync(v.Id, x: 50)));
        Assert.Equal(SyncResult.NoVehicle, r.ApplyDriverSync(3, Sync(999)));
        Assert.Equal(0f, v.X);
    }

    [Fact]
    public void DriverSync_SameValuesDoNotBumpVersion()
    {
        var r = new VehicleRegistry();
        var (v, _) = r.RegisterTraffic(3, Adder, 0, 0, 0, 0, 0, 0, 0, 0);
        r.ApplyDriverSync(3, Sync(v!.Id, x: 1));
        var version = v.Version;
        Assert.Equal(SyncResult.Unchanged, r.ApplyDriverSync(3, Sync(v.Id, x: 1)));
        Assert.Equal(version, v.Version);
    }

    [Fact]
    public void CollectAbandoned_RemovesOnlyEmptyNonPersistentAfterTtl()
    {
        var r = new VehicleRegistry();
        var empty = r.Create(Adder, 0, 0, 0, 0, 0, 0, 0, nowMs: 0)!;
        var kept = r.Create(Adder, 0, 0, 0, 0, 0, 0, 0, nowMs: 0, persistent: true)!;
        var (driven, _) = r.RegisterTraffic(9, Adder, 0, 0, 0, 0, 0, 0, 0, nowMs: 0);

        Assert.Empty(r.CollectAbandoned(nowMs: 299_000, ttlMs: 300_000));
        var removed = r.CollectAbandoned(nowMs: 300_000, ttlMs: 300_000);
        Assert.Equal(new[] { empty.Id }, removed.Select(v => v.Id));
        Assert.NotNull(r.Get(kept.Id));
        Assert.NotNull(r.Get(driven!.Id));

        // Отсчёт — от момента, когда в машине последний раз кто-то был.
        r.Leave(9, nowMs: 400_000);
        Assert.Empty(r.CollectAbandoned(nowMs: 600_000, ttlMs: 300_000));
        Assert.Single(r.CollectAbandoned(nowMs: 700_000, ttlMs: 300_000));
    }

    [Fact]
    public void CollectAbandoned_ZeroTtlMeansNever()
    {
        var r = new VehicleRegistry();
        r.Create(Adder, 0, 0, 0, 0, 0, 0, 0, nowMs: 0);
        Assert.Empty(r.CollectAbandoned(nowMs: long.MaxValue / 2, ttlMs: 0));
    }

    [Fact]
    public void Remove_FreesOccupants()
    {
        var r = new VehicleRegistry();
        var (v, _) = r.RegisterTraffic(3, Adder, 0, 0, 0, 0, 0, 0, 0, 0);
        r.Remove(v!.Id);
        Assert.Equal((0u, 0), r.SeatOf(3));
        Assert.False(r.Leave(3, 0));
    }

    [Fact]
    public void Ids_AreNotReusedImmediately()
    {
        var r = new VehicleRegistry();
        var a = r.Create(Adder, 0, 0, 0, 0, 0, 0, 0, 0)!;
        r.Remove(a.Id);
        var b = r.Create(Adder, 0, 0, 0, 0, 0, 0, 0, 0)!;
        Assert.NotEqual(a.Id, b.Id);
    }
}

public class NativeVehicleProtocolTests
{
    [Theory]
    [InlineData("1.0.6", true)]
    [InlineData("1.0.6-beta", true)]
    [InlineData("1.0.7", true)]
    [InlineData("1.1.0", true)]
    [InlineData("2.0.0-rc1", true)]
    [InlineData("dev", true)]
    [InlineData("DEV", true)]
    [InlineData("1.0.5-beta", false)]
    [InlineData("1.0.4", false)]
    [InlineData("1.0.0", false)]
    [InlineData("bot-1.0", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("1.0", false)]
    public void SupportsRegistry_ComparesMajorMinorPatchOnly(string? version, bool expected) =>
        Assert.Equal(expected, NativeVehicleProtocol.SupportsRegistry(version));

    [Fact]
    public void VSync_ParsesAndClamps()
    {
        var p = NativeProtocol.Parse("VSYNC\t12\t1.5\t-2\t30\t10\t-5\t170\t500\t0\t0\t1\t0\t2000\t-9000");
        Assert.True(NativeVehicleSync.TryParse(p, out var s));
        Assert.Equal(12u, s.VehicleId);
        Assert.Equal(1.5f, s.X);
        Assert.Equal(300f, s.Vx);
        Assert.True(s.EngineOn);
        Assert.False(s.SirenOn);
        Assert.Equal(1000f, s.BodyHealth);
        Assert.Equal(-4000f, s.EngineHealth);
    }

    [Theory]
    [InlineData("VSYNC\t0\t1\t2\t3\t0\t0\t0\t0\t0\t0\t1\t0\t1000\t1000")]        // нет ID
    [InlineData("VSYNC\t5\t99999\t2\t3\t0\t0\t0\t0\t0\t0\t1\t0\t1000\t1000")]   // за картой
    [InlineData("VSYNC\t5\tNaN\t2\t3\t0\t0\t0\t0\t0\t0\t1\t0\t1000\t1000")]
    [InlineData("VSYNC\t5\t1\t2\t3\t0\t0\t0\t0\t0\t0\t1\t0")]                  // без здоровья
    [InlineData("VSYNC\t5\t1\t2\t3\t0\t0\t0\t0\t0\t0\t1\t0\tx\t1000")]
    public void VSync_RejectsGarbage(string line) =>
        Assert.False(NativeVehicleSync.TryParse(NativeProtocol.Parse(line), out _));

    [Fact]
    public void Formats_FollowAgreedFieldOrder()
    {
        var v = new RegisteredVehicleView(3, 0xB779A091, 1, 2, 3, 4, 5, 6, 7, 8, 9, 0, true, false, true, 900, 800, "12ABC345");
        Assert.Equal("VADD\t3\t3078201489\t1\t2\t3\t4\t5\t6\t0\t5\t12ABC345", NativeVehicleProtocol.FormatAdd(v));
        Assert.Equal("VSTATE\t3\t1\t2\t3\t4\t5\t6\t7\t8\t9\t1\t0\t1\t900\t800", NativeVehicleProtocol.FormatState(v));
        Assert.Equal("VSET\t3\t1\t0\t1\t900\t800", NativeVehicleProtocol.FormatSet(v));
        Assert.Equal("VOWN\t3\t7\t-1", NativeVehicleProtocol.FormatOwner(3, 7, -1));
    }
}
