using AltV.Net;
using AltV.Net.Data;
using AltV.Net.Elements.Entities;
using FloVMP.Core.Players;

namespace FloVMP.Starter;

/// <summary>
/// Сохранение игрока (пункт 10 roadmap): где стоял, здоровье, броня, модель,
/// оружие — и хранилище «ключ → значение» для геймода (инвентарь, деньги).
/// Раньше это писал каждый геймод сам.
///
/// Что восстанавливать, решает владелец (players.restore_* в client.cfg или
/// flovmp:settings:set): у проекта с выбором персонажа позиция не нужна, у
/// проекта со своим инвентарём оружия — тоже. players.persistence = off
/// отдаёт всё геймоду.
/// </summary>
public partial class StarterResource
{
    private PlayerPersistence? _players;
    private long _nextPlayerSaveMs;
    // Кто сейчас на сервере под каким постоянным ID: события данных геймоду
    // идут по ID игрока, а хранилище живёт по постоянному ID.
    private readonly Dictionary<string, uint> _onlineIdentities = new();

    private static string IdentityOf(IPlayer player) => player.SocialClubId.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private void StartPlayerPersistence(string? dbConnection, string dataDir)
    {
        if (!_settings.Bool("players.persistence"))
        {
            Alt.Log("[FloV:MP] [Игроки] Сохранение игроков платформой выключено (players.persistence = off).");
            return;
        }
        try
        {
            var store = PlayerStoreFactory.Create(dbConnection, WorldName(), Path.Combine(dataDir, "players.json"));
            _players = new PlayerPersistence(store, Alt.LogWarning);
            var count = _players.LoadStates();
            Alt.Log($"[FloV:MP] [Игроки] Игроки сохраняются: {store.Describe}. Известно игроков: {count}.");
        }
        catch (Exception ex)
        {
            _players = null;
            Alt.LogError($"[FloV:MP] [Игроки] Сохранение игроков не запущено: {ex.Message}");
        }
    }

    private void StopPlayerPersistence()
    {
        if (_players is null) return;
        try
        {
            foreach (var p in AllPlayers())
                if (p.Exists && _clientReady.ContainsKey(p.Id)) SavePlayer(p);
            var ok = _players.FlushBlocking(TimeSpan.FromSeconds(10));
            Alt.Log("[FloV:MP] [Игроки] Игроки сохранены перед остановкой" + (ok ? "." : " — НЕ всё успело записаться."));
            _players.Dispose();
        }
        catch (Exception ex) { Alt.LogWarning($"[FloV:MP] [Игроки] Сохранение перед остановкой: {ex.Message}"); }
        _players = null;
    }

    /// <summary>Что восстановить при входе. Null-поля — брать значения по умолчанию.</summary>
    private readonly record struct RestorePlan(PersistedPlayer? Saved, bool Position, bool Health, bool Model, bool Weapons, bool Dimension);

    private RestorePlan PlanRestore(IPlayer player)
    {
        var saved = _players?.SavedState(IdentityOf(player));
        if (saved is null) return default;
        return new RestorePlan(saved,
            _settings.Bool("players.restore_position"),
            // 100 и меньше — вышел мёртвым: появляется целым, как после возрождения.
            _settings.Bool("players.restore_health") && saved.Health > 100,
            _settings.Bool("players.restore_model") && saved.Model != 0,
            _settings.Bool("players.restore_weapons") && saved.Weapons.Count > 0,
            _settings.Bool("players.restore_dimension"));
    }

    /// <summary>Вход: точка и модель — из сохранения, если так настроено.</summary>
    private (Position Position, float Heading) SpawnPointFor(RestorePlan plan)
    {
        if (plan.Saved is { } s && plan.Position) return (new Position(s.X, s.Y, s.Z), s.Heading);
        return NextSpawn();
    }

    private uint SpawnModelFor(RestorePlan plan) =>
        plan.Saved is { } s && plan.Model ? s.Model : SpawnModel();

    /// <summary>После спавна: здоровье, броня, измерение, оружие; геймоду — загрузка его данных.</summary>
    private void ApplyRestore(IPlayer player, RestorePlan plan)
    {
        if (_players is null) return;
        var identity = IdentityOf(player);
        _onlineIdentities[identity] = player.Id;
        if (_settings.Bool("players.data")) _players.BeginLoadData(identity);
        if (plan.Saved is not { } s) return;

        if (plan.Health)
        {
            player.Health = (ushort)Math.Clamp(s.Health, 101, 200);
            player.Armor = (ushort)Math.Clamp(s.Armor, 0, 100);
        }
        if (plan.Dimension && s.Dimension != player.Dimension) player.Dimension = s.Dimension;
        if (plan.Weapons)
            foreach (var w in s.Weapons)
            {
                player.GiveWeapon(w.Hash, Math.Max(0, w.Ammo), false);
                // Учёт — ровно сохранённый: GiveWeapon с нулём записал бы «без учёта».
                _weaponLedger.Restore(player.Id, w.Hash, w.Ammo);
            }
        if (plan.Position) NotifyTeleport(player);   // своё возвращение — не «телепорт» для античита
        Alt.Log($"[FloV:MP] [Игроки] {player.Name}: восстановлено" +
                (plan.Position ? " место" : "") + (plan.Health ? ", здоровье" : "") +
                (plan.Model ? ", модель" : "") + (plan.Weapons ? $", оружие ({s.Weapons.Count})" : "") +
                (plan.Dimension ? ", измерение" : "") + ".");
    }

