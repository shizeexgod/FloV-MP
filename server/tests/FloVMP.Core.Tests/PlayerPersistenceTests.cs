using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using FloVMP.Core.Database;
using FloVMP.Core.Players;
using Xunit;

namespace FloVMP.Core.Tests;

public class PlayerPersistenceTests
{
    private sealed class MemoryStore : IPlayerStore
    {
        public readonly Dictionary<string, PersistedPlayer> States = new();
        public readonly Dictionary<string, Dictionary<string, string>> Data = new();
        public ManualResetEventSlim? HoldLoad;
        public ManualResetEventSlim? HoldWrites;
        // Первое чтение снимает данные сразу, а отдаёт их, только когда отпустят:
        // так воспроизводится чтение, опоздавшее к следующей загрузке.
        public ManualResetEventSlim? DelayFirstRead;
        private int _reads;
        public string Describe => "память";
        public IReadOnlyList<PersistedPlayer> LoadAllStates() { lock (States) return States.Values.ToList(); }
        public void UpsertState(PersistedPlayer p) { lock (States) States[p.Identity] = p; }
        public IReadOnlyDictionary<string, string> LoadData(string identity)
        {
            if (DelayFirstRead is { } delay && Interlocked.Increment(ref _reads) == 1)
            {
                Dictionary<string, string> snap;
                lock (Data) snap = Data.TryGetValue(identity, out var d0) ? new(d0) : new();
                delay.Wait(5000);
                return snap;
            }
            HoldLoad?.Wait(5000);
            lock (Data) return Data.TryGetValue(identity, out var d) ? new Dictionary<string, string>(d) : new();
        }
        public void SetData(string identity, string key, string? value)
        {
            HoldWrites?.Wait(10000);
            lock (Data)
            {
                if (!Data.TryGetValue(identity, out var d)) Data[identity] = d = new();
                if (value is null) d.Remove(key); else d[key] = value;
            }
        }
        public void Flush() { }
    }

    private static PersistedPlayer Player(string id, float x = 1, int health = 180) =>
        new(id, "Nick_" + id, x, 2, 30, 90, 0, health, 50, 0x705E61F2, new[] { new SavedWeapon(0x1B06D571, 24) });

    private static List<(string, string?)> PumpUntil(PlayerPersistence p, int count)
    {
        var all = new List<(string, string?)>();
        // До 10 с: под нагрузкой всего набора соседние тесты держат потоки пула
        // (HoldLoad/DelayFirstRead), и фоновая загрузка встаёт в очередь — 2 с не хватало.
        for (var i = 0; i < 1000 && all.Count < count; i++) { all.AddRange(p.PumpLoaded()); Thread.Sleep(10); }
        return all;
    }

    [Fact]
    public void States_LoadedAtStart_AndSavedLatestWins()
    {
        var store = new MemoryStore();
        store.States["7"] = Player("7");
        using var p = new PlayerPersistence(store, _ => { });
        Assert.Equal(1, p.LoadStates());
        Assert.Equal(1f, p.SavedState("7")!.X);
        Assert.Null(p.SavedState("8"));

        p.SaveState(Player("7", x: 5));
        p.SaveState(Player("7", x: 9));
        Assert.Equal(9f, p.SavedState("7")!.X);   // сразу в памяти — следующий вход увидит
        p.FlushBlocking(TimeSpan.FromSeconds(5));
        Assert.Equal(9f, store.States["7"].X);
    }

    [Fact]
    public void Data_LoadsInBackground_AndIsReportedOnce()
    {
        var store = new MemoryStore();
        store.Data["7"] = new() { ["inventory"] = "[1,2]" };
        using var p = new PlayerPersistence(store, _ => { });
        p.BeginLoadData("7");
        p.BeginLoadData("7");                         // повторный вызов не грузит второй раз
        var done = PumpUntil(p, 1);
        Assert.Equal(new[] { ("7", (string?)null) }, done);
        Assert.True(p.IsLoaded("7"));
        Assert.Equal("[1,2]", p.GetData("7", "inventory"));
    }

    [Fact]
    public void Data_WriteBeforeLoadFinishes_WinsOverStored_AndDoesNotFakeLoaded()
    {
        var store = new MemoryStore { HoldLoad = new ManualResetEventSlim(false) };
        store.Data["7"] = new() { ["money"] = "100", ["job"] = "taxi" };
        using var p = new PlayerPersistence(store, _ => { });
        p.BeginLoadData("7");
        Assert.Null(p.SetData("7", "money", "250"));
        Assert.False(p.IsLoaded("7"));               // запись не делает данные «загруженными»
        store.HoldLoad.Set();
        PumpUntil(p, 1);
        Assert.Equal("250", p.GetData("7", "money"));
        Assert.Equal("taxi", p.GetData("7", "job"));
    }

