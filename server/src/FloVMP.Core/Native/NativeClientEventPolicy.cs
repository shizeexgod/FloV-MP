using System.Text;
using System.Text.Json;

namespace FloVMP.Core.Native;

/// <summary>Одинаковая проверка CEV/CEVS для всех клиентов и серверных ресурсов.</summary>
public static class NativeClientEventPolicy
{
    public const int MaxNameChars = 64;
    public const int MaxJsonChars = 3800;

    public static bool IsValidName(string? name)
    {
        if (name is null || name.Length is < 1 or > MaxNameChars) return false;
        foreach (var ch in name)
            if (!((ch is >= 'a' and <= 'z') || (ch is >= 'A' and <= 'Z') ||
                  (ch is >= '0' and <= '9') || ch is '_' or ':' or '.' or '-')) return false;
        return true;
    }

    public static bool IsValid(string? name, string? argsJson, bool fromClient)
    {
        if (!IsValidName(name)) return false;
        var json = argsJson ?? "[]";
        if (json.Length > MaxJsonChars) return false;
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array) return false;
        }
        catch (JsonException) { return false; }

        var line = NativeProtocol.Format(fromClient ? "CEVS" : "CEV", name!, json);
        return Encoding.UTF8.GetByteCount(line) <= NativeProtocol.MaxLineBytes;
    }
}
