using System.Collections.Concurrent;
using System.Text.Json;
using AltV.Net;
using AltV.Net.Elements.Entities;
using FloVMP.Core.Auth;
using FloVMP.Core.Items;

namespace FloVMP.Gamemode;

/// <summary>
/// Инвентарь игрока (каркас Фазы 3, паттерн az_inventory).
///
/// Сервер владеет состоянием: держит <see cref="Inventory"/> на вошедшего
/// игрока, применяет и валидирует действия клиента, персистит и шлёт
/// обратно полный срез строкой JSON (flovmp:inv:sync).
///
/// В <see cref="_live"/> хранится и accountId — чтобы сохранение при выходе
/// не зависело от того, успел ли AuthSystem уже вычистить свою запись
/// (обработчики OnPlayerDisconnect идут по порядку подписки).
///
/// Клиентские события: flovmp:inv:move {from,to}, flovmp:inv:drop {slot,qty},
/// flovmp:inv:use {slot}.
/// </summary>
public sealed class InventorySystem
{
    private readonly IInventoryStore _store;
    private readonly ConcurrentDictionary<uint, (Inventory inv, int accountId)> _live = new();
    private readonly AtomicInventoryTransactionService _atomicTransactions = new();
    // недоверенный клиент может спамить :move/:use/:drop — каждый обработчик
    // пишет JSON на диск + шлёт sync. Гейт: ≤10 действий/сек на игрока.
    private readonly Systems.ClientRateGate _gate = new(maxPerWindow: 10, windowMs: 1000);

    public AtomicInventoryTransactionService AtomicTransactions => _atomicTransactions;

    public InventorySystem(string storePath)
    {
        _store = new JsonInventoryStore(storePath);
    }

    public void Attach()
    {
        Alt.OnClient<int, int>("flovmp:inv:move", OnMove);
        Alt.OnClient<int, int>("flovmp:inv:drop", OnDrop);
        Alt.OnClient<string>("flovmp:inv:pickup", OnPickup);
        Alt.OnClient<int>("flovmp:inv:use", OnUse);
        Alt.OnPlayerDisconnect += OnDisconnect;
    }

    public void Detach()
    {
        Alt.OnPlayerDisconnect -= OnDisconnect;
    }

    /// <summary>Игрок вошёл — поднять инвентарь, выдать стартовый набор, синхронизировать.</summary>
    public void OnAuthed(IPlayer player, Account account) => Safe.Run("inv.OnAuthed", () =>
    {
        var inv = _store.Load(account.Id);

        if (inv.Slots.All(s => s is null))
        {
            inv.Add("phone", 1);
            inv.Add("water", 2);
            inv.Add("bread", 2);
            inv.Add("bandage", 3);
            _store.Save(account.Id, inv);
        }

        _live[player.Id] = (inv, account.Id);
        Sync(player, inv);
    });

    /// <summary>Сохранить все живые инвентари (автосейв / завершение работы).</summary>
    public void SaveAll()
    {
        foreach (var (_, (inv, accountId)) in _live)
            Safe.Run("inv.SaveAll", () => _store.Save(accountId, inv));
    }

    /// <summary>Выдать предмет игроку в инвентарь (административные команды / игровые награды).</summary>
    public bool TryGiveItem(IPlayer player, string itemId, int quantity)
    {
        if (!player.Exists || quantity <= 0) return false;
        if (!_live.TryGetValue(player.Id, out var e)) return false;
        if (!ItemCatalog.Exists(itemId)) return false;

        var res = e.inv.Add(itemId, quantity);
        if (res.Ok)
        {
            _store.Save(e.accountId, e.inv);
            Sync(player, e.inv);
            FloVMP.Core.Logging.GameLog.Item("admin_give",
                FloVMP.Core.Logging.LogActor.Player(e.accountId, player.Name), itemId, quantity);
            return true;
        }
        return false;
    }

    private void OnDisconnect(IPlayer player, string reason) => Safe.Run("inv.OnDisconnect", () =>
    {
        _gate.Forget(player.Id);
        if (_live.TryRemove(player.Id, out var e))
            _store.Save(e.accountId, e.inv);
    });

    private void OnMove(IPlayer player, int from, int to) => Safe.Run("inv.OnMove", () =>
    {
        if (!_gate.Allow(player.Id)) return;
        if (!_live.TryGetValue(player.Id, out var e)) return;
        var res = e.inv.Move(from, to);
        if (!res.Ok) return;               // ничего не поменялось — не пишем на диск
        _store.Save(e.accountId, e.inv);
        Sync(player, e.inv);
    });