    [Fact]
    public void Data_LateLoadAfterLeaveIsDropped()
    {
        var store = new MemoryStore { HoldLoad = new ManualResetEventSlim(false) };
        store.Data["7"] = new() { ["k"] = "v" };
        using var p = new PlayerPersistence(store, _ => { });
        p.BeginLoadData("7");
        p.Unload("7");                               // ушёл, пока грузилось
        store.HoldLoad.Set();
        Thread.Sleep(200);
        Assert.Empty(p.PumpLoaded());
        Assert.False(p.IsLoaded("7"));
        Assert.Null(p.GetData("7", "k"));
    }

    [Fact]
    public void Data_SetAndDeleteReachStore()
    {
        var store = new MemoryStore();
        using var p = new PlayerPersistence(store, _ => { });
        p.BeginLoadData("7");
        PumpUntil(p, 1);
        Assert.Null(p.SetData("7", "a", "1"));
        Assert.Null(p.SetData("7", "b", "2"));
        Assert.Null(p.SetData("7", "a", ""));      // пусто — удалить
        p.FlushBlocking(TimeSpan.FromSeconds(5));
        Assert.Equal(new Dictionary<string, string> { ["b"] = "2" }, store.Data["7"]);
        Assert.Equal(new[] { "b" }, p.DataKeys("7"));
    }

    [Theory]
    [InlineData("", "ключ")]
    [InlineData("с кириллицей", "ключ")]
    [InlineData("has space", "ключ")]
    public void Data_BadKeysRefused(string key, string expect)
    {
        using var p = new PlayerPersistence(new MemoryStore(), _ => { });
        Assert.Contains(expect, p.SetData("7", key, "1"));
    }

    [Fact]
    public void Data_LimitsOnValueSizeAndKeyCount()
    {
        using var p = new PlayerPersistence(new MemoryStore(), _ => { });
        Assert.NotNull(p.SetData("7", "big", new string('x', PlayerPersistence.MaxValueLength + 1)));
        for (var i = 0; i < PlayerPersistence.MaxKeysPerPlayer; i++) Assert.Null(p.SetData("7", "k" + i, "1"));
        Assert.NotNull(p.SetData("7", "one-more", "1"));
        Assert.Null(p.SetData("7", "k0", "2"));     // перезапись существующего — можно
    }

