using FloVMP.Core.Admin;
using Xunit;

namespace FloVMP.Core.Tests;

/// <summary>
/// Права администраторов в базе данных.
///
/// Сценарий установки: владелец выдаёт себе права в таблице admins, и сервер
/// обязан их увидеть — без перезапуска. Обновление с admins.json на базу не
/// должно отобрать права у существующих администраторов. Недоступная база не
/// должна отобрать права ни у кого.
/// </summary>
public sealed class AdminStoreTests : IDisposable
{
    private readonly string _dir;

    public AdminStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "flovmp-adminstore", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        Environment.SetEnvironmentVariable("FLOVMP_SETUP_TOKEN", null);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { /* временный каталог */ }
    }

    /// <summary>Имитация таблицы admins.</summary>
    private sealed class FakeStore : IAdminStore
    {
        public readonly Dictionary<string, (int Level, bool Founder)> Rows = new();
        public bool Broken { get; set; }
        public int Writes { get; private set; }

        public IReadOnlyList<AdminRecord> LoadAll()
        {
            if (Broken) throw new InvalidOperationException("база недоступна");
            return Rows.Where(r => r.Value.Level > 0)
                       .Select(r => new AdminRecord(r.Key, r.Value.Level, r.Value.Founder))
                       .ToList();
        }

        public void Upsert(string socialClub, int level, bool isFounder, string? grantedBy)
        {
            if (Broken) throw new InvalidOperationException("база недоступна");
            Writes++;
            if (level <= 0) Rows.Remove(socialClub);
            else Rows[socialClub] = (level, isFounder);
        }
    }

    private AdminBootstrapManager NewManager() =>
        new(Path.Combine(_dir, "admins.json"), () => DateTime.UtcNow);

    [Fact]
    public void AdminGrantedInDatabase_IsSeenByServer()
    {
        // Главный сценарий установки: права выданы прямо в таблице.
        var store = new FakeStore();
        store.Rows["111111"] = (8, true);

        var mgr = NewManager();
        mgr.AttachStore(store);

        Assert.Equal(8, mgr.GetAssignedRank(111111UL, "Owner"));
        Assert.True(mgr.IsFounder(111111UL, "Owner"));
    }

    [Fact]
    public void ChangeInDatabase_AppliesOnRefresh_WithoutRestart()
    {
        var store = new FakeStore();
        var mgr = NewManager();
        mgr.AttachStore(store);

        Assert.Equal(0, mgr.GetAssignedRank(222222UL, "Mod"));

        // Владелец правит таблицу руками, пока сервер работает.
        store.Rows["222222"] = (3, false);
        mgr.RefreshFromStore();

        Assert.Equal(3, mgr.GetAssignedRank(222222UL, "Mod"));
    }

    [Fact]
    public void RemovalInDatabase_RevokesOnRefresh()
    {
        var store = new FakeStore();
        store.Rows["333333"] = (5, false);
        var mgr = NewManager();
        mgr.AttachStore(store);
        Assert.Equal(5, mgr.GetAssignedRank(333333UL, "Admin"));

        // Снятие прав в базе обязано снимать их и на сервере — иначе уволенный
        // администратор сохранил бы доступ до перезапуска.
        store.Rows.Remove("333333");
        mgr.RefreshFromStore();

        Assert.Equal(0, mgr.GetAssignedRank(333333UL, "Admin"));
    }

    [Fact]
    public void Reload_PullsFromDatabase()
    {
        // reloadadmins в консоли — мгновенный способ применить ручную правку.
        var store = new FakeStore();
        var mgr = NewManager();
        mgr.AttachStore(store);

        store.Rows["444444"] = (7, false);
        mgr.Reload();

        Assert.Equal(7, mgr.GetAssignedRank(444444UL, "Head"));
    }

    [Fact]
    public void SetAdmin_WritesToDatabase()
    {
        var store = new FakeStore();
        var mgr = NewManager();
        mgr.AttachStore(store);

        mgr.SetAdmin("555555", 4);

        Assert.True(store.Rows.ContainsKey("555555"));
        Assert.Equal(4, store.Rows["555555"].Level);
    }

    [Fact]
    public void SetAdminZero_RemovesFromDatabase()
    {
        var store = new FakeStore();
        store.Rows["666666"] = (4, false);
        var mgr = NewManager();
        mgr.AttachStore(store);

        mgr.SetAdmin("666666", 0);

        Assert.False(store.Rows.ContainsKey("666666"));
        Assert.Equal(0, mgr.GetAssignedRank(666666UL, "X"));
    }

    [Fact]
    public void ClaimOwner_WritesFounderToDatabase()
    {
        var store = new FakeStore();
        var mgr = NewManager();
        mgr.AttachStore(store);

        Assert.True(mgr.TryClaimOwner(mgr.CurrentSetupToken, "Owner", 777777UL, out _));

        Assert.True(store.Rows.ContainsKey("777777"));
        Assert.Equal(8, store.Rows["777777"].Level);
        Assert.True(store.Rows["777777"].Founder);
    }

    [Fact]
    public void ExistingAdminsJson_IsImportedIntoEmptyDatabase()
    {
        // Обновление сервера до версии с базой: права из admins.json не должны
        // пропасть молча.
        var legacy = NewManager();
        legacy.SetAdmin("888888", 6);

        var store = new FakeStore();
        var mgr = NewManager();
        mgr.AttachStore(store);

        Assert.True(store.Rows.ContainsKey("888888"));
        Assert.Equal(6, mgr.GetAssignedRank(888888UL, "Old"));
    }

    [Fact]
    public void NonEmptyDatabase_IsNotOverwrittenByAdminsJson()
    {
        // База уже ведётся — старый файл не должен вернуть снятые в ней права.
        var legacy = NewManager();
        legacy.SetAdmin("999999", 6);

        var store = new FakeStore();
        store.Rows["123123"] = (8, true);

        var mgr = NewManager();
        mgr.AttachStore(store);

        Assert.False(store.Rows.ContainsKey("999999"));
        Assert.Equal(0, mgr.GetAssignedRank(999999UL, "Old"));
    }

    [Fact]
    public void BrokenDatabaseAtStart_KeepsAdminsJson()
    {
        var legacy = NewManager();
        legacy.SetAdmin("101010", 5);

        var store = new FakeStore { Broken = true };
        var mgr = NewManager();
        mgr.AttachStore(store);

        Assert.False(mgr.HasStore);
        Assert.Equal(5, mgr.GetAssignedRank(101010UL, "Admin"));
    }

    [Fact]
    public void DatabaseFailureOnRefresh_DoesNotRevokeAnyone()
    {
        // Сбой чтения базы не должен отобрать права у всех разом.
        var store = new FakeStore();
        store.Rows["202020"] = (8, true);
        var mgr = NewManager();
        mgr.AttachStore(store);

        store.Broken = true;
        Assert.Equal(-1, mgr.RefreshFromStore());
        Assert.Equal(8, mgr.GetAssignedRank(202020UL, "Owner"));
    }

    [Fact]
    public void DatabaseFailureOnSetAdmin_StillAppliesInMemory()
    {
        var store = new FakeStore();
        var mgr = NewManager();
        mgr.AttachStore(store);

        store.Broken = true;
        mgr.SetAdmin("303030", 2);

        Assert.Equal(2, mgr.GetAssignedRank(303030UL, "Helper"));
    }

    [Fact]
    public void GarbageKeysInDatabase_GrantNothing()
    {
        // Ник по ошибке вместо SocialClubId не должен стать чьими-то правами:
        // ник подделывается клиентом.
        var store = new FakeStore();
        store.Rows["Admin"] = (8, true);
        var mgr = NewManager();
        mgr.AttachStore(store);

        Assert.Equal(0, mgr.GetAssignedRank(0UL, "Admin"));
    }

    [Fact]
    public void WithoutStore_BehaviourIsUnchanged()
    {
        var mgr = NewManager();
        Assert.False(mgr.HasStore);
        Assert.Equal(-1, mgr.RefreshFromStore());
        mgr.SetAdmin("404040", 3);
        Assert.Equal(3, mgr.GetAssignedRank(404040UL, "X"));
    }
}
