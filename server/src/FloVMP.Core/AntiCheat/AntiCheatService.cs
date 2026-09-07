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
        if (_players.TryGetValue(accountId, out var state))
        {
            var time = timestamp ?? DateTime.UtcNow;
            state.LastValidPosition = newPos;
            state.LastTrackedTime = time;
            state.LastSpawnOrTeleportTime = time;
            state.ResetViolations();
        }
    }

    public void SetAdminExemption(int accountId, bool isExempt)
    {
        if (_players.TryGetValue(accountId, out var state))
        {
            state.IsAdminExempt = isExempt;
        }
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

        // Check Teleport
        if (!_teleportDetector.ValidateTeleport(state, currentPos, now, inVehicle, false, out var dist, out var teleReason))
        {
            HandleViolation(state, teleReason, AntiCheatAction.TeleportBack);
            return false;
        }

        // Check Speed
        if (!_speedDetector.ValidateSpeed(state, currentPos, now, inVehicle, out var speed, out var speedReason))
        {
            var action = state.ConsecutiveSpeedViolations >= _config.MaxConsecutiveViolations
                ? _config.DefaultViolationAction
                : AntiCheatAction.Warning;

            HandleViolation(state, speedReason, action);
            return false;
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

        if (!_weaponSecurity.ValidateWeaponEquipped(state, weaponHash, authorizedWeapons, out var reason))
        {
            HandleViolation(state, reason, AntiCheatAction.Disarm);
            return false;
        }

        state.EquippedWeaponHash = weaponHash;
        return true;
    }

    private void HandleViolation(PlayerTrackingState state, string reason, AntiCheatAction action)
    {
        OnViolationDetected?.Invoke(state.AccountId, reason, action);
    }
}
