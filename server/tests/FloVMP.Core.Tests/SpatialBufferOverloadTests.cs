using FloVMP.Core.AntiCheat;
using FloVMP.Core.Spatial;
using Xunit;

namespace FloVMP.Core.Tests;

/// <summary>
/// Перегрузки с буфером вызывающей стороны.
///
/// Они существуют ради горячего пути: запрос соседей делается для каждого
/// игрока каждый тик, и список на каждый вызов давал десятки мегабайт мусора
/// в минуту — то есть паузы сборщика прямо в игровом тике (нагрузочный стенд:
/// 83 МБ против 3 МБ за один и тот же прогон). Раз появилась вторая реализация
/// одного и того же поиска, она обязана давать РОВНО тот же ответ, что и
/// старая, иначе оптимизация превращается в тихий баг стриминга.
/// </summary>
public class SpatialBufferOverloadTests
{
    private static SpatialHashGrid<ulong> BuildGrid(int count, int seed = 7)
    {
        var grid = new SpatialHashGrid<ulong>(cellSize: 64f);
        var rng = new Random(seed);
        for (var i = 1; i <= count; i++)
        {
            grid.InsertOrUpdate((ulong)i,
                new Vector3D((float)(rng.NextDouble() * 600 - 300),
                             (float)(rng.NextDouble() * 600 - 300),
                             (float)(rng.NextDouble() * 40)),
                dimension: i % 7 == 0 ? 1 : 0);
        }
        return grid;
    }

    [Fact]
    public void FindInRadius_BufferOverload_MatchesAllocatingOne()
    {
        var grid = BuildGrid(400);
        var center = new Vector3D(0, 0, 10);
        var buffer = new List<ulong>();

        foreach (var radius in new[] { 10f, 50f, 150f, 400f })
        {
            var expected = grid.FindInRadius(center, radius).OrderBy(x => x).ToArray();
            grid.FindInRadius(center, radius, 0, buffer);
            Assert.Equal(expected, buffer.OrderBy(x => x).ToArray());
        }
    }

    [Fact]
    public void FindInRadius_BufferIsClearedBetweenCalls()
    {
        // Буфер переиспользуется между тиками. Если он не очищается, соседи
        // прошлого тика останутся видны в этом — игроки «призраки».
        var grid = BuildGrid(200);
        var buffer = new List<ulong>();

        grid.FindInRadius(new Vector3D(0, 0, 0), 500f, 0, buffer);
        var wide = buffer.Count;
        Assert.True(wide > 0);

        grid.FindInRadius(new Vector3D(10_000, 10_000, 0), 5f, 0, buffer);
        Assert.Empty(buffer);
    }

    [Fact]
    public void FindInRadiusWithPositions_ReturnsSameEntitiesAndCorrectPositions()
    {
        var grid = BuildGrid(300);
        var center = new Vector3D(20, -15, 5);
        var buffer = new List<(ulong Entity, Vector3D Position, int Dimension)>();

        var expected = grid.FindInRadius(center, 120f).OrderBy(x => x).ToArray();
        grid.FindInRadiusWithPositions(center, 120f, 0, buffer);

        Assert.Equal(expected, buffer.Select(b => b.Entity).OrderBy(x => x).ToArray());

        // Позиция обязана совпадать с тем, что отдаёт TryGetPosition — ради
        // экономии одной блокировки на соседа мы берём её прямо из записи сетки.
        foreach (var (entity, pos, dim) in buffer)
        {
            Assert.True(grid.TryGetPosition(entity, out var expectedPos, out var expectedDim));
            Assert.Equal(expectedPos.X, pos.X);
            Assert.Equal(expectedPos.Y, pos.Y);
            Assert.Equal(expectedPos.Z, pos.Z);
            Assert.Equal(expectedDim, dim);
        }
    }

    [Fact]
    public void FindInRadiusWithPositions_RespectsDimension()
    {
        var grid = new SpatialHashGrid<ulong>(64f);
        grid.InsertOrUpdate(1, new Vector3D(0, 0, 0), dimension: 0);
        grid.InsertOrUpdate(2, new Vector3D(1, 1, 0), dimension: 5);

        var buffer = new List<(ulong, Vector3D, int)>();
        Assert.Equal(1, grid.FindInRadiusWithPositions(new Vector3D(0, 0, 0), 50f, 0, buffer));
        Assert.Equal(1UL, buffer[0].Item1);

        Assert.Equal(1, grid.FindInRadiusWithPositions(new Vector3D(0, 0, 0), 50f, 5, buffer));
        Assert.Equal(2UL, buffer[0].Item1);
    }

