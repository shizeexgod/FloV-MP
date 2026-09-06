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

    public FactionService()
    {
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

            if (long.MaxValue - faction.TreasuryBalance < amount)
            {
                error = "Казна организации переполнена, невозможно зачислить средства";
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

            if (_cuffedAccounts.Contains(officerAccountId))
            {
                error = "Вы не можете применять наручники, находясь в наручниках";
                return false;
            }

            if (!HasPermission(officerAccountId, FactionPermissions.Cuffs))
            {
                error = "У вас нет права применять специальные средства (наручники)";
                return false;
            }

            if (_arrests.ContainsKey(targetAccountId))
            {
                error = "Гражданин уже отбывает срок в камере";
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
            if (_cuffedAccounts.Contains(officerAccountId))
            {
                error = "Вы не можете снимать наручники, находясь в наручниках";
                return false;
            }

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
        if (deltaSeconds <= 0) return released;

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
        if (onlineAccountIds == null) return payouts;

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
}
