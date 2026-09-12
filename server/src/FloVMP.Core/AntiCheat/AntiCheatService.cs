using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace FloVMP.Core.AntiCheat;

public class AntiCheatService
{
    private readonly AntiCheatConfig _config;
    private readonly SpeedHackDetector _speedDetector;
    private readonly TeleportDetector _teleportDetector;
    private readonly WeaponSecurity _weaponSecurity;
    private readonly ConcurrentDictionary<int, PlayerTrackingState> _players = new();

    public event Action<int, string, AntiCheatAction>? OnViolationDetected;
    public event Action<AntiCheatDetectionEvent>? OnDetection;
    public Func<AntiCheatDetectionEvent, AntiCheatAction>? CustomActionResolver;

    public AntiCheatService(AntiCheatConfig? config = null)
    {
        _config = config ?? new AntiCheatConfig();
        _speedDetector = new SpeedHackDetector(_config);
        _teleportDetector = new TeleportDetector(_config);
        _weaponSecurity = new WeaponSecurity(_config);
    }

    public PlayerTrackingState GetOrCreateState(int accountId, string username, Vector3D initialPos, DateTime? initialTime = null)
    {
        var time = initialTime ?? DateTime.UtcNow;
        return _players.GetOrAdd(accountId, id => new PlayerTrackingState
        {
            AccountId = id,
            Username = username,
            LastValidPosition = initialPos,
            LastTrackedTime = time,
            LastSpawnOrTeleportTime = time.AddSeconds(-10)
        });
    }

    public void RemovePlayer(int accountId)
    {
        _players.TryRemove(accountId, out _);
    }

    public void NotifyLegitimateTeleport(int accountId, Vector3D newPos, DateTime? timestamp = null)
    {
        var time = timestamp ?? DateTime.UtcNow;
        var state = _players.GetOrAdd(accountId, id => new PlayerTrackingState
        {
            AccountId = id,
            Username = string.Empty,
            LastValidPosition = newPos,
            LastTrackedTime = time,
            LastSpawnOrTeleportTime = time
        });

        state.LastValidPosition = newPos;
        state.LastTrackedTime = time;
        state.LastSpawnOrTeleportTime = time;
        state.ResetViolations();
    }

    public void SetAdminExemption(int accountId, bool isExempt)
    {
        var state = _players.GetOrAdd(accountId, id => new PlayerTrackingState
        {
            AccountId = id,
            Username = string.Empty,
            LastValidPosition = Vector3D.Zero,
            LastTrackedTime = DateTime.UtcNow,
            LastSpawnOrTeleportTime = DateTime.UtcNow
        });

        state.IsAdminExempt = isExempt;
    }

    public bool IsAdminExempt(int accountId)
    {
        return _players.TryGetValue(accountId, out var state) && state.IsAdminExempt;
    }

    public bool CheckMovement(int accountId, Vector3D currentPos, bool inVehicle, DateTime? timestamp = null)
    {
        if (!_players.TryGetValue(accountId, out var state))
            return true;

        var now = timestamp ?? DateTime.UtcNow;

        if (state.IsAdminExempt || !_config.Enabled)
        {
            state.ResetViolations();
            state.LastValidPosition = currentPos;
            state.LastTrackedTime = now;
            return true;
        }

        // Check Teleport
        if (!_teleportDetector.ValidateTeleport(state, currentPos, now, inVehicle, false, out var dist, out var teleReason))
        {
            HandleViolation(state, teleReason, AntiCheatAction.TeleportBack, AntiCheatSeverity.High, "Teleport", currentPos);
            return false;
        }

        // Check Speed
        if (!_speedDetector.ValidateSpeed(state, currentPos, now, inVehicle, out var speed, out var speedReason))
        {
            var isPersistent = state.ConsecutiveSpeedViolations >= _config.MaxConsecutiveViolations;
            var severity = isPersistent ? AntiCheatSeverity.High : AntiCheatSeverity.Medium;
            var action = isPersistent ? _config.DefaultViolationAction : AntiCheatAction.Warning;

            HandleViolation(state, speedReason, action, severity, "SpeedHack", currentPos);
            return false;
        }

        // Check NoClip (вертикальный набор высоты пешком без парашюта/вертолета)
        if (!inVehicle)
        {
            var dt = (float)(now - state.LastTrackedTime).TotalSeconds;
            if (dt > 0.05f && dt < 2.0f)
            {
                var deltaZ = currentPos.Z - state.LastValidPosition.Z;
                var verticalSpeed = deltaZ / dt;

                if (verticalSpeed > _config.MaxVerticalClimbSpeedMps)
                {
                    state.ConsecutiveClimbViolations++;
                    if (state.ConsecutiveClimbViolations >= _config.MaxConsecutiveClimbViolations)
                    {
                        var reason = $"Аномальный вертикальный подъем (NoClip/FlyHack): {verticalSpeed:F1} м/с (лимит {_config.MaxVerticalClimbSpeedMps:F1} м/с)";
                        HandleViolation(state, reason, AntiCheatAction.TeleportBack, AntiCheatSeverity.High, "NoClip", currentPos);
                        return false;
                    }
                }
                else
                {
                    state.ConsecutiveClimbViolations = 0;
                }
            }
        }

        // Both checks passed
        state.ResetViolations();
        state.LastValidPosition = currentPos;
        state.LastTrackedTime = now;
        return true;
    }

