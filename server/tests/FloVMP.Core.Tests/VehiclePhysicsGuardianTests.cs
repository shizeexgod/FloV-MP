using System;
using FloVMP.Core.AntiCheat;
using Xunit;

namespace FloVMP.Core.Tests;

public class VehiclePhysicsGuardianTests
{
    [Fact]
    public void NormalDriving_ProducesNoViolations()
    {
        var guardian = new VehiclePhysicsGuardian();
        var start = DateTime.UtcNow;
        guardian.RegisterVehicle(1, new Vector3D(0, 0, 0));

        // Нормальное плавное ускорение до 30 м/с (~108 км/ч)
        var res1 = guardian.ValidateTick(1, 100, new Vector3D(5, 0, 0), new Vector3D(10, 0, 0), 1000f, false, start.AddSeconds(1));
        var res2 = guardian.ValidateTick(1, 100, new Vector3D(25, 0, 0), new Vector3D(20, 0, 0), 1000f, false, start.AddSeconds(2));

        Assert.Equal(VehicleViolationType.None, res1);
        Assert.Equal(VehicleViolationType.None, res2);
    }

    [Fact]
    public void SuperAcceleration_IsFlagged()
    {
        var guardian = new VehiclePhysicsGuardian { MaxAccelerationMps2 = 30.0f };
        var start = DateTime.UtcNow;
        guardian.RegisterVehicle(1, new Vector3D(0, 0, 0));

        // Мгновенный скачок скорости от 0 до 60 м/с за 0.1 сек (ускорение 600 м/с²)
        guardian.ValidateTick(1, 100, new Vector3D(0, 0, 0), new Vector3D(0, 0, 0), 1000f, false, start);
        var violation = guardian.ValidateTick(1, 100, new Vector3D(6, 0, 0), new Vector3D(60, 0, 0), 1000f, false, start.AddSeconds(0.1));

        Assert.Equal(VehicleViolationType.SuperAcceleration, violation);
    }

    [Fact]
    public void FlyCar_AscendingInAir_IsFlagged()
    {
        var guardian = new VehiclePhysicsGuardian();
        var start = DateTime.UtcNow;
        guardian.RegisterVehicle(2, new Vector3D(100, 100, 10));

        // 3 секунды авто летит вверх со скоростью Vz = 12 м/с
        guardian.ValidateTick(2, 101, new Vector3D(100, 100, 10), new Vector3D(10, 0, 12), 1000f, isInAir: true, start);
        guardian.ValidateTick(2, 101, new Vector3D(110, 100, 22), new Vector3D(10, 0, 12), 1000f, isInAir: true, start.AddSeconds(1));
        guardian.ValidateTick(2, 101, new Vector3D(120, 100, 34), new Vector3D(10, 0, 12), 1000f, isInAir: true, start.AddSeconds(2));
        var violation = guardian.ValidateTick(2, 101, new Vector3D(130, 100, 46), new Vector3D(10, 0, 12), 1000f, isInAir: true, start.AddSeconds(3));

        Assert.Equal(VehicleViolationType.FlyCar, violation);
    }

    [Fact]
    public void VehicleTeleport_IsFlagged()
    {
        var guardian = new VehiclePhysicsGuardian { MaxTeleportDistance = 50.0f };
        var start = DateTime.UtcNow;
        guardian.RegisterVehicle(3, new Vector3D(0, 0, 0));

        guardian.ValidateTick(3, 102, new Vector3D(0, 0, 0), Vector3D.Zero, 1000f, false, start);
        // Скачок на 200 метров за 0.2 секунды
        var violation = guardian.ValidateTick(3, 102, new Vector3D(200, 0, 0), Vector3D.Zero, 1000f, false, start.AddSeconds(0.2));

        Assert.Equal(VehicleViolationType.VehicleTeleport, violation);
    }
}
