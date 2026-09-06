using System;

namespace FloVMP.Core.AntiCheat;

public class SpeedHackDetector
{
    private readonly AntiCheatConfig _config;

    public SpeedHackDetector(AntiCheatConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public bool ValidateSpeed(
        PlayerTrackingState state,
        Vector3D currentPos,
        DateTime currentTime,
        bool inVehicle,
        out float speedMps,
        out string violationReason)
    {
        speedMps = 0f;
        violationReason = string.Empty;

        if (state.IsAdminExempt || !_config.Enabled)
            return true;

        // Skip check right after spawn or admin teleport (grace period 2 seconds)
        var timeSinceTeleport = (currentTime - state.LastSpawnOrTeleportTime).TotalSeconds;
        if (timeSinceTeleport >= 0 && timeSinceTeleport < 2.0)
        {
            return true;
        }

        var timeDeltaSec = (float)(currentTime - state.LastTrackedTime).TotalSeconds;
        if (timeDeltaSec <= 0.05f) // Avoid division by near-zero in high tick bursts
            return true;

        var distance = state.LastValidPosition.DistanceTo(currentPos);
        speedMps = distance / timeDeltaSec;

        float maxAllowed = inVehicle ? _config.MaxVehicleSpeedMps : _config.MaxOnFootSpeedMps;

        if (speedMps > maxAllowed)
        {
            state.ConsecutiveSpeedViolations++;
            violationReason = $"Превышение скорости {(inVehicle ? "транспорта" : "пешком")}: {speedMps:F1} м/с (лимит: {maxAllowed:F1} м/с, дельта: {timeDeltaSec:F2}с, дистанция: {distance:F1}м)";
            return false;
        }

        return true;
    }
}
