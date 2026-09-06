using FloVMP.Core.Factions;

namespace FloVMP.Gamemode.Presets;

/// <summary>
/// Каталог организаций и государственных ведомств Москвы для сервера «Держава Онлайн».
/// Специфично для RP-проекта «Держава Онлайн», не привязано к ядру мультиплеера FloV:MP.
/// </summary>
public static class DerzhavaFactions
{
    public static void RegisterAll(FactionService service)
    {
        // 1. Мэрия Москвы (Правительство)
        var gov = new Faction(1, "МЭРИЯ", "Правительство Москвы", FactionType.Government, initialTreasury: 50_000_000);
        gov.AddRank(1, "Стажёр аппарата", 18_000, FactionPermissions.None);
        gov.AddRank(2, "Секретарь", 25_000, FactionPermissions.RadioFaction);
        gov.AddRank(3, "Юрист-консультант", 35_000, FactionPermissions.RadioFaction | FactionPermissions.RadioDepartment);
        gov.AddRank(4, "Инспектор мэрии", 48_000, FactionPermissions.RadioFaction | FactionPermissions.RadioDepartment | FactionPermissions.IssueFine);
        gov.AddRank(5, "Заместитель Мэра", 75_000, FactionPermissions.RadioFaction | FactionPermissions.RadioDepartment | FactionPermissions.Invite | FactionPermissions.Kick | FactionPermissions.Promote | FactionPermissions.Demote | FactionPermissions.TreasuryDeposit);
        gov.AddRank(6, "Мэр Москвы", 120_000, FactionPermissions.All);
        service.RegisterFaction(gov);

        // 2. ГУ МВД России по г. Москве
        var police = new Faction(2, "МВД", "ГУ МВД по г. Москве", FactionType.Police, initialTreasury: 25_000_000);
        police.AddRank(1, "Рядовой полиции", 20_000, FactionPermissions.RadioFaction);
        police.AddRank(2, "Сержант полиции", 28_000, FactionPermissions.RadioFaction | FactionPermissions.RadioDepartment | FactionPermissions.Cuffs | FactionPermissions.SearchInventory | FactionPermissions.Armory);
        police.AddRank(3, "Старшина полиции", 36_000, FactionPermissions.RadioFaction | FactionPermissions.RadioDepartment | FactionPermissions.Cuffs | FactionPermissions.SearchInventory | FactionPermissions.IssueFine | FactionPermissions.Armory);
        police.AddRank(4, "Лейтенант полиции", 46_000, FactionPermissions.RadioFaction | FactionPermissions.RadioDepartment | FactionPermissions.Cuffs | FactionPermissions.SearchInventory | FactionPermissions.IssueFine | FactionPermissions.Arrest | FactionPermissions.Armory);
        police.AddRank(5, "Капитан полиции", 58_000, FactionPermissions.RadioFaction | FactionPermissions.RadioDepartment | FactionPermissions.Cuffs | FactionPermissions.SearchInventory | FactionPermissions.IssueFine | FactionPermissions.Arrest | FactionPermissions.Armory | FactionPermissions.Invite | FactionPermissions.Promote);
        police.AddRank(6, "Майор полиции", 72_000, FactionPermissions.RadioFaction | FactionPermissions.RadioDepartment | FactionPermissions.Cuffs | FactionPermissions.SearchInventory | FactionPermissions.IssueFine | FactionPermissions.Arrest | FactionPermissions.Armory | FactionPermissions.Invite | FactionPermissions.Promote | FactionPermissions.Demote);
        police.AddRank(7, "Полковник полиции", 92_000, FactionPermissions.RadioFaction | FactionPermissions.RadioDepartment | FactionPermissions.Cuffs | FactionPermissions.SearchInventory | FactionPermissions.IssueFine | FactionPermissions.Arrest | FactionPermissions.Armory | FactionPermissions.Invite | FactionPermissions.Kick | FactionPermissions.Promote | FactionPermissions.Demote | FactionPermissions.TreasuryWithdraw | FactionPermissions.TreasuryDeposit);
        police.AddRank(8, "Генерал-майор полиции", 130_000, FactionPermissions.All);
        service.RegisterFaction(police);

        // 3. УФСБ России по г. Москве и МО
        var fsb = new Faction(3, "ФСБ", "УФСБ России по г. Москве", FactionType.SecurityService, initialTreasury: 35_000_000);
        fsb.AddRank(1, "Младший оперуполномоченный", 35_000, FactionPermissions.RadioFaction | FactionPermissions.Cuffs | FactionPermissions.SearchInventory | FactionPermissions.Armory);
        fsb.AddRank(2, "Оперуполномоченный ФСБ", 50_000, FactionPermissions.RadioFaction | FactionPermissions.RadioDepartment | FactionPermissions.Cuffs | FactionPermissions.SearchInventory | FactionPermissions.Arrest | FactionPermissions.Armory);
        fsb.AddRank(3, "Старший оперуполномоченный", 70_000, FactionPermissions.RadioFaction | FactionPermissions.RadioDepartment | FactionPermissions.Cuffs | FactionPermissions.SearchInventory | FactionPermissions.Arrest | FactionPermissions.Armory);
        fsb.AddRank(4, "Следователь по ОВД", 95_000, FactionPermissions.RadioFaction | FactionPermissions.RadioDepartment | FactionPermissions.Cuffs | FactionPermissions.SearchInventory | FactionPermissions.Arrest | FactionPermissions.Armory | FactionPermissions.Invite);
        fsb.AddRank(5, "Начальник отдела ФСБ", 125_000, FactionPermissions.RadioFaction | FactionPermissions.RadioDepartment | FactionPermissions.Cuffs | FactionPermissions.SearchInventory | FactionPermissions.Arrest | FactionPermissions.Armory | FactionPermissions.Invite | FactionPermissions.Kick | FactionPermissions.Promote | FactionPermissions.Demote);
        fsb.AddRank(6, "Директор УФСБ", 175_000, FactionPermissions.All);
        service.RegisterFaction(fsb);

        // 4. Городская Клиническая Больница (ГКБ им. Боткина)
        var hospital = new Faction(4, "ГКБ", "ГКБ им. С.П. Боткина", FactionType.Hospital, initialTreasury: 15_000_000);
        hospital.AddRank(1, "Интерн", 18_000, FactionPermissions.RadioFaction);
        hospital.AddRank(2, "Фельдшер СМП", 28_000, FactionPermissions.RadioFaction | FactionPermissions.RadioDepartment | FactionPermissions.Heal);
        hospital.AddRank(3, "Врач-терапевт", 40_000, FactionPermissions.RadioFaction | FactionPermissions.RadioDepartment | FactionPermissions.Heal);
        hospital.AddRank(4, "Врач-хирург", 58_000, FactionPermissions.RadioFaction | FactionPermissions.RadioDepartment | FactionPermissions.Heal | FactionPermissions.Invite);
        hospital.AddRank(5, "Заведующий отделением", 78_000, FactionPermissions.RadioFaction | FactionPermissions.RadioDepartment | FactionPermissions.Heal | FactionPermissions.Invite | FactionPermissions.Promote | FactionPermissions.Demote);
        hospital.AddRank(6, "Главный врач", 115_000, FactionPermissions.All);
        service.RegisterFaction(hospital);

        // 5. Вооруженные Силы РФ (Воинская часть)
        var army = new Faction(5, "АРМИЯ", "Воинская часть ВС РФ", FactionType.Army, initialTreasury: 20_000_000);
        army.AddRank(1, "Рядовой", 16_000, FactionPermissions.RadioFaction);
        army.AddRank(2, "Сержант", 26_000, FactionPermissions.RadioFaction | FactionPermissions.RadioDepartment | FactionPermissions.Armory);
        army.AddRank(3, "Прапорщик", 38_000, FactionPermissions.RadioFaction | FactionPermissions.RadioDepartment | FactionPermissions.Armory | FactionPermissions.Storage);
        army.AddRank(4, "Лейтенант", 52_000, FactionPermissions.RadioFaction | FactionPermissions.RadioDepartment | FactionPermissions.Armory | FactionPermissions.Invite);
        army.AddRank(5, "Майор", 72_000, FactionPermissions.RadioFaction | FactionPermissions.RadioDepartment | FactionPermissions.Armory | FactionPermissions.Invite | FactionPermissions.Promote | FactionPermissions.Demote);
        army.AddRank(6, "Командир полка", 115_000, FactionPermissions.All);
        service.RegisterFaction(army);

        // 6. Телеканал «Москва 24» (СМИ)
        var news = new Faction(6, "СМИ", "Телеканал Москва 24", FactionType.News, initialTreasury: 10_000_000);
        news.AddRank(1, "Стажёр редакции", 16_000, FactionPermissions.RadioFaction);
        news.AddRank(2, "Репортёр", 28_000, FactionPermissions.RadioFaction | FactionPermissions.NewsBroadcast);
        news.AddRank(3, "Ведущий эфира", 42_000, FactionPermissions.RadioFaction | FactionPermissions.NewsBroadcast);
        news.AddRank(4, "Режиссёр эфира", 60_000, FactionPermissions.RadioFaction | FactionPermissions.NewsBroadcast | FactionPermissions.Invite);
        news.AddRank(5, "Генеральный директор", 95_000, FactionPermissions.All);
        service.RegisterFaction(news);

        // 7. Солнцевская ОПГ (Криминал)
        var mafia = new Faction(7, "ОПГ-С", "Солнцевская ОПГ", FactionType.Mafia, initialTreasury: 12_000_000);
        mafia.AddRank(1, "Шнырь", 10_000, FactionPermissions.RadioFaction);
        mafia.AddRank(2, "Боец", 20_000, FactionPermissions.RadioFaction | FactionPermissions.Armory | FactionPermissions.Cuffs | FactionPermissions.SearchInventory);
        mafia.AddRank(3, "Бригадир", 38_000, FactionPermissions.RadioFaction | FactionPermissions.Armory | FactionPermissions.Cuffs | FactionPermissions.SearchInventory | FactionPermissions.Invite);
        mafia.AddRank(4, "Авторитет", 65_000, FactionPermissions.RadioFaction | FactionPermissions.Armory | FactionPermissions.Cuffs | FactionPermissions.SearchInventory | FactionPermissions.Invite | FactionPermissions.Promote | FactionPermissions.Demote | FactionPermissions.TreasuryWithdraw);
        mafia.AddRank(5, "Вор в законе", 110_000, FactionPermissions.All);
        service.RegisterFaction(mafia);
    }
}
