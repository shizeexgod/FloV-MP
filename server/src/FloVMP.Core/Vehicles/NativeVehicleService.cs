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
            WarnLimited((uint)driverId, $"[FloV:MP Античит] машина {vehicleId}, водитель [{driverId}]: {type} — {reason}");
    }

    public VehicleRegistry Registry { get; }
    /// <summary>Режим античита (FLOVMP_ANTICHEAT): в off нарушения физики не пишутся.</summary>
    public bool PhysicsChecks { get; set; } = true;

    // ------------------------------------------------------------------ входящие

    /// <summary>VREQ reqId model — игрок сел за руль машины трафика.</summary>
    public void HandleRequest(uint playerId, string[] p, long nowMs)
    {
        var reqId = NativeProtocol.UIntOr(p, 1, 0);
        var model = NativeProtocol.UIntOr(p, 2, 0);
        if (!_host.TryGetPlayer(playerId, out var pl) || !pl.UsesRegistry) return;
        if (!pl.HasState)
        {
            _host.Send(playerId, NativeProtocol.Format("VREJ", reqId, "нет позиции игрока"));
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
        Registry.ApplyDriverSync(playerId, s);
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
    public bool Remove(uint vehicleId)
    {
        var v = Registry.Remove(vehicleId);
        if (v is null) return false;
        Forget(vehicleId);
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
    /// Раз в тик синхронизации: брошенные машины, затем каждому клиенту 1.0.6+
    /// — машины в радиусе его измерения. Вошла — снимок (VADD, VSTATE, VOWN),
    /// изменилась — VSTATE с частотой по дальности, вышла — VDEL.
    /// </summary>
    public void Replicate(long nowMs, IEnumerable<VehiclePlayer> recipients, float radius, int maxStreamed, long abandonedTtlMs)
    {
        _tick++;
        foreach (var v in Registry.CollectAbandoned(nowMs, abandonedTtlMs))
        {
            Forget(v.Id);
            _host.Warn($"[FloV:MP] Машина {v.Id} ({v.Plate}) убрана: никого не было дольше {abandonedTtlMs / 1000} с.");
        }

        _grid.Clear();
        _stateLines.Clear();
        foreach (var v in Registry.All) _grid.InsertOrUpdate(v.Id, new Vector3D(v.X, v.Y, v.Z), v.Dimension);

        foreach (var pl in recipients)
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
