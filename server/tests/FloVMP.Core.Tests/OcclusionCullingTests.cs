using System.Collections.Generic;
using FloVMP.Core.AntiCheat;
using FloVMP.Core.Spatial;
using Xunit;

namespace FloVMP.Core.Tests;

public class OcclusionCullingTests
{
    [Fact]
    public void DifferentDimensions_IsAlwaysOccluded()
    {
        var service = new OcclusionCullingService();
        var viewer = new Vector3D(0, 0, 0);
        var target = new Vector3D(5, 0, 0);

        bool visible = service.IsVisible(viewer, new Vector3D(1, 0, 0), viewerDim: 0, target, targetDim: 1);
        Assert.False(visible);
    }

    [Fact]
    public void BeyondMaxDistance_IsCulled()
    {
        var service = new OcclusionCullingService { DefaultMaxDistance = 100.0f };
        var viewer = new Vector3D(0, 0, 0);
        var target = new Vector3D(150, 0, 0); // 150m > 100m

        bool visible = service.IsVisible(viewer, new Vector3D(1, 0, 0), 0, target, 0);
        Assert.False(visible);
    }

    [Fact]
    public void InsideOcclusionZone_WithoutEntrance_BlocksVisibility()
    {
        var service = new OcclusionCullingService();
        // Бункер/банк от (10,10,0) до (30,30,10)
        service.RegisterZone("bank_vault", new Vector3D(10, 10, 0), new Vector3D(30, 30, 10), dimension: 0);

        var viewerOutside = new Vector3D(0, 0, 0);
        var targetInside = new Vector3D(20, 20, 5);

        bool visible = service.IsVisible(viewerOutside, new Vector3D(1, 1, 0), 0, targetInside, 0);
        Assert.False(visible);
    }

    [Fact]
    public void InsideOcclusionZone_NearEntrance_AllowsVisibility()
    {
        var service = new OcclusionCullingService();
        // Бункер со входом в точке (10, 20, 0)
        service.RegisterZone("bunker", new Vector3D(10, 10, 0), new Vector3D(30, 30, 10), dimension: 0,
            entrance: new Vector3D(10, 20, 0), entranceRadius: 4.0f);

        var viewerAtEntrance = new Vector3D(11, 20, 0); // В пределах 4м от входа
        var targetInside = new Vector3D(15, 20, 1);

        bool visible = service.IsVisible(viewerAtEntrance, new Vector3D(1, 0, 0), 0, targetInside, 0);
        Assert.True(visible);
    }

    [Fact]
    public void HearingRadius_AlwaysVisibleEvenBehind()
    {
        var service = new OcclusionCullingService { HearingProximityRadius = 15.0f };
        var viewer = new Vector3D(0, 0, 0);
        var viewerHeading = new Vector3D(1, 0, 0); // Смотрит вперед
        var targetBehind = new Vector3D(-5, 0, 0); // Сзади на дистанции 5м (< 15м)

        bool visible = service.IsVisible(viewer, viewerHeading, 0, targetBehind, 0, useFovCheck: true);
        Assert.True(visible, "В упор шаги и звуки должны передаваться независимо от FOV");
    }
}
