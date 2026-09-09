using FloVMP.Core.Items;
using Xunit;

namespace FloVMP.Core.Tests;

public sealed class InventoryTests
{
    [Fact]
    public void Add_stacks_up_to_maxStack_then_uses_new_slot()
    {
        var inv = new Inventory(slotCount: 10, maxWeight: 100);
        Assert.True(inv.Add("water", 15).Ok); // water MaxStack = 10

        Assert.Equal(15, inv.CountOf("water"));
        Assert.Equal(10, inv.Slots[0]!.Quantity);
        Assert.Equal(5, inv.Slots[1]!.Quantity);
    }

    [Fact]
    public void Add_rejects_unknown_item()
    {
        var inv = new Inventory();
        Assert.Equal(InvError.UnknownItem, inv.Add("dragon", 1).Error);
    }

    [Fact]
    public void Add_enforces_weight_limit_atomically()
    {
        var inv = new Inventory(slotCount: 20, maxWeight: 2.0); // pistol = 1.1 kg
        Assert.True(inv.Add("pistol", 1).Ok);
        Assert.Equal(InvError.Overweight, inv.Add("pistol", 1).Error);
        Assert.Equal(1, inv.CountOf("pistol")); // ничего не добавилось
    }

    [Fact]
    public void Add_rolls_back_when_slots_run_out()
    {
        var inv = new Inventory(slotCount: 1, maxWeight: 1000);
        Assert.True(inv.Add("bread", 10).Ok);          // слот забит (MaxStack 10)
        var r = inv.Add("water", 5);                    // некуда
        Assert.Equal(InvError.Full, r.Error);
        Assert.Equal(0, inv.CountOf("water"));
        Assert.Equal(10, inv.CountOf("bread"));
    }

    [Fact]
    public void Remove_spans_multiple_stacks()
    {
        var inv = new Inventory(slotCount: 10, maxWeight: 100);
        inv.Add("bandage", 25); // MaxStack 20 -> 20 + 5
        Assert.True(inv.Remove("bandage", 22).Ok);
        Assert.Equal(3, inv.CountOf("bandage"));
    }

    [Fact]
    public void Remove_fails_when_not_enough()
    {
        var inv = new Inventory();
        inv.Add("water", 3);
        Assert.Equal(InvError.NotEnough, inv.Remove("water", 4).Error);
        Assert.Equal(3, inv.CountOf("water"));
    }

    [Fact]
    public void Move_to_empty_slot_relocates()
    {
        var inv = new Inventory(slotCount: 5, maxWeight: 100);
        inv.Add("phone", 1); // slot 0
        Assert.True(inv.Move(0, 3).Ok);
        Assert.Null(inv.Slots[0]);
        Assert.Equal("phone", inv.Slots[3]!.ItemId);
    }

    [Fact]
    public void Move_merges_same_item_and_keeps_overflow()
    {
        var inv = new Inventory(slotCount: 5, maxWeight: 100);
        inv.Slots[0] = new ItemStack { ItemId = "water", Quantity = 8 };
        inv.Slots[1] = new ItemStack { ItemId = "water", Quantity = 6 };

        Assert.True(inv.Move(1, 0).Ok); // 8 + 6, cap 10 -> 10 + 4
        Assert.Equal(10, inv.Slots[0]!.Quantity);
        Assert.Equal(4, inv.Slots[1]!.Quantity);
    }

    [Fact]
    public void Move_swaps_different_items()
    {
        var inv = new Inventory(slotCount: 5, maxWeight: 100);
        inv.Slots[0] = new ItemStack { ItemId = "phone", Quantity = 1 };
        inv.Slots[1] = new ItemStack { ItemId = "pistol", Quantity = 1 };

        Assert.True(inv.Move(0, 1).Ok);
        Assert.Equal("pistol", inv.Slots[0]!.ItemId);
        Assert.Equal("phone", inv.Slots[1]!.ItemId);
    }

    [Fact]
    public void Move_rejects_bad_slot_index()
    {
        var inv = new Inventory(slotCount: 3, maxWeight: 100);
        Assert.Equal(InvError.BadSlot, inv.Move(0, 99).Error);
        Assert.Equal(InvError.BadSlot, inv.Move(1, 2).Error); // from пуст
    }

    [Fact]
    public void Snapshot_roundtrips_through_store()
    {
        var dir = Path.Combine(Path.GetTempPath(), "flovmp-inv-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "inv.json");
            var a = new Inventory(slotCount: 12, maxWeight: 30);
            a.Add("water", 12);
            a.Add("phone", 1);
            new JsonInventoryStore(path).Save(7, a);

            var b = new JsonInventoryStore(path).Load(7);
            Assert.Equal(12, b.SlotCount);
            Assert.Equal(30, b.MaxWeight);
            Assert.Equal(12, b.CountOf("water"));
            Assert.Equal(1, b.CountOf("phone"));
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact]
    public void ItemCatalog_Contains_Medical_And_Vehicle_Items()
    {
        Assert.True(ItemCatalog.Exists("medkit"));
        Assert.True(ItemCatalog.Exists("repairkit"));
        Assert.True(ItemCatalog.Exists("fuelcan"));

        var medkit = ItemCatalog.Get("medkit");
        Assert.NotNull(medkit);
        Assert.Equal("Большая аптечка", medkit.Name);
        Assert.True(medkit.Stackable);

        var rep = ItemCatalog.Get("repairkit");
        Assert.NotNull(rep);
        Assert.Equal(3.0, rep.Weight);

        var fuel = ItemCatalog.Get("fuelcan");
        Assert.NotNull(fuel);
        Assert.Equal(4.0, fuel.Weight);

        Assert.True(ItemCatalog.Exists("armour"));
        Assert.True(ItemCatalog.Exists("radio"));
        Assert.True(ItemCatalog.Exists("lockpick"));

        var arm = ItemCatalog.Get("armour");
        Assert.NotNull(arm);
        Assert.Equal("Бронежилет", arm.Name);
        Assert.Equal(2.5, arm.Weight);
    }
}

