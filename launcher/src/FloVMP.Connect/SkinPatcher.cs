using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace FloVMP.Connect;

public static class SkinPatcher
{
    public static string ComputeSha256Hex(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static bool PatchSkinBin(string skinBinPath, string customUiUrl)
    {
        if (!File.Exists(skinBinPath)) return false;

        try
        {
            var content = File.ReadAllText(skinBinPath, Encoding.Latin1);
            const string marker = "\"customUiUrl\": [";
            var startIdx = content.IndexOf(marker, StringComparison.Ordinal);
            if (startIdx < 0) return false;

            var endIdx = content.IndexOf(']', startIdx);
            if (endIdx < 0) return false;

            var hash = ComputeSha256Hex(customUiUrl);
            var replacement = $"\"customUiUrl\": [\"{hash}\"]";

            var updated = content.Substring(0, startIdx) + replacement + content.Substring(endIdx + 1);
            File.WriteAllText(skinBinPath, updated, Encoding.Latin1);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
