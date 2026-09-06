using System;
using FloVMP.Core.AntiCheat;
using FloVMP.Core.Sync;
using Xunit;

namespace FloVMP.Core.Tests;

public sealed class DeadReckoningInterpolatorTests
{
    [Fact]
    public void ExtrapolatePosition_PredictsMovementBasedOnVelocity()
    {
        var interpolator = new DeadReckoningInterpolator();

        // Entity at (10, 20, 0) moving at +10 m/s along X at T=1000ms
        interpolator.RecordSnapshot(
            entityId: 42,
            timestampMs: 1000,
            position: new Vector3D(10f, 20f, 0f),
            velocity: new Vector3D(10f, 0f, 0f),
            rotation: Vector3D.Zero
        );

        // Extrapolate at T=1200ms (0.2s later) -> 10 + 10 * 0.2 = 12
        var extrapolated = interpolator.ExtrapolatePosition(42, currentTimestampMs: 1200);

        Assert.Equal(12.0f, extrapolated.X, precision: 2);
        Assert.Equal(20.0f, extrapolated.Y, precision: 2);
        Assert.Equal(0.0f, extrapolated.Z, precision: 2);
    }

    [Fact]
    public void HermiteInterpolate_EndpointsMatch()
    {
        var p0 = new Vector3D(0f, 0f, 0f);
        var v0 = new Vector3D(10f, 0f, 0f);
        var p1 = new Vector3D(20f, 0f, 0f);
        var v1 = new Vector3D(10f, 0f, 0f);

        var start = DeadReckoningInterpolator.HermiteInterpolate(p0, v0, p1, v1, 0.0f);
        var end = DeadReckoningInterpolator.HermiteInterpolate(p0, v0, p1, v1, 1.0f);
        var mid = DeadReckoningInterpolator.HermiteInterpolate(p0, v0, p1, v1, 0.5f);

        Assert.Equal(0f, start.X, precision: 3);
        Assert.Equal(20f, end.X, precision: 3);
        Assert.Equal(10f, mid.X, precision: 3);
    }

    [Fact]
    public void GetRewoundPosition_RewindsToExactTimestamp()
    {
        var interpolator = new DeadReckoningInterpolator();

        interpolator.RecordSnapshot(1, 1000, new Vector3D(0f, 0f, 0f), new Vector3D(10f, 0f, 0f), Vector3D.Zero);
        interpolator.RecordSnapshot(1, 1100, new Vector3D(10f, 0f, 0f), new Vector3D(10f, 0f, 0f), Vector3D.Zero);
        interpolator.RecordSnapshot(1, 1200, new Vector3D(20f, 0f, 0f), new Vector3D(10f, 0f, 0f), Vector3D.Zero);

        // Server time is 1200ms, rewind to 1050ms (between 1000 and 1100)
        var rewound = interpolator.GetRewoundPosition(1, targetTimestampMs: 1050, currentServerTimestampMs: 1200);

        Assert.NotNull(rewound);
        // Halfway between 0 and 10 with constant velocity -> exactly 5.0
        Assert.Equal(5.0f, rewound.Value.X, precision: 2);
    }

    [Fact]
    public void GetRewoundPosition_EnforcesMaxRewindLimit()
    {
        var interpolator = new DeadReckoningInterpolator();

        interpolator.RecordSnapshot(1, 500, new Vector3D(0f, 0f, 0f), Vector3D.Zero, Vector3D.Zero);
        interpolator.RecordSnapshot(1, 1500, new Vector3D(100f, 0f, 0f), Vector3D.Zero, Vector3D.Zero);

        // Current time is 1500ms, max rewind is 500ms -> cannot rewind prior to 1000ms
        var rewound = interpolator.GetRewoundPosition(
            entityId: 1,
            targetTimestampMs: 500, // Client claims they shot at 500ms (1000ms ago)
            currentServerTimestampMs: 1500,
            maxRewindMs: 500 // Min allowed is 1000ms
        );

        Assert.NotNull(rewound);
        // At 1000ms (halfway between 500 and 1500), position is ~50.0f
        Assert.Equal(50.0f, rewound.Value.X, precision: 1);
    }

    [Fact]
    public void ValidateHit_ValidatesLegitimateHitWithLagCompensation()
    {
        var interpolator = new DeadReckoningInterpolator();

        // Target was at (20, 0, 0) at T=1000ms, moving at +20 m/s along Y
        interpolator.RecordSnapshot(
            entityId: 99,
            timestampMs: 1000,
            position: new Vector3D(20f, 0f, 0f),
            velocity: new Vector3D(0f, 20f, 0f),
            rotation: Vector3D.Zero
        );

        // By T=1200ms, target moved to (20, 4, 0)
        interpolator.RecordSnapshot(
            entityId: 99,
            timestampMs: 1200,
            position: new Vector3D(20f, 4f, 0f),
            velocity: new Vector3D(0f, 20f, 0f),
            rotation: Vector3D.Zero
        );

        // Shooter fired at T=1000ms from (0, 0, 0) along vector (1, 0, 0) directly towards (20, 0, 0)
        // Current server time is 1200ms (shooter has 200ms round-trip latency)
        var result = interpolator.ValidateHit(
            shooterId: 1,
            targetId: 99,
            clientShotTimestampMs: 1000,
            rayOrigin: new Vector3D(0f, 0f, 0f),
            rayDirection: new Vector3D(1f, 0f, 0f),
            maxRange: 50.0f,
            hitboxRadius: 1.0f,
            currentServerTimestampMs: 1200,
            maxRewindMs: 500
        );

        Assert.True(result.IsHit);
        Assert.Null(result.Reason);
        Assert.True(result.DistanceToRay < 0.05f);
        Assert.Equal(20.0f, result.RewoundPosition.X, precision: 2);
    }

    [Fact]
    public void ValidateHit_RejectsShotWhenAimIsOff()
    {
        var interpolator = new DeadReckoningInterpolator();

        interpolator.RecordSnapshot(99, 1000, new Vector3D(20f, 0f, 0f), Vector3D.Zero, Vector3D.Zero);

        // Shooter aims in completely wrong direction: (0, 1, 0) instead of (1, 0, 0)
        var result = interpolator.ValidateHit(
            shooterId: 1,
            targetId: 99,
            clientShotTimestampMs: 1000,
            rayOrigin: new Vector3D(0f, 0f, 0f),
            rayDirection: new Vector3D(0f, 1f, 0f), // Aiming 90 degrees away
            maxRange: 50.0f,
            hitboxRadius: 1.0f,
            currentServerTimestampMs: 1000
        );

        Assert.False(result.IsHit);
        Assert.Equal("Bullet missed hitbox", result.Reason);
    }
}
