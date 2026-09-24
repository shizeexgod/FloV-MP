using System;
using System.Collections.Generic;
using AltV.Net;
using AltV.Net.Elements.Entities;
using FloVMP.Core.Native;
using FloVMP.Core.Vehicles;

namespace FloVMP.Starter;

/// <summary>
/// Серверный реестр транспорта для клиентов 3889 версии 1.0.6+ (пункт 8
/// roadmap). Логика и протокол — в FloVMP.Core (<see cref="NativeVehicleService"/>),
/// здесь только связь с сессиями, настройками и командами.
///
/// Переходный релиз: клиенты ниже 1.0.6 живут по-старому (машина — поля
/// STATE водителя). Старый путь убирается после 1.0.7.
/// </summary>
public partial class StarterResource
{
    private NativeVehicleService? _vehicles;
    // Клиенты, понимающие реестр. Версия из HELLO не меняется за сессию —
    // решаем один раз при входе, а не на каждой строке рассылки.
    private readonly HashSet<uint> _registryClients = new();
    private readonly List<VehiclePlayer> _vehicleRecipients = new();

    private NativeVehicleService Vehicles => _vehicles ??= CreateVehicleService();

    private NativeVehicleService CreateVehicleService()
    {
        var mode = (Environment.GetEnvironmentVariable("FLOVMP_ANTICHEAT") ?? "log").Trim().ToLowerInvariant();
        var service = new NativeVehicleService(new VehicleHost(this), new VehicleRegistry(_settings.Int("vehicles.max_registered")))
        {
            PhysicsChecks = mode != "off",
        };
        WireVehicleEvents(service);
        WireVehicleAntiCheat(service);
        return service;
    }

    private bool UsesRegistry(uint playerId) => _registryClients.Contains(playerId);

    /// <summary>
    /// Сидит ли игрок в машине реестра. Только тогда поля машины в его STATE
    /// ничего не значат; иначе (машина не зарегистрирована) они — единственный
    /// способ показать её остальным, и работают по старому пути.
    /// </summary>
    private bool InRegistryVehicle(uint playerId) =>
        UsesRegistry(playerId) && _vehicles?.Registry.VehicleOf(playerId) is not null;

    private VehiclePersistence? _vehiclePersistence;

    /// <summary>
    /// Сохранение машин (8d): хранилище по настройке vehicles.persistence,
    /// подъём сохранённых машин и событие flovmp:vehicles:loaded ресурсам.
    /// </summary>
    private void StartVehiclePersistence(string? dbConnection, string dataDir)
    {
        if (!_settings.Bool("vehicles.persistence"))
        {
            Alt.Log("[FloV:MP] [Транспорт] Сохранение машин платформой выключено (vehicles.persistence = off) — его делает ваш геймод.");
            return;
        }
        try
        {
            var world = WorldName();
            var store = VehicleStoreFactory.Create(dbConnection, world, Path.Combine(dataDir, "vehicles.json"));
            _vehiclePersistence = new VehiclePersistence(store, Alt.LogWarning);
            Vehicles.AttachPersistence(_vehiclePersistence);
            ApplyVehicleSettings();
            var (restored, skipped) = Vehicles.RestoreSaved(_settings.Bool("vehicles.restore_damage"), _clock.ElapsedMilliseconds);
            Alt.Log($"[FloV:MP] [Транспорт] Машины сохраняются: {store.Describe}. Восстановлено: {restored}" +
                    (skipped > 0 ? $", пропущено {skipped} (потолок vehicles.max_registered или повтор ID)." : "."));
            Alt.Emit("flovmp:vehicles:loaded", restored);
        }
        catch (Exception ex)
        {
            // Без машин сервер работать может, без запуска — нет.
            Alt.LogError($"[FloV:MP] [Транспорт] Сохранённые машины не подняты: {ex.Message}");
        }
    }

    /// <summary>Имя мира в базе (world.name): одно на машины и игроков.</summary>
    private string WorldName()
    {
        var world = _settings.Get("world.name").Trim();
        return world.Length is 0 or > 32 ? "main" : world;
    }

