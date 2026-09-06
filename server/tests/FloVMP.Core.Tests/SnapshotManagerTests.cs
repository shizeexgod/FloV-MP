using FloVMP.Core.Database;
using Xunit;

namespace FloVMP.Core.Tests
{
    public class SnapshotManagerTests
    {
        [Fact]
        public void CaptureSnapshot_StoresAndReturnsSnapshotWithCorrectData()
        {
            var manager = new SnapshotManager();
            PlayerStateSnapshot? eventReceived = null;
            manager.OnSnapshotCreated += s => eventReceived = s;

            var snapshot = manager.CaptureSnapshot(
                playerId: 42,
                characterName: "Mikhail_D",
                bank: 1_500_000,
                cash: 50_000,
                dirtyCash: 12_000,
                dimension: 0,
                x: 10.5f,
                y: -20.2f,
                z: 30.1f,
                heading: 180f,
                health: 100,
                armor: 50,
                inventoryJson: "[{\"id\":1,\"count\":5}]",
                licenses: new[] { "driver", "weapon" },
                ownedVehicles: new[] { "elegy", "sultan" },
                reason: "PreTradeSafety"
            );

            Assert.NotNull(snapshot);
            Assert.Equal(42ul, snapshot.PlayerId);
            Assert.Equal("Mikhail_D", snapshot.CharacterName);
            Assert.Equal(1_500_000, snapshot.BankBalance);
            Assert.Equal(50_000, snapshot.CashBalance);
            Assert.Equal(12_000, snapshot.DirtyCashBalance);
            Assert.Equal(0, snapshot.Dimension);
            Assert.Equal(10.5f, snapshot.PositionX);
            Assert.Equal("PreTradeSafety", snapshot.Reason);
            Assert.Contains("driver", snapshot.Licenses);
            Assert.Contains("elegy", snapshot.OwnedVehicles);

            Assert.Same(snapshot, eventReceived);

            var list = manager.GetSnapshots(42);
            Assert.Single(list);
            Assert.Equal(snapshot.Id, list[0].Id);
        }

        [Fact]
        public void RingBuffer_PrunesOldestWhenExceedingLimit()
        {
            var manager = new SnapshotManager { MaxSnapshotsPerPlayer = 3 };

            var s1 = manager.CaptureSnapshot(100, "User1", 100, 0, 0, 0, 0, 0, 0, reason: "Snap1");
            var s2 = manager.CaptureSnapshot(100, "User1", 200, 0, 0, 0, 0, 0, 0, reason: "Snap2");
            var s3 = manager.CaptureSnapshot(100, "User1", 300, 0, 0, 0, 0, 0, 0, reason: "Snap3");
            var s4 = manager.CaptureSnapshot(100, "User1", 400, 0, 0, 0, 0, 0, 0, reason: "Snap4");

            var snapshots = manager.GetSnapshots(100);
            Assert.Equal(3, snapshots.Count);

            // s1 was pruned as oldest
            Assert.Null(manager.GetSnapshot(100, s1.Id));
            Assert.NotNull(manager.GetSnapshot(100, s2.Id));
            Assert.NotNull(manager.GetSnapshot(100, s3.Id));
            Assert.NotNull(manager.GetSnapshot(100, s4.Id));
        }

        [Fact]
        public void RollbackToSnapshot_RestoresStateAndCreatesPreRollbackBackup()
        {
            var manager = new SnapshotManager();
            ulong restoredPlayerId = 0;
            PlayerStateSnapshot? restoredSnapshot = null;
            manager.OnSnapshotRestored += (id, s) =>
            {
                restoredPlayerId = id;
                restoredSnapshot = s;
            };

            var original = manager.CaptureSnapshot(200, "TraderBob", 1_000_000, 50_000, 0, 0, 10, 20, 30, reason: "CleanState");
            var exploitState = manager.CaptureSnapshot(200, "TraderBob", 999_999_999, 100_000, 0, 0, 10, 20, 30, reason: "AfterDupeBug");

            // Rollback to original clean state with backup of current state
            var result = manager.RollbackToSnapshot(200, original.Id, currentStateProvider: id =>
                new PlayerStateSnapshot
                {
                    PlayerId = id,
                    CharacterName = "TraderBob",
                    BankBalance = 999_999_999,
                    CashBalance = 100_000
                });

            Assert.NotNull(result);
            Assert.Equal(original.Id, result.Id);
            Assert.Equal(1_000_000, result.BankBalance);

            Assert.Equal(200ul, restoredPlayerId);
            Assert.Same(result, restoredSnapshot);

            // Verify a PreRollbackBackup snapshot was created in case admin needs to undo the rollback
            var history = manager.GetSnapshots(200);
            Assert.Contains(history, s => s.Reason.StartsWith("PreRollbackBackup"));
        }

        [Fact]
        public void ExportSnapshotsJson_ProducesValidNonEmptyJson()
        {
            var manager = new SnapshotManager();
            manager.CaptureSnapshot(300, "Alexey", 500_000, 20_000, 0, 0, 1, 2, 3);

            var json = manager.ExportSnapshotsJson(300);
            Assert.False(string.IsNullOrWhiteSpace(json));
            Assert.Contains("Alexey", json);
            Assert.Contains("500000", json);
        }

        [Fact]
        public void Clear_RemovesAllSnapshotsForPlayer()
        {
            var manager = new SnapshotManager();
            manager.CaptureSnapshot(400, "TestSubject", 10_000, 500, 0, 0, 0, 0, 0);
            Assert.Single(manager.GetSnapshots(400));

            manager.Clear(400);
            Assert.Empty(manager.GetSnapshots(400));
        }
    }
}
