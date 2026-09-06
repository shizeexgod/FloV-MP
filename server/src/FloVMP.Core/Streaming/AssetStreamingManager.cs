using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using FloVMP.Core.AntiCheat;

namespace FloVMP.Core.Streaming
{
    public enum ChunkStreamingType
    {
        Global,     // Required on initial connect (core scripts, fonts, hud)
        Spatial     // Streamed dynamically based on player's 3D coordinates (map mlo, ymap, custom vehicles, props)
    }

    public record AssetChunk(
        string ChunkId,
        string ResourceName,
        string RelativePath,
        long SizeBytes,
        string Sha256Checksum,
        ChunkStreamingType StreamingType = ChunkStreamingType.Spatial,
        Vector3D? Position = null,
        float StreamingRadius = 250f,
        int Priority = 1
    );

    public record StreamZone(
        string ZoneId,
        Vector3D Center,
        float Radius,
        List<string> ChunkIds
    );

    /// <summary>
    /// Dynamic Asset Streaming Manager.
    /// Manages progressive asset loading based on player coordinates to avoid
    /// massive upfront downloads, seamlessly coordinating with CDN FastDL.
    /// </summary>
    public class AssetStreamingManager
    {
        private readonly ConcurrentDictionary<string, AssetChunk> _chunks = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, StreamZone> _zones = new(StringComparer.OrdinalIgnoreCase);

        public int TotalChunks => _chunks.Count;
        public long TotalSizeBytes => _chunks.Values.Sum(c => c.SizeBytes);
        public int TotalZones => _zones.Count;

        public void RegisterChunk(AssetChunk chunk)
        {
            if (chunk == null || string.IsNullOrWhiteSpace(chunk.ChunkId)) return;
            _chunks[chunk.ChunkId.Trim()] = chunk;
        }

        public bool UnregisterChunk(string chunkId)
        {
            if (string.IsNullOrWhiteSpace(chunkId)) return false;
            return _chunks.TryRemove(chunkId.Trim(), out _);
        }

        public AssetChunk? GetChunk(string chunkId)
        {
            if (string.IsNullOrWhiteSpace(chunkId)) return null;
            return _chunks.TryGetValue(chunkId.Trim(), out var chunk) ? chunk : null;
        }

        public void RegisterZone(StreamZone zone)
        {
            if (zone == null || string.IsNullOrWhiteSpace(zone.ZoneId)) return;
            _zones[zone.ZoneId.Trim()] = zone;
        }

        public List<AssetChunk> GetInitialConnectChunks()
        {
            return _chunks.Values
                .Where(c => c.StreamingType == ChunkStreamingType.Global)
                .OrderByDescending(c => c.Priority)
                .ToList();
        }

        /// <summary>
        /// Calculates which chunks must be streamed to the player based on their 3D coordinates,
        /// filtering out chunks the player has already cached.
        /// </summary>
        public List<AssetChunk> GetRequiredChunksForPlayer(
            Vector3D playerPosition,
            ISet<string>? alreadyCachedChunkIds = null,
            float maxStreamingDistance = 350f)
        {
            var result = new List<AssetChunk>();

            foreach (var chunk in _chunks.Values)
            {
                if (chunk.StreamingType != ChunkStreamingType.Spatial)
                    continue;

                if (alreadyCachedChunkIds != null && alreadyCachedChunkIds.Contains(chunk.ChunkId))
                    continue;

                if (chunk.Position.HasValue)
                {
                    float distance = playerPosition.DistanceTo(chunk.Position.Value);
                    float effectiveRadius = Math.Min(chunk.StreamingRadius, maxStreamingDistance);

                    if (distance <= effectiveRadius)
                    {
                        result.Add(chunk);
                    }
                }
            }

            // Also check zone-based groupings
            foreach (var zone in _zones.Values)
            {
                float zoneDist = playerPosition.DistanceTo(zone.Center);
                if (zoneDist <= zone.Radius)
                {
                    foreach (var id in zone.ChunkIds)
                    {
                        if (alreadyCachedChunkIds != null && alreadyCachedChunkIds.Contains(id))
                            continue;

                        if (_chunks.TryGetValue(id, out var chunk) && !result.Contains(chunk))
                        {
                            result.Add(chunk);
                        }
                    }
                }
            }

            return result.OrderByDescending(c => c.Priority).ToList();
        }

        public static bool VerifyChunkChecksum(byte[] data, string expectedSha256)
        {
            if (data == null || string.IsNullOrWhiteSpace(expectedSha256)) return false;

            using var sha = SHA256.Create();
            byte[] hashBytes = sha.ComputeHash(data);
            string computedHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

            return string.Equals(computedHash, expectedSha256.Trim().ToLowerInvariant(), StringComparison.OrdinalIgnoreCase);
        }

        public string GenerateCdnDownloadUrl(AssetChunk chunk, string cdnBaseUrl)
        {
            if (chunk == null) throw new ArgumentNullException(nameof(chunk));
            string baseUri = (cdnBaseUrl ?? "http://127.0.0.1/cdn").TrimEnd('/');
            string path = chunk.RelativePath.TrimStart('/');
            return $"{baseUri}/{path}?sha={chunk.Sha256Checksum}";
        }
    }
}
