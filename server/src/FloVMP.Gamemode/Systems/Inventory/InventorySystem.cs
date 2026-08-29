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

    public InventorySystem(string storePath)
    {
        _store = new JsonInventoryStore(storePath);
    }

    public void Attach()
    {
        Alt.OnClient<int, int>("flovmp:inv:move", OnMove);
        Alt.OnClient<int, int>("flovmp:inv:drop", OnDrop);
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

    private void OnDisconnect(IPlayer player, string reason) => Safe.Run("inv.OnDisconnect", () =>
    {
        if (_live.TryRemove(player.Id, out var e))
            _store.Save(e.accountId, e.inv);
    });

    private void OnMove(IPlayer player, int from, int to) => Safe.Run("inv.OnMove", () =>
    {
        if (!_live.TryGetValue(player.Id, out var e)) return;
        e.inv.Move(from, to);
        _store.Save(e.accountId, e.inv);
        Sync(player, e.inv);
    });

    private void OnDrop(IPlayer player, int slot, int qty) => Safe.Run("inv.OnDrop", () =>
    {
        if (!_live.TryGetValue(player.Id, out var e)) return;
        if (slot < 0 || slot >= e.inv.SlotCount) return;
        var s = e.inv.Slots[slot];
        if (s is null) return;

        var take = Math.Clamp(qty, 1, s.Quantity);
        var itemId = s.ItemId;
        e.inv.Remove(itemId, take);
        FloVMP.Core.Logging.GameLog.Item("drop",
            FloVMP.Core.Logging.LogActor.Player(e.accountId, player.Name), itemId, take);
        // TODO: положить дроп на землю как объект мира (позже)
        _store.Save(e.accountId, e.inv);
        Sync(player, e.inv);
    });

    private void OnUse(IPlayer player, int slot) => Safe.Run("inv.OnUse", () =>
    {
        if (!_live.TryGetValue(player.Id, out var e)) return;
        if (slot < 0 || slot >= e.inv.SlotCount) return;
        var s = e.inv.Slots[slot];
        if (s is null) return;

        var itemId = s.ItemId;
        switch (itemId)
        {
            case "bandage":
                player.Health = (ushort)Math.Min(200, player.Health + 25);
                e.inv.Remove("bandage", 1);
                break;
            case "water":
            case "bread":
                e.inv.Remove(itemId, 1);
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
