using System.Text.Json;

namespace FloVMP.Core.Licensing;

/// <summary>Атомарный cache последнего RSA-подписанного online lease.</summary>
public static class LicenseLeaseCache
{
    public const string FileName = "license.lease";

    public static string PathFor(string? licensePath)
    {
        var directory = licensePath is null
            ? Directory.GetCurrentDirectory()
            : Path.GetDirectoryName(licensePath) ?? Directory.GetCurrentDirectory();
        return Path.Combine(directory, FileName);
    }

    public static void Save(string path, string payloadB64, string signatureB64)
    {
        var temp = path + ".tmp";
        var json = JsonSerializer.Serialize(new { payload_b64 = payloadB64, signature = signatureB64 });
        File.WriteAllText(temp, json);
        TrySetPrivatePermissions(temp);
        File.Move(temp, path, true);
        TrySetPrivatePermissions(path);
    }

    public static bool TryRead(string path, string expectedKey, string expectedServerId, out DateTime validUntilUtc, string? publicKeyPem = null)
    {
        validUntilUtc = default;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var payload = Convert.FromBase64String(doc.RootElement.GetProperty("payload_b64").GetString() ?? "");
            var signature = Convert.FromBase64String(doc.RootElement.GetProperty("signature").GetString() ?? "");
            if (!LicenseFile.VerifyAuthoritySignature(payload, signature, publicKeyPem)) return false;
            using var lease = JsonDocument.Parse(payload);
            var root = lease.RootElement;
            if (!root.GetProperty("valid").GetBoolean()) return false;
            if (!string.Equals(root.GetProperty("licenseKey").GetString()?.Trim(), expectedKey.Trim(), StringComparison.OrdinalIgnoreCase)) return false;
            if (!root.TryGetProperty("serverId", out var serverId) ||
                !string.Equals(serverId.GetString()?.Trim(), expectedServerId.Trim(), StringComparison.OrdinalIgnoreCase)) return false;
            validUntilUtc = root.GetProperty("validUntil").GetDateTime().ToUniversalTime();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void TrySetPrivatePermissions(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            try
            {
                File.SetUnixFileMode(path,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
            catch
            {
                // A non-Unix filesystem may not expose POSIX modes; the lease
                // remains signature-protected and the caller can still start.
            }
        }
    }
}
