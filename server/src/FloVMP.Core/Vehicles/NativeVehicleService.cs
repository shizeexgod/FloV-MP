using FloVMP.Core.AntiCheat;
using FloVMP.Core.Native;
using FloVMP.Core.Spatial;

namespace FloVMP.Core.Vehicles;

/// <summary>Что сервис знает об игроке 3889 в момент вызова.</summary>
public readonly record struct VehiclePlayer(uint Id, float X, float Y, float Z, int Dimension, bool UsesRegistry, bool HasState);

/// <summary>
/// Связь сервиса с сервером: отправка строк и данные игроков. В игре это
/// StarterResource, в тестах — подделка; так вся логика реестра проверяется
/// без alt:V.
/// </summary>
public interface IVehicleHost
{
    bool TryGetPlayer(uint id, out VehiclePlayer player);
    void Send(uint playerId, string line);
    void Warn(string message);
}

/// <summary>
/// Протокол реестра транспорта поверх <see cref="VehicleRegistry"/>: разбор
/// VREQ/VENTER/VLEAVE/VSYNC, ответы, рассылка снимков и изменений клиентам
/// 1.0.6+. Порядок и правила — docs/vehicle-registry-protocol.md.
/// Только главный поток.
/// </summary>
public sealed class NativeVehicleService
{
    private readonly IVehicleHost _host;
    private readonly VehiclePhysicsGuardian _physics = new();
    // Кому какая машина уже показана и какая её версия отправлена.
    private readonly Dictionary<uint, Dictionary<uint, (long Version, long Tick)>> _visible = new();
    private readonly SpatialHashGrid<uint> _grid = new(SpatialHashGrid<uint>.RecommendedCellSize(400f));
    private readonly List<uint> _candidates = new();
    private readonly List<(uint Id, float D2)> _near = new();
    private readonly HashSet<uint> _inRange = new();
    private readonly List<uint> _gone = new();
    private readonly Dictionary<uint, string> _stateLines = new();
    private readonly Dictionary<uint, long> _warnedAt = new();
    private readonly Dictionary<uint, uint> _physicsDriver = new();
    private long _tick;

    public NativeVehicleService(IVehicleHost host, VehicleRegistry? registry = null)
    {
        _host = host;
        Registry = registry ?? new VehicleRegistry();
        _physics.OnVehicleViolation += (vehicleId, driverId, type, reason) =>
        {
            WarnLimited((uint)driverId, $"[FloV:MP Античит] машина {vehicleId}, водитель [{driverId}]: {type} — {reason}");
            Suspicious?.Invoke((uint)driverId, $"машина {vehicleId}: {type} — {reason}");
        };
    }

    public VehicleRegistry Registry { get; }

    // --- события для геймода (8c): StarterResource превращает их в Alt.Emit ---

    /// <summary>Игрок сел: (игрок, машина, место; −1 — водитель).</summary>
    public event Action<uint, uint, int>? PlayerEnteredVehicle;
    /// <summary>Игрок вышел или его высадили: (игрок, машина, место).</summary>
    public event Action<uint, uint, int>? PlayerLeftVehicle;
    /// <summary>Урон по данным водителя: (машина, потеря кузова, потеря двигателя, водитель).</summary>
    public event Action<uint, float, float, uint>? Damaged;
    /// <summary>Двигатель дошёл до −4000 — машина взорвана.</summary>
    public event Action<uint>? Destroyed;
    /// <summary>Машина убрана из реестра: (машина, причина: api, command, abandoned).</summary>
    public event Action<uint, string>? Removed;
    /// <summary>Подозрение для журнала античита: (игрок, подробности).</summary>
    public event Action<uint, string>? Suspicious;

    /// <summary>Можно ли взять модель в реестр как «трафик» (anticheat.vehicle_blacklist).
    /// null — любую.</summary>
    public Func<uint, bool>? TrafficModelAllowed { get; set; }
    /// <summary>Режим античита (FLOVMP_ANTICHEAT): в off нарушения физики не пишутся.</summary>
    public bool PhysicsChecks { get; set; } = true;

    /// <summary>Брать ли в реестр машины трафика, в которые сел игрок
    /// (vehicles.register_traffic). RP-проект может разрешить только свои машины.</summary>
    public bool AllowTrafficRegistration { get; set; } = true;

    /// <summary>Как часто сохранять изменившиеся сохраняемые машины (vehicles.save_interval_sec).</summary>
    public long SaveIntervalMs { get; set; } = 30_000;