    private void StopVehiclePersistence()
    {
        if (_vehiclePersistence is null) return;
        try
        {
            var saved = Vehicles.SaveAll();
            var ok = _vehiclePersistence.FlushBlocking(TimeSpan.FromSeconds(10));
            Alt.Log($"[FloV:MP] [Транспорт] Машины сохранены перед остановкой ({saved} изменились)" +
                    (ok ? "." : " — НЕ всё успело записаться, проверьте хранилище."));
            _vehiclePersistence.Dispose();
        }
        catch (Exception ex) { Alt.LogWarning($"[FloV:MP] [Транспорт] Сохранение перед остановкой: {ex.Message}"); }
        _vehiclePersistence = null;
    }

    /// <summary>Настройки транспорта из client.cfg (и flovmp:settings:set) — на лету.</summary>
    private void ApplyVehicleSettings()
    {
        var v = Vehicles;
        v.Registry.MaxRegistered = _settings.Int("vehicles.max_registered");
        v.Registry.PlateFormat = _settings.Get("vehicles.plate_format");
        v.Registry.MaxEnterDistance = _settings.Float("vehicles.enter_distance");
        v.Registry.TrafficRegisterIntervalMs = (long)(_settings.Float("vehicles.register_cooldown_sec") * 1000);
        v.AllowTrafficRegistration = _settings.Bool("vehicles.register_traffic");
        v.SaveIntervalMs = _settings.Int("vehicles.save_interval_sec") * 1000L;
    }

    private void OnVehicleClientJoined(NativeSession session)
    {
        if (NativeVehicleProtocol.SupportsRegistry(session.ClientVersion))
        {
            _registryClients.Add(session.Id);
            return;
        }
        // Решение владельца: предупредить, что старый клиент видит не всё.
        Alt.LogWarning($"[FloV:MP b3889] {session.Name}: клиент {session.ClientVersion} старше 1.0.6 — " +
                       "машины реестра без водителя он не увидит. Попросите игрока обновить клиент.");
    }

    private void OnVehicleClientLeft(uint playerId)
    {
        _vehicles?.PlayerLeft(playerId, _clock.ElapsedMilliseconds);
        _registryClients.Remove(playerId);
        _legacyPassengerNoted.Remove(playerId);
    }

    private void OnVehicleMessage(NativeSession session, string[] p)
    {
        var now = _clock.ElapsedMilliseconds;
        switch (p[0])
        {
            case "VREQ": Vehicles.HandleRequest(session.Id, p, now); break;
            case "VENTER": Vehicles.HandleEnter(session.Id, p, now); break;
            case "VLEAVE": Vehicles.HandleLeave(session.Id, p, now); break;
        }
    }

    /// <summary>VSYNC копятся в сессиях (последний побеждает) — забираем перед рассылкой.</summary>
    private void TakeVehicleSyncs()
    {
        if (_registryClients.Count == 0) return;
        var nowUtc = DateTime.UtcNow;
        foreach (var id in _registryClients)
        {
            if (!_nativePlayers.TryGetValue(id, out var player)) continue;
            var session = ((NativePlayerProxy)(object)player).Session;
            if (session.TryTakeVehicleSync(out var sync)) Vehicles.HandleSync(id, sync, nowUtc);
        }
    }

    private void ReplicateVehicles(long nowMs)
    {
        if (_vehicles is null && _registryClients.Count == 0) return;
        ApplyVehicleSettings();
        // Все игроки 3889, не только 1.0.6+: рядом стоящий старый клиент тоже
        // не даёт убрать машину как брошенную. Рассылку сервис шлёт только 1.0.6+.
        _vehicleRecipients.Clear();
        foreach (var id in _nativeReady)
            if (TryGetVehiclePlayer(id, out var pl))
                _vehicleRecipients.Add(pl);
        Vehicles.Replicate(nowMs, _vehicleRecipients, _settings.Float("sync.stream_radius"),
            _settings.Int("sync.max_streamed"), _settings.Int("vehicles.abandoned_ttl_sec") * 1000L);
    }