    private PersistedPlayer Snapshot(IPlayer player)
    {
        var pos = player.Position;
        float heading;
        int dimension;
        IReadOnlyList<SavedWeapon> weapons;
        if (player is NativePlayerProxy np)
        {
            heading = np.State.Heading;
            dimension = np.DimensionValue;
            weapons = _weaponLedger.Snapshot(np.Session.Id).Select(w => new SavedWeapon(w.Weapon, w.Ammo)).ToList();
        }
        else
        {
            heading = ((player.Rotation.Yaw * 180f / MathF.PI) % 360f + 360f) % 360f;
            dimension = player.Dimension;
            weapons = Array.Empty<SavedWeapon>();   // у клиента alt:V учёта выданного оружия нет
        }
        var health = player.IsDead ? 0 : player.Health;
        return new PersistedPlayer(IdentityOf(player), player.Name, pos.X, pos.Y, pos.Z, heading, dimension,
            health, player.Armor, player.Model, weapons);
    }

    private void SavePlayer(IPlayer player)
    {
        if (_players is null) return;
        try { _players.SaveState(Snapshot(player)); }
        catch (Exception ex) { Alt.LogWarning($"[FloV:MP] [Игроки] {player.Name} не сохранён: {ex.Message}"); }
    }

    /// <summary>Выход игрока: сохранить, пока его состояние и учёт оружия ещё на месте.</summary>
    private void SavePlayerOnLeave(IPlayer player)
    {
        if (_players is null) return;
        var identity = IdentityOf(player);
        // Не вошедший в мир (отказ, обрыв на загрузке) — не сохраняем: у него
        // нет честной позиции, и точка появления перетёрла бы прежнее место.
        if (_clientReady.ContainsKey(player.Id)) SavePlayer(player);
        if (_onlineIdentities.TryGetValue(identity, out var id) && id == player.Id)
        {
            _onlineIdentities.Remove(identity);
            _players.Unload(identity);
        }
    }

    /// <summary>Тик: периодическое сохранение и готовность данных геймода.</summary>
    private void TickPlayerPersistence(long nowMs)
    {
        if (_players is null) return;
        foreach (var (identity, error) in _players.PumpLoaded())
        {
            if (!_onlineIdentities.TryGetValue(identity, out var id)) continue;
            if (error is not null) Alt.LogWarning($"[FloV:MP] [Игроки] данные игрока {id} не загружены: {error}");
            Alt.Emit("flovmp:player:dataLoaded", (int)id, error is null);
        }
        if (nowMs < _nextPlayerSaveMs) return;
        _nextPlayerSaveMs = nowMs + _settings.Int("players.save_interval_sec") * 1000L;
        foreach (var p in AllPlayers())
            if (p.Exists && _clientReady.ContainsKey(p.Id)) SavePlayer(p);
    }

    // --- API для геймода -------------------------------------------------------------

    private void RegisterPlayerApi()
    {
        // Значение — строка (обычно JSON). Пусто — удалить ключ.
        Alt.OnServer<int, string, string>("flovmp:player:data:set", (id, key, value) =>
            WithPersistedPlayer(id, "data:set", identity =>
            {
                var error = _players!.SetData(identity, key ?? "", value);
                if (error is not null) Alt.LogWarning($"[FloV:MP] flovmp:player:data:set «{key}»: {error}");
            }));
        // Ответ — flovmp:player:data (ID, ключ, значение, есть ли такой ключ).
        Alt.OnServer<int, string>("flovmp:player:data:get", (id, key) =>
            WithPersistedPlayer(id, "data:get", identity =>
            {
                if (!_players!.IsLoaded(identity))
                    Alt.LogWarning($"[FloV:MP] flovmp:player:data:get для {id}: данные ещё грузятся — дождитесь flovmp:player:dataLoaded");
                var value = _players.GetData(identity, key ?? "");
                Alt.Emit("flovmp:player:data", id, key ?? "", value ?? "", value is not null);
            }));
        // Сохранить сейчас (например, после важной сделки), не дожидаясь таймера.
        Alt.OnServer<int>("flovmp:player:save", id =>
        {
            var p = PlayerById((uint)id);
            if (p is not null && p.Exists && _clientReady.ContainsKey(p.Id)) SavePlayer(p);
        });
    }

    private void WithPersistedPlayer(int id, string what, Action<string> action)
    {
        if (_players is null || !_settings.Bool("players.data"))
        {
            Alt.LogWarning($"[FloV:MP] flovmp:player:{what}: хранилище игроков выключено (players.persistence / players.data)");
            return;
        }
        var p = id > 0 ? PlayerById((uint)id) : null;
        if (p is null || !p.Exists)
        {
            Alt.LogWarning($"[FloV:MP] flovmp:player:{what}: игрока с ID {id} нет на сервере");
            return;
        }
        try { action(IdentityOf(p)); }
        catch (Exception ex) { Alt.LogError($"[FloV:MP] flovmp:player:{what} для {id}: {ex.Message}"); }
    }
}
