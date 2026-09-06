using System.Collections.Generic;
using FloVMP.Core.AntiCheat;
using FloVMP.Core.Auth;
using FloVMP.Core.Housing;
using Xunit;

namespace FloVMP.Core.Tests;

public class HousingTests
{
    [Fact]
    public void Presets_LoadProperly()
    {
        var service = new HousingService(loadDefaultPresets: true);
        var props = service.GetAllProperties();

        Assert.Equal(4, props.Count);

        var city = service.GetProperty(1);
        Assert.NotNull(city);
        Assert.Contains("Башня Федерация", city.Address);
        Assert.Equal(25_000_000, city.Price);
        Assert.Equal(1001, city.Dimension);
        Assert.False(city.HasOwner);
        Assert.True(city.IsLocked);
    }

    [Fact]
    public void BuyProperty_SucceedsWithSufficientFunds()
    {
        var service = new HousingService(loadDefaultPresets: true);
        var acc = new Account { Id = 10, Username = "Oligarch", Bank = 50_000_000 };

        Assert.True(service.TryBuy(acc, 1, out var error));
        Assert.Empty(error);

        var prop = service.GetProperty(1)!;
        Assert.True(prop.HasOwner);
        Assert.Equal(acc.Id, prop.OwnerAccountId);
        Assert.Equal(25_000_000, acc.Bank); // 50M - 25M
        Assert.False(prop.IsLocked);

        // Cannot buy again
        var acc2 = new Account { Id = 11, Username = "Buyer2", Bank = 50_000_000 };
        Assert.False(service.TryBuy(acc2, 1, out var errAlready));
        Assert.Equal("У этого объекта уже есть владелец", errAlready);
    }

    [Fact]
    public void BuyProperty_FailsWithInsufficientFunds()
    {
        var service = new HousingService(loadDefaultPresets: true);
        var poorAcc = new Account { Id = 20, Username = "PoorPlayer", Bank = 10_000 };

        Assert.False(service.TryBuy(poorAcc, 1, out var error));
        Assert.Contains("Недостаточно средств", error);

        var prop = service.GetProperty(1)!;
        Assert.False(prop.HasOwner);
    }

    [Fact]
    public void SellProperty_GivesRefundAndClearsState()
    {
        var service = new HousingService(loadDefaultPresets: true);
        var acc = new Account { Id = 10, Username = "Owner", Bank = 30_000_000, Cash = 500_000 };

        service.TryBuy(acc, 1, out _);
        var prop = service.GetProperty(1)!;

        // Put 200k in safe
        service.TryDepositSafe(acc, 1, 200_000, out _);
        Assert.Equal(200_000, prop.SafeCash);

        // Add roommate
        service.TryAddRoommate(acc.Id, 1, 999, out _);

        // Sell: 75% of 25M is 18.75M + 200k safe = 18.95M refund
        long beforeBank = acc.Bank;
        Assert.True(service.TrySell(acc, 1, out long refund, out var err));
        Assert.Empty(err);
        Assert.Equal((long)(25_000_000 * 0.75), refund);
        Assert.Equal(beforeBank + refund + 200_000, acc.Bank);

        Assert.False(prop.HasOwner);
        Assert.True(prop.IsLocked);
        Assert.Equal(0, prop.SafeCash);
        Assert.Empty(prop.Roommates);
    }

    [Fact]
    public void LockAndRoommates_AuthorizationFlow()
    {
        var service = new HousingService(loadDefaultPresets: true);
        var owner = new Account { Id = 10, Bank = 20_000_000 };
        const int roommateId = 55;
        const int intruderId = 66;

        service.TryBuy(owner, 2, out _); // Buy ЖК Тверской (8.5M)
        var prop = service.GetProperty(2)!;

        // Intruder cannot lock
        Assert.False(service.TryToggleLock(intruderId, 2, out _, out var errIntruder));
        Assert.Equal("У вас нет ключей от этого объекта", errIntruder);

        // Add roommate
        Assert.True(service.TryAddRoommate(owner.Id, 2, roommateId, out _));

        // Roommate can toggle lock
        Assert.True(service.TryToggleLock(roommateId, 2, out bool locked, out _));
        Assert.True(locked);
        Assert.True(prop.IsLocked);

        // Remove roommate
        Assert.True(service.TryRemoveRoommate(owner.Id, 2, roommateId, out _));
        Assert.False(service.TryToggleLock(roommateId, 2, out _, out _));
    }

    [Fact]
    public void Safe_DepositAndWithdraw()
    {
        var service = new HousingService(loadDefaultPresets: true);
        var owner = new Account { Id = 10, Bank = 10_000_000, Cash = 100_000 };
        service.TryBuy(owner, 4, out _); // Гараж за 1.2M

        // Deposit 80k
        Assert.True(service.TryDepositSafe(owner, 4, 80_000, out _));
        Assert.Equal(20_000, owner.Cash);
        Assert.Equal(80_000, service.GetProperty(4)!.SafeCash);

        // Over-withdrawal fails
        Assert.False(service.TryWithdrawSafe(owner, 4, 150_000, out var errOver));
        Assert.Contains("В сейфе недостаточно средств", errOver);

        // Withdraw 50k
        Assert.True(service.TryWithdrawSafe(owner, 4, 50_000, out _));
        Assert.Equal(70_000, owner.Cash);
        Assert.Equal(30_000, service.GetProperty(4)!.SafeCash);
    }

    [Fact]
    public void NearbyProperty_LocatesProperly()
    {
        var service = new HousingService(loadDefaultPresets: true);
        var prop = service.GetProperty(1)!;

        // Position exactly at entrance
        var nearby = service.GetNearbyProperty(prop.EntrancePosition, radius: 2.0f);
        Assert.NotNull(nearby);
        Assert.Equal(1, nearby.Id);

        // Position far away
        var far = service.GetNearbyProperty(new Vector3D(5000f, 5000f, 0f));
        Assert.Null(far);
    }
}
