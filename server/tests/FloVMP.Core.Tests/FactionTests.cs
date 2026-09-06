using System.Collections.Generic;
using System.Linq;
using FloVMP.Core.Factions;
using Xunit;

namespace FloVMP.Core.Tests;

public class FactionTests
{
    [Fact]
    public void Presets_LoadCorrectly()
    {
        var service = new FactionService(loadDefaultPresets: true);
        var factions = service.GetAllFactions();

        Assert.Equal(7, factions.Count);

        var police = service.GetFaction(2);
        Assert.NotNull(police);
        Assert.Equal("МВД", police.Tag);
        Assert.Equal(FactionType.Police, police.Type);
        Assert.True(police.IsGovernment);
        Assert.False(police.IsCrime);
        Assert.Equal(8, police.Ranks.Count);

        var mafia = service.GetFaction(7);
        Assert.NotNull(mafia);
        Assert.True(mafia.IsCrime);
        Assert.False(mafia.IsGovernment);
    }

    [Fact]
    public void Leader_HasAllPermissions()
    {
        var service = new FactionService(loadDefaultPresets: true);
        const int leaderId = 100;
        const int policeFactionId = 2;

        Assert.True(service.TrySetLeader(policeFactionId, leaderId, out var error));
        Assert.Empty(error);

        var member = service.GetMember(leaderId);
        Assert.NotNull(member);
        Assert.Equal(policeFactionId, member.FactionId);
        Assert.Equal(8, member.RankLevel);

        Assert.True(service.HasPermission(leaderId, FactionPermissions.Invite));
        Assert.True(service.HasPermission(leaderId, FactionPermissions.Kick));
        Assert.True(service.HasPermission(leaderId, FactionPermissions.Arrest));
        Assert.True(service.HasPermission(leaderId, FactionPermissions.TreasuryWithdraw));
    }

    [Fact]
    public void InviteAndKick_WorkWithProperPermissions()
    {
        var service = new FactionService(loadDefaultPresets: true);
        const int leaderId = 100;
        const int newRecruitId = 101;
        const int civilianId = 102;

        service.TrySetLeader(2, leaderId, out _);

        // Civilian cannot invite
        Assert.False(service.TryInvite(civilianId, newRecruitId, out var err1));
        Assert.Equal("Вы не состоите в организации", err1);

        // Leader invites recruit
        Assert.True(service.TryInvite(leaderId, newRecruitId, out var err2));
        Assert.Empty(err2);

        var recruit = service.GetMember(newRecruitId);
        Assert.NotNull(recruit);
        Assert.Equal(1, recruit.RankLevel); // Рядовой

        // Recruit rank 1 doesn't have invite permission
        Assert.False(service.TryInvite(newRecruitId, civilianId, out var err3));
        Assert.Equal("У вас нет полномочий принимать новых сотрудников", err3);

        // Recruit cannot kick leader
        Assert.False(service.TryKick(newRecruitId, leaderId, "test", out var err4));
        Assert.Equal("У вас нет полномочий увольнять сотрудников", err4);

        // Leader kicks recruit
        Assert.True(service.TryKick(leaderId, newRecruitId, "Не прошёл стажировку", out var err5));
        Assert.Empty(err5);
        Assert.Null(service.GetMember(newRecruitId));
    }

    [Fact]
    public void PromoteAndDemote_RankHierarchyEnforced()
    {
        var service = new FactionService(loadDefaultPresets: true);
        const int leaderId = 100;
        const int captainId = 101;
        const int recruitId = 102;

        service.TrySetLeader(2, leaderId, out _);

        // Invite recruit and captain
        service.TryInvite(leaderId, captainId, out _);
        service.TryInvite(leaderId, recruitId, out _);

        // Leader promotes captain to rank 5 (Капитан)
        Assert.True(service.TrySetRank(leaderId, captainId, 5, out _));

        // Captain promotes recruit to rank 2
        Assert.True(service.TrySetRank(captainId, recruitId, 2, out var errPromote));
        Assert.Empty(errPromote);
        Assert.Equal(2, service.GetMember(recruitId)!.RankLevel);

        // Captain cannot promote recruit to rank 5 (equal to captain)
        Assert.False(service.TrySetRank(captainId, recruitId, 5, out var errEqual));
        Assert.Equal("Вы не можете повысить сотрудника до своего ранга или выше", errEqual);

        // Captain cannot demote without Demote permission
        Assert.False(service.TrySetRank(captainId, recruitId, 1, out var errNoDemote));
        Assert.Equal("У вас нет полномочий понижать сотрудников", errNoDemote);

        // Leader promotes officer to rank 6 (Майор), who has Demote permission
        Assert.True(service.TrySetRank(leaderId, captainId, 6, out _));

        // Major cannot demote leader (higher rank)
        Assert.False(service.TrySetRank(captainId, leaderId, 7, out var errDemoteLeader));
        Assert.Equal("Вы не можете менять должность сотруднику равного или старшего ранга", errDemoteLeader);

        // Major CAN demote recruit back to rank 1
        Assert.True(service.TrySetRank(captainId, recruitId, 1, out var errDemoteOk));
        Assert.Empty(errDemoteOk);
        Assert.Equal(1, service.GetMember(recruitId)!.RankLevel);
    }

