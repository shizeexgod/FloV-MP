using System.Globalization;
using System.Text.Json;
using AltV.Net;
using AltV.Net.Elements.Entities;
using FloVMP.Core.AntiCheat;

namespace FloVMP.Starter;

/// <summary>
/// Вторая линия античита (пункт 7 roadmap): журнал подозрений с весами, куда
/// сходятся все проверки платформы — движение, урон, машины, оружие, модели,
/// патроны, ход часов клиента. Решает не одно срабатывание, а счёт.
///
/// По умолчанию — только журнал и предупреждение администраторам: ложное
/// срабатывание не должно выкидывать честного игрока. Отключение по счёту
/// включает владелец (anticheat.kick_score), а бан и любые свои меры — его
/// геймод по событию flovmp:anticheat:threshold. Всё настраивается в
/// client.cfg (раздел «Античит») или из кода (flovmp:settings:set).
/// </summary>
public partial class StarterResource
{
    private NativeAntiCheat? _ledgerAc;
    private bool _acStrict;

    private void StartSuspicionLedger(string mode)
    {
        _acStrict = mode == "strict";
        _ledgerAc = new NativeAntiCheat();
        _ledgerAc.Suspected += (player, type, weight, score, details) =>
        {
            var name = PlayerById(player)?.Name ?? "?";
            Alt.LogWarning($"[FloV:MP Античит] {type} +{weight:0.#} (счёт {score:0.#}): {name} [{player}] — {details}");
            Alt.Emit("flovmp:anticheat:suspicion", (int)player, type, weight, score, details);
        };
        _ledgerAc.ThresholdCrossed += OnSuspicionThreshold;
        ApplyAntiCheatSettings();
    }

    /// <summary>Настройки раздела «Античит» — при загрузке, reloadsettings и flovmp:settings:set.</summary>
    private void ApplyAntiCheatSettings()
    {
        var ac = _ledgerAc;
        if (ac is null) return;
        ac.Ledger.NotifyScore = _settings.Float("anticheat.notify_score");
        ac.Ledger.KickScore = _settings.Float("anticheat.kick_score");
        ac.Ledger.DecayPerMinute = _settings.Float("anticheat.decay_per_minute");
        ac.Weights[SuspicionKind.Movement] = _settings.Float("anticheat.weight_movement");
        ac.Weights[SuspicionKind.Hit] = _settings.Float("anticheat.weight_hit");
        ac.Weights[SuspicionKind.Weapon] = _settings.Float("anticheat.weight_weapon");
        ac.Weights[SuspicionKind.Ammo] = _settings.Float("anticheat.weight_ammo");
        ac.Weights[SuspicionKind.TimeScale] = _settings.Float("anticheat.weight_timescale");
        ac.Weights[SuspicionKind.Vehicle] = _settings.Float("anticheat.weight_vehicle");
        ac.Weights[SuspicionKind.Model] = _settings.Float("anticheat.weight_model");
        ac.WeaponBlacklist = GameHash.ParseList(_settings.Get("anticheat.weapon_blacklist"));
        ac.IssuedWeaponsOnly = _settings.Bool("anticheat.issued_weapons_only");
        ac.AmmoAccounting = _settings.Bool("anticheat.ammo_accounting");
        var peds = GameHash.ParseList(_settings.Get("anticheat.ped_whitelist"));
        // Модель появления — всегда своя: иначе список без неё выкидывал бы всех на входе.
        if (peds.Count > 0) peds.Add(SpawnModel());
        ac.PedWhitelist = peds;
        ac.VehicleBlacklist = GameHash.ParseList(_settings.Get("anticheat.vehicle_blacklist"));
        ac.Clock.MaxRatio = _settings.Float("anticheat.timescale_ratio");
        if (_vehicles is not null) WireVehicleAntiCheat(_vehicles);
    }

    /// <summary>Машины → журнал подозрений (чужой VSYNC, физика, запрещённая модель).</summary>
    private void WireVehicleAntiCheat(FloVMP.Core.Vehicles.NativeVehicleService service)
    {
        if (_ledgerAc is null || service.TrafficModelAllowed is not null) return;
        service.TrafficModelAllowed = model => _ledgerAc?.VehicleAllowed(model) ?? true;
        service.Suspicious += (player, details) =>
            _ledgerAc?.ReportLimited(player, SuspicionKind.Vehicle, details, _clock.ElapsedMilliseconds);
    }

    private void OnSuspicionThreshold(uint playerId, SuspicionLevel level, float score)
    {
        var player = PlayerById(playerId);
        var name = player?.Name ?? "?";
        var recent = string.Join("; ", (_ledgerAc?.Ledger.History(playerId) ?? Array.Empty<Suspicion>())
            .TakeLast(3).Select(s => s.Type));
        Alt.Emit("flovmp:anticheat:threshold", (int)playerId, level == SuspicionLevel.Kick ? "kick" : "notify", score);
        foreach (var admin in AllPlayers())
            if (IsAdmin(admin, 1))
                SendChatMessage(admin, $"{{f59e0b}}[Античит] {name} ({playerId}) — счёт {score:0}: {recent}. Подробнее: /ac {playerId}");
        Alt.LogWarning($"[FloV:MP Античит] {name} [{playerId}] пересёк порог «{level}» (счёт {score:0.#}).");
        if (level == SuspicionLevel.Kick && player is not null && player.Exists && !IsAdmin(player, 1))
            player.Kick("Античит: слишком много подозрительных действий. Если это ошибка — обратитесь к администрации.");
    }

