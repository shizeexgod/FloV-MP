using System;

namespace FloVMP.Core.AntiCheat;

public class TeleportDetector
{
    private readonly AntiCheatConfig _config;

    public TeleportDetector(AntiCheatConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public bool ValidateTeleport(
        PlayerTrackingState state,
        Vector3D currentPos,
        DateTime currentTime,
        bool inVehicle,
        bool wasLegitimateTeleport,
        out float distanceMeters,
        out string violationReason)
    {
        distanceMeters = 0f;
        violationReason = string.Empty;

        if (state.IsAdminExempt || !_config.Enabled)
            return true;

        var timeSinceTeleport = (currentTime - state.LastSpawnOrTeleportTime).TotalSeconds;
        if (wasLegitimateTeleport || (timeSinceTeleport >= 0 && timeSinceTeleport < 2.0))
        {
            return true;
        }

        var timeDelta = (float)(currentTime - state.LastTrackedTime).TotalSeconds;
        distanceMeters = state.LastValidPosition.DistanceTo(currentPos);

        float threshold = inVehicle ? (_config.MaxVehicleSpeedMps * 2.0f) : _config.MaxTeleportThresholdMeters;

        // Instantaneous jump > threshold over short time (e.g. < 1.5 sec)
        if (distanceMeters > threshold && timeDelta < 1.5f)
        {
            state.ConsecutiveTeleportViolations++;
            violationReason = $"Обнаружен несанкционированный телепорт: скачок на {distanceMeters:F1} м за {timeDelta:F2} сек";
            return false;
        }

        return true;
    }
}
