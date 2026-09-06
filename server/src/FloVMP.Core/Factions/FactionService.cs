using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace FloVMP.Core.Factions;

/// <summary>
/// Сервис управления организациями, кадрами, казной, рангами и наручниками/арестами.
/// Полностью независим от сетевого движка (чистый .NET 8), потокобезопасен.
/// </summary>
public sealed class FactionService
{
    private readonly object _lock = new();
    private readonly Dictionary<int, Faction> _factions = new();
    private readonly Dictionary<int, FactionMember> _members = new();
    private readonly HashSet<int> _cuffedAccounts = new();
    private readonly Dictionary<int, ArrestRecord> _arrests = new();

    public FactionService(bool loadDefaultPresets = true)
    {
        if (loadDefaultPresets)
        {
            LoadDefaultPresets();
        }
    }

    public void RegisterFaction(Faction faction)
    {
        lock (_lock)
        {
            _factions[faction.Id] = faction;
        }
    }

    public Faction? GetFaction(int factionId)
    {
        lock (_lock)
        {
            return _factions.TryGetValue(factionId, out var f) ? f : null;
        }
    }

    public IReadOnlyList<Faction> GetAllFactions()
    {
        lock (_lock)
        {
            return _factions.Values.ToList();
        }
    }

    public FactionMember? GetMember(int accountId)
    {
        lock (_lock)
        {
            return _members.TryGetValue(accountId, out var m) ? m : null;
        }
    }

    public IReadOnlyList<FactionMember> GetFactionMembers(int factionId)
    {
        lock (_lock)
        {
            return _members.Values.Where(m => m.FactionId == factionId).ToList();
        }
    }

    public bool HasPermission(int accountId, FactionPermissions permission)
    {
        lock (_lock)
        {
            if (!_members.TryGetValue(accountId, out var member)) return false;
            if (!_factions.TryGetValue(member.FactionId, out var faction)) return false;

            if (faction.LeaderAccountId == accountId) return true;

            var rank = faction.GetRank(member.RankLevel);
            if (rank == null) return false;

            return (rank.Permissions & permission) == permission || (rank.Permissions & FactionPermissions.All) == FactionPermissions.All;
        }
    }

    public bool TrySetLeader(int factionId, int targetAccountId, out string error)
    {
        lock (_lock)
        {
            if (!_factions.TryGetValue(factionId, out var faction))
            {
                error = "Организация не найдена";
                return false;
            }

            int maxRank = faction.Ranks.Keys.DefaultIfEmpty(1).Max();
            faction.LeaderAccountId = targetAccountId;

            _members[targetAccountId] = new FactionMember(targetAccountId, factionId, maxRank, "Лидер");
            error = string.Empty;
            return true;
        }
    }

    public bool TryInvite(int officerAccountId, int targetAccountId, out string error)
    {
        lock (_lock)
        {
            if (!_members.TryGetValue(officerAccountId, out var officer))
            {
                error = "Вы не состоите в организации";
                return false;
            }

            if (!_factions.TryGetValue(officer.FactionId, out var faction))
            {
                error = "Организация не найдена";
                return false;
            }

            bool isLeader = faction.LeaderAccountId == officerAccountId;
            var rank = faction.GetRank(officer.RankLevel);
            bool canInvite = isLeader || (rank != null && (rank.Permissions & FactionPermissions.Invite) != 0);

            if (!canInvite)
            {
                error = "У вас нет полномочий принимать новых сотрудников";
                return false;
            }

            if (_members.ContainsKey(targetAccountId))
            {
                error = "Игрок уже состоит в организации";
                return false;
            }

            _members[targetAccountId] = new FactionMember(targetAccountId, officer.FactionId, 1);
            error = string.Empty;
            return true;
        }
    }

