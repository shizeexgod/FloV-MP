using System;
using System.Collections.Generic;

namespace FloVMP.Core.AntiCheat;

public enum HitboxZone
{
    Head = 0,
    Torso = 1,
    LeftArm = 2,
    RightArm = 3,
    LeftLeg = 4,
    RightLeg = 5
}

public enum CombatViolationType
{
    None = 0,
    RapidFire = 1,
    ExtremeDistance = 2,
    CrossDimensionAttack = 3,
    BlacklistedWeaponUsed = 4,
    SuspectedGodmode = 5
}

public sealed class WeaponBallisticProfile
{
    public string Name { get; set; } = "Unknown";
    public float BaseDamage { get; set; } = 25.0f;
    public float MaxRangeMeters { get; set; } = 80.0f;
    public float MinShotIntervalSeconds { get; set; } = 0.15f; // ~400 RPM
    public float HeadshotMultiplier { get; set; } = 2.0f;
    public float LimbMultiplier { get; set; } = 0.65f;
}

public sealed class HitValidationResult
{
    public bool IsValid { get; set; }
    public CombatViolationType Violation { get; set; }
    public string Message { get; set; } = string.Empty;
    public float CalculatedDamage { get; set; }
    public float NewArmour { get; set; }
    public float NewHealth { get; set; }
    public bool IsFatal { get; set; }
}

/// <summary>
/// Сервис серверной валидации боя и урона (Server-Authoritative Combat).
/// Рассчитывает баллистику, попадания по хитбоксам и урон на сервере.
/// Пресекает читы Rapid Fire, Godmode, Silent Aim на запредельных дистанциях.
/// </summary>
public sealed class CombatValidationService
{
    private readonly object _lock = new();
    private readonly Dictionary<uint, WeaponBallisticProfile> _profiles = new();
    private readonly Dictionary<int, (uint Weapon, DateTime LastShotUtc)> _lastShots = new();
    private readonly Dictionary<int, int> _unprocessedDamageStrikes = new();

    public event Action<int, CombatViolationType, string>? OnCombatViolation;

    public CombatValidationService()
    {
        RegisterDefaultProfiles();
    }

    public void RegisterWeapon(uint hash, WeaponBallisticProfile profile)
    {
        lock (_lock)
        {
            _profiles[hash] = profile;
        }
    }

    /// <summary>
    /// Валидирует выстрел и рассчитывает легитимный урон с учётом брони и зоны попадания.
    /// </summary>
    public HitValidationResult ValidateHit(
        int attackerId,
        Vector3D attackerPos,
        int attackerDim,
        int victimId,
        Vector3D victimPos,
        int victimDim,
        float currentVictimHealth,
        float currentVictimArmour,
        uint weaponHash,
        HitboxZone zone,
        DateTime shotTimeUtc)
    {
        lock (_lock)
        {
            // 1. Проверка виртуальных миров
            if (attackerDim != victimDim)
            {
                OnCombatViolation?.Invoke(attackerId, CombatViolationType.CrossDimensionAttack,
                    $"Атака сквозь измерения: нападающий Dim {attackerDim}, жертва Dim {victimDim}");
                return new HitValidationResult
                {
                    IsValid = false,
                    Violation = CombatViolationType.CrossDimensionAttack,
                    Message = "Невозможно нанести урон игроку в другом измерении"
                };
            }

            // 2. Получение баллистического профиля оружия
            if (!_profiles.TryGetValue(weaponHash, out var profile))
            {
                profile = new WeaponBallisticProfile { BaseDamage = 25.0f, MaxRangeMeters = 80.0f, MinShotIntervalSeconds = 0.1f };
            }

            // 3. Проверка темпа стрельбы (Rapid Fire Hack)
            if (_lastShots.TryGetValue(attackerId, out var lastShot))
            {
                if (lastShot.Weapon == weaponHash)
                {
                    float interval = (float)(shotTimeUtc - lastShot.LastShotUtc).TotalSeconds;
                    if (interval >= 0 && interval < profile.MinShotIntervalSeconds * 0.55f) // Буфер 45% на сетевой джиттер
                    {
                        OnCombatViolation?.Invoke(attackerId, CombatViolationType.RapidFire,
                            $"Rapid Fire: интервал между выстрелами {interval * 1000:F0}мс (лимит {profile.MinShotIntervalSeconds * 1000:F0}мс)");
                        return new HitValidationResult
                        {
                            IsValid = false,
                            Violation = CombatViolationType.RapidFire,
                            Message = "Превышен максимальный темп стрельбы оружия"
                        };
                    }
                }
            }
            _lastShots[attackerId] = (weaponHash, shotTimeUtc);

            // 4. Проверка дистанции поражения
            float distance = attackerPos.DistanceTo(victimPos);
            if (distance > profile.MaxRangeMeters)
            {
                OnCombatViolation?.Invoke(attackerId, CombatViolationType.ExtremeDistance,
                    $"Выстрел дальше эффективной дальности: {distance:F1}м (макс {profile.MaxRangeMeters:F1}м)");
                return new HitValidationResult
                {
                    IsValid = false,
                    Violation = CombatViolationType.ExtremeDistance,
                    Message = "Дистанция выстрела превышает баллистический предел"
                };
            }

            // 5. Серверный расчёт урона по хитбоксу
            float damage = profile.BaseDamage;
            if (zone == HitboxZone.Head)
            {
                damage *= profile.HeadshotMultiplier;
            }
            else if (zone is HitboxZone.LeftArm or HitboxZone.RightArm or HitboxZone.LeftLeg or HitboxZone.RightLeg)
            {
                damage *= profile.LimbMultiplier;
            }

            // Спад урона на пределе дистанции
            if (distance > profile.MaxRangeMeters * 0.7f)
            {
                float falloffRatio = 1.0f - ((distance - (profile.MaxRangeMeters * 0.7f)) / (profile.MaxRangeMeters * 0.3f)) * 0.35f;
                damage *= Math.Clamp(falloffRatio, 0.65f, 1.0f);
            }

            // 6. Распределение урона по броне и здоровью
            float newArmour = currentVictimArmour;
            float newHealth = currentVictimHealth;

            if (newArmour > 0)
            {
                float armourAbsorb = damage * 0.7f; // Броня гасит 70% урона
                float healthDamage = damage * 0.3f;

                if (newArmour >= armourAbsorb)
                {
                    newArmour -= armourAbsorb;
                }
                else
                {
                    float overflow = armourAbsorb - newArmour;
                    newArmour = 0;
                    healthDamage += overflow;
                }

                newHealth = Math.Max(0, newHealth - healthDamage);
            }
            else
            {
                newHealth = Math.Max(0, newHealth - damage);
            }

            return new HitValidationResult
            {
                IsValid = true,
                Violation = CombatViolationType.None,
                CalculatedDamage = damage,
                NewArmour = (float)Math.Round(newArmour, 1),
                NewHealth = (float)Math.Round(newHealth, 1),
                IsFatal = newHealth <= 0
            };
        }
    }

