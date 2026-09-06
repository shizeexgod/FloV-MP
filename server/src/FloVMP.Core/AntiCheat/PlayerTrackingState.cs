using System;

namespace FloVMP.Core.AntiCheat;

public class PlayerTrackingState
{
    public int AccountId { get; set; }
    public string Username { get; set; } = string.Empty;
    public Vector3D LastValidPosition { get; set; }
    public DateTime LastTrackedTime { get; set; } = DateTime.UtcNow;
    public bool IsInVehicle { get; set; }
    public int ConsecutiveSpeedViolations { get; set; }
    public int ConsecutiveTeleportViolations { get; set; }
    public uint EquippedWeaponHash { get; set; }
    public bool IsAdminExempt { get; set; }
    public DateTime LastSpawnOrTeleportTime { get; set; } = DateTime.UtcNow;

    public void ResetViolations()
    {
        ConsecutiveSpeedViolations = 0;
        ConsecutiveTeleportViolations = 0;
    }
}
