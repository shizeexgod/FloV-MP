using System;
using FloVMP.Core.AntiCheat;
using Xunit;

namespace FloVMP.Core.Tests;

public class CombatValidationTests
{
    [Fact]
    public void LegitimatePistolTorsoHit_CalculatesCorrectArmorAndHealth()
    {
        var combat = new CombatValidationService();
        uint pistolHash = 0x1B06D571; // Pistol: 26 base damage

        var res = combat.ValidateHit(
            attackerId: 1,
            attackerPos: new Vector3D(0, 0, 0),
            attackerDim: 0,
            victimId: 2,
            victimPos: new Vector3D(10, 0, 0), // 10м
            victimDim: 0,
            currentVictimHealth: 100f,
            currentVictimArmour: 50f,
            weaponHash: pistolHash,
            zone: HitboxZone.Torso,
            shotTimeUtc: DateTime.UtcNow
        );

        Assert.True(res.IsValid);
        Assert.Equal(CombatViolationType.None, res.Violation);
        Assert.Equal(26f, res.CalculatedDamage);

        // 70% от 26 урона (18.2) поглощает броня: 50 - 18.2 = 31.8
        // 30% от 26 урона (7.8) идёт в здоровье: 100 - 7.8 = 92.2
        Assert.Equal(31.8f, res.NewArmour);
        Assert.Equal(92.2f, res.NewHealth);
        Assert.False(res.IsFatal);
    }

    [Fact]
    public void Headshot_AppliesHeadshotMultiplier()
    {
        var combat = new CombatValidationService();
        uint pistolHash = 0x1B06D571; // 26 base * 2.0 head multiplier = 52 damage

        var res = combat.ValidateHit(
            attackerId: 1,
            attackerPos: new Vector3D(0, 0, 0),
            attackerDim: 0,
            victimId: 2,
            victimPos: new Vector3D(5, 0, 0),
            victimDim: 0,
            currentVictimHealth: 100f,
            currentVictimArmour: 0f, // Без брони
            weaponHash: pistolHash,
            zone: HitboxZone.Head,
            shotTimeUtc: DateTime.UtcNow
        );

        Assert.True(res.IsValid);
        Assert.Equal(52f, res.CalculatedDamage);
        Assert.Equal(48f, res.NewHealth); // 100 - 52
    }

    [Fact]
    public void ExtremeDistance_ShotgunHit_IsBlocked()
    {
        var combat = new CombatValidationService();
        uint shotgunHash = 0x1D073A89; // Pump Shotgun: max range 35м

        var res = combat.ValidateHit(
            attackerId: 1,
            attackerPos: new Vector3D(0, 0, 0),
            attackerDim: 0,
            victimId: 2,
            victimPos: new Vector3D(90, 0, 0), // 90м (> 35м)
            victimDim: 0,
            currentVictimHealth: 100f,
            currentVictimArmour: 100f,
            weaponHash: shotgunHash,
            zone: HitboxZone.Torso,
            shotTimeUtc: DateTime.UtcNow
        );

        Assert.False(res.IsValid);
        Assert.Equal(CombatViolationType.ExtremeDistance, res.Violation);
    }

    [Fact]
    public void RapidFireHack_IsBlocked()
    {
        var combat = new CombatValidationService();
        uint sniperHash = 0x05FC3C11; // Sniper Rifle: min interval 1.2s
        var now = DateTime.UtcNow;

        // Первый легитимный выстрел
        combat.ValidateHit(1, new Vector3D(0, 0, 0), 0, 2, new Vector3D(50, 0, 0), 0, 100f, 0f, sniperHash, HitboxZone.Torso, now);

        // Второй выстрел через 50 миллисекунд (чит Rapid Fire)
        var res = combat.ValidateHit(1, new Vector3D(0, 0, 0), 0, 2, new Vector3D(50, 0, 0), 0, 100f, 0f, sniperHash, HitboxZone.Torso, now.AddMilliseconds(50));

        Assert.False(res.IsValid);
        Assert.Equal(CombatViolationType.RapidFire, res.Violation);
    }

