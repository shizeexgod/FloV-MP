using FloVMP.Core.Security;
using Xunit;

namespace FloVMP.Core.Tests;

/// <summary>
/// Долговечность блокировок.
///
/// До появления хранилища MultiTierBanService держал баны только в памяти:
/// рестарт сервера снимал все блокировки разом, а при нескольких инстансах
/// забаненный на одном спокойно заходил на другой. Для платформы, где бан по
/// HWID/MAC/IP заявлен как штатная возможность, это дыра, а не мелочь.
/// </summary>
public class BanPersistenceTests : IDisposable
{
    private readonly string _dir;
    private readonly string _path;

    public BanPersistenceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "flovmp-bans", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _path = Path.Combine(_dir, "bans.json");
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { /* временный каталог */ }
    }

    private static BanRecord Ban(MultiTierBanService svc, string name = "Читер", string ip = "10.0.0.5") =>
        svc.CreateBan(42, name, ip, "sc-123", "hwid-abc", "AA:BB:CC:DD:EE:FF",
                      BanTier.HardBan, "Админ", "чит", durationDays: 0);

    [Fact]
    public void Ban_SurvivesRestart()
    {
        var store = new JsonBanStore(_path);
        var svc = new MultiTierBanService(store);
        var created = Ban(svc);
        store.Flush();
        store.Dispose();

        // «Перезапуск сервера»: новое хранилище, новый сервис, тот же файл.
        var store2 = new JsonBanStore(_path);
        var svc2 = new MultiTierBanService(store2);

        var found = svc2.GetAllBans().SingleOrDefault(b => b.Id == created.Id);
        Assert.NotNull(found);
        Assert.Equal("Читер", found!.Username);
        Assert.Equal("hwid-abc", found.HwidHash);
        Assert.True(found.IsActive);
        store2.Dispose();
    }

    [Fact]
    public void BannedPlayer_StillBlockedAfterRestart()
    {
        // Смысл всей правки: после рестарта забаненный не должен зайти.
        var store = new JsonBanStore(_path);
        var svc = new MultiTierBanService(store);
        Ban(svc);
        store.Flush();
        store.Dispose();

        var store2 = new JsonBanStore(_path);
        var svc2 = new MultiTierBanService(store2);

        var check = svc2.CheckConnection(42, "10.0.0.5", "sc-123", "hwid-abc", "AA:BB:CC:DD:EE:FF", HwidPolicyMode.Strict);
        Assert.True(check.IsBlocked);
        store2.Dispose();
    }

    [Fact]
    public void Unban_IsPersistedToo()
    {
        var store = new JsonBanStore(_path);
        var svc = new MultiTierBanService(store);
        var created = Ban(svc);
        Assert.Equal(1, svc.Unban(created.Id));
        store.Flush();
        store.Dispose();

        var store2 = new JsonBanStore(_path);
        var svc2 = new MultiTierBanService(store2);

        // Снятие бана обязано пережить рестарт так же, как и сам бан —
        // иначе разбаненный игрок снова окажется заблокирован.
        var found = svc2.GetAllBans().Single(b => b.Id == created.Id);
        Assert.False(found.IsActive);
        Assert.False(svc2.CheckConnection(42, "10.0.0.5", "sc-123", "hwid-abc", "AA:BB:CC:DD:EE:FF", HwidPolicyMode.Strict).IsBlocked);
        store2.Dispose();
    }

    [Fact]
    public void Dispose_FlushesWithoutExplicitFlush()
    {
        var store = new JsonBanStore(_path);
        var svc = new MultiTierBanService(store);
        Ban(svc);
        store.Dispose(); // остановка сервера

        var store2 = new JsonBanStore(_path);
        Assert.Single(store2.LoadAll());
        store2.Dispose();
    }

    /// <summary>Общее хранилище «на двоих» — имитация одной таблицы в БД.</summary>
    private sealed class SharedBanStore : IBanStore
    {
        private readonly Dictionary<string, BanRecord> _records = new(StringComparer.Ordinal);
        public IReadOnlyList<BanRecord> LoadAll() => _records.Values.ToList();
        public void Upsert(BanRecord record) => _records[record.Id] = record;
        public void Flush() { }
    }

    [Fact]
    public void RefreshFromStore_PicksUpBansFromAnotherInstance()
    {
        // Горизонталь: два инстанса на одной общей таблице. Бан, выданный на
        // первом, должен доходить до второго без перезапуска.
        var shared = new SharedBanStore();
        var instanceA = new MultiTierBanService(shared);
        var instanceB = new MultiTierBanService(shared);

        Ban(instanceA, "Нарушитель");

        // Инстанс B ещё не обновлялся и о бане не знает.
        Assert.Empty(instanceB.GetAllBans());

        Assert.Equal(1, instanceB.RefreshFromStore());
        Assert.True(instanceB.CheckConnection(42, "10.0.0.5", "sc-123", "hwid-abc",
                                              "AA:BB:CC:DD:EE:FF", HwidPolicyMode.Strict).IsBlocked);

        // Повторное обновление ничего не меняет — лишних записей быть не должно.
        Assert.Equal(0, instanceB.RefreshFromStore());
        Assert.Single(instanceB.GetAllBans());
    }

    [Fact]
    public void RefreshFromStore_PicksUpUnbanFromAnotherInstance()
    {
        var shared = new SharedBanStore();
        var instanceA = new MultiTierBanService(shared);
        var instanceB = new MultiTierBanService(shared);

        var created = Ban(instanceA);
        instanceB.RefreshFromStore();
        Assert.True(instanceB.CheckConnection(42, "10.0.0.5", "sc-123", "hwid-abc",
                                              "AA:BB:CC:DD:EE:FF", HwidPolicyMode.Strict).IsBlocked);

        // Разбан на первом инстансе обязан доходить до второго так же, как бан:
        // иначе разбаненный останется заблокирован на половине серверов.
        instanceA.Unban(created.Id);
        Assert.Equal(1, instanceB.RefreshFromStore());
        Assert.False(instanceB.CheckConnection(42, "10.0.0.5", "sc-123", "hwid-abc",
                                               "AA:BB:CC:DD:EE:FF", HwidPolicyMode.Strict).IsBlocked);
    }

    [Fact]
    public void RefreshFromStore_WithoutStore_IsNoOp()
    {
        Assert.Equal(0, new MultiTierBanService().RefreshFromStore());
    }

    [Fact]
    public void ServiceWithoutStore_StillWorksInMemory()
    {
        // Конструктор без хранилища остаётся рабочим — на нём держатся тесты
        // самой логики банов, и ломать его правкой персистентности нельзя.
        var svc = new MultiTierBanService();
        var created = Ban(svc);
        Assert.True(svc.CheckConnection(42, "10.0.0.5", "sc-123", "hwid-abc", "AA:BB:CC:DD:EE:FF", HwidPolicyMode.Strict).IsBlocked);
        Assert.Equal(1, svc.Unban(created.Id));
    }

    [Fact]
    public void CorruptFile_IsQuarantined_AndServerStillStarts()
    {
        File.WriteAllText(_path, "{ это не json ]");

        var store = new JsonBanStore(_path);
        Assert.Empty(store.LoadAll());
        Assert.Single(Directory.GetFiles(_dir, "bans.json.corrupt-*"));

        // И после карантина сервис продолжает выдавать баны.
        var svc = new MultiTierBanService(store);
        Ban(svc);
        store.Dispose();
        Assert.Single(new JsonBanStore(_path).LoadAll());
    }

    [Fact]
    public void Upsert_IgnoresRecordWithoutId()
    {
        var store = new JsonBanStore(_path);
        store.Upsert(new BanRecord("", 1, "X", null, null, null, null,
                                   BanFlags.Account, "admin", "r", DateTime.UtcNow, null));
        Assert.Empty(store.LoadAll());
        store.Dispose();
    }
}
