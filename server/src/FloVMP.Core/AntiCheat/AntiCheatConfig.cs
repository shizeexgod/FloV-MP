using System.Collections.Generic;

namespace FloVMP.Core.AntiCheat;

public enum AntiCheatAction
{
    LogOnly = 0,
    Warning = 1,
    Disarm = 2,
    TeleportBack = 3,
    Kick = 4,
    Ban = 5
}

public class AntiCheatConfig
{
    public float MaxOnFootSpeedMps { get; set; } = 12.5f; // ~45 km/h (горизонталь)
    public float MaxVehicleSpeedMps { get; set; } = 125.0f; // ~450 km/h
    // Вертикаль пешком: падение в GTA достигает ~55 м/с (терминальная),
    // скайдайв/парашют ещё быстрее. Лимит с запасом, чтобы легитимное падение
    // НЕ считалось спидхаком (иначе прыжок с высоты = ложный кик).
    public float MaxFallSpeedMps { get; set; } = 90.0f;
    public float MaxTeleportThresholdMeters { get; set; } = 100.0f;
    public int MaxConsecutiveViolations { get; set; } = 3;
    // Анти-чит НЕ кикает и не наказывает игроков — это инструмент РАПОРТА:
    // сработки/варнинги уходят администрации + в лог. Владелец/админы вообще
    // освобождены от проверок. Поэтому дефолт — Warning (рапорт), не Kick.
    public AntiCheatAction DefaultViolationAction { get; set; } = AntiCheatAction.Warning;
    public bool Enabled { get; set; } = true;

    // Prohibited high-damage explosive / destructive weapons in standard RP
    public HashSet<uint> BlacklistedWeapons { get; set; } = new()
    {
        0x42BF8A85, // Minigun
        0xB1CA77B1, // RPG
        0x63AB0442, // Homing Launcher
        0xA284510B, // Grenade Launcher
        0x6D544C39, // Railgun
        0xB62D1F67, // Widowmaker
        0xAF113F9E, // Up-n-Atomizer
        0x787F0BB,  // Compact EMP Launcher
    };
}
