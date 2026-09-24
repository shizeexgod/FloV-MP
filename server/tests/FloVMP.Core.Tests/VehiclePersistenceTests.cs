using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using FloVMP.Core.Native;
using FloVMP.Core.Settings;
using FloVMP.Core.Vehicles;
using Xunit;

namespace FloVMP.Core.Tests;

public class VehiclePersistenceTests
{
    private const uint Adder = 0xB779A091;

    /// <summary>Хранилище в памяти; умеет «падать», как недоступная база.</summary>
    private sealed class MemoryStore : IVehicleStore
    {
        public readonly Dictionary<uint, PersistedVehicle> Rows = new();
        public volatile bool Broken;
        public int Writes;
        public string Describe => "память";
        public IReadOnlyList<PersistedVehicle> LoadAll() { lock (Rows) return Rows.Values.ToList(); }
        public void Upsert(PersistedVehicle v)
        {
            if (Broken) throw new InvalidOperationException("база недоступна");
            lock (Rows) { Rows[v.Id] = v; Writes++; }
        }
        public void Delete(uint id)
        {
            if (Broken) throw new InvalidOperationException("база недоступна");
            lock (Rows) Rows.Remove(id);
        }
        public void Flush() { }
        public int Count { get { lock (Rows) return Rows.Count; } }
    }

    private sealed class Host : IVehicleHost
    {
        public readonly Dictionary<uint, VehiclePlayer> Players = new();
        public readonly List<(uint To, string Line)> Sent = new();
        public bool TryGetPlayer(uint id, out VehiclePlayer p) => Players.TryGetValue(id, out p);
        public void Send(uint playerId, string line) => Sent.Add((playerId, line));
        public void Warn(string message) { }
    }

    private static (NativeVehicleService Svc, Host Host, MemoryStore Store, VehiclePersistence P) Make()
    {
        var host = new Host();
        host.Players[1] = new VehiclePlayer(1, 0, 0, 30, 0, true, true);
        var store = new MemoryStore();
        var p = new VehiclePersistence(store, _ => { });
        var svc = new NativeVehicleService(host);
        svc.AttachPersistence(p);
        return (svc, host, store, p);
    }

    private static PersistedVehicle Row(uint id, float body = 1000, float engine = 1000) =>
        new(id, Adder, "AB 12", 10, 20, 30, 1, 2, 90, 0, true, body, engine);

    // ------------------------------------------------------------ реестр

    [Fact]
    public void Restore_KeepsIdAndState_NewIdsGoAbove()
    {
        var r = new VehicleRegistry();
        var v = r.Restore(Row(41, body: 500), restoreDamage: true, 0)!;
        Assert.Equal(41u, v.Id);
        Assert.True(v.Persistent);
        Assert.True(v.Locked);
        Assert.False(v.EngineOn);            // после рестарта — заглушена
        Assert.Equal(500f, v.BodyHealth);
        Assert.Equal((10f, 20f, 30f, 90f), (v.X, v.Y, v.Z, v.Rz));
        Assert.Equal(42u, r.Create(Adder, 0, 0, 0, 0, 0, 0, 0, 0)!.Id);
    }

    [Fact]
    public void Restore_WithoutDamage_StandsRepaired_AndSkipsDuplicateId()
    {
        var r = new VehicleRegistry();
        var v = r.Restore(Row(5, body: 100, engine: -4000), restoreDamage: false, 0)!;
        Assert.Equal(1000f, v.BodyHealth);
        Assert.False(v.Destroyed);
        Assert.Null(r.Restore(Row(5), true, 0));
    }

    [Theory]
    [InlineData("99AAA999", "^[0-9]{2}[A-Z]{3}[0-9]{3}$")]
    [InlineData("RP 9999", "^RP [0-9]{4}$")]
    [InlineData("a999aa", "^[A-Z][0-9]{3}[A-Z]{2}$")]
    public void PlateFormat_DrivesRandomPlates(string format, string pattern)
    {
        var r = new VehicleRegistry { PlateFormat = format };
        for (var i = 0; i < 20; i++) Assert.Matches(pattern, r.Create(Adder, 0, 0, 0, 0, 0, 0, 0, 0)!.Plate);
    }

    [Theory]
    [InlineData("")]
    [InlineData("TOO LONG1")]
    [InlineData("AA-999")]
    public void PlateFormat_InvalidFallsBackToDefault(string format)
    {
        var r = new VehicleRegistry { PlateFormat = format };
        Assert.Equal(VehicleRegistry.DefaultPlateFormat, r.PlateFormat);
    }

    // ------------------------------------------------------------ хранилища

