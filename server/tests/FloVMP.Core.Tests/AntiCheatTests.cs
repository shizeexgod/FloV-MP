using System;
using System.Collections.Generic;
using FloVMP.Core.AntiCheat;
using Xunit;

namespace FloVMP.Core.Tests;

public class AntiCheatTests
{
    [Fact]
    public void Normal_Walking_And_Sprinting_Is_Permitted()
    {
        var ac = new AntiCheatService();
        var t0 = new DateTime(2026, 9, 6, 12, 0, 0, DateTimeKind.Utc);
        ac.GetOrCreateState(1, "Player1", new Vector3D(0, 0, 0), t0);

        // Move 8 meters in 1 second (~8 m/s, sprint)
        var t1 = t0.AddSeconds(1.0);
        bool valid = ac.CheckMovement(1, new Vector3D(8, 0, 0), inVehicle: false, timestamp: t1);

        Assert.True(valid);
    }

    [Fact]
    public void OnFoot_SpeedHack_Is_Detected()
    {
        var ac = new AntiCheatService();
        var t0 = new DateTime(2026, 9, 6, 12, 0, 0, DateTimeKind.Utc);
        ac.GetOrCreateState(1, "Hacker", new Vector3D(0, 0, 0), t0);

        string? violation = null;
        AntiCheatAction action = AntiCheatAction.LogOnly;
        ac.OnViolationDetected += (id, reason, act) =>
        {
            violation = reason;
            action = act;
        };

        // Move 60 meters in 1 second on foot (60 m/s > 12.5 m/s)
        var t1 = t0.AddSeconds(1.0);
        bool valid = ac.CheckMovement(1, new Vector3D(60, 0, 0), inVehicle: false, timestamp: t1);

        Assert.False(valid);
        Assert.NotNull(violation);
        Assert.Contains("Превышение скорости пешком", violation);
    }

    [Fact]
    public void Vehicle_Speed_Within_Limit_Is_Permitted()
    {
        var ac = new AntiCheatService();
        var t0 = new DateTime(2026, 9, 6, 12, 0, 0, DateTimeKind.Utc);
        ac.GetOrCreateState(1, "Racer", new Vector3D(0, 0, 0), t0);

        // Move 50 meters in 1 second in vehicle (50 m/s ~ 180 km/h, allowed)
        var t1 = t0.AddSeconds(1.0);
        bool valid = ac.CheckMovement(1, new Vector3D(50, 0, 0), inVehicle: true, timestamp: t1);

        Assert.True(valid);
    }

    [Fact]
    public void Vehicle_Fly_SpeedHack_Is_Detected()
    {
        var ac = new AntiCheatService();
        var t0 = new DateTime(2026, 9, 6, 12, 0, 0, DateTimeKind.Utc);
        ac.GetOrCreateState(1, "VehicleFlyHacker", new Vector3D(0, 0, 0), t0);

        string? violation = null;
        ac.OnViolationDetected += (_, reason, _) => violation = reason;

        // Move 180 meters in 1 second (180 m/s > 125 m/s, speedhack without teleport jump)
        var t1 = t0.AddSeconds(1.0);
        bool valid = ac.CheckMovement(1, new Vector3D(180, 0, 0), inVehicle: true, timestamp: t1);

        Assert.False(valid);
        Assert.NotNull(violation);
        Assert.Contains("Превышение скорости транспорта", violation);
    }

    [Fact]
    public void Sudden_Teleport_Is_Detected()
    {
        var ac = new AntiCheatService();
        var t0 = new DateTime(2026, 9, 6, 12, 0, 0, DateTimeKind.Utc);
        ac.GetOrCreateState(1, "Teleporter", new Vector3D(100, 200, 30), t0);

        string? violation = null;
        AntiCheatAction action = AntiCheatAction.LogOnly;
        ac.OnViolationDetected += (_, reason, act) =>
        {
            violation = reason;
            action = act;
        };

        // Jump 500 meters in 0.5 sec
        var t1 = t0.AddSeconds(0.5);
        bool valid = ac.CheckMovement(1, new Vector3D(600, 200, 30), inVehicle: false, timestamp: t1);

        Assert.False(valid);
        Assert.NotNull(violation);
        Assert.Contains("несанкционированный телепорт", violation);
        Assert.Equal(AntiCheatAction.TeleportBack, action);
    }

    [Fact]
    public void Legitimate_Admin_Teleport_Is_Exempt()
    {
        var ac = new AntiCheatService();
        var t0 = new DateTime(2026, 9, 6, 12, 0, 0, DateTimeKind.Utc);
        ac.GetOrCreateState(1, "Admin", new Vector3D(100, 200, 30), t0);

        // Notify legit teleport
        var targetPos = new Vector3D(1500, 2500, 40);
        ac.NotifyLegitimateTeleport(1, targetPos, t0);

        var t1 = t0.AddSeconds(0.2);
        bool valid = ac.CheckMovement(1, targetPos, inVehicle: false, timestamp: t1);

        Assert.True(valid);
    }

    [Fact]
    public void Blacklisted_Weapon_Is_Immediately_Disarmed()
    {
        var ac = new AntiCheatService();
        ac.GetOrCreateState(1, "Gunslinger", new Vector3D(0, 0, 0));

        string? violation = null;
        AntiCheatAction action = AntiCheatAction.LogOnly;
        ac.OnViolationDetected += (_, reason, act) =>
        {
            violation = reason;
            action = act;
        };

        uint minigunHash = 0x42BF8A85;
        var inventory = new HashSet<uint> { minigunHash }; // Even if cheater spawned it into inventory

        bool valid = ac.CheckWeapon(1, minigunHash, inventory);

        Assert.False(valid);
        Assert.Contains("запрещённое тяжелое оружие", violation);
        Assert.Equal(AntiCheatAction.Disarm, action);
    }

    [Fact]
    public void Unregistered_Weapon_Is_Flagged()
    {
        var ac = new AntiCheatService();
        ac.GetOrCreateState(1, "Player", new Vector3D(0, 0, 0));

        string? violation = null;
        ac.OnViolationDetected += (_, reason, _) => violation = reason;

        uint pistolHash = 0x1B06D571;
        var emptyInventory = new HashSet<uint>(); // Has no pistol in inventory

        bool valid = ac.CheckWeapon(1, pistolHash, emptyInventory);

        Assert.False(valid);
        Assert.Contains("отсутствующее в инвентаре", violation);
    }
}
