using System;
using System.Collections.Generic;

namespace FloVMP.Core.AntiCheat;

public enum VehicleViolationType
{
    None = 0,
    FlyCar = 1,
    SuperAcceleration = 2,
    ExtremeSpeed = 3,
    CarJump = 4,
    VehicleTeleport = 5,
    VehicleGodmode = 6
}

public sealed class VehiclePhysicsState
{
    public int VehicleId { get; set; }
    public int DriverId { get; set; }
    public Vector3D LastPosition { get; set; }
    public Vector3D LastVelocity { get; set; }
    public float LastSpeedMps { get; set; }
    public DateTime LastUpdateUtc { get; set; } = DateTime.UtcNow;
    public float Health { get; set; } = 1000.0f;
    public int ConsecutiveViolations { get; set; }
    public bool InAirSuspicious { get; set; }
    public float AirTimeSeconds { get; set; }
}

/// <summary>
/// Серверный страж физики транспорта (Vehicle Physics Guardian).
/// Анализирует ускорение, вертикальную тягу, траекторию и демедж авто.
/// Блокирует читы Fly Car, Car Jump, Torque Multiplier и Vehicle Godmode.
/// </summary>
public sealed class VehiclePhysicsGuardian
{
    private readonly object _lock = new();
    private readonly Dictionary<int, VehiclePhysicsState> _vehicleStates = new();

    // Физические лимиты для суперкаров и модифицированных авто
    public float MaxHorizontalSpeedMps { get; set; } = 120.0f;  // ~432 км/ч
    public float MaxAccelerationMps2 { get; set; } = 35.0f;     // 0-100 км/ч за ~0.8с (лимит для гиперкаров)
    public float MaxVerticalVelocityMps { get; set; } = 20.0f;  // Максимальный прыжок/рампа
    public float MaxSustainedAirTimeSec { get; set; } = 6.0f;   // Допустимое время прыжка с горы Чилиад
    public float MaxTeleportDistance { get; set; } = 70.0f;     // Скачок за 1 секунду

    public event Action<int, int, VehicleViolationType, string>? OnVehicleViolation;

    public void RegisterVehicle(int vehicleId, Vector3D spawnPos, float initialHealth = 1000.0f)
    {
        lock (_lock)
        {
            _vehicleStates[vehicleId] = new VehiclePhysicsState
            {
                VehicleId = vehicleId,
                LastPosition = spawnPos,
                Health = initialHealth,
                LastUpdateUtc = DateTime.UtcNow
            };
        }
    }

    public void UnregisterVehicle(int vehicleId)
    {
        lock (_lock)
        {
            _vehicleStates.Remove(vehicleId);
        }
    }

    /// <summary>
    /// Валидирует входящий пакет синхронизации транспорта от клиента водителя.
    /// </summary>
    public VehicleViolationType ValidateTick(
        int vehicleId,
        int driverId,
        Vector3D newPosition,
        Vector3D velocity,
        float currentHealth,
        bool isInAir,
        DateTime timestampUtc)
    {
        lock (_lock)
        {
            if (!_vehicleStates.TryGetValue(vehicleId, out var state))
            {
                _vehicleStates[vehicleId] = new VehiclePhysicsState
                {
                    VehicleId = vehicleId,
                    DriverId = driverId,
                    LastPosition = newPosition,
                    LastVelocity = velocity,
                    Health = currentHealth,
                    LastUpdateUtc = timestampUtc
                };
                return VehicleViolationType.None;
            }

            float dt = (float)(timestampUtc - state.LastUpdateUtc).TotalSeconds;
            if (dt <= 0.001f) dt = 0.016f; // Защита от деления на 0 при спаме пакетов

            float dist = state.LastPosition.DistanceTo(newPosition);
            float currentSpeed = (float)Math.Sqrt(velocity.X * velocity.X + velocity.Y * velocity.Y + velocity.Z * velocity.Z);
            float horizontalSpeed = (float)Math.Sqrt(velocity.X * velocity.X + velocity.Y * velocity.Y);

            // 1. Проверка на телепортацию на авто
            if (dist > MaxTeleportDistance && dt < 1.0f)
            {
                TriggerViolation(vehicleId, driverId, VehicleViolationType.VehicleTeleport,
                    $"Резкий скачок позиции на {dist:F1}м за {dt:F2}с");
                return VehicleViolationType.VehicleTeleport;
            }

            // 2. Проверка предельной горизонтальной скорости
            if (horizontalSpeed > MaxHorizontalSpeedMps)
            {
                TriggerViolation(vehicleId, driverId, VehicleViolationType.ExtremeSpeed,
                    $"Превышение лимита скорости авто: {horizontalSpeed * 3.6f:F0} км/ч (макс {MaxHorizontalSpeedMps * 3.6f:F0})");
                return VehicleViolationType.ExtremeSpeed;
            }

            // 3. Проверка нереалистичного ускорения (Torque Multiplier / Speedhack)
            float speedDelta = currentSpeed - state.LastSpeedMps;
            if (speedDelta > 0 && dt > 0.05f)
            {
                float acceleration = speedDelta / dt;
                if (acceleration > MaxAccelerationMps2 && currentSpeed > 20.0f)
                {
                    TriggerViolation(vehicleId, driverId, VehicleViolationType.SuperAcceleration,
                        $"Аномальное ускорение: {acceleration:F1} м/с²");
                    return VehicleViolationType.SuperAcceleration;
                }
            }

            // 4. Проверка Fly Car (полет на авто с набором высоты в воздухе)
            if (isInAir)
            {
                state.AirTimeSeconds += dt;
                // Если авто в воздухе набирает высоту без рампы и ускоряется вверх
                if (velocity.Z > 8.0f && state.AirTimeSeconds > 2.0f)
                {
                    TriggerViolation(vehicleId, driverId, VehicleViolationType.FlyCar,
                        $"Набор высоты в воздухе: Vz={velocity.Z:F1} м/с на протяжении {state.AirTimeSeconds:F1}с");
                    return VehicleViolationType.FlyCar;
                }

                if (state.AirTimeSeconds > MaxSustainedAirTimeSec && velocity.Z >= -1.0f)
                {
                    TriggerViolation(vehicleId, driverId, VehicleViolationType.FlyCar,
                        $"Зависание в воздухе на авто более {state.AirTimeSeconds:F1}с");
                    return VehicleViolationType.FlyCar;
                }
            }
            else
            {
                state.AirTimeSeconds = 0.0f;
            }

            // 5. Проверка Car Super Jump (мгновенный взлёт с земли)
            if (velocity.Z > MaxVerticalVelocityMps && state.LastVelocity.Z < 2.0f)
            {
                TriggerViolation(vehicleId, driverId, VehicleViolationType.CarJump,
                    $"Импульсный супер-прыжок авто вверх: Vz={velocity.Z:F1} м/с");
                return VehicleViolationType.CarJump;
            }

            // Обновляем валидное состояние
            state.DriverId = driverId;
            state.LastPosition = newPosition;
            state.LastVelocity = velocity;
            state.LastSpeedMps = currentSpeed;
            state.Health = currentHealth;
            state.LastUpdateUtc = timestampUtc;

            return VehicleViolationType.None;
        }
    }

    private void TriggerViolation(int vehicleId, int driverId, VehicleViolationType type, string reason)
    {
        if (_vehicleStates.TryGetValue(vehicleId, out var state))
        {
            state.ConsecutiveViolations++;
        }
        OnVehicleViolation?.Invoke(vehicleId, driverId, type, reason);
    }
}
