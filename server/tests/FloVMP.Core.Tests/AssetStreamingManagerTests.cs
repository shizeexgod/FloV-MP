using System.Collections.Generic;
using System.Text;
using FloVMP.Core.AntiCheat;
using FloVMP.Core.Streaming;
using Xunit;

namespace FloVMP.Core.Tests
{
    public class AssetStreamingManagerTests
    {
        [Fact]
        public void InitialConnectChunks_ReturnsOnlyGlobalChunksSortedByPriority()
        {
            var manager = new AssetStreamingManager();

            manager.RegisterChunk(new AssetChunk("core-ui", "ui", "ui/bundle.js", 1024, "abc", ChunkStreamingType.Global, Priority: 10));
            manager.RegisterChunk(new AssetChunk("core-fonts", "ui", "ui/fonts.woff2", 512, "def", ChunkStreamingType.Global, Priority: 5));
            manager.RegisterChunk(new AssetChunk("moscow-city-mlo", "map", "stream/moscow.ydr", 4096, "xyz", ChunkStreamingType.Spatial, new Vector3D(100, 200, 30)));

            var initial = manager.GetInitialConnectChunks();

            Assert.Equal(2, initial.Count);
            Assert.Equal("core-ui", initial[0].ChunkId);
            Assert.Equal("core-fonts", initial[1].ChunkId);
        }

        [Fact]
        public void GetRequiredChunksForPlayer_FiltersBySpatialDistanceAndAlreadyCached()
        {
            var manager = new AssetStreamingManager();

            // Near player (at 0, 0, 0 with radius 100)
            manager.RegisterChunk(new AssetChunk("hospital-mlo", "map", "mlo/hospital.ydr", 2048, "hash1",
                ChunkStreamingType.Spatial, new Vector3D(50, 0, 0), StreamingRadius: 100f, Priority: 2));

            // Far from player (at 500, 0, 0 with radius 100)
            manager.RegisterChunk(new AssetChunk("airport-mlo", "map", "mlo/airport.ydr", 8192, "hash2",
                ChunkStreamingType.Spatial, new Vector3D(500, 0, 0), StreamingRadius: 100f, Priority: 1));

            var playerPos = new Vector3D(10, 0, 0);

            // Test 1: Player hasn't cached anything
            var needed1 = manager.GetRequiredChunksForPlayer(playerPos);
            Assert.Single(needed1);
            Assert.Equal("hospital-mlo", needed1[0].ChunkId);

            // Test 2: Player has already cached hospital-mlo
            var cached = new HashSet<string> { "hospital-mlo" };
            var needed2 = manager.GetRequiredChunksForPlayer(playerPos, cached);
            Assert.Empty(needed2);
        }

        [Fact]
        public void VerifyChunkChecksum_ValidatesSha256Correctly()
        {
            byte[] data = Encoding.UTF8.GetBytes("FloVMP-Chunk-Content-2026");

            using var sha = System.Security.Cryptography.SHA256.Create();
            string expectedHash = BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "").ToLowerInvariant();

            Assert.True(AssetStreamingManager.VerifyChunkChecksum(data, expectedHash));
            Assert.False(AssetStreamingManager.VerifyChunkChecksum(data, "invalid_hash_value"));
        }

        [Fact]
        public void GenerateCdnDownloadUrl_FormatsCleanUrlWithQuery()
        {
            var manager = new AssetStreamingManager();
            var chunk = new AssetChunk("test-chunk", "cars", "vehicles/bmw_m5.yft", 5000, "9944aaff");

            string url = manager.GenerateCdnDownloadUrl(chunk, "http://188.127.229.224/cdn/");
            Assert.Equal("http://188.127.229.224/cdn/vehicles/bmw_m5.yft?sha=9944aaff", url);
        }
    }
}