    private bool TryGetVehiclePlayer(uint id, out VehiclePlayer player)
    {
        player = default;
        if (!_nativePlayers.TryGetValue(id, out var p)) return false;
        var np = (NativePlayerProxy)(object)p;
        var st = np.State;
        player = new VehiclePlayer(id, st.X, st.Y, st.Z, np.DimensionValue, UsesRegistry(id), np.Session.HasState);
        return true;
    }

    /// <summary>
    /// STATE клиента 1.0.6+ в машине реестра: поля машины игнорируются
    /// (решение владельца — место берём только из VENTER/VLEAVE/VOWN), флаг
    /// «в транспорте» остаётся для анимаций и проверок урона.
    /// </summary>
    private static NativePlayerState WithoutVehicleFields(NativePlayerState st) =>
        st.VehicleModel == 0 && st.VehicleOwner == 0 && st.Seat == -1
            ? st
            : st with { VehicleModel = 0, VehicleOwner = 0, Seat = -1 };

    /// <summary>Тот же игрок 1.0.6+ глазами старого клиента: машина — полями PSTATE.</summary>
    private NativePlayerState LegacyViewOf(uint playerId, NativePlayerState st)
    {
        if (_vehicles is not null && _vehicles.TryLegacyFields(playerId, out var model, out var driver, out var seat,
                out var rx, out var ry, out var rz))
            return st with
            {
                Flags = st.Flags | NativePlayerState.FlagInVehicle,
                VehicleModel = model, VehicleOwner = driver, Seat = seat, Rx = rx, Ry = ry, Rz = rz,
            };
        // Машину без водителя старый клиент показать не может — для него игрок пеший.
        return (st.Flags & NativePlayerState.FlagInVehicle) == 0 ? st : st with { Flags = st.Flags & ~NativePlayerState.FlagInVehicle };
    }

    /// <summary>Команды транспорта клиента 1.0.6+ — через реестр. false — не наша команда.</summary>
    private bool HandleRegistryVehicleCommand(IPlayer player, NativePlayerProxy np, string cmd, string[] parts)
    {
        var id = np.Session.Id;
        var now = _clock.ElapsedMilliseconds;
        var current = Vehicles.Registry.VehicleOf(id);
        switch (cmd)
        {
            case "car":
            case "veh":
                var modelName = parts.Length > 1 ? parts[1] : "adder";
                modelName = new string(Array.FindAll(modelName.ToCharArray(), ch => char.IsLetterOrDigit(ch) || ch == '_'));
                if (modelName.Length == 0 || modelName.Length > 32)
                {
                    SendChatMessage(player, "{fde047}Использование: /car <модель, например adder>");
                    return true;
                }
                if (!np.Session.HasState)
                {
                    SendChatMessage(player, "{fde047}[Транспорт] Сервер ещё не знает, где вы — попробуйте через секунду.");
                    return true;
                }
                var v = Vehicles.SpawnForPlayer(id, Alt.Hash(modelName.ToLowerInvariant()), np.State.Heading, now);
                if (v is null) SendChatMessage(player, "{f87171}[Транспорт] На сервере слишком много машин.");
                else Alt.Log($"[FloV:MP] {np.Session.Name} создал {modelName}: машина {v.Id} ({v.Plate}).");
                return true;
            case "fix":
            case "repair":
                if (current is null) { SendChatMessage(player, "{fde047}Сядьте в транспорт, чтобы починить его."); return true; }
                Vehicles.Repair(current.Id);
                SendChatMessage(player, "{34d399}Транспорт отремонтирован.");
                return true;
            case "dv":
            case "delveh":
            case "destroyveh":
                // Как у старого клиента: убрать можно и стоя рядом — ближайшую
                // свою машину в радиусе DeleteOwnRadius (решение владельца).
                var doomed = current ?? (np.Session.HasState
                    ? Vehicles.Registry.NearestOwned(id, np.State.X, np.State.Y, np.State.Z, np.DimensionValue, DeleteOwnRadius)
                    : null);
                if (doomed is null)
                {
                    SendChatMessage(player, $"{{fde047}}[Транспорт] Рядом нет вашей машины: сядьте в неё или подойдите ближе {DeleteOwnRadius:0} м.");
                    return true;
                }
                Vehicles.Remove(doomed.Id, "command");
                SendChatMessage(player, "{34d399}[Транспорт] Машина убрана.");
                return true;
            case "engine":
                if (current is null) { SendChatMessage(player, "{fde047}[Транспорт] Вы должны находиться в транспортном средстве."); return true; }
                Vehicles.SetEngine(current.Id, !current.EngineOn);
                return true;
            case "lock":
                // Как у старого клиента: закрыть можно и снаружи — свою машину
                // (в которой сидишь, иначе последнюю, что создал или угнал).
                // Ключи и доступ по владельцу-персонажу — забота геймода (8c).
                var target = current ?? LastOwnVehicle(id);
                if (target is null) { SendChatMessage(player, "{fde047}[Транспорт] Поблизости нет вашего транспорта."); return true; }
                Vehicles.SetLocked(target.Id, !target.Locked);
                SendChatMessage(player, target.Locked ? "{f87171}[Транспорт] Двери заблокированы." : "{34d399}[Транспорт] Двери разблокированы.");
                return true;
        }
        return false;
    }