    private void OnDrop(IPlayer player, int slot, int qty) => Safe.Run("inv.OnDrop", () =>
    {
        if (!_gate.Allow(player.Id)) return;
        if (!_live.TryGetValue(player.Id, out var e)) return;
        if (slot < 0 || slot >= e.inv.SlotCount) return;
        var s = e.inv.Slots[slot];
        if (s is null) return;

        var take = Math.Clamp(qty, 1, s.Quantity);
        var itemId = s.ItemId;
        var pPos = new FloVMP.Core.AntiCheat.Vector3D(player.Position.X, player.Position.Y, player.Position.Z);

        var drop = _atomicTransactions.DropItem(e.inv, itemId, take, pPos, player.Dimension, player.Id);
        if (drop != null)
        {
            FloVMP.Core.Logging.GameLog.Item("drop",
                FloVMP.Core.Logging.LogActor.Player(e.accountId, player.Name), itemId, take);
            _store.Save(e.accountId, e.inv);
            Sync(player, e.inv);
            player.Emit("flovmp:inv:notice", $"Вы выбросили {ItemCatalog.Get(itemId)?.Name ?? itemId} x{take}");
        }
    });

    private void OnPickup(IPlayer player, string dropId) => Safe.Run("inv.OnPickup", () =>
    {
        if (!_gate.Allow(player.Id)) return;
        if (string.IsNullOrEmpty(dropId) || dropId.Length > 64) return;
        if (!_live.TryGetValue(player.Id, out var e)) return;
        var pPos = new FloVMP.Core.AntiCheat.Vector3D(player.Position.X, player.Position.Y, player.Position.Z);
        if (_atomicTransactions.TryPickupGroundItem(player.Id, dropId, e.inv, pPos, 4.0f, out var picked) && picked != null)
        {
            _store.Save(e.accountId, e.inv);
            Sync(player, e.inv);
            player.Emit("flovmp:inv:notice", $"Вы подобрали {ItemCatalog.Get(picked.ItemId)?.Name ?? picked.ItemId} x{picked.Quantity}");
            FloVMP.Core.Logging.GameLog.Item("pickup",
                FloVMP.Core.Logging.LogActor.Player(e.accountId, player.Name), picked.ItemId, picked.Quantity);
        }
        else
        {
            player.Emit("flovmp:inv:notice", "Не удалось подобрать предмет (слишком далеко или инвентарь полон)");
        }
    });

    private void OnUse(IPlayer player, int slot) => Safe.Run("inv.OnUse", () =>
    {
        if (!_gate.Allow(player.Id)) return;
        if (!_live.TryGetValue(player.Id, out var e)) return;
        if (slot < 0 || slot >= e.inv.SlotCount) return;
        var s = e.inv.Slots[slot];
        if (s is null) return;

        var itemId = s.ItemId;
        switch (itemId)
        {
            case "bandage":
                if (player.Health <= 0)
                {
                    player.Emit("flovmp:inv:notice", "Вы тяжело ранены и не можете перевязать себя");
                    return;
                }
                if (player.Health >= 200)
                {
                    player.Emit("flovmp:inv:notice", "У вас уже максимальное здоровье (200 HP)");
                    return;
                }
                player.Health = (ushort)Math.Min(200, player.Health + 25);
                s.Quantity -= 1;
                if (s.Quantity <= 0) e.inv.Slots[slot] = null;
                player.Emit("flovmp:inv:notice", $"Вы перевязали раны (+25 HP). Текущее: {player.Health}/200");
                break;
            case "medkit":
                if (player.Health <= 0)
                {
                    player.Emit("flovmp:inv:notice", "Вы тяжело ранены и не можете использовать аптечку");
                    return;
                }
                if (player.Health >= 200)
                {
                    player.Emit("flovmp:inv:notice", "У вас уже максимальное здоровье (200 HP)");
                    return;
                }
                player.Health = (ushort)Math.Min(200, player.Health + 75);
                s.Quantity -= 1;
                if (s.Quantity <= 0) e.inv.Slots[slot] = null;
                player.Emit("flovmp:inv:notice", $"Вы использовали аптечку (+75 HP). Текущее: {player.Health}/200");
                break;
            case "armour":
                if (player.Health <= 0)
                {
                    player.Emit("flovmp:inv:notice", "Вы тяжело ранены и не можете надеть бронежилет");
                    return;
                }
                if (player.Armor >= 100)
                {
                    player.Emit("flovmp:inv:notice", "У вас уже максимальный уровень брони (100)");
                    return;
                }
                player.Armor = (ushort)Math.Min(100, player.Armor + 100);
                s.Quantity -= 1;
                if (s.Quantity <= 0) e.inv.Slots[slot] = null;
                player.Emit("flovmp:inv:notice", "Вы надели бронежилет (+100 брони)");
                break;
            case "radio":
                player.Emit("flovmp:inv:notice", "Рация включена (настроена на общественную волну 100.0 MHz)");
                break;
            case "lockpick":
                var lockVeh = player.Vehicle ?? FindNearestVehicle(player.Position, player.Dimension, 4.0f);
                if (lockVeh == null)
                {
                    player.Emit("flovmp:inv:notice", "Рядом нет транспорта для взлома (до 4м)");
                    return;
                }
                if (lockVeh.LockState == AltV.Net.Enums.VehicleLockState.Unlocked)
                {
                    player.Emit("flovmp:inv:notice", "Этот транспорт уже открыт");
                    return;
                }
                lockVeh.LockState = AltV.Net.Enums.VehicleLockState.Unlocked;
                s.Quantity -= 1;
                if (s.Quantity <= 0) e.inv.Slots[slot] = null;
                player.Emit("flovmp:inv:notice", "Вы успешно взломали замок автомобиля отмычкой!");
                break;
            case "repairkit":
                var repVeh = player.Vehicle ?? FindNearestVehicle(player.Position, player.Dimension, 5.0f);
                if (repVeh == null)
                {
                    player.Emit("flovmp:inv:notice", "Рядом нет транспорта для ремонта (до 5м)");
                    return;
                }
                repVeh.EngineHealth = 1000;
                repVeh.BodyHealth = 1000;
                s.Quantity -= 1;
                if (s.Quantity <= 0) e.inv.Slots[slot] = null;
                player.Emit("flovmp:inv:notice", "Транспорт успешно отремонтирован ремкомплектом");
                break;
            case "fuelcan":
                var fuelVeh = player.Vehicle ?? FindNearestVehicle(player.Position, player.Dimension, 5.0f);
                if (fuelVeh == null)
                {
                    player.Emit("flovmp:inv:notice", "Рядом нет транспорта для заправки (до 5м)");
                    return;
                }
                float curF = 100.0f;
                if (fuelVeh.GetStreamSyncedMetaData("fuel", out float fMeta)) curF = fMeta;
                float addedF = Math.Min(100.0f, curF + 35.0f);
                fuelVeh.SetStreamSyncedMetaData("fuel", addedF);
                s.Quantity -= 1;
                if (s.Quantity <= 0) e.inv.Slots[slot] = null;
                player.Emit("flovmp:inv:notice", $"Заправлено +35% бензина. Уровень: {Math.Round(addedF)}%");
                break;
            case "water":
                s.Quantity -= 1;
                if (s.Quantity <= 0) e.inv.Slots[slot] = null;
                player.Emit("flovmp:inv:notice", "Вы выпили чистую воду и освежились");
                break;
            case "bread":
                s.Quantity -= 1;
                if (s.Quantity <= 0) e.inv.Slots[slot] = null;
                player.Emit("flovmp:inv:notice", "Вы перекусили свежим хлебом");
                break;
            case "pistol":
                const uint pistolHash = 0x1B06D571;
                if (player.CurrentWeapon == pistolHash)
                {
                    player.RemoveWeapon(pistolHash);
                    player.Emit("flovmp:inv:notice", "Пистолет убран в кобуру");
                }
                else
                {
                    // ammo=0: боезапас даёт только патронный предмет (ammo9),
                    // иначе тоггл предмета фармил бы по 50 патронов бесплатно.
                    player.GiveWeapon(pistolHash, 0, true);
                    var rounds = e.inv.CountOf("ammo9");
                    if (rounds > 0) player.SetWeaponAmmo(pistolHash, (ushort)Math.Min(rounds, 250));
                    player.Emit("flovmp:inv:notice", rounds > 0
                        ? "Пистолет взведён и заряжён"
                        : "Пистолет взведён, но патронов нет");
                }
                break;
            default:
                player.Emit("flovmp:inv:notice", $"{itemId}: пока нельзя использовать");
                return;
        }

        FloVMP.Core.Logging.GameLog.Item("use",
            FloVMP.Core.Logging.LogActor.Player(e.accountId, player.Name), itemId, 1);
        _store.Save(e.accountId, e.inv);
        Sync(player, e.inv);
    });

