using System.Diagnostics;
using FloVMP.Core.Items;
using Xunit;

namespace FloVMP.Core.Tests;

/// <summary>
/// Отложенная запись инвентарей.
///
/// Раньше каждый Save() сериализовал словарь целиком (инвентари ВСЕХ
/// аккаунтов) и переписывал весь файл. Автосейв зовёт Save() на каждого
/// игрока онлайн — то есть делал N полных перезаписей файла, в котором лежат
/// все N инвентарей. Это O(n^2) прямо в игровом тике.
///
/// На боевом сервере это выглядело как
///   [Warning] resourceManager.Update() took: 240988 ms
/// — сервер замер на четыре минуты.
///
/// Эти тесты закрепляют оба обещания сразу: быстро И без потери данных.
/// </summary>
public class InventoryStorePerfTests : IDisposable
{
    private readonly string _dir;
    private readonly string _path;

    public InventoryStorePerfTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "flovmp-inv-perf", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _path = Path.Combine(_dir, "inventories.json");
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { /* временный каталог */ }
    }

    private static Inventory Filled(int items = 5)
    {
        var inv = new Inventory(slotCount: 24, maxWeight: 40);
        inv.Add("water", items);
        inv.Add("phone", 1);
        return inv;
    }

    [Fact]
    public void ManySaves_DoNotRewriteFileEachTime()
    {
        // Имитация автосейва при большом онлайне: 500 сохранений подряд.
        // Со старым поведением это 500 полных перезаписей растущего файла.
        var store = new JsonInventoryStore(_path);

        var sw = Stopwatch.StartNew();
        for (var i = 0; i < 500; i++) store.Save(i, Filled());
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 2000,
            $"500 сохранений заняли {sw.ElapsedMilliseconds} мс — похоже, вернулась " +
            "полная перезапись файла на каждое сохранение");

        store.Dispose();
    }

    [Fact]
    public void Flush_PersistsEverything()
    {
        var store = new JsonInventoryStore(_path);
        for (var i = 1; i <= 50; i++) store.Save(i, Filled(i % 7 + 1));
        store.Flush();

        var reopened = new JsonInventoryStore(_path);
        for (var i = 1; i <= 50; i++)
            Assert.Equal(i % 7 + 1, reopened.Load(i).CountOf("water"));

        reopened.Dispose();
        store.Dispose();
    }

    [Fact]
    public void Dispose_FlushesWithoutExplicitCall()
    {
        // Остановка сервера обязана донести инвентари до диска.
        var store = new JsonInventoryStore(_path);
        store.Save(42, Filled(9));
        store.Dispose();

        var reopened = new JsonInventoryStore(_path);
        Assert.Equal(9, reopened.Load(42).CountOf("water"));
        reopened.Dispose();
    }

    [Fact]
    public void Save_IsVisibleInMemoryImmediately()
    {
        // Отложенной должна быть только ЗАПИСЬ НА ДИСК. Чтение из того же
        // экземпляра обязано видеть свежие данные сразу, иначе игрок положил
        // предмет, открыл инвентарь и не увидел его.
        var store = new JsonInventoryStore(_path);
        store.Save(5, Filled(3));

        Assert.Equal(3, store.Load(5).CountOf("water"));
        store.Dispose();
    }

    [Fact]
    public void LastWriteWins_ForSameAccount()
    {
        var store = new JsonInventoryStore(_path);
        store.Save(1, Filled(2));
        store.Save(1, Filled(8));
        store.Flush();

        var reopened = new JsonInventoryStore(_path);
        Assert.Equal(8, reopened.Load(1).CountOf("water"));
        reopened.Dispose();
        store.Dispose();
    }

    [Fact]
    public void DoubleDispose_IsSafe()
    {
        var store = new JsonInventoryStore(_path);
        store.Save(1, Filled());
        store.Dispose();
        store.Dispose(); // остановка сервера может позвать Dispose дважды
    }

    [Fact]
    public void UnknownAccount_ReturnsEmptyInventory()
    {
        var store = new JsonInventoryStore(_path);
        Assert.Equal(0, store.Load(999).CountOf("water"));
        store.Dispose();
    }
}
