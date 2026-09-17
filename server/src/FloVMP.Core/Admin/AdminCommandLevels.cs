using System.Text;

namespace FloVMP.Core.Admin;

/// <summary>
/// Файл владельца <c>server/config/admin-commands.cfg</c>: какая команда с какого
/// уровня прав работает. Формат — строка «команда уровень», решётка начинает
/// комментарий:
///
/// <code>
/// # кого пускать к какой команде: 1..8, 0 — доступна всем
/// kick 2
/// ban  3
/// </code>
///
/// Зачем файл: у каждого проекта своя раскладка (где-то модератор уже банит,
/// где-то только кикает). Без файла это правилось бы только пересборкой
/// сервера, то есть владелец лицензии ждал бы обновления платформы.
///
/// Разбор нарочно терпимый: незнакомая команда или мусорная строка — просто
/// предупреждение в лог, сервер стартует с уровнями по умолчанию. Файл с
/// опечаткой не должен ни ронять сервер, ни молча открывать команды всем.
/// </summary>
public static class AdminCommandLevels
{
    public const string FileName = "admin-commands.cfg";

    /// <summary>Прочитать файл. Возвращает пары «команда → уровень» и список замечаний.</summary>
    public static (Dictionary<string, int> Levels, List<string> Problems) Parse(string text)
    {
        var levels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var problems = new List<string>();
        var lineNo = 0;

        foreach (var raw in text.Split('\n'))
        {
            lineNo++;
            var line = raw;
            var hash = line.IndexOf('#');
            if (hash >= 0) line = line[..hash];
            line = line.Trim();
            if (line.Length == 0) continue;

            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2 || !int.TryParse(parts[1], out var level))
            {
                problems.Add($"строка {lineNo}: ожидалось «команда уровень», получено «{line}»");
                continue;
            }
            if (level is < 0 or > 8)
            {
                problems.Add($"строка {lineNo}: уровень {level} вне 0..8");
                continue;
            }

            var name = parts[0].TrimStart('/').ToLowerInvariant();
            if (AdminCommandRegistry.Get(name) is null)
            {
                problems.Add($"строка {lineNo}: команды «{name}» нет в платформе — строка не применена");
                continue;
            }
            levels[name] = level;
        }

        return (levels, problems);
    }

    /// <summary>Содержимое файла-образца: все команды платформы с уровнями по умолчанию.</summary>
    public static string DefaultFileContent()
    {
        var sb = new StringBuilder();
        sb.Append("# FloV:MP — кто и что может делать.\n");
        sb.Append("#\n");
        sb.Append("# Уровень игрока хранится в базе (таблица admins) или в config/admins.json:\n");
        sb.Append("# 0 — обычный игрок, 1..8 — администратор. Права действуют сразу при заходе,\n");
        sb.Append("# без пароля и дежурства. Выдать: setadmin <ID|sc:SocialClubId> <уровень>\n");
        sb.Append("# в консоли сервера или /setadmin <ID> <уровень> в игре.\n");
        sb.Append("#\n");
        sb.Append("# По умолчанию все команды — уровень 8, то есть доступны только создателю\n");
        sb.Append("# сервера. Нужны младшие администраторы — поставьте команде уровень ниже\n");
        sb.Append("# и выдайте помощнику этот уровень. Например, модератор с киком и мутом:\n");
        sb.Append("#     kick 2\n");
        sb.Append("#     vmute 2\n");
        sb.Append("#     bans 2\n");
        sb.Append("# Уровень 0 открывает команду всем игрокам.\n");
        sb.Append("#\n");
        sb.Append("# После правки: команда reloadadmins в консоли сервера (перезапуск не нужен).\n");

        sb.Append('\n');
        foreach (var cmd in AdminCommandRegistry.All)
        {
            sb.Append(cmd.Name.PadRight(14)).Append(cmd.MinLevel)
              .Append("   # ").Append(cmd.Description).Append('\n');
        }
        return sb.ToString();
    }

    /// <summary>
    /// Прочитать файл рядом с сервером и применить. Если файла нет — создать
    /// образец с уровнями по умолчанию: владелец должен видеть, что вообще
    /// можно настраивать, а не искать это в документации.
    /// </summary>
    public static string LoadOrCreate(string configDir, Action<string>? warn = null)
    {
        AdminCommandRegistry.ResetToDefaults();
        var path = Path.Combine(configDir, FileName);
        try
        {
            if (!File.Exists(path))
            {
                Directory.CreateDirectory(configDir);
                File.WriteAllText(path, DefaultFileContent(), new UTF8Encoding(false));
                return $"{FileName}: создан файл с уровнями по умолчанию";
            }

            var (levels, problems) = Parse(File.ReadAllText(path));
            foreach (var p in problems) warn?.Invoke($"[FloV:MP] {FileName}: {p}");
            var applied = AdminCommandRegistry.ApplyOverrides(levels);
            return $"{FileName}: прочитано строк {levels.Count}, изменено уровней {applied}" +
                   (problems.Count > 0 ? $", пропущено строк {problems.Count}" : "");
        }
        catch (Exception ex)
        {
            warn?.Invoke($"[FloV:MP] {FileName}: не прочитан ({ex.Message}) — уровни по умолчанию");
            return $"{FileName}: не прочитан";
        }
    }
}
