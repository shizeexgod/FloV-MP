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
            // BUGFIX: работаем с байтами напрямую, чтобы не повредить бинарные
            // данные при text-кодировании. Ищем ASCII-маркер в сырых байтах,
            // заменяем значение в квадратных скобках, пишем обратно как binary.
            var data = File.ReadAllBytes(skinBinPath);
            var markerBytes = Encoding.ASCII.GetBytes("\"customUiUrl\": [");
            int startIdx = FindBytes(data, markerBytes);
            if (startIdx < 0) return false;

            int bracketStart = startIdx + markerBytes.Length;
            int bracketEnd = -1;
            for (int i = bracketStart; i < data.Length; i++)
            {
                if (data[i] == (byte)']') { bracketEnd = i; break; }
            }
            if (bracketEnd < 0) return false;

            var hash = ComputeSha256Hex(customUiUrl);
            var replacementValue = Encoding.ASCII.GetBytes($"\"{hash}\"");

            // Собираем новый файл: [до bracketStart] + replacementValue + [после bracketEnd]
            var result = new byte[bracketStart + replacementValue.Length + (data.Length - bracketEnd)];
            Buffer.BlockCopy(data, 0, result, 0, bracketStart);
            Buffer.BlockCopy(replacementValue, 0, result, bracketStart, replacementValue.Length);
            Buffer.BlockCopy(data, bracketEnd, result, bracketStart + replacementValue.Length, data.Length - bracketEnd);

            File.WriteAllBytes(skinBinPath, result);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static int FindBytes(byte[] haystack, byte[] needle)
    {
        for (int i = 0; i <= haystack.Length - needle.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j]) { match = false; break; }
            }
            if (match) return i;
        }
        return -1;
    }
}