    private VehiclePersistence? _persistence;
    // Какая версия машины уже в хранилище: не пишем то, что не менялось.
    private readonly Dictionary<uint, long> _savedVersion = new();
    private long _nextSaveMs;

    /// <summary>Подключить хранилище (8d). Без него реестр живёт только в памяти.</summary>
    public void AttachPersistence(VehiclePersistence persistence) => _persistence = persistence;

    /// <summary>
    /// Поднять сохранённые машины при старте. Возвращает (восстановлено,
    /// пропущено). restoreDamage false — все встают целыми (vehicles.restore_damage).
    /// </summary>
    public (int Restored, int Skipped) RestoreSaved(bool restoreDamage, long nowMs)
    {
        if (_persistence is null) return (0, 0);
        int restored = 0, skipped = 0;
        foreach (var p in _persistence.LoadAll())
        {
            var v = Registry.Restore(p, restoreDamage, nowMs);
            if (v is null) { skipped++; continue; }
            _savedVersion[v.Id] = v.Version;
            restored++;
        }
        return (restored, skipped);
    }

    /// <summary>Сохранять ли машину между перезапусками. Снятие флага удаляет её из хранилища.</summary>
    public bool SetPersistent(uint vehicleId, bool persistent)
    {
        var v = Registry.Get(vehicleId);
        if (v is null) return false;
        v.Persistent = persistent;
        if (persistent) SaveNow(v);
        else if (_savedVersion.Remove(vehicleId)) _persistence?.Delete(vehicleId);
        return true;
    }

    /// <summary>Сохранить все изменившиеся сохраняемые машины (по таймеру и при остановке).</summary>
    public int SaveAll()
    {
        if (_persistence is null) return 0;
        var saved = 0;
        foreach (var v in Registry.All)
            if (v.Persistent && (!_savedVersion.TryGetValue(v.Id, out var ver) || ver != v.Version))
            {
                SaveNow(v);
                saved++;
            }
        return saved;
    }

    private void SaveNow(RegisteredVehicle v)
    {
        if (_persistence is null || !v.Persistent) return;
        _persistence.Save(VehicleRegistry.Snapshot(v));
        _savedVersion[v.Id] = v.Version;
    }

    // ------------------------------------------------------------------ входящие

    /// <summary>VREQ reqId model — игрок сел за руль машины трафика.</summary>
    public void HandleRequest(uint playerId, string[] p, long nowMs)
    {
        var reqId = NativeProtocol.UIntOr(p, 1, 0);
        var model = NativeProtocol.UIntOr(p, 2, 0);
        if (!_host.TryGetPlayer(playerId, out var pl) || !pl.UsesRegistry) return;
        if (!AllowTrafficRegistration)
        {
            _host.Send(playerId, NativeProtocol.Format("VREJ", reqId, "на сервере машины трафика не регистрируются"));
            return;
        }
        if (!pl.HasState)
        {
            _host.Send(playerId, NativeProtocol.Format("VREJ", reqId, "нет позиции игрока"));
            return;
        }
        if (TrafficModelAllowed is { } allowed && !allowed(model))
        {
            // Такие машины по улицам не ездят: «сел в трафик» на ней — значит, создал её сам.
            _host.Send(playerId, NativeProtocol.Format("VREJ", reqId, "эта модель не берётся в реестр"));
            Suspicious?.Invoke(playerId, $"сел за руль запрещённой модели 0x{model:X8} (не из трафика)");
            return;
        }
        var (v, refusal) = Registry.RegisterTraffic(playerId, model, pl.X, pl.Y, pl.Z, 0, 0, 0, pl.Dimension, nowMs);
        if (v is null)
        {
            _host.Send(playerId, NativeProtocol.Format("VREJ", reqId, refusal ?? "отказано"));
            return;
        }
        // Машина уже есть в игре водителя: VADD ему не нужен, иначе клиент
        // создал бы вторую поверх своей. Отмечаем её показанной.
        VisibleOf(playerId)[v.Id] = (v.Version, _tick);
        _host.Send(playerId, NativeProtocol.Format("VREG", reqId, v.Id));
        FlushSeatChanges();
    }