    public bool TryKick(int officerAccountId, int targetAccountId, string reason, out string error)
    {
        lock (_lock)
        {
            if (!_members.TryGetValue(officerAccountId, out var officer))
            {
                error = "Вы не состоите в организации";
                return false;
            }

            if (!_members.TryGetValue(targetAccountId, out var target))
            {
                error = "Игрок не состоит в вашей организации";
                return false;
            }

            if (officer.FactionId != target.FactionId)
            {
                error = "Игрок состоит в другой организации";
                return false;
            }

            if (!_factions.TryGetValue(officer.FactionId, out var faction))
            {
                error = "Организация не найдена";
                return false;
            }

            bool isLeader = faction.LeaderAccountId == officerAccountId;
            var officerRank = faction.GetRank(officer.RankLevel);
            bool canKick = isLeader || (officerRank != null && (officerRank.Permissions & FactionPermissions.Kick) != 0);

            if (!canKick)
            {
                error = "У вас нет полномочий увольнять сотрудников";
                return false;
            }

            if (!isLeader && officer.RankLevel <= target.RankLevel)
            {
                error = "Нельзя уволить сотрудника равного или старшего по званию";
                return false;
            }

            if (targetAccountId == faction.LeaderAccountId)
            {
                error = "Нельзя уволить лидера организации";
                return false;
            }

            _members.Remove(targetAccountId);
            error = string.Empty;
            return true;
        }
    }

    public bool TrySetRank(int officerAccountId, int targetAccountId, int newRankLevel, out string error)
    {
        lock (_lock)
        {
            if (!_members.TryGetValue(officerAccountId, out var officer))
            {
                error = "Вы не состоите в организации";
                return false;
            }

            if (!_members.TryGetValue(targetAccountId, out var target))
            {
                error = "Игрок не состоит в вашей организации";
                return false;
            }

            if (officer.FactionId != target.FactionId)
            {
                error = "Игрок состоит в другой организации";
                return false;
            }

            if (!_factions.TryGetValue(officer.FactionId, out var faction))
            {
                error = "Организация не найдена";
                return false;
            }

            if (!faction.Ranks.ContainsKey(newRankLevel))
            {
                error = $"Ранг {newRankLevel} не существует в организации";
                return false;
            }

            bool isLeader = faction.LeaderAccountId == officerAccountId;
            var officerRank = faction.GetRank(officer.RankLevel);

            bool isPromote = newRankLevel > target.RankLevel;
            var requiredPerm = isPromote ? FactionPermissions.Promote : FactionPermissions.Demote;

            bool canManage = isLeader || (officerRank != null && (officerRank.Permissions & requiredPerm) != 0);
            if (!canManage)
            {
                error = isPromote ? "У вас нет полномочий повышать сотрудников" : "У вас нет полномочий понижать сотрудников";
                return false;
            }

            if (!isLeader)
            {
                if (target.RankLevel >= officer.RankLevel)
                {
                    error = "Вы не можете менять должность сотруднику равного или старшего ранга";
                    return false;
                }

                if (newRankLevel >= officer.RankLevel)
                {
                    error = "Вы не можете повысить сотрудника до своего ранга или выше";
                    return false;
                }
            }

            target.RankLevel = newRankLevel;
            error = string.Empty;
            return true;
        }
    }

    public bool TryLeave(int accountId, out string error)
    {
        lock (_lock)
        {
            if (!_members.TryGetValue(accountId, out var member))
            {
                error = "Вы не состоите ни в одной организации";
                return false;
            }

            if (_factions.TryGetValue(member.FactionId, out var faction) && faction.LeaderAccountId == accountId)
            {
                error = "Лидер не может покинуть организацию без передачи лидерства";
                return false;
            }

            _members.Remove(accountId);
            error = string.Empty;
            return true;
        }
    }

    public bool TryDepositTreasury(int accountId, long amount, out string error)
    {
        if (amount <= 0)
        {
            error = "Сумма пополнения должна быть больше нуля";
            return false;
        }

        lock (_lock)
        {
            if (!_members.TryGetValue(accountId, out var member))
            {
                error = "Вы не состоите в организации";
                return false;
            }

            if (!_factions.TryGetValue(member.FactionId, out var faction))
            {
                error = "Организация не найдена";
                return false;
            }

            faction.TreasuryBalance += amount;
            error = string.Empty;
            return true;
        }
    }

