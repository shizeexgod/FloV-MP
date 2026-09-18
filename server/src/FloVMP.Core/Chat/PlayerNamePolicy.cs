namespace FloVMP.Core.Chat;

/// <summary>
/// Проверка ника при входе на сервер.
///
/// Ник задаёт клиент, а сервер вставляет его в системные сообщения
/// («[Бан] {ник} заблокирован…»), в лог и в подсказки администраторам.
/// Без проверки игрок с ником вида <c>Bob{ef4444}[Администрация] Сервер
/// перезапускается</c> подкрашивал и подделывал системные строки чата, а
/// перевод строки в нике добавлял в лог сервера поддельную строку — например,
/// о выдаче прав.
///
/// Правила нарочно простые и понятные игроку: лучше отказать во входе с ясной
/// причиной, чем пустить и потом разбираться, кто на самом деле «Администратор».
/// </summary>
public static class PlayerNamePolicy
{
    public const int MinLength = 2;
    public const int MaxLength = 32;

    /// <summary>
    /// Можно ли войти с таким ником. <c>reason</c> — готовый текст для игрока.
    /// </summary>
    public static bool IsAllowed(string? name, out string reason)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            reason = "ник пустой";
            return false;
        }

        if (name.Length != name.Trim().Length)
        {
            reason = "ник начинается или заканчивается пробелом";
            return false;
        }

        if (name.Length < MinLength || name.Length > MaxLength)
        {
            reason = $"длина ника — от {MinLength} до {MaxLength} символов";
            return false;
        }

        foreach (var ch in name)
        {
            if (char.IsControl(ch) || ChatSanitizer.IsInvisibleFormatting(ch))
            {
                reason = "в нике служебные или невидимые символы";
                return false;
            }

            // Фигурные скобки — разметка цвета в чате ({RRGGBB}), квадратные —
            // префиксы системных сообщений ([Администрация], [Бан]). В нике
            // ни то ни другое не нужно, а подделать ими системную строку можно.
            if (ch is '{' or '}' or '[' or ']' or '<' or '>')
            {
                reason = $"символ «{ch}» в нике запрещён";
                return false;
            }
        }

        reason = "";
        return true;
    }

    /// <summary>
    /// Ник для строки лога до проверки: служебные символы заменяются, длина
    /// ограничивается. Иначе отклонённый ник с переводом строки всё равно
    /// попал бы в лог в строке «подключается…».
    /// </summary>
    public static string ForLog(string? name)
    {
        if (string.IsNullOrEmpty(name)) return "(пусто)";
        var chars = name.Length > MaxLength * 2 ? name[..(MaxLength * 2)].ToCharArray() : name.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (char.IsControl(chars[i]) || ChatSanitizer.IsInvisibleFormatting(chars[i]))
                chars[i] = '?';
        }
        return new string(chars);
    }
}
