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
    private readonly VehiclePhysicsGuardian _vehicleGuardian;
    private readonly CombatValidationService _combatValidation;
    private long _lastTickMs;

    public AntiCheatService Service => _service;
    public VehiclePhysicsGuardian VehicleGuardian => _vehicleGuardian;
    public CombatValidationService CombatValidation => _combatValidation;

    public AntiCheatSystem(
        Func<IPlayer, Account?> accountOf,
        Func<IPlayer, ISet<uint>?>? inventoryWeaponsOf = null,
        AntiCheatConfig? config = null)
    {
        _accountOf = accountOf ?? throw new ArgumentNullException(nameof(accountOf));
        _inventoryWeaponsOf = inventoryWeaponsOf ?? (_ => null);
        _service = new AntiCheatService(config);
        _vehicleGuardian = new VehiclePhysicsGuardian();
        _combatValidation = new CombatValidationService();

        _service.OnViolationDetected += HandleViolation;
        _vehicleGuardian.OnVehicleViolation += HandleVehicleViolation;
        _combatValidation.OnCombatViolation += HandleCombatViolation;

        Alt.OnWeaponDamage += OnWeaponDamage;
        Alt.OnPlayerEnterVehicle += OnPlayerEnterVehicle;
        Alt.OnPlayerLeaveVehicle += OnPlayerLeaveVehicle;
        Alt.OnClient<bool>("flovmp:admin:noclip", OnClientNoClip);
    }

    public void Detach()
    {
        Alt.OnWeaponDamage -= OnWeaponDamage;
        Alt.OnPlayerEnterVehicle -= OnPlayerEnterVehicle;
        Alt.OnPlayerLeaveVehicle -= OnPlayerLeaveVehicle;
    }

    private void OnClientNoClip(IPlayer player, bool enabled)
    {
        if (!player.Exists) return;
        var acc = _accountOf(player);
        if (acc == null) return;
        if (acc.AdminLevel > 0 || _service.IsAdminExempt(acc.Id))
        {
            _service.SetAdminExemption(acc.Id, enabled);
            if (!enabled)
            {
                NotifyAdminTeleport(player, player.Position);
            }
        }
    }

    private WeaponDamageResponse OnWeaponDamage(
        IPlayer player,
        IEntity target,
        uint weapon,
        ushort damage,
        Position shotOffset,
        BodyPart bodyPart,
        IEntity sourceEntity)
    {
        if (target is not IPlayer victim)
        {
            return true; // Разрешаем урон по объектам и транспорту
        }

        if (player == null || !player.Exists || !victim.Exists)
        {
            return false;
        }

        var attackerAcc = _accountOf(player);
        var victimAcc = _accountOf(victim);
        if (attackerAcc == null)
        {
            return false; // Неавторизованный игрок не наносит урон
        }

        if (attackerAcc.AdminLevel >= 4 || _service.IsAdminExempt(attackerAcc.Id))
        {
            return true; // Администраторы 4+ ранга освобождены от проверки
        }

        // Трансляция alt:V BodyPart в серверный HitboxZone
        HitboxZone zone = bodyPart switch
        {
            BodyPart.Head or BodyPart.Neck => HitboxZone.Head,
            BodyPart.LeftShoulder or BodyPart.LeftUpperArm or BodyPart.LeftElbow or BodyPart.LeftWrist => HitboxZone.LeftArm,
            BodyPart.RightShoulder or BodyPart.RightUpperArm or BodyPart.RightElbow or BodyPart.RightWrist => HitboxZone.RightArm,
            BodyPart.LeftHip or BodyPart.LeftLeg or BodyPart.LeftFoot => HitboxZone.LeftLeg,
            BodyPart.RightHip or BodyPart.RightLeg or BodyPart.RightFoot => HitboxZone.RightLeg,
            _ => HitboxZone.Torso
        };

        var attPos = new Vector3D(player.Position.X, player.Position.Y, player.Position.Z);
        var vicPos = new Vector3D(victim.Position.X, victim.Position.Y, victim.Position.Z);

        var result = _combatValidation.ValidateHit(
            attackerAcc.Id,
            attPos,
            player.Dimension,
            victimAcc?.Id ?? 0,
            vicPos,
            victim.Dimension,
            victim.Health,
            victim.Armor,
            weapon,
            zone,
            DateTime.UtcNow);

        if (!result.IsValid)
        {
            Alt.Log($"[FloV:Shield Combat] Блокирован подозрительный урон от {attackerAcc.Username} (acc:{attackerAcc.Id}) -> {result.Violation}: {result.Message}");
            return false; // Полная блокировка читерского урона
        }

        // Авторитетное серверное применение рассчитанного урона с баллистикой
        uint finalDmg = (uint)Math.Max(1, Math.Round(result.CalculatedDamage));
        return finalDmg;
    }

    private void OnPlayerEnterVehicle(IVehicle vehicle, IPlayer player, byte seat)
    {
        if (vehicle != null && vehicle.Exists)
        {
            var pos = new Vector3D(vehicle.Position.X, vehicle.Position.Y, vehicle.Position.Z);
            _vehicleGuardian.RegisterVehicle((int)vehicle.Id, pos, vehicle.BodyHealth);
        }
    }

    private void OnPlayerLeaveVehicle(IVehicle vehicle, IPlayer player, byte seat)
    {
        // При выходе сбрасываем статус
    }

    public void OnAuthed(IPlayer player, Account account)
    {
        if (!player.Exists) return;
        var pos = new Vector3D(player.Position.X, player.Position.Y, player.Position.Z);
        var state = _service.GetOrCreateState(account.Id, account.Username, pos);
        // Создатель проекта и администраторы освобождены от античита полностью.
        if (account.AdminLevel > 0 || IsOwner(account.Username))
        {
            _service.SetAdminExemption(account.Id, true);
        }
    }

    // Владельцы проекта из env FLOVMP_OWNER (список ников через запятую) — полное
    // освобождение от античита. Проверки нет ни для них, ни для админов.
    private static readonly HashSet<string> _owners = ParseOwners();
    private static HashSet<string> ParseOwners()
    {
        var raw = Environment.GetEnvironmentVariable("FLOVMP_OWNER") ?? "";
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            set.Add(name);
        return set;
    }
    private static bool IsOwner(string username) => _owners.Contains(username);

    public void OnDisconnect(IPlayer player)
    {
        var acc = _accountOf(player);
        if (acc != null)
        {
            _service.RemovePlayer(acc.Id);
            _combatValidation.CleanupPlayer(acc.Id);
        }
        _combatValidation.CleanupPlayer((int)player.Id);
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

            if (inVehicle && player.Vehicle != null && player.Vehicle.Driver == player)
            {
                var veh = player.Vehicle;
                var vPos = new Vector3D(veh.Position.X, veh.Position.Y, veh.Position.Z);
                var vVel = new Vector3D(veh.Velocity.X, veh.Velocity.Y, veh.Velocity.Z);
                bool inAir = Math.Abs(vVel.Z) > 8.0f || (vPos.Z > 25.0f && Math.Abs(vVel.Z) > 3.0f);

                _vehicleGuardian.ValidateTick(
                    (int)veh.Id,
                    acc.Id,
                    vPos,
                    vVel,
                    veh.BodyHealth,
                    inAir,
                    DateTime.UtcNow);
            }

            if (player.CurrentWeapon != 0)
            {
                var allowed = _inventoryWeaponsOf(player);
                _service.CheckWeapon(acc.Id, player.CurrentWeapon, allowed ?? new HashSet<uint> { player.CurrentWeapon });
            }
        }
    }

    private void HandleVehicleViolation(int vehicleId, int driverId, VehicleViolationType type, string reason)
    {
        Alt.Log($"[FloV:Shield Vehicle] Нарушение от водителя acc:{driverId} на авто veh:{vehicleId} -> {type}: {reason}");
        GameLog.System("anticheat_vehicle_violation", ("driverId", driverId), ("vehicleId", vehicleId), ("type", type.ToString()), ("reason", reason));

        IPlayer? driver = null;
        foreach (var p in Alt.GetAllPlayers())
        {
            if (p.Exists && _accountOf(p)?.Id == driverId)
            {
                driver = p;
                break;
            }
        }

        // Report-only: не глушим мотор и не наказываем — только рапорт админам.
        if (driver != null && driver.Exists)
        {
            var uname = _accountOf(driver)?.Username ?? driver.Name;
            ReportToAdmins($"{uname} (acc:{driverId}): физика транспорта ({type}): {reason}");
        }
    }

    private void HandleCombatViolation(int attackerId, CombatViolationType type, string reason)
    {
        Alt.Log($"[FloV:Shield Combat] Нарушение от стрелка acc:{attackerId} -> {type}: {reason}");
        GameLog.System("anticheat_combat_violation", ("attackerId", attackerId), ("type", type.ToString()), ("reason", reason));

        IPlayer? attacker = null;
        foreach (var p in Alt.GetAllPlayers())
        {
            if (p.Exists && _accountOf(p)?.Id == attackerId)
            {
                attacker = p;
                break;
            }
        }

        if (attacker != null && attacker.Exists)
        {
            attacker.Emit("flovmp:chat:msg", "system", "", $"[FloV:Shield] Выстрел отклонён античитом ({type}): {reason}");
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

        var uname = _accountOf(target)?.Username ?? target.Name;
        switch (action)
        {
            case AntiCheatAction.Disarm:
                // Изъятие запрещённого тяжёлого оружия (миниган/RPG и т.п.) —
                // защитная мера, не наказание игрока. Оставляем.
                target.RemoveWeapon(target.CurrentWeapon);
                ReportToAdmins($"{uname} (acc:{accountId}): {reason} [оружие изъято]");
                break;

            // Перемещение/скорость/телепорт — ТОЛЬКО рапорт администрации.
            // Анти-чит не кикает, не телепортит и не наказывает игрока: это
            // инструмент сработок/варнингов для админов (по требованию владельца).
            case AntiCheatAction.TeleportBack:
            case AntiCheatAction.Kick:
            case AntiCheatAction.Warning:
            default:
                ReportToAdmins($"{uname} (acc:{accountId}): {reason}");
                break;
        }
    }

    /// <summary>
    /// Отправить сработку античита всей онлайн-администрации (AdminLevel > 0) и
    /// в системный лог. Игрока НЕ трогаем — только рапорт.
    /// </summary>
    private void ReportToAdmins(string text)
    {
        var msg = $"[FloV:Shield] {text}";
        foreach (var p in Alt.GetAllPlayers())
        {
            if (!p.Exists) continue;
            if ((_accountOf(p)?.AdminLevel ?? 0) > 0)
                p.Emit("flovmp:chat:msg", "system", "Античит", msg);
        }
    }
}
