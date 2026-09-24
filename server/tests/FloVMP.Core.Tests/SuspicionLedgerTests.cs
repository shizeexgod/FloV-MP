using System.Collections.Generic;
using System.Linq;
using FloVMP.Core.AntiCheat;
using FloVMP.Core.Native;
using Xunit;

namespace FloVMP.Core.Tests;

public class SuspicionLedgerTests
{
    private static readonly uint Pistol = GameHash.Joaat("weapon_pistol");
    private static readonly uint Rpg = GameHash.Joaat("weapon_rpg");

    private static NativePlayerState State(uint weapon = 0, uint ped = 0) =>
        new(0, 0, 30, 0, 0, 0, 0, 0, 0, 0, -1, 0, 0, 0, 200, 0, weapon, 0, ped);

    // ------------------------------------------------------------ журнал

    [Fact]
    public void Ledger_ScoreDecaysOverTime()
    {
        var l = new SuspicionLedger { DecayPerMinute = 10 };
        l.Add(1, "hit", 30, "", 0);
        Assert.Equal(30f, l.Score(1, 0));
        Assert.Equal(20f, l.Score(1, 60_000), 3);
        Assert.Equal(0f, l.Score(1, 600_000));
    }

    [Fact]
    public void Ledger_ThresholdsCrossOnce_AndRearmAfterDecay()
    {
        var l = new SuspicionLedger { NotifyScore = 50, KickScore = 100, DecayPerMinute = 60 };
        Assert.Equal(SuspicionLevel.None, l.Add(1, "x", 40, "", 0).Crossed);
        Assert.Equal(SuspicionLevel.Notify, l.Add(1, "x", 20, "", 0).Crossed);
        Assert.Equal(SuspicionLevel.None, l.Add(1, "x", 10, "", 0).Crossed);   // уже сообщили
        Assert.Equal(SuspicionLevel.Kick, l.Add(1, "x", 40, "", 0).Crossed);

        // Растаял до нуля — следующее пересечение снова событие.
        Assert.Equal(SuspicionLevel.Notify, l.Add(1, "x", 60, "", 10 * 60_000).Crossed);
    }

    [Fact]
    public void Ledger_KickOffByDefault()
    {
        var l = new SuspicionLedger();
        Assert.Equal(0f, l.KickScore);
        var (score, crossed) = l.Add(1, "x", 100000, "", 0);
        Assert.Equal(SuspicionLevel.Notify, crossed);
        Assert.True(score >= 100000);
    }

    [Fact]
    public void Ledger_HistoryIsBoundedAndForgiveClears()
    {
        var l = new SuspicionLedger();
        for (var i = 0; i < 50; i++) l.Add(1, "t" + i, 1, "", i);
        var h = l.History(1);
        Assert.Equal(SuspicionLedger.HistoryLimit, h.Count);
        Assert.Equal("t49", h[^1].Type);
        l.Forgive(1);
        Assert.Empty(l.History(1));
        Assert.Equal(0f, l.Score(1, 100));
    }

    // ------------------------------------------------------------ оружие и патроны

    [Fact]
    public void Hashes_MatchGameNames()
    {
        Assert.Equal(0x1B06D571u, Pistol);
        Assert.Equal(0x6D544C99u, GameHash.Joaat("weapon_railgun"));
        Assert.Equal(new HashSet<uint> { Rpg, 0x10u, 0xABu }, GameHash.ParseList("weapon_rpg, 16; 0xAB"));
    }

    [Fact]
    public void Blacklist_WeaponReportedOnceWhileHeld()
    {
        var ac = new NativeAntiCheat { WeaponBlacklist = new() { Rpg } };
        var reports = new List<string>();
        ac.Suspected += (_, type, _, _, _) => reports.Add(type);
        Assert.True(ac.CheckState(1, State(Rpg), 0));
        Assert.True(ac.CheckState(1, State(Rpg), 50));
        Assert.False(ac.CheckState(1, State(Pistol), 100));
        Assert.Equal(new[] { "weapon" }, reports);
    }

    [Fact]
    public void IssuedOnly_UnissuedWeaponIsSuspect_IssuedIsFine()
    {
        var ac = new NativeAntiCheat { IssuedWeaponsOnly = true };
        Assert.True(ac.CheckState(1, State(Pistol), 0));
        ac.Weapons.Issue(2, Pistol, 50);
        Assert.False(ac.CheckState(2, State(Pistol), 0));
        Assert.False(ac.CheckState(3, State(WeaponLedger.Unarmed), 0));
        Assert.False(ac.CheckState(3, State(GameHash.Joaat("gadget_parachute")), 0));
    }

    [Fact]
    public void Ammo_MoreHitsThanIssuedIsSuspect()
    {
        var ac = new NativeAntiCheat();
        var reports = 0;
        ac.Suspected += (_, type, _, _, _) => { if (type == "ammo") reports++; };
        ac.Weapons.Issue(1, Pistol, 3);
        for (var i = 0; i < 3; i++) ac.OnHit(1, Pistol, i);
        Assert.Equal(0, reports);
        ac.OnHit(1, Pistol, 10);
        Assert.Equal(1, reports);

        // Доливка патронов сервером снова открывает счёт.
        ac.Weapons.Issue(1, Pistol, 5);
        ac.OnHit(1, Pistol, 20_000);
        Assert.Equal(1, reports);
    }