    public bool TryWithdrawTreasury(int accountId, long amount, string reason, out string error)
    {
        if (amount <= 0)
        {
            error = "Сумма снятия должна быть больше нуля";
            return false;
        }

        lock (_lock)
        {
            if (!_members.TryGetValue(accountId, out var member))
            {
                error = "Вы не состоите в организации";
                return false;
            }

            if (!_factions.TryGetValue(member.FactionId, out var faction))
            {
                error = "Организация не найдена";
                return false;
            }

            if (!HasPermission(accountId, FactionPermissions.TreasuryWithdraw))
            {
                error = "У вас нет права распоряжаться казной организации";
                return false;
            }

            if (faction.TreasuryBalance < amount)
            {
                error = "В казне организации недостаточно средств";
                return false;
            }

            faction.TreasuryBalance -= amount;
            error = string.Empty;
            return true;
        }
    }

    // --- Наручники и арест ------------------------------------------------

    public bool TryCuff(int officerAccountId, int targetAccountId, out string error)
    {
        lock (_lock)
        {
            if (officerAccountId == targetAccountId)
            {
                error = "Нельзя надеть наручники на самого себя";
                return false;
            }

            if (!HasPermission(officerAccountId, FactionPermissions.Cuffs))
            {
                error = "У вас нет права применять специальные средства (наручники)";
                return false;
            }

            if (_cuffedAccounts.Contains(targetAccountId))
            {
                error = "Гражданин уже в наручниках";
                return false;
            }

            _cuffedAccounts.Add(targetAccountId);
            error = string.Empty;
            return true;
        }
    }

    public bool TryUncuff(int officerAccountId, int targetAccountId, out string error)
    {
        lock (_lock)
        {
            if (!HasPermission(officerAccountId, FactionPermissions.Cuffs))
            {
                error = "У вас нет права снимать специальные средства (наручники)";
                return false;
            }

            if (!_cuffedAccounts.Contains(targetAccountId))
            {
                error = "На гражданине нет наручников";
                return false;
            }

            _cuffedAccounts.Remove(targetAccountId);
            error = string.Empty;
            return true;
        }
    }

    public bool IsCuffed(int accountId)
    {
        lock (_lock)
        {
            return _cuffedAccounts.Contains(accountId);
        }
    }

    public bool TryArrest(int officerAccountId, int targetAccountId, int seconds, string reason, out string error)
    {
        if (seconds <= 0 || seconds > 7200)
        {
            error = "Время ареста должно быть от 1 до 7200 секунд (2 часа)";
            return false;
        }

        lock (_lock)
        {
            if (!HasPermission(officerAccountId, FactionPermissions.Arrest))
            {
                error = "У вас нет полномочий производить арест";
                return false;
            }

            _cuffedAccounts.Remove(targetAccountId); // Снимаем наручники при оформлении в камеру
            _arrests[targetAccountId] = new ArrestRecord(targetAccountId, officerAccountId, reason, seconds);
            error = string.Empty;
            return true;
        }
    }