    public bool CheckWeapon(int accountId, uint weaponHash, ISet<uint> authorizedWeapons)
    {
        if (!_players.TryGetValue(accountId, out var state))
            return true;

        if (state.IsAdminExempt || !_config.Enabled)
            return true;

        if (!_weaponSecurity.ValidateWeaponEquipped(state, weaponHash, authorizedWeapons, out var reason))
        {
            HandleViolation(state, reason, AntiCheatAction.Disarm, AntiCheatSeverity.High, "BlacklistedWeapon");
            return false;
        }

        state.EquippedWeaponHash = weaponHash;
        return true;
    }

    /// <summary>
    /// Проверка частоты клиентских сетевых событий (защита от краш-спама и инъекций мод-меню)
    /// </summary>
    public bool CheckEventRateLimit(int accountId, string eventName, DateTime? timestamp = null)
    {
        if (!_players.TryGetValue(accountId, out var state))
            return true;

        if (state.IsAdminExempt || !_config.Enabled)
            return true;

        var now = timestamp ?? DateTime.UtcNow;
        var currentSecond = new DateTimeOffset(now).ToUnixTimeSeconds();

        if (state.CurrentWindowSecond != currentSecond)
        {
            state.CurrentWindowSecond = currentSecond;
            state.EventCountInCurrentWindow = 0;
        }

        state.EventCountInCurrentWindow++;

        if (state.EventCountInCurrentWindow > _config.MaxEventsPerSecond)
        {
            var reason = $"Флуд сетевыми событиями ({eventName}): {state.EventCountInCurrentWindow} пакетов/сек (лимит {_config.MaxEventsPerSecond}/сек)";
            HandleViolation(state, reason, AntiCheatAction.Kick, AntiCheatSeverity.Critical, "EventSpam");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Проверка на неуязвимость (GodMode): подтвержденный входящий урон без изменения здоровья
    /// </summary>
    public bool CheckGodMode(int accountId, int incomingDamage, int actualHealthReduction)
    {
        if (!_players.TryGetValue(accountId, out var state))
            return true;

        if (state.IsAdminExempt || !_config.Enabled)
            return true;

        if (incomingDamage >= 25 && actualHealthReduction <= 0)
        {
            var reason = $"Игрок игнорирует подтвержденный урон {incomingDamage} HP (GodMode)";
            HandleViolation(state, reason, AntiCheatAction.Kick, AntiCheatSeverity.Critical, "GodMode");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Фиксация попытки несанкционированного вызова административных команд/событий игроком без прав
    /// </summary>
    public void RecordUnauthorizedAdminAttempt(int accountId, string actionName)
    {
        if (!_players.TryGetValue(accountId, out var state))
            return;

        var reason = $"Попытка несанкционированного вызова админ-функции/события: {actionName}";
        HandleViolation(state, reason, AntiCheatAction.Warning, AntiCheatSeverity.High, "UnauthorizedAdminAttempt");
    }

    private void HandleViolation(
        PlayerTrackingState state,
        string reason,
        AntiCheatAction defaultAction,
        AntiCheatSeverity severity = AntiCheatSeverity.Medium,
        string detectionType = "Unknown",
        Vector3D? location = null)
    {
        var action = defaultAction;
        if (_config.SeverityActionMap.TryGetValue(severity, out var mappedAction))
        {
            if (mappedAction >= AntiCheatAction.Kick || action == AntiCheatAction.Warning || action == AntiCheatAction.LogOnly)
            {
                action = mappedAction;
            }
        }

        var detectionEvent = new AntiCheatDetectionEvent
        {
            AccountId = state.AccountId,
            Username = state.Username,
            DetectionType = detectionType,
            Severity = severity,
            Details = reason,
            Location = location ?? state.LastValidPosition,
            Timestamp = DateTime.UtcNow,
            SuggestedAction = action
        };

        if (CustomActionResolver != null)
        {
            action = CustomActionResolver.Invoke(detectionEvent);
        }

        OnDetection?.Invoke(detectionEvent);
        OnViolationDetected?.Invoke(state.AccountId, reason, action);
    }
}
