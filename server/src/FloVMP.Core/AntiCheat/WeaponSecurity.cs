using System;
using System.Collections.Generic;

namespace FloVMP.Core.AntiCheat;

public class WeaponSecurity
{
    private readonly AntiCheatConfig _config;

    public WeaponSecurity(AntiCheatConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public bool IsWeaponBlacklisted(uint weaponHash)
    {
        return _config.BlacklistedWeapons.Contains(weaponHash);
    }

    public bool ValidateWeaponEquipped(
        PlayerTrackingState state,
        uint weaponHash,
        ISet<uint> playerInventoryWeapons,
        out string violationReason)
    {
        violationReason = string.Empty;

        if (state.IsAdminExempt || !_config.Enabled)
            return true;

        // Fist / Unarmed (0xA2719263 and 0xA2719248) is always allowed
        if (weaponHash == 0 || weaponHash == 0xA2719263 || weaponHash == 0xA2719248)
            return true;

        if (IsWeaponBlacklisted(weaponHash))
        {
            violationReason = $"Обнаружено запрещённое тяжелое оружие (Hash: 0x{weaponHash:X8})";
            return false;
        }

        if (playerInventoryWeapons != null && !playerInventoryWeapons.Contains(weaponHash))
        {
            violationReason = $"Игрок взял оружие (Hash: 0x{weaponHash:X8}), отсутствующее в инвентаре/складе";
            return false;
        }

        return true;
    }
}