    private void RegisterDefaultProfiles()
    {
        // Пистолеты
        _profiles[0x1B06D571] = new WeaponBallisticProfile { Name = "Pistol", BaseDamage = 26f, MaxRangeMeters = 55f, MinShotIntervalSeconds = 0.2f };
        _profiles[0x5EF9FEC4] = new WeaponBallisticProfile { Name = "Combat Pistol", BaseDamage = 28f, MaxRangeMeters = 60f, MinShotIntervalSeconds = 0.18f };
        _profiles[0x99AEEB3B] = new WeaponBallisticProfile { Name = "Heavy Pistol", BaseDamage = 40f, MaxRangeMeters = 65f, MinShotIntervalSeconds = 0.25f };

        // ПП / SMG
        _profiles[0x2BE6766B] = new WeaponBallisticProfile { Name = "SMG", BaseDamage = 22f, MaxRangeMeters = 85f, MinShotIntervalSeconds = 0.08f };
        _profiles[0x13532244] = new WeaponBallisticProfile { Name = "Micro SMG", BaseDamage = 21f, MaxRangeMeters = 70f, MinShotIntervalSeconds = 0.07f };

        // Штурмовые винтовки
        _profiles[0x83BF0278] = new WeaponBallisticProfile { Name = "Carbine Rifle", BaseDamage = 32f, MaxRangeMeters = 150f, MinShotIntervalSeconds = 0.095f };
        _profiles[0xBFEFFF6D] = new WeaponBallisticProfile { Name = "Assault Rifle", BaseDamage = 30f, MaxRangeMeters = 145f, MinShotIntervalSeconds = 0.1f };
        _profiles[0xAF112F55] = new WeaponBallisticProfile { Name = "Special Carbine", BaseDamage = 34f, MaxRangeMeters = 155f, MinShotIntervalSeconds = 0.09f };

        // Дробовики
        _profiles[0x1D073A89] = new WeaponBallisticProfile { Name = "Pump Shotgun", BaseDamage = 65f, MaxRangeMeters = 35f, MinShotIntervalSeconds = 0.8f };

        // Снайперские винтовки
        _profiles[0x05FC3C11] = new WeaponBallisticProfile { Name = "Sniper Rifle", BaseDamage = 95f, MaxRangeMeters = 350f, MinShotIntervalSeconds = 1.2f, HeadshotMultiplier = 2.5f };
        _profiles[0x0C472FE2] = new WeaponBallisticProfile { Name = "Heavy Sniper", BaseDamage = 150f, MaxRangeMeters = 450f, MinShotIntervalSeconds = 1.5f, HeadshotMultiplier = 3.0f };
    }
}