    /// <summary>VENTER id seat. Отказ — VOWN с тем, кто на самом деле на этом месте.</summary>
    public void HandleEnter(uint playerId, string[] p, long nowMs)
    {
        var vehicleId = NativeProtocol.UIntOr(p, 1, 0);
        var seat = NativeProtocol.IntOr(p, 2, int.MinValue);
        if (!_host.TryGetPlayer(playerId, out var pl) || !pl.UsesRegistry) return;
        var result = pl.HasState
            ? Registry.TryEnter(playerId, vehicleId, seat, pl.X, pl.Y, pl.Z, pl.Dimension, nowMs)
            : EnterResult.TooFar;
        if (result == EnterResult.Ok)
        {
            FlushSeatChanges();
            return;
        }
        var v = Registry.Get(vehicleId);
        if (result is EnterResult.NoVehicle)
        {
            // Машины нет — клиенту нечего показывать: пусть уберёт свою копию.
            _host.Send(playerId, NativeProtocol.Format("VDEL", vehicleId));
            VisibleOf(playerId).Remove(vehicleId);
            return;
        }
        var occupant = v is not null && v.Seats.TryGetValue(seat, out var o) ? o : 0u;
        _host.Send(playerId, NativeVehicleProtocol.FormatOwner(vehicleId, occupant,
            Math.Clamp(seat, VehicleRegistry.DriverSeat, VehicleRegistry.MaxSeat)));
    }

    /// <summary>VLEAVE id — игрок вышел. Чужой ID игнорируем: выйти можно только из своей.</summary>
    public void HandleLeave(uint playerId, string[] p, long nowMs)
    {
        var vehicleId = NativeProtocol.UIntOr(p, 1, 0);
        if (Registry.SeatOf(playerId).VehicleId != vehicleId || vehicleId == 0) return;
        Registry.Leave(playerId, nowMs);
        FlushSeatChanges();
    }

    /// <summary>VSYNC — только от водителя; остальное в журнал античита.</summary>
    public void HandleSync(uint playerId, in NativeVehicleSync s, DateTime nowUtc)
    {
        var v = Registry.Get(s.VehicleId);
        if (v is null) return; // машину удалили, а строка ещё шла — не нарушение
        if (v.Driver != playerId)
        {
            WarnLimited(playerId, $"[FloV:MP Античит] VSYNC машины {s.VehicleId} от [{playerId}], а водитель — [{v.Driver}] — отброшено.");
            Suspicious?.Invoke(playerId, $"VSYNC чужой машины {s.VehicleId}");
            return;
        }
        if (PhysicsChecks)
        {
            // Новый водитель — новая история: скачок от места, где машину
            // бросил прежний, телепортом не считается.
            if (!_physicsDriver.TryGetValue(v.Id, out var tracked) || tracked != playerId)
            {
                _physics.UnregisterVehicle((int)v.Id);
                _physicsDriver[v.Id] = playerId;
            }
            // Режим log: нарушение только в журнал (решение владельца), значение принимаем.
            _physics.ValidateTick((int)v.Id, (int)playerId, new Vector3D(s.X, s.Y, s.Z),
                new Vector3D(s.Vx, s.Vy, s.Vz), s.BodyHealth, isInAir: false, nowUtc);
        }
        var body = v.BodyHealth;
        var engine = v.EngineHealth;
        var wasDestroyed = v.Destroyed;
        if (Registry.ApplyDriverSync(playerId, s) != SyncResult.Applied) return;
        if (v.BodyHealth < body || v.EngineHealth < engine)
            Damaged?.Invoke(v.Id, body - v.BodyHealth, engine - v.EngineHealth, playerId);
        if (!wasDestroyed && v.EngineHealth <= NativeVehicleProtocol.MinEngineHealth)
        {
            v.Destroyed = true;
            Destroyed?.Invoke(v.Id);
        }
    }

    /// <summary>Игрок ушёл или сменил измерение: место освобождается.</summary>
    public void PlayerLeft(uint playerId, long nowMs)
    {
        Registry.RemovePlayer(playerId, nowMs);
        _visible.Remove(playerId);
        _warnedAt.Remove(playerId);
        FlushSeatChanges();
    }

    public void PlayerDimensionChanged(uint playerId, long nowMs)
    {
        if (Registry.Leave(playerId, nowMs)) FlushSeatChanges();
    }

    // ------------------------------------------------------------ команды сервера