    [Fact]
    public void Treasury_DepositAndWithdraw()
    {
        var service = new FactionService(loadDefaultPresets: true);
        const int leaderId = 100;
        const int recruitId = 101;
        const int factionId = 2;

        service.TrySetLeader(factionId, leaderId, out _);
        service.TryInvite(leaderId, recruitId, out _);

        var faction = service.GetFaction(factionId)!;
        long initialTreasury = faction.TreasuryBalance;

        // Recruit deposits
        Assert.True(service.TryDepositTreasury(recruitId, 100_000, out _));
        Assert.Equal(initialTreasury + 100_000, faction.TreasuryBalance);

        // Recruit cannot withdraw
        Assert.False(service.TryWithdrawTreasury(recruitId, 50_000, "Премия", out var errWith));
        Assert.Equal("У вас нет права распоряжаться казной организации", errWith);

        // Leader can withdraw
        Assert.True(service.TryWithdrawTreasury(leaderId, 50_000, "Премия", out _));
        Assert.Equal(initialTreasury + 50_000, faction.TreasuryBalance);

        // Over-withdrawal fails
        Assert.False(service.TryWithdrawTreasury(leaderId, faction.TreasuryBalance + 1_000_000, "Откат", out var errOver));
        Assert.Equal("В казне организации недостаточно средств", errOver);
    }

    [Fact]
    public void CuffsAndArrest_Flow()
    {
        var service = new FactionService(loadDefaultPresets: true);
        const int leaderId = 100;
        const int suspectId = 200;

        service.TrySetLeader(2, leaderId, out _);

        // Self-cuff is prevented
        Assert.False(service.TryCuff(leaderId, leaderId, out var errSelf));
        Assert.Equal("Нельзя надеть наручники на самого себя", errSelf);

        // Cuff suspect
        Assert.False(service.IsCuffed(suspectId));
        Assert.True(service.TryCuff(leaderId, suspectId, out _));
        Assert.True(service.IsCuffed(suspectId));

        // Already cuffed
        Assert.False(service.TryCuff(leaderId, suspectId, out var errAlready));
        Assert.Equal("Гражданин уже в наручниках", errAlready);

        // Uncuff
        Assert.True(service.TryUncuff(leaderId, suspectId, out _));
        Assert.False(service.IsCuffed(suspectId));

        // Arrest suspect for 60 seconds
        Assert.True(service.TryArrest(leaderId, suspectId, 60, "228 УК РФ", out _));
        Assert.True(service.IsArrested(suspectId, out int rem, out string reason));
        Assert.Equal(60, rem);
        Assert.Equal("228 УК РФ", reason);

        // Tick 30 seconds
        var released30 = service.TickArrests(30);
        Assert.Empty(released30);
        Assert.True(service.IsArrested(suspectId, out rem, out _));
        Assert.Equal(30, rem);

        // Tick another 30 seconds -> released
        var released60 = service.TickArrests(30);
        Assert.Single(released60);
        Assert.Equal(suspectId, released60[0]);
        Assert.False(service.IsArrested(suspectId, out _, out _));
    }

    [Fact]
    public void Salaries_CalculatedProperlyOnPayDay()
    {
        var service = new FactionService(loadDefaultPresets: true);
        const int leaderId = 100;
        const int captainId = 101;
        const int recruitId = 102;
        const int civilianId = 999;

        service.TrySetLeader(2, leaderId, out _);
        service.TryInvite(leaderId, captainId, out _);
        service.TrySetRank(leaderId, captainId, 5, out _);
        service.TryInvite(leaderId, recruitId, out _);

        var online = new List<int> { leaderId, captainId, recruitId, civilianId };
        var payouts = service.CalculateSalaries(online);

        // Leader (General, Rank 8) -> 130,000
        Assert.Equal(130_000, payouts[leaderId]);
        // Captain (Rank 5) -> 58,000
        Assert.Equal(58_000, payouts[captainId]);
        // Recruit (Rank 1) -> 20,000
        Assert.Equal(20_000, payouts[recruitId]);
        // Civilian has no salary
        Assert.False(payouts.ContainsKey(civilianId));
    }
}