    // Старый клиент пассажиром в машине реестра: одна строка на поездку, а
    // не 20 в секунду (STATE идёт с такой частотой).
    private readonly Dictionary<uint, uint> _legacyPassengerNoted = new();

    /// <summary>
    /// Клиент ниже 1.0.6 сел пассажиром к водителю 1.0.6+: машину реестра он
    /// видит только полями PSTATE, а его место сервер подтвердить не может —
    /// для остальных он остаётся пешим. Это ограничение переходного релиза,
    /// а не баг: владельцу сервера пишем об этом отдельной строкой.
    /// </summary>
    private void NoteLegacyPassengerRefused(uint passengerId, uint driverId)
    {
        var vehicleId = _vehicles?.Registry.VehicleOf(driverId)?.Id ?? 0;
        if (_legacyPassengerNoted.TryGetValue(passengerId, out var noted) && noted == vehicleId) return;
        _legacyPassengerNoted[passengerId] = vehicleId;
        var name = _nativePlayers.TryGetValue(passengerId, out var p) ? ((NativePlayerProxy)(object)p).Session.Name : "?";
        Alt.LogWarning($"[FloV:MP Транспорт] переходный период 1.0.6: {name} [{passengerId}] со старым клиентом " +
                       $"не может ехать пассажиром в машине реестра {vehicleId} (водитель [{driverId}]) — " +
                       "для остальных он высажен. Обновление клиента игрока это снимет.");
    }

    /// <summary>/dv снаружи: своя машина не дальше этого, м.</summary>
    private const float DeleteOwnRadius = 10f;

    private RegisteredVehicle? LastOwnVehicle(uint playerId)
    {
        RegisteredVehicle? last = null;
        foreach (var v in Vehicles.Registry.All)
            if (v.RegisteredBy == playerId && (last is null || v.Id > last.Id)) last = v;
        return last;
    }

    /// <summary>Мост к сервису: отправка и данные игроков.</summary>
    private sealed class VehicleHost : IVehicleHost
    {
        private readonly StarterResource _owner;
        public VehicleHost(StarterResource owner) => _owner = owner;

        public bool TryGetPlayer(uint id, out VehiclePlayer player) => _owner.TryGetVehiclePlayer(id, out player);

        public void Send(uint playerId, string line)
        {
            if (_owner._nativePlayers.TryGetValue(playerId, out var p))
                ((NativePlayerProxy)(object)p).Session.Send(line);
        }

        public void Warn(string message) => Alt.LogWarning(message);
    }
}
