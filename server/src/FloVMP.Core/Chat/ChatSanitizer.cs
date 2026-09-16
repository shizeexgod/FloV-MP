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
            // Невидимые символы нулевой ширины и управления направлением текста.
            // Они не управляющие с точки зрения char.IsControl, поэтому раньше
            // проходили. Ими подменяют вид ника («admin» с невидимым символом
            // внутри), разворачивают текст справа налево и обходят фильтры слов.
            if (IsInvisibleFormatting(ch)) continue;
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

    /// <summary>
    /// Символы нулевой ширины и переопределения направления текста (U+200B–U+200F,
    /// U+202A–U+202E, U+2066–U+2069, U+FEFF). Видимого смысла в чате не несут,
    /// используются только для подделки и обхода фильтров.
    /// </summary>
    public static bool IsInvisibleFormatting(char ch) =>
        ch is >= '\u200B' and <= '\u200F'
           or >= '\u202A' and <= '\u202E'
           or >= '\u2066' and <= '\u2069'
           or '\uFEFF';

    private static readonly System.Text.RegularExpressions.Regex ColorCode =
        new(@"\{[0-9a-fA-F]{6}\}", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>
    /// Убрать цветовые коды вида {RRGGBB} из текста, который написал ИГРОК.
    ///
    /// Цвет в чате — привилегия системы. Без этого игрок красил своё сообщение
    /// в красный «[FloV:MP Security] Сервер перезапускается, выйдите из игры»:
    /// имя автора остаётся, но цвет системного предупреждения убедителен.
    /// Системные и административные сообщения сюда не пропускаются — у них цвет
    /// законный.
    /// </summary>
    public static string StripColorCodes(string? text) =>
        string.IsNullOrEmpty(text) ? string.Empty : ColorCode.Replace(text, string.Empty);

    /// <summary>
    /// Полная очистка текста, написанного игроком: <see cref="Clean"/> плюс
    /// удаление цветовых кодов. Возвращает null, если после очистки пусто.
    /// </summary>
    public static string? CleanPlayerText(string? raw)
    {
        var cleaned = Clean(raw);
        if (cleaned is null) return null;
        var stripped = StripColorCodes(cleaned).Trim();
        return stripped.Length == 0 ? null : stripped;
    }

    /// <summary>true, если сообщение — команда (начинается с одиночного '/').</summary>
    public static bool IsCommand(string? text) =>
        !string.IsNullOrEmpty(text) && text.Length > 1 && text[0] == '/' && text[1] != '/';

    /// <summary>Разбирает "/cmd arg1 arg2" → ("cmd", ["arg1","arg2"]).</summary>
    public static (string cmd, string[] args) ParseCommand(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length < 2 || text[0] != '/')
            return ("", Array.Empty<string>());

        var parts = text[1..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return ("", Array.Empty<string>());
        return (parts[0].ToLowerInvariant(), parts[1..]);
    }
}
