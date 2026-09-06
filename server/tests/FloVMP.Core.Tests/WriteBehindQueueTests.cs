using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FloVMP.Core.Database;
using Xunit;

namespace FloVMP.Core.Tests
{
    public class WriteBehindQueueTests
    {
        private class TestPlayerData
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public long Bank { get; set; }
        }

        [Fact]
        public void MarkDirty_IncreasesPendingCountAndTracksFields()
        {
            using var queue = new WriteBehindQueue<int, TestPlayerData>();

            var player = new TestPlayerData { Id = 1, Name = "Player1", Bank = 5000 };
            queue.MarkDirty(player.Id, player, "Bank");
            queue.MarkDirty(player.Id, player, "Cash");

            Assert.Equal(1, queue.PendingCount);
        }

        [Fact]
        public async Task FlushAsync_CallsPersisterAndCleansSavedEntities()
        {
            var persisted = new List<int>();
            using var queue = new WriteBehindQueue<int, TestPlayerData>(
                persister: batch =>
                {
                    foreach (var item in batch)
                    {
                        persisted.Add(item.Key);
                    }
                    return Task.FromResult(true);
                }
            );

            var p1 = new TestPlayerData { Id = 101, Name = "Alice", Bank = 10000 };
            var p2 = new TestPlayerData { Id = 102, Name = "Bob", Bank = 20000 };

            queue.MarkDirty(p1.Id, p1, "Bank");
            queue.MarkDirty(p2.Id, p2, "Bank");
            Assert.Equal(2, queue.PendingCount);

            int savedCount = await queue.FlushAsync();

            Assert.Equal(2, savedCount);
            Assert.Equal(0, queue.PendingCount);
            Assert.Equal(2, queue.TotalPersisted);
            Assert.Contains(101, persisted);
            Assert.Contains(102, persisted);
        }

        [Fact]
        public async Task FlushAsync_RetainsEntitiesIfPersisterReturnsFalse()
        {
            using var queue = new WriteBehindQueue<int, TestPlayerData>(
                persister: batch => Task.FromResult(false) // Симулируем сбой БД
            );

            var p = new TestPlayerData { Id = 500, Name = "Charlie", Bank = 3000 };
            queue.MarkDirty(p.Id, p, "Bank");

            int saved = await queue.FlushAsync();

            Assert.Equal(0, saved);
            Assert.Equal(1, queue.PendingCount); // Не удалены, остаются на повтор
            Assert.Equal(1, queue.FailedFlushes);
        }

        [Fact]
        public async Task FlushEntityImmediatelyAsync_FlushesOnlyTargetEntity()
        {
            using var queue = new WriteBehindQueue<int, TestPlayerData>();

            var p1 = new TestPlayerData { Id = 1, Name = "One" };
            var p2 = new TestPlayerData { Id = 2, Name = "Two" };

            queue.MarkDirty(1, p1, "Position");
            queue.MarkDirty(2, p2, "Position");

            Assert.Equal(2, queue.PendingCount);

            bool saved = await queue.FlushEntityImmediatelyAsync(1, record =>
            {
                Assert.Equal(1, record.Key);
                return Task.FromResult(true);
            });

            Assert.True(saved);
            Assert.Equal(1, queue.PendingCount); // Остался только ID 2
        }
    }
}
