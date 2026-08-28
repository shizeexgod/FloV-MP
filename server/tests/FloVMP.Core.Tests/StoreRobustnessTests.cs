using FloVMP.Core.Auth;
using FloVMP.Core.Items;
using Xunit;

namespace FloVMP.Core.Tests;

/// <summary>
/// Устойчивость файловых хранилищ: битый JSON не роняет сервер и не
/// затирается молча — уезжает в карантин, стартуем с пустого.
/// </summary>
public sealed class StoreRobustnessTests : IDisposable
{
    private readonly string _dir;

    public StoreRobustnessTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "flovmp-store-robust", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    [Fact]
    public void Account_store_quarantines_corrupt_file_and_starts_empty()
    {
        var path = Path.Combine(_dir, "accounts.json");
        File.WriteAllText(path, "{ this is not valid json ]");

        var store = new JsonAccountStore(path); // не бросает
        Assert.Null(store.FindByUsername("anyone"));

        Assert.Single(Directory.GetFiles(_dir, "accounts.json.corrupt-*"));

        // новое сохранение создаёт свежий валидный файл
        store.Create("Fresh", "hash");
        Assert.True(File.Exists(path));
        Assert.NotNull(new JsonAccountStore(path).FindByUsername("Fresh"));
    }

    [Fact]
    public void Inventory_store_quarantines_corrupt_file_and_starts_empty()
    {
        var path = Path.Combine(_dir, "inventories.json");
        File.WriteAllText(path, "not json at all");

        var store = new JsonInventoryStore(path); // не бросает
        var inv = store.Load(1);
        Assert.All(inv.Slots, s => Assert.Null(s));

        Assert.Single(Directory.GetFiles(_dir, "inventories.json.corrupt-*"));
    }

    [Fact]
    public void Account_store_survives_valid_reload_after_quarantine()
    {
        var path = Path.Combine(_dir, "accounts.json");
        File.WriteAllText(path, "\0\0\0garbage");
        new JsonAccountStore(path).Create("A", "h");

        var reopened = new JsonAccountStore(path);
        Assert.NotNull(reopened.FindByUsername("A"));
    }
}