    [Fact]
    public void CrossDimensionAttack_IsBlocked()
    {
        var combat = new CombatValidationService();

        var res = combat.ValidateHit(
            attackerId: 1,
            attackerPos: new Vector3D(0, 0, 0),
            attackerDim: 0, // Улица
            victimId: 2,
            victimPos: new Vector3D(2, 0, 0),
            victimDim: 10, // Интерьер
            currentVictimHealth: 100f,
            currentVictimArmour: 0f,
            weaponHash: 0x1B06D571,
            zone: HitboxZone.Torso,
            shotTimeUtc: DateTime.UtcNow
        );

        Assert.False(res.IsValid);
        Assert.Equal(CombatViolationType.CrossDimensionAttack, res.Violation);
    }

    [Fact]
    public void CleanupPlayer_ClearsCachedLastShots()
    {
        var combat = new CombatValidationService();
        uint sniperHash = 0x05FC3C11;
        var now = DateTime.UtcNow;

        combat.ValidateHit(10, new Vector3D(0, 0, 0), 0, 2, new Vector3D(50, 0, 0), 0, 100f, 0f, sniperHash, HitboxZone.Torso, now);

        // Без очистки повторный выстрел через 50мс был бы RapidFire
        // Очищаем игрока при дисконнекте:
        combat.CleanupPlayer(10);

        // Новый выстрел с тем же интервалом принимается как первый выстрел новой сессии
        var res = combat.ValidateHit(10, new Vector3D(0, 0, 0), 0, 2, new Vector3D(50, 0, 0), 0, 100f, 0f, sniperHash, HitboxZone.Torso, now.AddMilliseconds(50));
        Assert.True(res.IsValid);
    }

    [Fact]
    public void Shotgun_MultiplePelletsInSameShot_AreAllowed()
    {
        var combat = new CombatValidationService();
        uint pumpShotgunHash = 0x1D073A89;
        var now = DateTime.UtcNow;

        // Имитируем попадание 6 дробинок из одного залпа дробовика (с интервалами 2-5мс)
        for (int i = 0; i < 6; i++)
        {
            var res = combat.ValidateHit(
                attackerId: 1,
                attackerPos: new Vector3D(0, 0, 0),
                attackerDim: 0,
                victimId: 2,
                victimPos: new Vector3D(8, 0, 0),
                victimDim: 0,
                currentVictimHealth: 100f,
                currentVictimArmour: 50f,
                weaponHash: pumpShotgunHash,
                zone: HitboxZone.Torso,
                shotTimeUtc: now.AddMilliseconds(i * 3)
            );

            Assert.True(res.IsValid, $"Pellet #{i + 1} should be valid");
            Assert.Equal(CombatViolationType.None, res.Violation);
        }
    }

    [Fact]
    public void Shotgun_ExceedingMaxPellets_TriggersRapidFire()
    {
        var combat = new CombatValidationService();
        uint pumpShotgunHash = 0x1D073A89; // Max 8 pellets
        var now = DateTime.UtcNow;

        // 8 дробинок проходят
        for (int i = 0; i < 8; i++)
        {
            var r = combat.ValidateHit(1, new Vector3D(0, 0, 0), 0, 2, new Vector3D(8, 0, 0), 0, 100f, 0f, pumpShotgunHash, HitboxZone.Torso, now.AddMilliseconds(i * 2));
            Assert.True(r.IsValid);
        }

        // 9-я дробинка в том же окне (< 80мс) превышает лимит залпа и триггерит RapidFire
        var res9 = combat.ValidateHit(1, new Vector3D(0, 0, 0), 0, 2, new Vector3D(8, 0, 0), 0, 100f, 0f, pumpShotgunHash, HitboxZone.Torso, now.AddMilliseconds(20));
        Assert.False(res9.IsValid);
        Assert.Equal(CombatViolationType.RapidFire, res9.Violation);
    }
}