    [Fact]
    public void JsonStore_RoundTripAndDelete()
    {
        var dir = Path.Combine(Path.GetTempPath(), "flovmp-veh-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(dir, "vehicles.json");
        try
        {
            using (var s = new JsonVehicleStore(path))
            {
                s.Upsert(Row(1));
                s.Upsert(Row(2));
                s.Delete(1);
            }
            using var again = new JsonVehicleStore(path);
            var rows = again.LoadAll();
            Assert.Equal(new[] { 2u }, rows.Select(r => r.Id));
            Assert.Equal(Row(2), rows[0]);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void JsonStore_CorruptFileIsSetAsideNotOverwritten()
    {
        var dir = Path.Combine(Path.GetTempPath(), "flovmp-veh-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "vehicles.json");
        try
        {
            File.WriteAllText(path, "{ не json");
            using var s = new JsonVehicleStore(path);
            Assert.Empty(s.LoadAll());
            Assert.Single(Directory.GetFiles(dir, "vehicles.json.corrupt-*"));
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void Persistence_StoreOutage_KeepsLatestAndRetries()
    {
        var store = new MemoryStore { Broken = true };
        using var p = new VehiclePersistence(store, _ => { });
        p.Save(Row(1, body: 900));
        p.Save(Row(2));
        Thread.Sleep(300);
        p.Save(Row(1, body: 800));    // пока база лежит, пришло свежее
        Assert.Equal(0, store.Count);

        store.Broken = false;
        Assert.True(p.FlushBlocking(TimeSpan.FromSeconds(5)));
        Assert.Equal(2, store.Count);
        Assert.Equal(800f, store.Rows[1].BodyHealth);   // ушло последнее, а не устаревшее
    }

    // ------------------------------------------------------------ правила сохранения

    [Fact]
    public void Create_Persistent_IsSavedImmediately_NonPersistentNever()
    {
        var (svc, _, store, p) = Make();
        var kept = svc.Create(Adder, 1, 0, 30, 0, 0, null, persistent: true, 0)!;
        svc.Create(Adder, 2, 0, 30, 0, 0, null, persistent: false, 0);
        p.FlushBlocking(TimeSpan.FromSeconds(5));
        Assert.Equal(new[] { kept.Id }, store.Rows.Keys);
        p.Dispose();
    }

    [Fact]
    public void SaveAll_WritesOnlyChanged()
    {
        var (svc, _, store, p) = Make();
        var v = svc.Create(Adder, 1, 0, 30, 0, 0, null, true, 0)!;
        p.FlushBlocking(TimeSpan.FromSeconds(5));
        Assert.Equal(0, svc.SaveAll());       // ничего не менялось
        svc.SetLocked(v.Id, true);
        Assert.Equal(1, svc.SaveAll());
        p.FlushBlocking(TimeSpan.FromSeconds(5));
        Assert.True(store.Rows[v.Id].Locked);
        p.Dispose();
    }

    [Fact]
    public void Parking_SavesPositionRightAway()
    {
        var (svc, _, store, p) = Make();
        var v = svc.Create(Adder, 1, 0, 30, 0, 0, null, true, 0)!;
        svc.HandleEnter(1, new[] { "VENTER", v.Id.ToString(), "-1" }, 0);
        svc.HandleSync(1, new NativeVehicleSync(v.Id, 123, 45, 30, 0, 0, 180, 0, 0, 0, true, false, 1000, 1000), DateTime.UtcNow);
        svc.HandleLeave(1, new[] { "VLEAVE", v.Id.ToString() }, 0);
        p.FlushBlocking(TimeSpan.FromSeconds(5));
        Assert.Equal((123f, 45f, 180f), (store.Rows[v.Id].X, store.Rows[v.Id].Y, store.Rows[v.Id].Rz));
        p.Dispose();
    }

    [Fact]
    public void RemoveAndUnmark_DeleteFromStore()
    {
        var (svc, _, store, p) = Make();
        var a = svc.Create(Adder, 1, 0, 30, 0, 0, null, true, 0)!;
        var b = svc.Create(Adder, 2, 0, 30, 0, 0, null, true, 0)!;
        p.FlushBlocking(TimeSpan.FromSeconds(5));
        svc.Remove(a.Id, "command");
        svc.SetPersistent(b.Id, false);
        p.FlushBlocking(TimeSpan.FromSeconds(5));
        Assert.Empty(store.Rows);
        Assert.NotNull(svc.Registry.Get(b.Id));   // в мире осталась, просто не сохраняется
        p.Dispose();
    }

    [Fact]
    public void RestoreSaved_BringsBackAndDoesNotRewriteUnchanged()
    {
        var (svc, _, store, p) = Make();
        store.Rows[7] = Row(7);
        store.Rows[9] = Row(9);
        var (restored, skipped) = svc.RestoreSaved(restoreDamage: true, 0);
        Assert.Equal((2, 0), (restored, skipped));
        var writes = store.Writes;
        Assert.Equal(0, svc.SaveAll());
        Assert.Equal(writes, store.Writes);
        p.Dispose();
    }

    [Fact]
    public void TrafficRegistration_CanBeDisabledByOwner()
    {
        var (svc, host, _, p) = Make();
        svc.AllowTrafficRegistration = false;
        svc.HandleRequest(1, new[] { "VREQ", "3", Adder.ToString() }, 0);
        Assert.Contains(host.Sent, s => s.Line.StartsWith("VREJ\t3\t"));
        Assert.Equal(0, svc.Registry.Count);
        p.Dispose();
    }

    // ------------------------------------------------------------ настройки из кода

    [Fact]
    public void Settings_TrySet_ValidatesLikeFile()
    {
        var s = new ServerSettings();
        Assert.True(s.TrySet("vehicles.save_interval_sec", "60", out _));
        Assert.Equal(60, s.Int("vehicles.save_interval_sec"));
        Assert.False(s.TrySet("vehicles.save_interval_sec", "1", out var why));
        Assert.Contains("допустимо", why);
        Assert.False(s.TrySet("vehicles.nope", "1", out why));
        Assert.Contains("неизвестный ключ", why);
        Assert.True(s.TrySet("vehicles.register_traffic", "выкл", out _));
        Assert.False(s.Bool("vehicles.register_traffic"));
    }
}
