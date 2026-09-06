using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using AltV.Net;
using AltV.Net.Data;
using AltV.Net.Elements.Entities;
using FloVMP.Core.AntiCheat;
using FloVMP.Core.Auth;
using FloVMP.Core.Logging;

namespace FloVMP.Gamemode.Systems.AntiCheat;

public class AntiCheatSystem
{
    private readonly AntiCheatService _service;
    private readonly Func<IPlayer, Account?> _accountOf;
    private readonly Func<IPlayer, ISet<uint>?> _inventoryWeaponsOf;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private long _lastTickMs;

    public AntiCheatService Service => _service;

    public AntiCheatSystem(
        Func<IPlayer, Account?> accountOf,
        Func<IPlayer, ISet<uint>?>? inventoryWeaponsOf = null,
        AntiCheatConfig? config = null)
    {
        _accountOf = accountOf ?? throw new ArgumentNullException(nameof(accountOf));
        _inventoryWeaponsOf = inventoryWeaponsOf ?? (_ => null);
        _service = new AntiCheatService(config);

        _service.OnViolationDetected += HandleViolation;
    }

    public void OnAuthed(IPlayer player, Account account)
    {
        if (!player.Exists) return;
        var pos = new Vector3D(player.Position.X, player.Position.Y, player.Position.Z);
        var state = _service.GetOrCreateState(account.Id, account.Username, pos);
        if (account.AdminLevel >= 4)
        {
            _service.SetAdminExemption(account.Id, true);
        }
    }

    public void OnDisconnect(IPlayer player)
    {
        var acc = _accountOf(player);
        if (acc != null)
        {
            _service.RemovePlayer(acc.Id);
        }
    }

    public void NotifyAdminTeleport(IPlayer player, Position newPos)
    {
        var acc = _accountOf(player);
        if (acc != null)
        {
            _service.NotifyLegitimateTeleport(acc.Id, new Vector3D(newPos.X, newPos.Y, newPos.Z));
        }
    }

    public void Tick()
    {
        var nowMs = _clock.ElapsedMilliseconds;
        // Check movement and weapons every 500ms
        if (nowMs - _lastTickMs < 500) return;
        _lastTickMs = nowMs;

        foreach (var player in Alt.GetAllPlayers())
        {
            if (!player.Exists) continue;
            var acc = _accountOf(player);
            if (acc == null) continue;

            var pos = new Vector3D(player.Position.X, player.Position.Y, player.Position.Z);
            bool inVehicle = player.IsInVehicle;

            _service.CheckMovement(acc.Id, pos, inVehicle);

            if (player.CurrentWeapon != 0)
            {
                var allowed = _inventoryWeaponsOf(player);
                _service.CheckWeapon(acc.Id, player.CurrentWeapon, allowed ?? new HashSet<uint> { player.CurrentWeapon });
            }
        }
    }

    private void HandleViolation(int accountId, string reason, AntiCheatAction action)
    {
        Alt.Log($"[FloV:MP Shield] Нарушение от игрока acc:{accountId} -> {reason} (Действие: {action})");
        GameLog.System("anticheat_violation", ("accountId", accountId), ("reason", reason), ("action", action.ToString()));

        IPlayer? target = null;
        foreach (var p in Alt.GetAllPlayers())
        {
            if (p.Exists && _accountOf(p)?.Id == accountId)
            {
                target = p;
                break;
            }
        }

        if (target == null || !target.Exists) return;

        switch (action)
        {
            case AntiCheatAction.TeleportBack:
                var state = _service.GetOrCreateState(accountId, target.Name, Vector3D.Zero);
                target.Position = new Position(state.LastValidPosition.X, state.LastValidPosition.Y, state.LastValidPosition.Z);
                target.Emit("flovmp:chat:system", "[FloV:Shield] Обнаружена рассинхронизация перемещения. Вы возвращены на позицию.");
                break;

            case AntiCheatAction.Disarm:
                target.RemoveWeapon(target.CurrentWeapon);
                target.Emit("flovmp:chat:system", "[FloV:Shield] Запрещённое оружие изъято сервером.");
                break;

            case AntiCheatAction.Kick:
                target.Kick($"[FloV:Shield] {reason}");
                break;

            case AntiCheatAction.Warning:
                target.Emit("flovmp:chat:system", $"[FloV:Shield Предупреждение] {reason}");
                break;
        }
    }
}