    [Fact]
    public void Ammo_UntrackedAndMeleeAreNotCounted()
    {
        var ac = new NativeAntiCheat();
        var reports = 0;
        ac.Suspected += (_, _, _, _, _) => reports++;
        var knife = GameHash.Joaat("weapon_knife");
        ac.Weapons.Issue(1, knife, 0);                  // холодное: без учёта
        for (var i = 0; i < 100; i++) { ac.OnHit(1, knife, i); ac.OnHit(1, Pistol, i); }
        Assert.Equal(0, reports);
    }

    // ------------------------------------------------------------ модели, машины, часы

    [Fact]
    public void PedWhitelist_AllowsIssuedModel()
    {
        var allowed = GameHash.Joaat("mp_m_freemode_01");
        var cop = GameHash.Joaat("s_m_y_cop_01");
        var ac = new NativeAntiCheat { PedWhitelist = new() { allowed } };
        var reports = 0;
        ac.Suspected += (_, _, _, _, _) => reports++;
        ac.CheckState(1, State(ped: allowed), 0);
        ac.NoteIssuedModel(2, cop);
        ac.CheckState(2, State(ped: cop), 0);
        Assert.Equal(0, reports);
        ac.CheckState(3, State(ped: cop), 0);
        Assert.Equal(1, reports);
    }

    [Fact]
    public void VehicleBlacklist_ByName()
    {
        var ac = new NativeAntiCheat { VehicleBlacklist = GameHash.ParseList("rhino, lazer") };
        Assert.False(ac.VehicleAllowed(GameHash.Joaat("rhino")));
        Assert.True(ac.VehicleAllowed(0xB779A091));
    }

    [Fact]
    public void Clock_NormalIsFine_SpeedhackIsCaught()
    {
        var c = new ClockDriftDetector { WindowMs = 20_000, MaxRatio = 1.25f };
        // Честный клиент: часы идут вместе с сервером (с разбросом сети).
        Assert.Null(c.Sample(1, 1_000, 5_000));
        for (long t = 2_000; t <= 60_000; t += 2_000)
            Assert.Null(c.Sample(1, 1_000 + t + (t % 4000 == 0 ? 80 : -80), 5_000 + t));

        // Чит: часы клиента вдвое быстрее.
        Assert.Null(c.Sample(2, 0, 0));
        float? caught = null;
        for (long t = 2_000; t <= 40_000 && caught is null; t += 2_000) caught = c.Sample(2, t * 2, t);
        Assert.NotNull(caught);
        Assert.InRange(caught!.Value, 1.9f, 2.1f);
    }

    [Fact]
    public void Clock_ClientRestartDoesNotLookLikeSpeedhack()
    {
        var c = new ClockDriftDetector { WindowMs = 20_000 };
        c.Sample(1, 900_000, 0);
        Assert.Null(c.Sample(1, 1_000, 30_000));      // часы клиента сброшены — новое окно
        Assert.Null(c.Sample(1, 31_000, 60_000));
    }

    [Fact]
    public void Report_UsesKindWeights_CustomUsesGiven()
    {
        var ac = new NativeAntiCheat();
        ac.Weights[SuspicionKind.Hit] = 7;
        Assert.Equal(7f, ac.Report(1, SuspicionKind.Hit, "x", 0));
        Assert.Equal(107f, ac.Report(1, SuspicionKind.Custom, "дюп денег", 0, weight: 100));
    }

    [Fact]
    public void ReportLimited_OncePerCooldown()
    {
        var ac = new NativeAntiCheat();
        var n = 0;
        ac.Suspected += (_, _, _, _, _) => n++;
        for (var t = 0; t < 4000; t += 50) ac.ReportLimited(1, SuspicionKind.Movement, "лаг", t);
        Assert.Equal(1, n);
        ac.ReportLimited(1, SuspicionKind.Movement, "лаг", NativeAntiCheat.RepeatCooldownMs + 1);
        Assert.Equal(2, n);
    }

    [Fact]
    public void Threshold_EventCarriesLevel()
    {
        var ac = new NativeAntiCheat();
        ac.Ledger.NotifyScore = 20;
        var levels = new List<SuspicionLevel>();
        ac.ThresholdCrossed += (_, level, _) => levels.Add(level);
        ac.Report(1, SuspicionKind.Weapon, "rpg", 0);
        Assert.Equal(new[] { SuspicionLevel.Notify }, levels);
    }

    [Fact]
    public void Restore_KeepsZeroAmmoCounted()
    {
        // Аудит: GiveWeapon с нулём превращал сохранённые 0 патронов в «без учёта».
        var ac = new NativeAntiCheat();
        var reports = 0;
        ac.Suspected += (_, type, _, _, _) => { if (type == "ammo") reports++; };
        ac.Weapons.Issue(1, Pistol, 0);          // так сделал бы GiveWeapon(0)
        ac.Weapons.Restore(1, Pistol, 0);        // сохранение возвращает ровно 0
        ac.OnHit(1, Pistol, 0);
        Assert.Equal(1, reports);
        Assert.Equal(0, ac.Weapons.AmmoLeft(1, Pistol));
        ac.Weapons.Restore(1, GameHash.Joaat("weapon_knife"), -1);
        Assert.Equal(-1, ac.Weapons.AmmoLeft(1, GameHash.Joaat("weapon_knife")));
    }
}
