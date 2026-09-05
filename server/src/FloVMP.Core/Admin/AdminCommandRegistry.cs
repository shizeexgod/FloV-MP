using System.Collections.Concurrent;

namespace FloVMP.Core.Admin;

/// <summary>
/// Реестр административных команд 8-уровневой системы «Держава Онлайн».
/// Предоставляет проверку прав, валидацию уровня доступа и автогенерацию справки.
/// </summary>
public static class AdminCommandRegistry
{
    private static readonly Dictionary<string, AdminCommandDef> Commands = new(StringComparer.OrdinalIgnoreCase);

    static AdminCommandRegistry()
    {
        // ── Уровень 1: Хелпер (Мл. Модератор) ──────────────────────
        Register("a", 1, "/a <текст>", "Внутренний чат администрации");
        Register("stats", 1, "/stats <ID/ник>", "Статистика и информация об игроке");
        Register("freeze", 1, "/freeze <ID/ник>", "Заморозить игрока на месте");
        Register("unfreeze", 1, "/unfreeze <ID/ник>", "Разморозить игрока");
        Register("sp", 1, "/sp <ID/ник>", "Следить за игроком (режим спектатора)");
        Register("spoff", 1, "/spoff", "Выйти из режима слежки");
        Register("ans", 1, "/ans <ID/ник> <ответ>", "Ответить на обращение / репорт");

        // ── Уровень 2: Модератор ───────────────────────────────────
        Register("goto", 2, "/goto <ID/ник>", "Телепортироваться к игроку");
        Register("gethere", 2, "/gethere <ID/ник>", "Телепортировать игрока к себе");
        Register("kick", 2, "/kick <ID/ник> [причина]", "Исключить игрока с сервера");
        Register("mute", 2, "/mute <ID/ник> <минут> [причина]", "Заблокировать текстовый чат игроку");
        Register("unmute", 2, "/unmute <ID/ник>", "Снять блокировку чата");
        Register("jail", 2, "/jail <ID/ник> <минут> [причина]", "Посадить в деморган (КПЗ)");
        Register("unjail", 2, "/unjail <ID/ник>", "Выпустить из деморгана");

        // ── Уровень 3: Старший Модератор ──────────────────────────
        Register("ban", 3, "/ban <ID/ник> <дней> [причина]", "Заблокировать аккаунт игрока");
        Register("unban", 3, "/unban <ник>", "Разблокировать аккаунт");
        Register("warn", 3, "/warn <ID/ник> [причина]", "Выдать предупреждение (варн)");
        Register("unwarn", 3, "/unwarn <ID/ник>", "Снять предупреждение");
        Register("slap", 3, "/slap <ID/ник>", "Подбросить игрока (проверка на АФК/бот)");

        // ── Уровень 4: Администратор ──────────────────────────────
        Register("veh", 4, "/veh <модель> [цвет1] [цвет2]", "Создать временный транспорт");
        Register("dv", 4, "/dv [радиус]", "Удалить ближайший/занимаемый транспорт");
        Register("sethp", 4, "/sethp <ID/ник> <кол-во 0-100>", "Установить уровень здоровья игрока");
        Register("setarmor", 4, "/setarmor <ID/ник> <кол-во 0-100>", "Установить уровень брони игрока");
        Register("repair", 4, "/repair", "Починить транспорт, в котором находится админ");
        Register("fuel", 4, "/fuel", "Заправить транспорт до 100%");

        // ── Уровень 5: Старший Администратор ──────────────────────
        Register("tp", 5, "/tp <X> <Y> <Z>", "Телепорт по точным координатам");
        Register("tpm", 5, "/tpm [redsquare|city|police|hospital]", "Быстрый телепорт по ключевым локациям Москвы");
        Register("setweather", 5, "/setweather <ID_погоды>", "Изменить погоду на сервере");
        Register("settime", 5, "/settime <часы 0-23> [минуты]", "Изменить игровое время");
        Register("setskin", 5, "/setskin <ID/ник> <модель>", "Изменить модель персонажа (скин)");

        // ── Уровень 6: Куратор / Зам. ГА ──────────────────────────
        Register("givemoney", 6, "/givemoney <ID/ник> <сумма>", "Выдать наличные средства игроку");
        Register("takemoney", 6, "/takemoney <ID/ник> <сумма>", "Изъять наличные средства у игрока");
        Register("giveitem", 6, "/giveitem <ID/ник> <item_id> <кол-во>", "Выдать предмет в инвентарь");
        Register("setdim", 6, "/setdim <ID/ник> <dimension>", "Установить виртуальный мир (дименшн)");

        // ── Уровень 7: Главный Администратор (ГА) ─────────────────
        Register("makeadmin", 7, "/makeadmin <ID/ник> <уровень 0-6>", "Назначить администратора (до 6 ранга)");
        Register("banip", 7, "/banip <IP-адрес> [причина]", "Заблокировать IP-адрес на сервере");
        Register("clearadmin", 7, "/clearadmin <ник>", "Снять администратора");

        // ── Уровень 8: Руководитель проекта / Разработчик ─────────
        Register("setadminlevel", 8, "/setadminlevel <ID/ник> <уровень 0-8>", "Полный доступ к уровням администрации");
        Register("srvrestart", 8, "/srvrestart [секунд]", "Перезапустить сервер с оповещением");
    }

    private static void Register(string name, int minLevel, string usage, string description)
    {
        Commands[name] = new AdminCommandDef(name, minLevel, usage, description);
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
            .ThenBy(c => c.Name)
            .ToList();
    }
}