    /// <summary>
    /// Получить множество разрешённого оружия, находящегося в инвентаре игрока.
    /// </summary>
    public ISet<uint> GetAllowedWeapons(IPlayer player)
    {
        var set = new HashSet<uint>();
        if (!player.Exists) return set;
        if (_live.TryGetValue(player.Id, out var e))
        {
            foreach (var s in e.inv.Slots)
            {
                if (s == null) continue;
                if (s.ItemId == "pistol" || s.ItemId == "ammo9")
                {
                    set.Add(0x1B06D571); // Pistol
                }
            }
        }
        return set;
    }

    private static IVehicle? FindNearestVehicle(AltV.Net.Data.Position pos, int dimension, float maxDistance = 5.0f)
    {
        IVehicle? best = null;
        float bestDist = maxDistance;
        foreach (var v in Alt.GetAllVehicles())
        {
            if (!v.Exists || v.Dimension != dimension) continue;
            var dist = v.Position.Distance(pos);
            if (dist <= bestDist)
            {
                bestDist = dist;
                best = v;
            }
        }
        return best;
    }


    private static void Sync(IPlayer player, Inventory inv)
    {
        if (!player.Exists) return;
        player.Emit("flovmp:inv:sync", BuildPayload(inv));
    }

    private static string BuildPayload(Inventory inv)
    {
        var slots = inv.Slots.Select(s => s is null
            ? null
            : new { itemId = s.ItemId, qty = s.Quantity }).ToArray();

        var defs = ItemCatalog.All.ToDictionary(
            d => d.Id,
            d => new { name = d.Name, weight = d.Weight, maxStack = d.MaxStack });

        return JsonSerializer.Serialize(new
        {
            slotCount = inv.SlotCount,
            maxWeight = inv.MaxWeight,
            weight = Math.Round(inv.TotalWeight(), 2),
            slots,
            defs,
        });
    }
}
