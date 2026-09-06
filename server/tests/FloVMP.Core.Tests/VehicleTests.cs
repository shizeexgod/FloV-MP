using FloVMP.Core.Vehicles;
using Xunit;

namespace FloVMP.Core.Tests;

public sealed class VehicleTests
{
    [Fact]
    public void Owner_can_toggle_lock()
    {
        var veh = new VehicleData { Id = 1, OwnerAccountId = 10, IsLocked = true };

        var ok = veh.ToggleLock(10, adminLevel: 0, out var msg);
        Assert.True(ok);
        Assert.False(veh.IsLocked);
        Assert.Equal("Двери разблокированы", msg);

        veh.ToggleLock(10, adminLevel: 0, out _);
        Assert.True(veh.IsLocked);
    }

    [Fact]
    public void Non_owner_cannot_toggle_lock()
    {
        var veh = new VehicleData { Id = 1, OwnerAccountId = 10, IsLocked = true };

        var ok = veh.ToggleLock(99, adminLevel: 0, out var msg);
        Assert.False(ok);
        Assert.True(veh.IsLocked);
        Assert.Equal("У вас нет ключей от этого транспортного средства", msg);
    }

    [Fact]
    public void Admin_level_4_has_master_key()
    {
        var veh = new VehicleData { Id = 1, OwnerAccountId = 10, IsLocked = true };

        var ok = veh.ToggleLock(99, adminLevel: 4, out var msg);
        Assert.True(ok);
        Assert.False(veh.IsLocked);
    }

    [Fact]
    public void Cannot_start_engine_with_no_fuel()
    {
        var veh = new VehicleData { Id = 1, OwnerAccountId = 10, Fuel = 0.0f, EngineOn = false };

        var ok = veh.ToggleEngine(10, adminLevel: 0, out var msg);
        Assert.False(ok);
        Assert.False(veh.EngineOn);
        Assert.Contains("Бак пуст", msg);
    }

    [Fact]
    public void Fuel_consumption_turns_off_engine_when_depleted()
    {
        var veh = new VehicleData { Id = 1, OwnerAccountId = 10, Fuel = 0.5f, EngineOn = true };

        veh.ConsumeFuel(0.6f);
        Assert.Equal(0.0f, veh.Fuel);
        Assert.False(veh.EngineOn);
    }

    [Fact]
    public void VehicleService_registers_and_finds_by_plate_and_owner()
    {
        var svc = new VehicleService();
        var v1 = new VehicleData { Id = 1, Plate = "M777MM77", OwnerAccountId = 5 };
        var v2 = new VehicleData { Id = 2, Plate = "A111AA77", OwnerAccountId = 5 };
        var v3 = new VehicleData { Id = 3, Plate = "X999XX99", OwnerAccountId = 12 };

        svc.RegisterVehicle(v1);
        svc.RegisterVehicle(v2);
        svc.RegisterVehicle(v3);

        Assert.Equal(v1, svc.FindByPlate("m777mm77"));
        Assert.Equal(2, svc.GetVehiclesByOwner(5).Count);
        Assert.Single(svc.GetVehiclesByOwner(12));

        svc.UnregisterVehicle(1);
        Assert.Null(svc.FindByPlate("M777MM77"));
        Assert.Single(svc.GetVehiclesByOwner(5));
    }

    [Fact]
    public void VehicleService_UpdatePlate_UpdatesLookupAndRemovesStalePlate()
    {
        var svc = new VehicleService();
        var v = new VehicleData { Id = 10, Plate = "OLD_PLATE", OwnerAccountId = 1 };
        svc.RegisterVehicle(v);

        Assert.Equal(v, svc.FindByPlate("OLD_PLATE"));

        svc.UpdatePlate(v.Id, "NEW_PLATE");

        Assert.Equal("NEW_PLATE", v.Plate);
        Assert.Null(svc.FindByPlate("OLD_PLATE"));
        Assert.Equal(v, svc.FindByPlate("NEW_PLATE"));
        Assert.Equal(v, svc.FindByPlate("new_plate"));
    }
}