    /// <summary>/car у клиента 1.0.6+: машина реестра на месте игрока, игрок за рулём.</summary>
    public RegisteredVehicle? SpawnForPlayer(uint playerId, uint model, float heading, long nowMs)
    {
        if (!_host.TryGetPlayer(playerId, out var pl) || !pl.HasState) return null;
        var v = Registry.Create(model, pl.X, pl.Y, pl.Z, 0, 0, heading, pl.Dimension, nowMs, registeredBy: playerId);
        if (v is null) return null;
        v.EngineOn = true;
        // Снимок сразу, не дожидаясь тика рассылки: игрок должен оказаться в
        // машине в тот же момент, иначе «/car» выглядит как задержка.
        SendSnapshot(playerId, v);
        Registry.PutInto(playerId, v.Id, VehicleRegistry.DriverSeat, nowMs);
        FlushSeatChanges();
        return v;
    }

    /// <summary>Удалить машину и сразу убрать её у всех, кому она показана.</summary>
    public bool Remove(uint vehicleId, string reason = "api")
    {
        var v = Registry.Remove(vehicleId);
        if (v is null) return false;
        Forget(vehicleId);
        // Убранная машина не должна вернуться после перезапуска.
        if (_savedVersion.Remove(vehicleId)) _persistence?.Delete(vehicleId);
        FlushSeatChanges();   // сидевшим — событие «вышел»
        Removed?.Invoke(vehicleId, reason);
        return true;
    }

    /// <summary>Машина от геймода (API): стоит пустой, пока в неё не сядут.</summary>
    public RegisteredVehicle? Create(uint model, float x, float y, float z, float heading, int dimension,
        string? plate, bool persistent, long nowMs)
    {
        var v = Registry.Create(model, x, y, z, 0, 0, heading, dimension, nowMs, plate, persistent);
        if (v is not null) SaveNow(v);   // сохраняемая — в хранилище сразу, а не через таймер
        return v;
    }

    /// <summary>
    /// Посадить игрока (API). null — посажен, иначе причина отказа. Игроку,
    /// которому машина ещё не показана, сначала уходит её снимок: иначе VOWN
    /// пришёл бы про машину, которой у него нет.
    /// </summary>
    public string? PutInto(uint playerId, uint vehicleId, int seat, long nowMs)
    {
        if (!_host.TryGetPlayer(playerId, out var pl)) return "игрока нет на сервере";
        if (!pl.UsesRegistry) return "у игрока клиент старше 1.0.6";
        var v = Registry.Get(vehicleId);
        if (v is null) return "машины нет";
        if (seat < VehicleRegistry.DriverSeat || seat > VehicleRegistry.MaxSeat) return "нет такого места";
        if (v.Dimension != pl.Dimension) return "машина в другом измерении";
        if (!VisibleOf(playerId).ContainsKey(vehicleId)) SendSnapshot(playerId, v);
        Registry.PutInto(playerId, vehicleId, seat, nowMs);
        FlushSeatChanges();
        return null;
    }

    /// <summary>Высадить игрока (API): его клиент получит VOWN «место не ваше» и выйдет.</summary>
    public bool RemoveFrom(uint playerId, long nowMs)
    {
        if (Registry.SeatOf(playerId).VehicleId == 0) return false;
        Registry.Leave(playerId, nowMs);
        FlushSeatChanges();
        return true;
    }

    public bool SetHealth(uint vehicleId, float body, float engine)
    {
        var wasDestroyed = Registry.Get(vehicleId)?.Destroyed ?? false;
        if (!Registry.SetHealth(vehicleId, body, engine)) return false;
        // Геймод «взорвал» машину сам — событие то же, что при уроне от водителя.
        if (!wasDestroyed && Registry.Get(vehicleId)!.Destroyed) Destroyed?.Invoke(vehicleId);
        return PushToDriver(vehicleId);
    }

    /// <summary>Номер есть только в VADD — рассылаем снимок заново всем, кому машина показана.</summary>
    public bool SetPlate(uint vehicleId, string plate)
    {
        if (!Registry.SetPlate(vehicleId, plate)) return false;
        var v = Registry.Get(vehicleId)!;
        foreach (var (playerId, visible) in _visible)
            if (visible.ContainsKey(vehicleId)) SendSnapshot(playerId, v);
        return true;
    }

    public bool Repair(uint vehicleId) => Registry.Repair(vehicleId) && PushToDriver(vehicleId);
    public bool SetEngine(uint vehicleId, bool on) => Registry.SetEngine(vehicleId, on) && PushToDriver(vehicleId);
    public bool SetLocked(uint vehicleId, bool locked) => Registry.SetLocked(vehicleId, locked) && PushToDriver(vehicleId);

