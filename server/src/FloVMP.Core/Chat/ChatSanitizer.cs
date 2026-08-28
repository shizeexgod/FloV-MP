using System.Text;

namespace FloVMP.Core.Chat;

/// <summary>Очистка и валидация сообщений чата. Чистая логика, тестируется.</summary>
public static class ChatSanitizer
{
    public const int MaxLength = 256;

    /// <summary>
    /// Обрезает пробелы, убирает управляющие символы, схлопывает пробелы,
    /// ограничивает длину. Возвращает null, если после очистки пусто.
    /// </summary>
    public static string? Clean(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var sb = new StringBuilder(raw.Length);
        var lastSpace = false;
        foreach (var ch in raw.Trim())
        {
            if (char.IsControl(ch)) continue;
            if (ch == ' ')
            {
                if (lastSpace) continue;
                lastSpace = true;
            }
            else
            {
                lastSpace = false;
            }
            sb.Append(ch);
            if (sb.Length >= MaxLength) break;
        }

        var result = sb.ToString().Trim();
        return result.Length == 0 ? null : result;
    }

    /// <summary>true, если сообщение — команда (начинается с одиночного '/').</summary>
    public static bool IsCommand(string text) =>
        text.Length > 1 && text[0] == '/' && text[1] != '/';

    /// <summary>Разбирает "/cmd arg1 arg2" → ("cmd", ["arg1","arg2"]).</summary>
    public static (string cmd, string[] args) ParseCommand(string text)
    {
        var parts = text[1..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return ("", Array.Empty<string>());
        return (parts[0].ToLowerInvariant(), parts[1..]);
    }
}
