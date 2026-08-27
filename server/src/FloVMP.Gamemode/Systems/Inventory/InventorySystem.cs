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
/// Клиентские события: flovmp:inv:move {from,to}, flovmp:inv:drop {slot,qty},
/// flovmp:inv:use {slot}.
/// </summary>
public sealed class InventorySystem
{
    private readonly IInventoryStore _store;
    private readonly Func<IPlayer, Account?> _accountOf;
    private readonly ConcurrentDictionary<uint, Inventory> _live = new();

    public InventorySystem(string storePath, Func<IPlayer, Account?> accountOf)
    {
        _store = new JsonInventoryStore(storePath);
        _accountOf = accountOf;
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
    public void OnAuthed(IPlayer player, Account account)
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

        _live[player.Id] = inv;
        Sync(player, inv);
    }

    private void OnDisconnect(IPlayer player, string reason)
    {
        if (_live.TryRemove(player.Id, out var inv))
        {
            var acc = _accountOf(player);
            if (acc is not null) _store.Save(acc.Id, inv);
        }
    }

    private void OnMove(IPlayer player, int from, int to)
    {
        if (!_live.TryGetValue(player.Id, out var inv)) return;
        inv.Move(from, to);
        Persist(player, inv);
        Sync(player, inv);
    }

    private void OnDrop(IPlayer player, int slot, int qty)
    {
        if (!_live.TryGetValue(player.Id, out var inv)) return;
        if (slot < 0 || slot >= inv.SlotCount) return;
        var s = inv.Slots[slot];
        if (s is null) return;

        var take = Math.Clamp(qty, 1, s.Quantity);
        inv.Remove(s.ItemId, take);
        Alt.Log($"[FloV:MP] inv: {player.Name} выбросил {s.ItemId} x{take}");
        // TODO: положить дроп на землю как объект мира (позже)
        Persist(player, inv);
        Sync(player, inv);
    }

    private void OnUse(IPlayer player, int slot)
    {
        if (!_live.TryGetValue(player.Id, out var inv)) return;
        if (slot < 0 || slot >= inv.SlotCount) return;
        var s = inv.Slots[slot];
        if (s is null) return;

        switch (s.ItemId)
        {
            case "bandage":
                player.Health = (ushort)Math.Min(200, player.Health + 25);
                inv.Remove("bandage", 1);
                break;
            case "water":
            case "bread":
                inv.Remove(s.ItemId, 1);
                break;
            default:
                player.Emit("flovmp:inv:notice", $"{s.ItemId}: пока нельзя использовать");
                return;
        }

        Persist(player, inv);
        Sync(player, inv);
    }

    private void Persist(IPlayer player, Inventory inv)
    {
        var acc = _accountOf(player);
        if (acc is not null) _store.Save(acc.Id, inv);
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