    /// <summary>
    /// Переходный релиз: как показать игрока 1.0.6+, сидящего в машине
    /// реестра, старому клиенту (&lt; 1.0.6). Старый умеет машину только как
    /// поля PSTATE водителя: модель, «владелец» = ID водителя, место, поворот.
    /// Машину без водителя старый показать не может — тогда false, и игрок
    /// для него идёт пешком (ограничение согласовано владельцем).
    /// </summary>
    public bool TryLegacyFields(uint playerId, out uint model, out int driverId, out int seat,
        out float rx, out float ry, out float rz)
    {
        model = 0; driverId = 0; seat = -1; rx = ry = rz = 0;
        var (vehicleId, s) = Registry.SeatOf(playerId);
        var v = vehicleId == 0 ? null : Registry.Get(vehicleId);
        if (v is null || v.Driver == 0) return false;
        model = v.Model;
        driverId = (int)v.Driver;
        seat = s;
        rx = v.Rx; ry = v.Ry; rz = v.Rz;
        return true;
    }

    // ---------------------------------------------------------------- рассылка

    /// <summary>
    /// Раз в тик синхронизации. players — все игроки 3889: и старые клиенты
    /// держат машину «нужной», пока стоят рядом. Затем уборка брошенного
    /// трафика и каждому клиенту 1.0.6+ — машины в радиусе его измерения.
    /// Вошла — снимок (VADD, VSTATE, VOWN), изменилась — VSTATE с частотой по
    /// дальности, вышла — VDEL.
    /// </summary>
    public void Replicate(long nowMs, IReadOnlyList<VehiclePlayer> players, float radius, int maxStreamed, long abandonedTtlMs)
    {
        _tick++;
        if (_persistence is not null && nowMs >= _nextSaveMs)
        {
            _nextSaveMs = nowMs + Math.Max(1000, SaveIntervalMs);
            SaveAll();
        }
        _grid.Clear();
        _stateLines.Clear();
        foreach (var v in Registry.All) _grid.InsertOrUpdate(v.Id, new Vector3D(v.X, v.Y, v.Z), v.Dimension);

        // Машина, которую кто-то видит, не брошена: игрок отошёл к дому или
        // магазину — вернётся к ней. Уборка только там, где никого нет.
        foreach (var pl in players)
        {
            if (!pl.HasState) continue;
            _candidates.Clear();
            _grid.FindInRadius(new Vector3D(pl.X, pl.Y, pl.Z), radius, pl.Dimension, _candidates, use3D: false);
            foreach (var id in _candidates) Registry.MarkActive(Registry.Get(id)!, nowMs);
        }
        foreach (var v in Registry.CollectAbandoned(nowMs, abandonedTtlMs))
        {
            _grid.Remove(v.Id);
            Forget(v.Id);
            Removed?.Invoke(v.Id, "abandoned");
            _host.Warn($"[FloV:MP] Машина трафика {v.Id} ({v.Plate}) убрана: пустая, рядом никого не было {abandonedTtlMs / 1000} с.");
        }

        foreach (var pl in players)
        {
            if (!pl.UsesRegistry) continue;
            var visible = VisibleOf(pl.Id);
            _near.Clear();
            if (pl.HasState)
            {
                _candidates.Clear();
                _grid.FindInRadius(new Vector3D(pl.X, pl.Y, pl.Z), radius, pl.Dimension, _candidates, use3D: false);
                foreach (var id in _candidates)
                {
                    var v = Registry.Get(id)!;
                    var dx = v.X - pl.X; var dy = v.Y - pl.Y;
                    _near.Add((id, dx * dx + dy * dy));
                }
                if (_near.Count > maxStreamed)
                {
                    _near.Sort((a, b) => a.D2.CompareTo(b.D2));
                    _near.RemoveRange(maxStreamed, _near.Count - maxStreamed);
                }
            }
            // Своя машина видна всегда: иначе при лимите потока водитель мог бы
            // получить VDEL машины, в которой сидит.
            var own = Registry.SeatOf(pl.Id).VehicleId;
            if (own != 0 && !_near.Exists(n => n.Id == own)) _near.Add((own, 0));

            _inRange.Clear();
            foreach (var (id, _) in _near) _inRange.Add(id);
            _gone.Clear();
            foreach (var id in visible.Keys) if (!_inRange.Contains(id)) _gone.Add(id);
            foreach (var id in _gone)
            {
                visible.Remove(id);
                _host.Send(pl.Id, NativeProtocol.Format("VDEL", id));
            }

            foreach (var (id, d2) in _near)
            {
                var v = Registry.Get(id)!;
                if (!visible.TryGetValue(id, out var sent))
                {
                    SendSnapshot(pl.Id, v);
                    continue;
                }
                if (sent.Version == v.Version) continue;
                // Водитель считает физику сам: VSTATE своей машины дёргал бы
                // его локальную машину. Серверные правки он получает через VSET.
                if (v.Driver == pl.Id) { visible[id] = (v.Version, _tick); continue; }
                var every = d2 < 60 * 60 ? 1 : d2 < 150 * 150 ? 2 : 4;
                if (_tick - sent.Tick < every) continue;
                visible[id] = (v.Version, _tick);
                if (!_stateLines.TryGetValue(id, out var line))
                    _stateLines[id] = line = NativeVehicleProtocol.FormatState(v.View());
                _host.Send(pl.Id, line);
            }
        }
    }

