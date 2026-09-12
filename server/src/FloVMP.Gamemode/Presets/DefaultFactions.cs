using FloVMP.Core.Factions;

namespace FloVMP.Gamemode.Presets;

/// <summary>
/// Каталог базовых организаций и государственных ведомств для RP-гейммода FloV:MP.
/// </summary>
public static class DefaultFactions
{
    public static void RegisterAll(FactionService service)
    {
        // 1. Мэрия (Правительство)
        var gov = new Faction(1, "МЭРИЯ", "Городская Администрация", FactionType.Government, initialTreasury: 50_000_000);
        gov.AddRank(1, "Стажёр аппарата", 18_000, FactionPermissions.None);
        gov.AddRank(2, "Секретарь", 25_000, FactionPermissions.RadioFaction);
        gov.AddRank(3, "Юрист-консультант", 35_000, FactionPermissions.RadioFaction | FactionPermissions.RadioDepartment);
        gov.AddRank(4, "Инспектор мэрии", 48_000, FactionPermissions.RadioFaction | FactionPermissions.RadioDepartment | FactionPermissions.IssueFine);
        gov.AddRank(5, "Заместитель Главы", 75_000, FactionPermissions.RadioFaction | FactionPermissions.RadioDepartment | FactionPermissions.Invite | FactionPermissions.Kick | FactionPermissions.Promote | FactionPermissions.Demote | FactionPermissions.TreasuryDeposit);
        gov.AddRank(6, "Глава города", 120_000, FactionPermissions.All);
        service.RegisterFaction(gov);

        // 2. Полицейский департамент
        var police = new Faction(2, "ПОЛИЦИЯ", "Городское управление полиции", FactionType.Police, initialTreasury: 25_000_000);
        police.AddRank(1, "Кадет полиции", 20_000, FactionPermissions.RadioFaction);
        police.AddRank(2, "Сержант полиции", 28_000, FactionPermissions.RadioFaction | FactionPermissions.RadioDepartment | FactionPermissions.Cuffs | FactionPermissions.SearchInventory | FactionPermissions.Armory);
        police.AddRank(3, "Старшина полиции", 36_000, FactionPermissions.RadioFaction | FactionPermissions.RadioDepartment | FactionPermissions.Cuffs | FactionPermissions.SearchInventory | FactionPermissions.IssueFine | FactionPermissions.Armory);
        police.AddRank(4, "Лейтенант полиции", 46_000, FactionPermissions.RadioFaction | FactionPermissions.RadioDepartment | FactionPermissions.Cuffs | FactionPermissions.SearchInventory | FactionPermissions.IssueFine | FactionPermissions.Arrest | FactionPermissions.Armory);
        police.AddRank(5, "Капитан полиции", 58_000, FactionPermissions.RadioFaction | FactionPermissions.RadioDepartment | FactionPermissions.Cuffs | FactionPermissions.SearchInventory | FactionPermissions.IssueFine | FactionPermissions.Arrest | FactionPermissions.Armory | FactionPermissions.Invite | FactionPermissions.Promote);
        police.AddRank(6, "Майор полиции", 72_000, FactionPermissions.RadioFaction | FactionPermissions.RadioDepartment | FactionPermissions.Cuffs | FactionPermissions.SearchInventory | FactionPermissions.IssueFine | FactionPermissions.Arrest | FactionPermissions.Armory | FactionPermissions.Invite | FactionPermissions.Promote | FactionPermissions.Demote);
        police.AddRank(7, "Полковник полиции", 92_000, FactionPermissions.RadioFaction | FactionPermissions.RadioDepartment | FactionPermissions.Cuffs | FactionPermissions.SearchInventory | FactionPermissions.IssueFine | FactionPermissions.Arrest | FactionPermissions.Armory | FactionPermissions.Invite | FactionPermissions.Kick | FactionPermissions.Promote | FactionPermissions.Demote | FactionPermissions.TreasuryWithdraw | FactionPermissions.TreasuryDeposit);
        police.AddRank(8, "Шеф полиции", 130_000, FactionPermissions.All);
        service.RegisterFaction(police);

        // 3. Служба безопасности
        var sec = new Faction(3, "СБ", "Служба безопасности", FactionType.SecurityService, initialTreasury: 35_000_000);
        sec.AddRank(1, "Младший оперативник", 35_000, FactionPermissions.RadioFaction | FactionPermissions.Cuffs | FactionPermissions.SearchInventory | FactionPermissions.Armory);
        sec.AddRank(2, "Оперативник", 50_000, FactionPermissions.RadioFaction | FactionPermissions.RadioDepartment | FactionPermissions.Cuffs | FactionPermissions.SearchInventory | FactionPermissions.Arrest | FactionPermissions.Armory);
        sec.AddRank(3, "Старший оперативник", 70_000, FactionPermissions.RadioFaction | FactionPermissions.RadioDepartment | FactionPermissions.Cuffs | FactionPermissions.SearchInventory | FactionPermissions.Arrest | FactionPermissions.Armory);
        sec.AddRank(4, "Следователь", 95_000, FactionPermissions.RadioFaction | FactionPermissions.RadioDepartment | FactionPermissions.Cuffs | FactionPermissions.SearchInventory | FactionPermissions.Arrest | FactionPermissions.Armory | FactionPermissions.Invite);
        sec.AddRank(5, "Начальник отдела", 125_000, FactionPermissions.RadioFaction | FactionPermissions.RadioDepartment | FactionPermissions.Cuffs | FactionPermissions.SearchInventory | FactionPermissions.Arrest | FactionPermissions.Armory | FactionPermissions.Invite | FactionPermissions.Kick | FactionPermissions.Promote | FactionPermissions.Demote);
        sec.AddRank(6, "Директор службы", 175_000, FactionPermissions.All);
        service.RegisterFaction(sec);

        // 4. Городская больница (ЕМС)
        var hospital = new Faction(4, "ЕМС", "Центральная городская больница", FactionType.Hospital, initialTreasury: 15_000_000);
        hospital.AddRank(1, "Интерн", 18_000, FactionPermissions.RadioFaction);
        hospital.AddRank(2, "Фельдшер СМП", 28_000, FactionPermissions.RadioFaction | FactionPermissions.RadioDepartment | FactionPermissions.Heal);
        hospital.AddRank(3, "Врач-терапевт", 40_000, FactionPermissions.RadioFaction | FactionPermissions.RadioDepartment | FactionPermissions.Heal);
        hospital.AddRank(4, "Врач-хирург", 58_000, FactionPermissions.RadioFaction | FactionPermissions.RadioDepartment | FactionPermissions.Heal | FactionPermissions.Invite);
        hospital.AddRank(5, "Заведующий отделением", 78_000, FactionPermissions.RadioFaction | FactionPermissions.RadioDepartment | FactionPermissions.Heal | FactionPermissions.Invite | FactionPermissions.Promote | FactionPermissions.Demote);
        hospital.AddRank(6, "Главный врач", 115_000, FactionPermissions.All);
        service.RegisterFaction(hospital);

        // 5. Национальная гвардия / Армия
        var army = new Faction(5, "АРМИЯ", "Национальная гвардия", FactionType.Army, initialTreasury: 20_000_000);
        army.AddRank(1, "Рядовой", 16_000, FactionPermissions.RadioFaction);
        army.AddRank(2, "Сержант", 26_000, FactionPermissions.RadioFaction | FactionPermissions.RadioDepartment | FactionPermissions.Armory);
        army.AddRank(3, "Прапорщик", 38_000, FactionPermissions.RadioFaction | FactionPermissions.RadioDepartment | FactionPermissions.Armory | FactionPermissions.Storage);
        army.AddRank(4, "Лейтенант", 52_000, FactionPermissions.RadioFaction | FactionPermissions.RadioDepartment | FactionPermissions.Armory | FactionPermissions.Invite);
        army.AddRank(5, "Майор", 72_000, FactionPermissions.RadioFaction | FactionPermissions.RadioDepartment | FactionPermissions.Armory | FactionPermissions.Invite | FactionPermissions.Promote | FactionPermissions.Demote);
        army.AddRank(6, "Командир базы", 115_000, FactionPermissions.All);
        service.RegisterFaction(army);

        // 6. Средства Массовой Информации (СМИ)
        var news = new Faction(6, "СМИ", "Информационное агентство", FactionType.News, initialTreasury: 10_000_000);
        news.AddRank(1, "Стажёр редакции", 16_000, FactionPermissions.RadioFaction);
        news.AddRank(2, "Репортёр", 28_000, FactionPermissions.RadioFaction | FactionPermissions.NewsBroadcast);
        news.AddRank(3, "Ведущий эфира", 42_000, FactionPermissions.RadioFaction | FactionPermissions.NewsBroadcast);
        news.AddRank(4, "Режиссёр эфира", 60_000, FactionPermissions.RadioFaction | FactionPermissions.NewsBroadcast | FactionPermissions.Invite);
        news.AddRank(5, "Генеральный директор", 95_000, FactionPermissions.All);
        service.RegisterFaction(news);

        // 7. Преступный синдикат (Криминал)
        var mafia = new Faction(7, "СИНДИКАТ", "Преступный синдикат", FactionType.Mafia, initialTreasury: 12_000_000);
        mafia.AddRank(1, "Новичок", 10_000, FactionPermissions.RadioFaction);
        mafia.AddRank(2, "Боец", 20_000, FactionPermissions.RadioFaction | FactionPermissions.Armory | FactionPermissions.Cuffs | FactionPermissions.SearchInventory);
        mafia.AddRank(3, "Бригадир", 38_000, FactionPermissions.RadioFaction | FactionPermissions.Armory | FactionPermissions.Cuffs | FactionPermissions.SearchInventory | FactionPermissions.Invite);
        mafia.AddRank(4, "Авторитет", 65_000, FactionPermissions.RadioFaction | FactionPermissions.Armory | FactionPermissions.Cuffs | FactionPermissions.SearchInventory | FactionPermissions.Invite | FactionPermissions.Promote | FactionPermissions.Demote | FactionPermissions.TreasuryWithdraw);
        mafia.AddRank(5, "Босс", 110_000, FactionPermissions.All);
        service.RegisterFaction(mafia);
    }
}
