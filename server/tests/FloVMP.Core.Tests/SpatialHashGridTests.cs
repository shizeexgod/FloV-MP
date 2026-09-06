using FloVMP.Core.AntiCheat;
using FloVMP.Core.Spatial;
using Xunit;

namespace FloVMP.Core.Tests
{
    public class SpatialHashGridTests
    {
        [Fact]
        public void InsertAndFind_ReturnsEntitiesWithinSpecifiedRadius()
        {
            var grid = new SpatialHashGrid<ulong>(cellSize: 64.0f);

            grid.InsertOrUpdate(101, new Vector3D(10f, 10f, 0f));
            grid.InsertOrUpdate(102, new Vector3D(12f, 11f, 0f));
            grid.InsertOrUpdate(103, new Vector3D(500f, 500f, 0f)); // Далеко

            var nearby = grid.FindInRadius(new Vector3D(10f, 10f, 0f), radius: 5.0f);

            Assert.Equal(2, nearby.Count);
            Assert.Contains(101ul, nearby);
            Assert.Contains(102ul, nearby);
            Assert.DoesNotContain(103ul, nearby);
        }

        [Fact]
        public void DimensionIsolation_DoesNotReturnEntitiesInDifferentDimension()
        {
            var grid = new SpatialHashGrid<string>(cellSize: 64.0f);

            grid.InsertOrUpdate("Player_Dim0", new Vector3D(0f, 0f, 0f), dimension: 0);
            grid.InsertOrUpdate("Player_Dim1", new Vector3D(0f, 0f, 0f), dimension: 1);

            var inDim0 = grid.FindInRadius(new Vector3D(0f, 0f, 0f), radius: 10.0f, dimension: 0);
            var inDim1 = grid.FindInRadius(new Vector3D(0f, 0f, 0f), radius: 10.0f, dimension: 1);

            Assert.Single(inDim0);
            Assert.Equal("Player_Dim0", inDim0[0]);

            Assert.Single(inDim1);
            Assert.Equal("Player_Dim1", inDim1[0]);
        }

        [Fact]
        public void CellMigration_CorrectlyUpdatesBucketsWhenMovingAcrossCells()
        {
            var grid = new SpatialHashGrid<int>(cellSize: 64.0f);

            grid.InsertOrUpdate(1, new Vector3D(10f, 10f, 0f));
            Assert.Equal(1, grid.Count);

            // Перемещение в другой чанк
            grid.InsertOrUpdate(1, new Vector3D(300f, 300f, 0f));
            Assert.Equal(1, grid.Count);

            // В старой ячейке никого не должно остаться
            var oldNearby = grid.FindInRadius(new Vector3D(10f, 10f, 0f), radius: 20.0f);
            Assert.Empty(oldNearby);

            // В новой ячейке сущность находится
            var newNearby = grid.FindInRadius(new Vector3D(300f, 300f, 0f), radius: 20.0f);
            Assert.Single(newNearby);
            Assert.Equal(1, newNearby[0]);
        }

        [Fact]
        public void FindInRadiusWithDistance_ReturnsSortedByProximity()
        {
            var grid = new SpatialHashGrid<string>(cellSize: 64.0f);

            var center = new Vector3D(0f, 0f, 0f);
            grid.InsertOrUpdate("Far", new Vector3D(10f, 0f, 0f));
            grid.InsertOrUpdate("Closest", new Vector3D(2f, 0f, 0f));
            grid.InsertOrUpdate("Middle", new Vector3D(5f, 0f, 0f));

            var results = grid.FindInRadiusWithDistance(center, radius: 15.0f);

            Assert.Equal(3, results.Count);
            Assert.Equal("Closest", results[0].Entity);
            Assert.Equal(2f, results[0].Distance, precision: 1);

            Assert.Equal("Middle", results[1].Entity);
            Assert.Equal(5f, results[1].Distance, precision: 1);

            Assert.Equal("Far", results[2].Entity);
            Assert.Equal(10f, results[2].Distance, precision: 1);
        }

        [Fact]
        public void Remove_DeletesEntityAndCleansEmptyBucket()
        {
            var grid = new SpatialHashGrid<int>(cellSize: 64.0f);

            grid.InsertOrUpdate(99, new Vector3D(50f, 50f, 0f));
            Assert.Equal(1, grid.Count);

            bool removed = grid.Remove(99);
            Assert.True(removed);
            Assert.Equal(0, grid.Count);

            var nearby = grid.FindInRadius(new Vector3D(50f, 50f, 0f), radius: 10.0f);
            Assert.Empty(nearby);

            // Повторное удаление возвращает false
            Assert.False(grid.Remove(99));
        }
    }
}
