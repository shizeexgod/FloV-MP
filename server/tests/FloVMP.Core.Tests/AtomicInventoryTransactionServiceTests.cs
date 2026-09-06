using System.Collections.Generic;
using System.Threading.Tasks;
using FloVMP.Core.AntiCheat;
using FloVMP.Core.Items;
using Xunit;

namespace FloVMP.Core.Tests;

public sealed class AtomicInventoryTransactionServiceTests
{
    [Fact]
    public void DropItem_RemovesFromSourceInventory_CreatesGroundItem()
    {
        var service = new AtomicInventoryTransactionService();
        var inv = new Inventory(slotCount: 10, maxWeight: 100);
        inv.Add("water", 5);

        var drop = service.DropItem(inv, "water", 3, new Vector3D(100f, 200f, 10f), dimension: 0, playerId: 101);

        Assert.NotNull(drop);
        Assert.Equal(2, inv.CountOf("water"));
        Assert.Equal(1, service.ActiveDropsCount);
        Assert.Equal(3, drop.Item.Quantity);
        Assert.Equal("water", drop.Item.ItemId);
        Assert.Equal(101ul, drop.DroppedByPlayerId);
    }

    [Fact]
    public void TryPickupGroundItem_TransfersToPlayer_RemovesFromGround()
    {
        var service = new AtomicInventoryTransactionService();
        var inv = new Inventory(slotCount: 10, maxWeight: 100);
        inv.Add("bandage", 10);

        var drop = service.DropItem(inv, "bandage", 4, new Vector3D(10f, 10f, 1f), dimension: 0, playerId: 101);
        Assert.NotNull(drop);

        var player2Inv = new Inventory(slotCount: 10, maxWeight: 100);
        var pickupSuccess = service.TryPickupGroundItem(
            playerId: 102,
            dropId: drop.DropId,
            playerInv: player2Inv,
            playerPos: new Vector3D(11f, 10f, 1f),
            maxDistance: 3.0f,
            out var pickedItem
        );

        Assert.True(pickupSuccess);
        Assert.NotNull(pickedItem);
        Assert.Equal(4, player2Inv.CountOf("bandage"));
        Assert.Equal(0, service.ActiveDropsCount);
        Assert.True(drop.IsConsumed);
    }

    [Fact]
    public void TryPickupGroundItem_DistanceCheck_FailsWhenTooFar()
    {
        var service = new AtomicInventoryTransactionService();
        var inv = new Inventory(slotCount: 10, maxWeight: 100);
        inv.Add("bandage", 5);

        var drop = service.DropItem(inv, "bandage", 2, new Vector3D(0f, 0f, 0f), dimension: 0, playerId: 101);
        Assert.NotNull(drop);

        var pickerInv = new Inventory(slotCount: 10, maxWeight: 100);
        var success = service.TryPickupGroundItem(
            playerId: 102,
            dropId: drop.DropId,
            playerInv: pickerInv,
            playerPos: new Vector3D(10f, 0f, 0f),
            maxDistance: 2.0f,
            out var picked
        );

        Assert.False(success);
        Assert.Null(picked);
        Assert.Equal(0, pickerInv.CountOf("bandage"));
        Assert.Equal(1, service.ActiveDropsCount);
    }

    [Fact]
    public async Task TryPickupGroundItem_ConcurrentRaceCondition_ExactlyOneWinner()
    {
        var service = new AtomicInventoryTransactionService();
        var spawnerInv = new Inventory(slotCount: 10, maxWeight: 100);
        spawnerInv.Add("pistol", 1);

        var drop = service.DropItem(spawnerInv, "pistol", 1, new Vector3D(5f, 5f, 0f), dimension: 0, playerId: 1);
        Assert.NotNull(drop);

        int concurrentPickups = 30;
        int successfulPickups = 0;

        var tasks = new List<Task>();
        for (int i = 0; i < concurrentPickups; i++)
        {
            ulong pId = (ulong)(100 + i);
            var testInv = new Inventory(slotCount: 10, maxWeight: 100);

            tasks.Add(Task.Run(() =>
            {
                if (service.TryPickupGroundItem(pId, drop.DropId, testInv, new Vector3D(5.5f, 5f, 0f), 2.0f, out var item))
                {
                    System.Threading.Interlocked.Increment(ref successfulPickups);
                }
            }));
        }

        await Task.WhenAll(tasks);

        Assert.Equal(1, successfulPickups);
        Assert.Equal(0, service.ActiveDropsCount);
        Assert.True(drop.IsConsumed);
    }