    [Fact]
    public void FindInRadiusWithDistance_BufferOverload_MatchesAllocatingOne()
    {
        var grid = BuildGrid(250);
        var center = new Vector3D(-40, 60, 12);
        var buffer = new List<(ulong Entity, float Distance)>();

        var expected = grid.FindInRadiusWithDistance(center, 200f);
        grid.FindInRadiusWithDistance(center, 200f, 0, buffer);

        Assert.Equal(expected.Count, buffer.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            Assert.Equal(expected[i].Entity, buffer[i].Entity);
            Assert.Equal(expected[i].Distance, buffer[i].Distance, 4);
        }
    }

    [Fact]
    public void FindInRadiusWithDistance_Unsorted_HasSameSetButSkipsSorting()
    {
        var grid = BuildGrid(250);
        var center = new Vector3D(0, 0, 0);
        var sorted = new List<(ulong Entity, float Distance)>();
        var unsorted = new List<(ulong Entity, float Distance)>();

        grid.FindInRadiusWithDistance(center, 250f, 0, sorted);
        grid.FindInRadiusWithDistance(center, 250f, 0, unsorted, use3D: true, sorted: false);

        Assert.Equal(sorted.Select(x => x.Entity).OrderBy(x => x),
                     unsorted.Select(x => x.Entity).OrderBy(x => x));

        // Отсортированный результат действительно отсортирован — голосу порядок
        // не нужен, но остальным потребителям он обещан документацией.
        for (var i = 1; i < sorted.Count; i++)
            Assert.True(sorted[i - 1].Distance <= sorted[i].Distance);
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(0f)]
    [InlineData(-5f)]
    public void BufferOverloads_RejectBadRadius_AndLeaveBufferEmpty(float radius)
    {
        var grid = BuildGrid(50);
        var buffer = new List<ulong> { 999 }; // заведомо мусор в буфере

        Assert.Equal(0, grid.FindInRadius(new Vector3D(0, 0, 0), radius, 0, buffer));
        Assert.Empty(buffer);
    }

    [Fact]
    public void BufferOverloads_RejectNaNCenter()
    {
        var grid = BuildGrid(50);
        var buffer = new List<ulong>();
        Assert.Equal(0, grid.FindInRadius(new Vector3D(float.NaN, 0, 0), 100f, 0, buffer));
        Assert.Equal(0, grid.FindInRadiusWithPositions(
            new Vector3D(0, float.PositiveInfinity, 0), 100f, 0,
            new List<(ulong, Vector3D, int)>()));
    }

    [Fact]
    public void BufferOverloads_RejectNullBuffer()
    {
        var grid = BuildGrid(10);
        Assert.Throws<ArgumentNullException>(() =>
            grid.FindInRadius(new Vector3D(0, 0, 0), 10f, 0, null!));
    }

    [Fact]
    public void FilterVisibleInto_MatchesFilterVisible()
    {
        var occlusion = new OcclusionCullingService { DefaultMaxDistance = 100f };
        occlusion.RegisterZone("interior", new Vector3D(10, 10, 0), new Vector3D(30, 30, 10));

        var candidates = new List<(ulong Item, Vector3D Pos, int Dim)>
        {
            (1, new Vector3D(5, 5, 0), 0),
            (2, new Vector3D(20, 20, 5), 0),   // внутри зоны
            (3, new Vector3D(500, 500, 0), 0), // за пределами дистанции
            (4, new Vector3D(6, 6, 0), 3),     // другое измерение
        };

        var viewer = new Vector3D(0, 0, 0);
        var heading = new Vector3D(1, 0, 0);

        var expected = occlusion.FilterVisible(viewer, heading, 0, candidates);
        var buffer = new List<ulong> { 777 };
        var count = occlusion.FilterVisibleInto(viewer, heading, 0, candidates, buffer);

        Assert.Equal(expected.Count, count);
        Assert.Equal(expected, buffer);
    }

    [Fact]
    public void FilterVisibleInto_RejectsNullBuffer()
    {
        var occlusion = new OcclusionCullingService();
        Assert.Throws<ArgumentNullException>(() => occlusion.FilterVisibleInto(
            new Vector3D(0, 0, 0), new Vector3D(1, 0, 0), 0,
            Array.Empty<(ulong, Vector3D, int)>(), null!));
    }
}
