namespace FloVMP.Core.Admin;

/// <summary>
/// Какая команда с какого уровня прав доступна.
///
/// Уровень игрока хранится один — в базе (таблица <c>admins</c>) или в
/// <c>config/admins.json</c>: 0 — обычный игрок, 1..8 — администратор. Права
/// действуют сразу при заходе на сервер, никакого дежурства и паролей.
///
/// На новом сервере администратор ровно один — создатель (уровень 8), и ему
/// доступно всё: поэтому по умолчанию у каждой команды уровень 8. Младшие
/// администраторы появляются тогда, когда владелец сам решит, что им можно:
/// он снижает уровень нужных команд в <c>server/config/admin-commands.cfg</c>
/// (см. <see cref="AdminCommandLevels"/>) и выдаёт игрокам уровни 1..7.
/// </summary>
public static class AdminCommandRegistry
{
    private static readonly Dictionary<string, AdminCommandDef> Commands = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, int> Defaults = new(StringComparer.OrdinalIgnoreCase);

    static AdminCommandRegistry()
    {
        // ── Инструменты администратора ─────────────────────────────
        Register("a", 8, "/a <текст>", "Чат администрации");
        Register("admin", 8, "/admin <текст>", "Чат администрации (алиас /a)");
        Register("esp", 8, "/esp [0-3]", "Админ-видение (F3)");
        Register("noclip", 8, "/noclip", "Полёт сквозь стены (F4)");
        Register("fly", 8, "/fly", "Полёт сквозь стены (алиас /noclip)");
        Register("tpm", 8, "/tpm", "Телепорт на метку карты (F5)");
        Register("tp", 8, "/tp <X> <Y> <Z>", "Телепорт по координатам");
        Register("goto", 8, "/goto <ID/ник>", "Телепортироваться к игроку");
        Register("gethere", 8, "/gethere <ID/ник>", "Телепортировать игрока к себе");
        Register("freeze", 8, "/freeze <ID/ник>", "Заморозить игрока");
        Register("unfreeze", 8, "/unfreeze <ID/ник>", "Разморозить игрока");
        Register("revive", 8, "/revive [ID/ник]", "Поднять погибшего игрока");
        Register("heal", 8, "/heal [ID/ник]", "Восстановить здоровье");
        Register("armor", 8, "/armor [ID/ник] [0-100]", "Выдать броню");
        Register("god", 8, "/god", "Неуязвимость");
        Register("godmode", 8, "/godmode", "Неуязвимость (алиас /god)");
        Register("kill", 8, "/kill [ID/ник]", "Убить игрока");
        Register("suicide", 8, "/suicide", "Убить себя (алиас /kill)");
        Register("speed", 8, "/speed [множитель] [ID/ник]", "Множитель скорости бега");
        Register("car", 8, "/car <модель>", "Создать транспорт");
        Register("veh", 8, "/veh <модель>", "Создать транспорт (алиас /car)");
        Register("fix", 8, "/fix", "Починить транспорт");
        Register("repair", 8, "/repair", "Починить транспорт (алиас /fix)");
        Register("dv", 8, "/dv", "Удалить транспорт");
        Register("delveh", 8, "/delveh", "Удалить транспорт (алиас /dv)");
        Register("destroyveh", 8, "/destroyveh", "Удалить транспорт (алиас /dv)");
        Register("weapon", 8, "/weapon <название>", "Выдать оружие");
        Register("gun", 8, "/gun <название>", "Выдать оружие (алиас /weapon)");
        Register("givegun", 8, "/givegun <название>", "Выдать оружие (алиас /weapon)");
        Register("disarm", 8, "/disarm [ID/ник]", "Забрать оружие");
        Register("removeweapons", 8, "/removeweapons [ID/ник]", "Забрать оружие (алиас /disarm)");
        Register("skin", 8, "/skin <модель>", "Сменить модель персонажа");
        Register("ped", 8, "/ped <модель>", "Сменить модель персонажа (алиас /skin)");
        Register("setdim", 8, "/setdim <ID/ник> <номер>", "Виртуальный мир игрока");
        Register("dim", 8, "/dim <ID/ник> <номер>", "Виртуальный мир (алиас /setdim)");
        Register("dimension", 8, "/dimension <ID/ник> <номер>", "Виртуальный мир (алиас /setdim)");
        Register("weather", 8, "/weather <название|0-14>", "Погода на сервере");
        Register("time", 8, "/time <часы> [минуты]", "Игровое время на сервере");

        // ── Модерация ──────────────────────────────────────────────
        Register("kick", 8, "/kick <ID/ник> [причина]", "Исключить игрока");
        Register("mute", 8, "/mute <ID> [минут]", "Заглушить чат игрока (переживает перезаход)");
        Register("unmute", 8, "/unmute <ID>", "Вернуть игроку чат");
        Register("vmute", 8, "/vmute <ID> [минут]", "Заглушить голос игрока (переживает перезаход)");
        Register("voicemute", 8, "/voicemute <ID> [минут]", "Заглушить голос (алиас /vmute)");
        Register("bans", 8, "/bans", "Список блокировок");
        Register("banlist", 8, "/banlist", "Список блокировок (алиас /bans)");

        // ── Блокировки ─────────────────────────────────────────────
        Register("ban", 8, "/ban <ID/ник> <дней> [причина]", "Заблокировать игрока");
        Register("banip", 8, "/banip <ID/ник> <дней> [причина]", "Заблокировать игрока и его IP");

        Register("hwidban", 8, "/hwidban <ID/ник> <дней> [причина]", "Блокировка по железу (HWID + MAC)");
        Register("unban", 8, "/unban <ник|IP|HWID|ID бана>", "Снять блокировку");

        Register("hardban", 8, "/hardban <ID/ник> [причина]", "Блокировка навсегда по всем признакам");

        // ── Права администрации ────────────────────────────────────
        Register("setadmin", 8, "/setadmin <ID> <уровень 0-8>", "Выдать или снять права администратора");
    }

