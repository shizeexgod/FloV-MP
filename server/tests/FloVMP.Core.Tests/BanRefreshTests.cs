using FloVMP.Core.Security;
using Xunit;

namespace FloVMP.Core.Tests;

/// <summary>
/// Синхронизация блокировок между инстансами.
///
/// Инстанс раз в полминуты дозагружает баны, выданные на других серверах.
/// Перечитывать при этом ВСЮ таблицу — постоянная нагрузка на базу, растущая
/// вместе с числом банов, поэтому у хранилища есть инкрементальный путь.
/// Здесь закреплено и то, что он используется, и то, что он ничего не теряет:
/// пропущенный бан означает, что забаненный спокойно заходит на соседний
/// инстанс.
/// </summary>
public class BanRefreshTests
{
    /// <summary>Хранилище с отметками времени — имитация общей таблицы.</summary>
    private sealed class TimestampedStore : IBanStore, IIncrementalBanStore
    {
        private readonly List<(BanRecord Record, DateTime UpdatedUtc)> _rows = new();

        /// <summary>Запросы, которые сервис сделал: null = «отдай всё».</summary>
        public List<DateTime?> Queries { get; } = new();

        // Отметки времени — настоящие: сервис сравнивает их со своими часами
        // (DateTime.UtcNow), и подставной клок здесь только увёл бы тест от
        // того, что происходит на самом деле.
        public DateTime Now => DateTime.UtcNow;

        public IReadOnlyList<BanRecord> LoadAll()
        {
            Queries.Add(null);
            return _rows.Select(r => r.Record).ToList();
        }

        public IReadOnlyList<BanRecord> LoadChangedSince(DateTime? sinceUtc)
        {
            Queries.Add(sinceUtc);
            if (sinceUtc is null) return _rows.Select(r => r.Record).ToList();
            return _rows.Where(r => r.UpdatedUtc >= sinceUtc.Value).Select(r => r.Record).ToList();
        }

        public void Upsert(BanRecord record)
        {
            _rows.RemoveAll(r => r.Record.Id == record.Id);
            _rows.Add((record, Now));
        }

        public void Flush() { }
    }

    private static BanRecord Make(string id, bool active = true) => new(
        Id: id, AccountId: 1, Username: "Читер_" + id,
        Ip: "10.0.0." + id.Length, SocialClubId: "sc" + id,
        HwidHash: "hw" + id, MacAddress: "mac" + id,
        Flags: BanFlags.Account | BanFlags.Hwid,
        AdminUsername: "Админ", Reason: "чит",
        BannedAtUtc: new DateTime(2026, 9, 15, 11, 0, 0, DateTimeKind.Utc),
        ExpiresAtUtc: null, IsActive: active);

    [Fact]
    public void FirstRefresh_AsksForEverything()
    {
        var store = new TimestampedStore();
        var svc = new MultiTierBanService(store);
        store.Queries.Clear(); // конструктор уже позвал LoadAll

        store.Upsert(Make("a"));
        svc.RefreshFromStore();

        // Синхронизаций ещё не было — спрашиваем всё.
        Assert.Single(store.Queries);
        Assert.Null(store.Queries[0]);
    }

    [Fact]
    public void SecondRefresh_AsksOnlyForChanges()
    {
        var store = new TimestampedStore();
        var svc = new MultiTierBanService(store);

        svc.RefreshFromStore();
        store.Queries.Clear();
        svc.RefreshFromStore();

        Assert.Single(store.Queries);
        Assert.NotNull(store.Queries[0]);
    }

    [Fact]
    public void Refresh_LooksSlightlyBackInTime()
    {
        // Часы инстанса и базы расходятся. Запрос «строго после прошлой
        // синхронизации» пропустил бы бан, записанный в ту же секунду, —
        // а пропущенный бан означает, что забаненный заходит на соседний
        // инстанс. Поэтому окно сдвинуто назад.
        var store = new TimestampedStore();
        var svc = new MultiTierBanService(store);

        var before = DateTime.UtcNow;
        svc.RefreshFromStore();
        store.Queries.Clear();
        svc.RefreshFromStore();

        var since = store.Queries[0];
        Assert.NotNull(since);
        Assert.True(since!.Value < before,
            "окно синхронизации не сдвинуто назад — свежий бан может потеряться");
    }

    [Fact]
    public void BanFromAnotherInstance_ArrivesOnRefresh()
    {
        var store = new TimestampedStore();
        var instanceB = new MultiTierBanService(store);
        svcRefreshTwice(instanceB);

        // Соседний инстанс выдал бан уже после нашей синхронизации.
        store.Upsert(Make("new"));

        Assert.Equal(1, instanceB.RefreshFromStore());
        Assert.True(instanceB.CheckConnection(1, "10.0.0.3", "scnew", "hwnew", "macnew",
                                              HwidPolicyMode.Strict).IsBlocked);
    }

    [Fact]
    public void UnbanFromAnotherInstance_ArrivesOnRefresh()
    {
        var store = new TimestampedStore();
        store.Upsert(Make("x"));
        var instanceB = new MultiTierBanService(store);

        Assert.True(instanceB.CheckConnection(1, "10.0.0.1", "scx", "hwx", "macx",
                                              HwidPolicyMode.Strict).IsBlocked);

        // Разбан на соседнем инстансе обязан доходить так же, как бан:
        // иначе разбаненный останется заблокирован на половине серверов.
        store.Upsert(Make("x", active: false));

        Assert.Equal(1, instanceB.RefreshFromStore());
        Assert.False(instanceB.CheckConnection(1, "10.0.0.1", "scx", "hwx", "macx",
                                               HwidPolicyMode.Strict).IsBlocked);
    }

    [Fact]
    public void Refresh_WithNothingNew_ReportsZero()
    {
        var store = new TimestampedStore();
        store.Upsert(Make("a"));
        var svc = new MultiTierBanService(store);

        svc.RefreshFromStore();
        Assert.Equal(0, svc.RefreshFromStore());
    }

    [Fact]
    public void NonIncrementalStore_StillWorks()
    {
        // Файловое хранилище инкремент не умеет — сервис обязан просто
        // перечитывать всё, а не падать.
        var store = new PlainStore();
        var svc = new MultiTierBanService(store);
        store.Add(Make("a"));
        Assert.Equal(1, svc.RefreshFromStore());
    }

    private sealed class PlainStore : IBanStore
    {
        private readonly List<BanRecord> _rows = new();
        public void Add(BanRecord r) => _rows.Add(r);
        public IReadOnlyList<BanRecord> LoadAll() => _rows;
        public void Upsert(BanRecord record) => _rows.Add(record);
        public void Flush() { }
    }

    private static void svcRefreshTwice(MultiTierBanService svc)
    {
        svc.RefreshFromStore();
        svc.RefreshFromStore();
    }
}