    /// <summary>Полный снимок: VADD, VSTATE, VOWN на каждое занятое место.</summary>
    private void SendSnapshot(uint playerId, RegisteredVehicle v)
    {
        var view = v.View();
        _host.Send(playerId, NativeVehicleProtocol.FormatAdd(view));
        _host.Send(playerId, NativeVehicleProtocol.FormatState(view));
        foreach (var (seat, occupant) in v.Seats)
            _host.Send(playerId, NativeVehicleProtocol.FormatOwner(v.Id, occupant, seat));
        VisibleOf(playerId)[v.Id] = (v.Version, _tick);
    }

    /// <summary>VOWN — сразу, не ждём тика: посадка должна выглядеть мгновенной.
    /// Получают все, кому машина показана, и сам севший/вышедший.</summary>
    private void FlushSeatChanges()
    {
        foreach (var c in Registry.DrainSeatChanges())
        {
            var line = NativeVehicleProtocol.FormatOwner(c.VehicleId, c.PlayerId, c.Seat);
            foreach (var (playerId, visible) in _visible)
                if (visible.ContainsKey(c.VehicleId)) _host.Send(playerId, line);
            // Сам севший и тот, кого сместили, узнают о своём месте всегда —
            // даже если машина ещё не попала в их рассылку.
            foreach (var who in new[] { c.PlayerId, c.PreviousPlayerId })
                if (who != 0 && (!_visible.TryGetValue(who, out var seen) || !seen.ContainsKey(c.VehicleId)))
                    _host.Send(who, line);
            if (c.PreviousPlayerId != 0 && c.PreviousPlayerId != c.PlayerId)
            {
                // Водитель припарковал машину — сохраняем место сразу, не ждём таймера:
                // сервер может упасть раньше, и машина «уедет» на полчаса назад.
                if (c.Seat == VehicleRegistry.DriverSeat && Registry.Get(c.VehicleId) is { } parked) SaveNow(parked);
                PlayerLeftVehicle?.Invoke(c.PreviousPlayerId, c.VehicleId, c.Seat);
            }
            if (c.PlayerId != 0) PlayerEnteredVehicle?.Invoke(c.PlayerId, c.VehicleId, c.Seat);
        }
    }

    /// <summary>Правка сервера: водителю VSET (он применит у себя), остальным
    /// уйдёт VSTATE обычной рассылкой — версия уже выросла.</summary>
    private bool PushToDriver(uint vehicleId)
    {
        var v = Registry.Get(vehicleId);
        if (v is null) return false;
        if (v.Driver != 0) _host.Send(v.Driver, NativeVehicleProtocol.FormatSet(v.View()));
        return true;
    }

    private void Forget(uint vehicleId)
    {
        _physics.UnregisterVehicle((int)vehicleId);
        _physicsDriver.Remove(vehicleId);
        foreach (var (playerId, visible) in _visible)
            if (visible.Remove(vehicleId)) _host.Send(playerId, NativeProtocol.Format("VDEL", vehicleId));
    }

    private Dictionary<uint, (long Version, long Tick)> VisibleOf(uint playerId)
    {
        if (!_visible.TryGetValue(playerId, out var v)) _visible[playerId] = v = new();
        return v;
    }

    /// <summary>В журнал — не чаще раза в 10 с на игрока: поток поддельных
    /// строк не должен забивать лог.</summary>
    private void WarnLimited(uint playerId, string message)
    {
        var now = Environment.TickCount64;
        if (_warnedAt.TryGetValue(playerId, out var at) && now - at < 10_000) return;
        _warnedAt[playerId] = now;
        _host.Warn(message);
    }
}