    private static void Register(string name, int minLevel, string usage, string description)
    {
        Commands[name] = new AdminCommandDef(name, minLevel, usage, description);
        Defaults[name] = minLevel;
    }

    /// <summary>Уровни по умолчанию — из них пишется образец admin-commands.cfg.</summary>
    public static IReadOnlyDictionary<string, int> DefaultLevels => Defaults;

    /// <summary>
    /// Применить раскладку владельца из <c>server/config/admin-commands.cfg</c>.
    /// Незнакомые имена игнорируются: опечатка в файле не должна открывать
    /// команду всем и не должна ронять сервер. Команды, которых в файле нет,
    /// остаются с уровнем по умолчанию.
    /// </summary>
    public static int ApplyOverrides(IReadOnlyDictionary<string, int> levels)
    {
        var applied = 0;
        foreach (var (name, level) in levels)
        {
            if (!Commands.TryGetValue(name, out var def)) continue;
            var clamped = Math.Clamp(level, 0, 8);
            if (clamped == def.MinLevel) continue;
            Commands[name] = def with { MinLevel = clamped };
            applied++;
        }
        return applied;
    }

    /// <summary>Вернуть уровни по умолчанию (используется в тестах и при сбросе).</summary>
    public static void ResetToDefaults()
    {
        foreach (var (name, level) in Defaults)
            Commands[name] = Commands[name] with { MinLevel = level };
    }

    public static AdminCommandDef? Get(string commandName)
    {
        if (string.IsNullOrWhiteSpace(commandName)) return null;
        Commands.TryGetValue(commandName, out var def);
        return def;
    }

    public static bool CanExecute(int playerAdminLevel, string commandName)
    {
        var cmd = Get(commandName);
        if (cmd == null) return false;
        return playerAdminLevel >= cmd.MinLevel;
    }

    public static IReadOnlyList<AdminCommandDef> GetAvailableCommands(int playerAdminLevel)
    {
        return Commands.Values
            .Where(c => playerAdminLevel >= c.MinLevel)
            .OrderBy(c => c.MinLevel)
            .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IReadOnlyList<AdminCommandDef> All =>
        Commands.Values.OrderBy(c => c.MinLevel).ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList();
}
