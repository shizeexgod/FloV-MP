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

        // Skip check right after spawn or admin teleport (grace period 5 seconds)
        var timeSinceTeleport = (currentTime - state.LastSpawnOrTeleportTime).TotalSeconds;
        if (timeSinceTeleport >= 0 && timeSinceTeleport < 5.0)
        {
            return true;
        }

        var timeDeltaSec = (float)(currentTime - state.LastTrackedTime).TotalSeconds;
        if (timeDeltaSec <= 0.05f) // Avoid division by near-zero in high tick bursts
            return true;

        // Горизонталь и вертикаль считаем РАЗДЕЛЬНО. Раньше бралась 3D-скорость,
        // и легитимное падение (вертикаль ~55 м/с) превышало пеший лимит 12.5 →
        // ложный кик за «спидхак» после нескольких тиков падения. Горизонтальная
        // скорость ловит спидхак/полёт по земле; вертикальная допускает падение
        // до терминальной скорости (MaxFallSpeedMps).
        var horizontalDist = state.LastValidPosition.DistanceTo2D(currentPos);
        var horizontalSpeed = horizontalDist / timeDeltaSec;
        var verticalSpeed = System.MathF.Abs(currentPos.Z - state.LastValidPosition.Z) / timeDeltaSec;
        speedMps = horizontalSpeed;

        float maxHoriz = inVehicle ? _config.MaxVehicleSpeedMps : _config.MaxOnFootSpeedMps;
        if (horizontalSpeed > maxHoriz)
        {
            state.ConsecutiveSpeedViolations++;
            violationReason = $"Превышение скорости {(inVehicle ? "транспорта" : "пешком")}: {horizontalSpeed:F1} м/с (лимит: {maxHoriz:F1} м/с, дельта: {timeDeltaSec:F2}с, дистанция: {horizontalDist:F1}м)";
            return false;
        }

        // Вертикаль: в технике летать вверх/вниз можно (лимит транспорта),
        // пешком — только падать/прыгать до терминальной скорости.
        float maxVert = inVehicle ? _config.MaxVehicleSpeedMps : _config.MaxFallSpeedMps;
        if (verticalSpeed > maxVert)
        {
            state.ConsecutiveSpeedViolations++;
            speedMps = verticalSpeed;
            violationReason = $"Аномальная вертикальная скорость: {verticalSpeed:F1} м/с (лимит: {maxVert:F1} м/с, дельта: {timeDeltaSec:F2}с)";
            return false;
        }

        return true;
    }
}
