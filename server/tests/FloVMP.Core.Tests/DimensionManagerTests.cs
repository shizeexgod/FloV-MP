using FloVMP.Core.World;
using Xunit;

namespace FloVMP.Core.Tests
{
    public class DimensionManagerTests
    {
        [Fact]
        public void Defaults_IncludeGlobalAdminJailAndEventDimensions()
        {
            var manager = new DimensionManager();
            var dims = manager.GetAllDimensions();

            Assert.Contains(dims, d => d.Id == DimensionManager.GlobalDimension && d.Type == DimensionType.Global);
            Assert.Contains(dims, d => d.Id == DimensionManager.AdminJailDimension && d.Type == DimensionType.AdminJail);
            Assert.Contains(dims, d => d.Id == DimensionManager.EventDimension && d.Type == DimensionType.EventZone);
        }

        [Fact]
        public void AllocateHousingDimension_AssignsUniqueDimensionInRange()
        {
            var manager = new DimensionManager();
            var dim1 = manager.AllocateHousingDimension(101, "Moscow City Tower 42", "acc-101");
            var dim2 = manager.AllocateHousingDimension(102, "Barvikha Villa 7", "acc-102");

            Assert.Equal(DimensionManager.HousingMin + 101, dim1);
            Assert.Equal(DimensionManager.HousingMin + 102, dim2);
            Assert.NotEqual(dim1, dim2);

            var info = manager.Get(dim1);
            Assert.NotNull(info);
            Assert.Equal(DimensionType.Housing, info.Type);
            Assert.Equal("acc-101", info.OwnerIdentifier);
        }

        [Fact]
        public void AllocateTemporaryInstance_ProvidesNewIncrementalIds()
        {
            var manager = new DimensionManager();
            var inst1 = manager.AllocateTemporaryInstance("Driving School Test 1");
            var inst2 = manager.AllocateTemporaryInstance("Driving School Test 2");

            Assert.True(inst1 >= DimensionManager.InstanceMin);
            Assert.True(inst2 > inst1);

            var info = manager.Get(inst1);
            Assert.NotNull(info);
            Assert.Equal(DimensionType.PrivateInstance, info.Type);
        }

        [Fact]
        public void TrackEntityMove_UpdatesEntityCountsAccurately()
        {
            var manager = new DimensionManager();
            var dim = manager.AllocateHousingDimension(200, "Penthouse", "acc-200");

            manager.TrackEntityMove(1001, DimensionManager.GlobalDimension, dim);
            manager.TrackEntityMove(1002, DimensionManager.GlobalDimension, dim);

            Assert.Equal(2, manager.GetEntityCountInDimension(dim));

            manager.TrackEntityMove(1001, dim, DimensionManager.GlobalDimension);
            Assert.Equal(1, manager.GetEntityCountInDimension(dim));
        }

        [Fact]
        public void Unregister_SystemDimensionsCannotBeDeleted()
        {
            var manager = new DimensionManager();
            Assert.False(manager.Unregister(DimensionManager.GlobalDimension));
            Assert.False(manager.Unregister(DimensionManager.AdminJailDimension));
        }
    }
}