    [Fact]
    public void JsonStore_RoundTripStatesAndData()
    {
        var dir = Path.Combine(Path.GetTempPath(), "flovmp-pl-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(dir, "players.json");
        try
        {
            using (var s = new JsonPlayerStore(path))
            {
                s.UpsertState(Player("7", x: 12));
                s.SetData("7", "inventory", "{\"water\":2}");
                s.SetData("8", "money", "5");
                s.SetData("8", "money", null);
            }
            using var again = new JsonPlayerStore(path);
            var st = Assert.Single(again.LoadAllStates());
            Assert.Equal(12f, st.X);
            Assert.Equal(24, st.Weapons[0].Ammo);
            Assert.Equal("{\"water\":2}", again.LoadData("7")["inventory"]);
            Assert.Empty(again.LoadData("8"));
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void WeaponsJson_ToleratesGarbage()
    {
        Assert.Empty(PlayerWeaponsJson.Parse("не json"));
        Assert.Empty(PlayerWeaponsJson.Parse(null));
        var list = PlayerWeaponsJson.Parse(PlayerWeaponsJson.Format(new[] { new SavedWeapon(5, -1) }));
        Assert.Equal(new SavedWeapon(5, -1), Assert.Single(list));
    }

    [Fact]
    public void Writer_LatestWinsPerKey_AndRetriesAfterFailure()
    {
        var fail = true;
        var written = new List<string>();
        using var w = new LatestWinsWriter<string>("test-writer", "тест", _ => { });
        void Write(string v) { if (fail) throw new InvalidOperationException("нет базы"); lock (written) written.Add(v); }
        w.Enqueue("a", () => Write("a1"));
        w.Enqueue("b", () => Write("b1"));
        Thread.Sleep(200);
        w.Enqueue("a", () => Write("a2"));
        fail = false;
        Assert.True(w.FlushBlocking(TimeSpan.FromSeconds(5)));
        Assert.Equal(new[] { "a2", "b1" }, written.OrderBy(x => x));
    }

    // --- найдено повторным аудитом ---

    [Fact]
    public void FastReconnect_SeesWritesStillQueued()
    {
        // База пишет медленно: последняя запись игрока ещё в очереди, а он уже
        // перезашёл. Без слоя неподтверждённых записей загрузка отдала бы 100,
        // и геймод записал бы 100 поверх 250 (потеря денег).
        var store = new MemoryStore { HoldWrites = new ManualResetEventSlim(false) };
        store.Data["7"] = new() { ["money"] = "100", ["old"] = "x" };
        using var p = new PlayerPersistence(store, _ => { });
        p.BeginLoadData("7");
        PumpUntil(p, 1);
        Assert.Null(p.SetData("7", "money", "250"));
        Assert.Null(p.SetData("7", "old", ""));       // и удаление тоже
        p.Unload("7");                                // вышел

        p.BeginLoadData("7");                         // тут же зашёл снова — база ещё старая
        PumpUntil(p, 1);
        Assert.Equal("250", p.GetData("7", "money"));
        Assert.Null(p.GetData("7", "old"));

        store.HoldWrites.Set();
        Assert.True(p.FlushBlocking(TimeSpan.FromSeconds(5)));
        Assert.Equal("250", store.Data["7"]["money"]);
    }

    [Fact]
    public void DeleteDuringLoad_IsNotResurrectedByStoredValue()
    {
        // Запись в базу задержана: в момент чтения там ещё лежит «done».
        var store = new MemoryStore { HoldLoad = new ManualResetEventSlim(false), HoldWrites = new ManualResetEventSlim(false) };
        store.Data["7"] = new() { ["quest"] = "done" };
        using var p = new PlayerPersistence(store, _ => { });
        p.BeginLoadData("7");
        Assert.Null(p.SetData("7", "quest", ""));    // геймод удалил, пока шла загрузка
        store.HoldLoad.Set();
        PumpUntil(p, 1);
        Assert.Null(p.GetData("7", "quest"));
        store.HoldWrites.Set();
    }

    [Fact]
    public void StaleReadFromPreviousLoad_IsIgnored()
    {
        // Второй проход аудита: вошёл — чтение №1 сняло «100» и задержалось;
        // геймод записал 250, база подтвердила; вышел и тут же зашёл, чтение №2
        // отдало 250. Опоздавшее чтение №1 принимать нельзя.
        var delay = new ManualResetEventSlim(false);
        var store = new MemoryStore { DelayFirstRead = delay };
        store.Data["7"] = new() { ["money"] = "100" };
        using var p = new PlayerPersistence(store, _ => { });
        p.BeginLoadData("7");
        Thread.Sleep(100);                              // чтение №1 уже сняло 100 и ждёт
        Assert.Null(p.SetData("7", "money", "250"));
        Assert.True(p.FlushBlocking(TimeSpan.FromSeconds(5)));   // база подтвердила 250
        p.Unload("7");
        store.HoldLoad = new ManualResetEventSlim(false);
        p.BeginLoadData("7");                           // чтение №2 висит
        delay.Set();                                    // опоздавшее №1 приходит первым
        Thread.Sleep(200);
        p.PumpLoaded();
        Assert.False(p.IsLoaded("7"));                  // его не приняли
        store.HoldLoad.Set();                           // теперь №2
        PumpUntil(p, 1);
        Assert.Equal("250", p.GetData("7", "money"));
    }

    [Fact]
    public void Writer_OneAlwaysFailingWriteDoesNotBlockOthers()
    {
        // Аудит: неудачная запись возвращалась первой в каждую пачку и цикл
        // обрывался — одна «ядовитая» строка (внешний ключ) останавливала все.
        var written = new List<string>();
        using var w = new LatestWinsWriter<string>("test-writer", "тест", _ => { });
        w.Enqueue("poison", () => throw new InvalidOperationException("FK 1451"));
        for (var i = 0; i < 5; i++)
        {
            var key = "car" + i;
            w.Enqueue(key, () => { lock (written) written.Add(key); });
        }
        for (var i = 0; i < 300 && written.Count < 5; i++) Thread.Sleep(10);
        lock (written) Assert.Equal(5, written.Count);
        Assert.Equal(1, w.Pending);                   // ядовитая осталась и будет повторяться
    }
}