    public bool TryRelease(int officerAccountId, int targetAccountId, out string error)
    {
        lock (_lock)
        {
            if (!HasPermission(officerAccountId, FactionPermissions.Arrest))
            {
                error = "У вас нет полномочий освобождать заключённых";
                return false;
            }

            if (!_arrests.Remove(targetAccountId))
            {
                error = "Игрок не находится под арестом";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }

    public bool IsArrested(int accountId, out int remainingSeconds, out string reason)
    {
        lock (_lock)
        {
            if (_arrests.TryGetValue(accountId, out var rec))
            {
                remainingSeconds = rec.RemainingSeconds;
                reason = rec.Reason;
                return true;
            }

            remainingSeconds = 0;
            reason = string.Empty;
            return false;
        }
    }

    public List<int> TickArrests(int deltaSeconds)
    {
        var released = new List<int>();
        lock (_lock)
        {
            var keys = _arrests.Keys.ToList();
            foreach (var id in keys)
            {
                var record = _arrests[id];
                record.RemainingSeconds -= deltaSeconds;
                if (record.RemainingSeconds <= 0)
                {
                    _arrests.Remove(id);
                    released.Add(id);
                }
            }
        }
        return released;
    }

    // --- Зарплаты в пейдей (PayDay) ---------------------------------------

    public Dictionary<int, long> CalculateSalaries(IEnumerable<int> onlineAccountIds)
    {
        var payouts = new Dictionary<int, long>();
        lock (_lock)
        {
            foreach (var accountId in onlineAccountIds)
            {
                if (!_members.TryGetValue(accountId, out var member)) continue;
                if (!_factions.TryGetValue(member.FactionId, out var faction)) continue;

                var rank = faction.GetRank(member.RankLevel);
                if (rank != null && rank.Salary > 0)
                {
                    payouts[accountId] = rank.Salary;
                }
            }
        }
        return payouts;
    }

    // --- Пресеты «Держава Онлайн» ------------------------------------------

    private void LoadDefaultPresets()
    {
        // 1. Мэрия Москвы (Правительство)
        var gov = new Faction(1, "МЭРИЯ", "Правительство Москвы", FactionType.Government, initialTreasury: 50_000_000);
        gov.AddRank(1, "Стажёр аппарата", 18_000, FactionPermissions.None);
        gov.AddRank(2, "Секретарь", 25_000, FactionPermissions.RadioFaction);
        gov.AddRank(3, "Юрист-консультант", 35_000, FactionPermissions.RadioFaction | FactionPermissions.RadioDepartment);
        gov.AddRank(4, "Инспектор мэрии", 48_000, FactionPermissions.RadioFaction | FactionPermissions.RadioDepartment | FactionPermissions.IssueFine);
        gov.AddRank(5, "Заместитель Мэра", 75_000, FactionPermissions.RadioFaction | FactionPermissions.RadioDepartment | FactionPermissions.Invite | FactionPermissions.Kick | FactionPermissions.Promote | FactionPermissions.Demote | FactionPermissions.TreasuryDeposit);
        gov.AddRank(6, "Мэр Москвы", 120_000, FactionPermissions.All);
        RegisterFaction(gov);

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
        RegisterFaction(police);

        // 3. УФСБ России по г. Москве и МО
        var fsb = new Faction(3, "ФСБ", "УФСБ России по г. Москве", FactionType.SecurityService, initialTreasury: 35_000_000);
        fsb.AddRank(1, "Младший оперуполномоченный", 35_000, FactionPermissions.RadioFaction | FactionPermissions.Cuffs | FactionPermissions.SearchInventory | FactionPermissions.Armory);
        fsb.AddRank(2, "Оперуполномоченный ФСБ", 50_000, FactionPermissions.RadioFaction | FactionPermissions.RadioDepartment | FactionPermissions.Cuffs | FactionPermissions.SearchInventory | FactionPermissions.Arrest | FactionPermissions.Armory);
        fsb.AddRank(3, "Старший оперуполномоченный", 70_000, FactionPermissions.RadioFaction | FactionPermissions.RadioDepartment | FactionPermissions.Cuffs | FactionPermissions.SearchInventory | FactionPermissions.Arrest | FactionPermissions.Armory);
        fsb.AddRank(4, "Следователь по ОВД", 95_000, FactionPermissions.RadioFaction | FactionPermissions.RadioDepartment | FactionPermissions.Cuffs | FactionPermissions.SearchInventory | FactionPermissions.Arrest | FactionPermissions.Armory | FactionPermissions.Invite);
        fsb.AddRank(5, "Начальник отдела ФСБ", 125_000, FactionPermissions.RadioFaction | FactionPermissions.RadioDepartment | FactionPermissions.Cuffs | FactionPermissions.SearchInventory | FactionPermissions.Arrest | FactionPermissions.Armory | FactionPermissions.Invite | FactionPermissions.Kick | FactionPermissions.Promote | FactionPermissions.Demote);
        fsb.AddRank(6, "Директор УФСБ", 175_000, FactionPermissions.All);
        RegisterFaction(fsb);

        // 4. Городская Клиническая Больница (ГКБ им. Боткина)
        var hospital = new Faction(4, "ГКБ", "ГКБ им. С.П. Боткина", FactionType.Hospital, initialTreasury: 15_000_000);
        hospital.AddRank(1, "Интерн", 18_000, FactionPermissions.RadioFaction);
        hospital.AddRank(2, "Фельдшер СМП", 28_000, FactionPermissions.RadioFaction | FactionPermissions.RadioDepartment | FactionPermissions.Heal);
        hospital.AddRank(3, "Врач-терапевт", 40_000, FactionPermissions.RadioFaction | FactionPermissions.RadioDepartment | FactionPermissions.Heal);
        hospital.AddRank(4, "Врач-хирург", 58_000, FactionPermissions.RadioFaction | FactionPermissions.RadioDepartment | FactionPermissions.Heal | FactionPermissions.Invite);
        hospital.AddRank(5, "Заведующий отделением", 78_000, FactionPermissions.RadioFaction | FactionPermissions.RadioDepartment | FactionPermissions.Heal | FactionPermissions.Invite | FactionPermissions.Promote | FactionPermissions.Demote);
        hospital.AddRank(6, "Главный врач", 115_000, FactionPermissions.All);
        RegisterFaction(hospital);

        // 5. Вооруженные Силы РФ (Воинская часть)
        var army = new Faction(5, "АРМИЯ", "Воинская часть ВС РФ", FactionType.Army, initialTreasury: 20_000_000);
        army.AddRank(1, "Рядовой", 16_000, FactionPermissions.RadioFaction);
        army.AddRank(2, "Сержант", 26_000, FactionPermissions.RadioFaction | FactionPermissions.RadioDepartment | FactionPermissions.Armory);
        army.AddRank(3, "Прапорщик", 38_000, FactionPermissions.RadioFaction | FactionPermissions.RadioDepartment | FactionPermissions.Armory | FactionPermissions.Storage);
        army.AddRank(4, "Лейтенант", 52_000, FactionPermissions.RadioFaction | FactionPermissions.RadioDepartment | FactionPermissions.Armory | FactionPermissions.Invite);
        army.AddRank(5, "Майор", 72_000, FactionPermissions.RadioFaction | FactionPermissions.RadioDepartment | FactionPermissions.Armory | FactionPermissions.Invite | FactionPermissions.Promote | FactionPermissions.Demote);
        army.AddRank(6, "Командир полка", 115_000, FactionPermissions.All);
        RegisterFaction(army);

        // 6. Телеканал «Москва 24» (СМИ)
        var news = new Faction(6, "СМИ", "Телеканал Москва 24", FactionType.News, initialTreasury: 10_000_000);
        news.AddRank(1, "Стажёр редакции", 16_000, FactionPermissions.RadioFaction);
        news.AddRank(2, "Репортёр", 28_000, FactionPermissions.RadioFaction | FactionPermissions.NewsBroadcast);
        news.AddRank(3, "Ведущий эфира", 42_000, FactionPermissions.RadioFaction | FactionPermissions.NewsBroadcast);
        news.AddRank(4, "Режиссёр эфира", 60_000, FactionPermissions.RadioFaction | FactionPermissions.NewsBroadcast | FactionPermissions.Invite);
        news.AddRank(5, "Генеральный директор", 95_000, FactionPermissions.All);
        RegisterFaction(news);

        // 7. Солнцевская ОПГ (Криминал)
        var mafia = new Faction(7, "ОПГ-С", "Солнцевская ОПГ", FactionType.Mafia, initialTreasury: 12_000_000);
        mafia.AddRank(1, "Шнырь", 10_000, FactionPermissions.RadioFaction);
        mafia.AddRank(2, "Боец", 20_000, FactionPermissions.RadioFaction | FactionPermissions.Armory | FactionPermissions.Cuffs | FactionPermissions.SearchInventory);
        mafia.AddRank(3, "Бригадир", 38_000, FactionPermissions.RadioFaction | FactionPermissions.Armory | FactionPermissions.Cuffs | FactionPermissions.SearchInventory | FactionPermissions.Invite);
        mafia.AddRank(4, "Авторитет", 65_000, FactionPermissions.RadioFaction | FactionPermissions.Armory | FactionPermissions.Cuffs | FactionPermissions.SearchInventory | FactionPermissions.Invite | FactionPermissions.Promote | FactionPermissions.Demote | FactionPermissions.TreasuryWithdraw);
        mafia.AddRank(5, "Вор в законе", 110_000, FactionPermissions.All);
        RegisterFaction(mafia);
    }
}