    /// <summary>Подозрение из прежних проверок (движение, урон) — в общий журнал.</summary>
    private void ReportSuspicion(uint playerId, SuspicionKind kind, string details) =>
        _ledgerAc?.ReportLimited(playerId, kind, details, _clock.ElapsedMilliseconds);

    /// <summary>
    /// Раз в тик античита — игроки 3889: оружие и модель из STATE, ход часов
    /// из PING. Администраторы не проверяются: у них свои инструменты (/weapon,
    /// /skin, NoClip) — как и в проверке движения.
    /// </summary>
    private void TickSuspicionChecks(long nowMs)
    {
        var ac = _ledgerAc;
        if (ac is null) return;
        foreach (var (id, player) in _nativePlayers)
        {
            var np = (NativePlayerProxy)(object)player;
            var session = np.Session;
            var pinged = session.TryTakePing(out var clientMs, out var serverMs);
            if (!_nativeReady.Contains(id) || !session.HasState || IsAdmin(player, 1)) continue;
            if (pinged) ac.OnPing(id, clientMs, serverMs);
            if (ac.CheckState(id, np.State, nowMs) && _acStrict)
            {
                // Строгий режим: запрещённое оружие забираем сразу, счёт — отдельно.
                player.RemoveAllWeapons(true);
                SendChatMessage(player, "{f59e0b}[FloV:MP] Это оружие на сервере запрещено.");
            }
        }
    }

    /// <summary>Сервер выдал оружие — учёт патронов и «только выданное».</summary>
    internal void NoteWeaponIssued(uint playerId, uint weapon, int ammo) => _ledgerAc?.Weapons.Issue(playerId, weapon, ammo);
    internal void NoteWeaponsCleared(uint playerId) => _ledgerAc?.Weapons.Clear(playerId);
    internal void NoteModelIssued(uint playerId, uint model) => _ledgerAc?.NoteIssuedModel(playerId, model);

    private void ForgetSuspicions(uint playerId) => _ledgerAc?.RemovePlayer(playerId);

    // --- API для геймода -------------------------------------------------------------

    private void RegisterAntiCheatApi()
    {
        // Своё подозрение геймода (дюп денег, телепорт в закрытую зону): вес — свой.
        Alt.OnServer<int, string, float>("flovmp:anticheat:report", (id, details, weight) =>
        {
            if (_ledgerAc is null || id <= 0 || !Finite(weight)) return;
            _ledgerAc.Report((uint)id, SuspicionKind.Custom, Clean(details, 200), _clock.ElapsedMilliseconds, Math.Max(0, weight));
        });
        Alt.OnServer<int>("flovmp:anticheat:forgive", id => _ledgerAc?.Ledger.Forgive((uint)id));
        // Ответ: flovmp:anticheat:state (ID, счёт, JSON последних подозрений).
        Alt.OnServer<int>("flovmp:anticheat:query", id => Alt.Emit("flovmp:anticheat:state", id,
            _ledgerAc?.Ledger.Score((uint)id, _clock.ElapsedMilliseconds) ?? 0f, HistoryJson((uint)id)));
    }

    private string HistoryJson(uint playerId) => JsonSerializer.Serialize(
        (_ledgerAc?.Ledger.History(playerId) ?? Array.Empty<Suspicion>())
        .Select(s => new { type = s.Type, weight = s.Weight, details = s.Details, agoSec = (_clock.ElapsedMilliseconds - s.AtMs) / 1000 }));

    /// <summary>/ac ID — счёт и последние подозрения; /acforgive ID — обнулить.</summary>
    private bool HandleAntiCheatCommand(IPlayer admin, string cmd, string[] parts)
    {
        if (cmd is not ("ac" or "acforgive")) return false;
        if (_ledgerAc is null) { SendChatMessage(admin, "{fde047}Античит выключен (FLOVMP_ANTICHEAT=off)."); return true; }
        if (parts.Length < 2 || !uint.TryParse(parts[1], out var id))
        {
            SendChatMessage(admin, $"{{fde047}}Использование: /{cmd} <ID>");
            return true;
        }
        var name = PlayerById(id)?.Name ?? "не на сервере";
        if (cmd == "acforgive")
        {
            _ledgerAc.Ledger.Forgive(id);
            SendChatMessage(admin, $"{{34d399}}[Античит] Подозрения {name} ({id}) сброшены.");
            Alt.Log($"[FloV:MP Античит] {admin.Name} сбросил подозрения [{id}] {name}");
            return true;
        }
        var now = _clock.ElapsedMilliseconds;
        SendChatMessage(admin, $"{{38bdf8}}[Античит] {name} ({id}): счёт {_ledgerAc.Ledger.Score(id, now):0.#}" +
                               $" (предупреждение с {_ledgerAc.Ledger.NotifyScore:0}, отключение " +
                               (_ledgerAc.Ledger.KickScore > 0 ? $"с {_ledgerAc.Ledger.KickScore:0})" : "выключено)"));
        foreach (var s in _ledgerAc.Ledger.History(id).TakeLast(5))
            SendChatMessage(admin, $"{{a1a1aa}}  {(now - s.AtMs) / 1000} с назад · {s.Type} +{s.Weight:0.#} · {s.Details}");
        return true;
    }
}
