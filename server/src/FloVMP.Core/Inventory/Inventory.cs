namespace FloVMP.Core.Items;

/// <summary>Экземпляр предмета в слоте инвентаря.</summary>
public sealed class ItemStack
{
    public string ItemId { get; set; } = "";
    public int Quantity { get; set; }
}

public enum InvError { None, UnknownItem, NotEnough, Overweight, BadSlot, Full }

public sealed record InvResult(bool Ok, InvError Error = InvError.None)
{
    public static readonly InvResult Success = new(true);
    public static InvResult Fail(InvError e) => new(false, e);
}

/// <summary>
/// Инвентарь игрока: список слотов фиксированного размера + ограничение по
/// суммарному весу. Чистая логика, без alt:V. Стекование по
/// <see cref="ItemDef.MaxStack"/>.
/// </summary>
public sealed class Inventory
{
    public int SlotCount { get; }
    public double MaxWeight { get; }
    public ItemStack?[] Slots { get; }

    public Inventory(int slotCount = 24, double maxWeight = 40.0)
    {
        SlotCount = slotCount;
        MaxWeight = maxWeight;
        Slots = new ItemStack?[slotCount];
    }

    public double TotalWeight()
    {
        double w = 0;
        foreach (var s in Slots)
        {
            if (s is null) continue;
            var def = ItemCatalog.Get(s.ItemId);
            if (def is not null) w += def.Weight * s.Quantity;
        }
        return w;
    }

    public int CountOf(string itemId)
    {
        var n = 0;
        foreach (var s in Slots)
            if (s is not null && s.ItemId == itemId) n += s.Quantity;
        return n;
    }

    public InvResult Add(string itemId, int qty)
    {
        if (qty <= 0) return InvResult.Fail(InvError.NotEnough);
        var def = ItemCatalog.Get(itemId);
        if (def is null) return InvResult.Fail(InvError.UnknownItem);
        if (TotalWeight() + def.Weight * qty > MaxWeight + 1e-9)
            return InvResult.Fail(InvError.Overweight);

        var left = qty;

        // 1) дозаполнить существующие стеки
        foreach (var s in Slots)
        {
            if (left == 0) break;
            if (s is null || s.ItemId != itemId) continue;
            var room = def.MaxStack - s.Quantity;
            if (room <= 0) continue;
            var take = Math.Min(room, left);
            s.Quantity += take;
            left -= take;
        }

        // 2) занять пустые слоты
        for (var i = 0; i < Slots.Length && left > 0; i++)
        {
            if (Slots[i] is not null) continue;
            var take = Math.Min(def.MaxStack, left);
            Slots[i] = new ItemStack { ItemId = itemId, Quantity = take };
            left -= take;
        }

        if (left > 0)
        {
            // откат: убрать то, что успели добавить
            Remove(itemId, qty - left);
            return InvResult.Fail(InvError.Full);
        }

        return InvResult.Success;
    }

    public InvResult Remove(string itemId, int qty)
    {
        if (qty <= 0) return InvResult.Fail(InvError.NotEnough);
        if (CountOf(itemId) < qty) return InvResult.Fail(InvError.NotEnough);

        var left = qty;
        for (var i = 0; i < Slots.Length && left > 0; i++)
        {
            var s = Slots[i];
            if (s is null || s.ItemId != itemId) continue;
            var take = Math.Min(s.Quantity, left);
            s.Quantity -= take;
            left -= take;
            if (s.Quantity == 0) Slots[i] = null;
        }
        return InvResult.Success;
    }

    /// <summary>Переместить/объединить содержимое слота from в слот to.</summary>
    public InvResult Move(int from, int to)
    {
        if (from < 0 || from >= SlotCount || to < 0 || to >= SlotCount)
            return InvResult.Fail(InvError.BadSlot);
        if (from == to) return InvResult.Success;

        var src = Slots[from];
        if (src is null) return InvResult.Fail(InvError.BadSlot);

        var dst = Slots[to];
        if (dst is null)
        {
            Slots[to] = src;
            Slots[from] = null;
            return InvResult.Success;
        }

        if (dst.ItemId == src.ItemId)
        {
            var def = ItemCatalog.Get(src.ItemId);
            var max = def?.MaxStack ?? 1;
            var room = max - dst.Quantity;
            var move = Math.Min(room, src.Quantity);
            dst.Quantity += move;
            src.Quantity -= move;
            if (src.Quantity == 0) Slots[from] = null;
            return InvResult.Success;
        }

        // разные предметы — своп
        (Slots[from], Slots[to]) = (dst, src);
        return InvResult.Success;
    }

    public List<ItemStack?> Snapshot()
    {
        var list = new List<ItemStack?>(SlotCount);
        foreach (var s in Slots)
            list.Add(s is null ? null : new ItemStack { ItemId = s.ItemId, Quantity = s.Quantity });
        return list;
    }

    public void LoadSnapshot(IReadOnlyList<ItemStack?>? slots)
    {
        if (slots is null) return;
        for (var i = 0; i < SlotCount; i++)
        {
            var s = i < slots.Count ? slots[i] : null;
            Slots[i] = s is null || string.IsNullOrEmpty(s.ItemId) || s.Quantity <= 0
                ? null
                : new ItemStack { ItemId = s.ItemId, Quantity = s.Quantity };
        }
    }
}
