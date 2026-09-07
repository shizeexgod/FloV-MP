using System;
using FloVMP.Core.AntiCheat;
using FloVMP.Core.Spatial;
using Xunit;

namespace FloVMP.Core.Tests;

public class AdaptiveTickManagerTests
{
    [Fact]
    public void CombatOrHighSpeed_AlwaysSyncsEveryTick_60Hz()
    {
        var manager = new AdaptiveTickManager<int>();
        manager.RegisterEntity(1, new Vector3D(0, 0, 0));
        manager.RegisterEntity(2, new Vector3D(5, 0, 0));

        // Включаем бой
        manager.UpdateEntityState(1, new Vector3D(0, 0, 0), Vector3D.Zero, isInCombat: true);

        // Проверяем 10 последовательных тиков
        for (long tick = 1; tick <= 10; tick++)
        {
            Assert.True(manager.ShouldSyncThisTick(1, 2, tick));
        }

        Assert.Equal(60, manager.GetEffectiveTickRate(1));
    }

    [Fact]
    public void HighSpeedVehicle_GetsFull60Hz()
    {
        var manager = new AdaptiveTickManager<int>();
        manager.RegisterEntity(10, new Vector3D(100, 100, 0));
        manager.RegisterEntity(20, new Vector3D(110, 100, 0));

        // Скорость 20 м/с (> 15 м/с порога)
        manager.UpdateEntityState(10, new Vector3D(100, 100, 0), new Vector3D(20, 0, 0), isInCombat: false);

        for (long tick = 1; tick <= 10; tick++)
        {
            Assert.True(manager.ShouldSyncThisTick(10, 20, tick));
        }

        Assert.Equal(60, manager.GetEffectiveTickRate(10));
    }

    [Fact]
    public void DistantPassiveEntity_ScalesDownTickrate()
    {
        var manager = new AdaptiveTickManager<int>();
        manager.RegisterEntity(1, new Vector3D(0, 0, 0));
        manager.RegisterEntity(2, new Vector3D(80, 0, 0)); // 80 метров (> 50м FarDistanceThreshold)

        // Пассивное состояние
        manager.UpdateEntityState(1, new Vector3D(0, 0, 0), Vector3D.Zero, isInCombat: false);

        int syncCount = 0;
        for (long tick = 1; tick <= 10; tick++)
        {
            if (manager.ShouldSyncThisTick(1, 2, tick))
            {
                syncCount++;
            }
        }

        // При 30 Hz sync должен происходить примерно 5 раз за 10 тиков
        Assert.True(syncCount < 10, "Пассивный удаленный объект не должен слать пакеты каждый тик");
        Assert.Equal(30, manager.GetEffectiveTickRate(1, distanceToViewer: 80f));
    }

    [Fact]
    public void DifferentDimensions_NeverSync()
    {
        var manager = new AdaptiveTickManager<int>();
        manager.RegisterEntity(1, new Vector3D(10, 10, 0), dimension: 0);
        manager.RegisterEntity(2, new Vector3D(10, 10, 0), dimension: 5); // Другой дименшн

        for (long tick = 1; tick <= 10; tick++)
        {
            Assert.False(manager.ShouldSyncThisTick(1, 2, tick));
        }
    }
}