    [Fact]
    public void TryPickupGroundItem_WhenOverweight_RollsBackClaim()
    {
        var service = new AtomicInventoryTransactionService();
        var spawnerInv = new Inventory(slotCount: 10, maxWeight: 100);
        spawnerInv.Add("pistol", 2); // pistol = 1.1 кг каждый -> 2.2 кг

        var drop = service.DropItem(spawnerInv, "pistol", 2, new Vector3D(0f, 0f, 0f), dimension: 0, playerId: 1);
        Assert.NotNull(drop);

        // Инвентарь с лимитом 2.0 кг не может вместить 2 пистолета (2.2 кг)
        var heavyInv = new Inventory(slotCount: 10, maxWeight: 2.0);

        var failPickup = service.TryPickupGroundItem(2, drop.DropId, heavyInv, new Vector3D(0f, 0f, 0f), 2.0f, out _);
        Assert.False(failPickup);
        Assert.False(drop.IsConsumed);

        // Другой игрок с вместительным рюкзаком (50 кг) пробует подобрать — должно сработать, так как Claim был освобожден
        var normalInv = new Inventory(slotCount: 10, maxWeight: 50.0);
        var successPickup = service.TryPickupGroundItem(3, drop.DropId, normalInv, new Vector3D(0f, 0f, 0f), 2.0f, out var item);
        Assert.True(successPickup);
        Assert.NotNull(item);
        Assert.Equal(2, normalInv.CountOf("pistol"));
    }

    [Fact]
    public void ExecuteAtomicTrade_TransfersItemsAtomically()
    {
        var service = new AtomicInventoryTransactionService();
        var invA = new Inventory(slotCount: 10, maxWeight: 50);
        invA.Add("bread", 5);

        var invB = new Inventory(slotCount: 10, maxWeight: 50);
        invB.Add("water", 3);

        var offerA = new List<ItemStack> { new ItemStack { ItemId = "bread", Quantity = 2 } };
        var offerB = new List<ItemStack> { new ItemStack { ItemId = "water", Quantity = 1 } };

        var ok = service.ExecuteAtomicTrade(101, invA, offerA, 102, invB, offerB);

        Assert.True(ok);
        Assert.Equal(3, invA.CountOf("bread"));
        Assert.Equal(1, invA.CountOf("water"));
        Assert.Equal(2, invB.CountOf("bread"));
        Assert.Equal(2, invB.CountOf("water"));
    }

    [Fact]
    public void ExecuteAtomicTrade_WhenInsufficientItems_AbortsWithoutChanges()
    {
        var service = new AtomicInventoryTransactionService();
        var invA = new Inventory(slotCount: 10, maxWeight: 50);
        invA.Add("bread", 1);

        var invB = new Inventory(slotCount: 10, maxWeight: 50);
        invB.Add("water", 5);

        var offerA = new List<ItemStack> { new ItemStack { ItemId = "bread", Quantity = 5 } };
        var offerB = new List<ItemStack> { new ItemStack { ItemId = "water", Quantity = 2 } };

        var ok = service.ExecuteAtomicTrade(101, invA, offerA, 102, invB, offerB);

        Assert.False(ok);
        Assert.Equal(1, invA.CountOf("bread"));
        Assert.Equal(0, invA.CountOf("water"));
        Assert.Equal(0, invB.CountOf("bread"));
        Assert.Equal(5, invB.CountOf("water"));
    }
}
