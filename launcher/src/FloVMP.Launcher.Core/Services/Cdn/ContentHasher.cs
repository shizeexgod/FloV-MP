using System.IO;
using System.Security.Cryptography;

namespace FloVMP.Launcher.Services.Cdn;

/// <summary>Потоковый SHA-256, hex в нижнем регистре.</summary>
public static class ContentHasher
{
    public static async Task<string> Sha256FileAsync(string path, CancellationToken ct = default)
    {
        await using var fs = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 1 << 20, useAsync: true);
        return await Sha256StreamAsync(fs, ct);
    }

    public static async Task<string> Sha256StreamAsync(Stream stream, CancellationToken ct = default)
    {
        using var sha = SHA256.Create();
        var buffer = new byte[1 << 20];
        int read;
        while ((read = await stream.ReadAsync(buffer, ct)) > 0)
        {
            sha.TransformBlock(buffer, 0, read, null, 0);
        }
        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return Convert.ToHexString(sha.Hash!).ToLowerInvariant();
    }
}
