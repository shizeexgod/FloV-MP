using FloVMP.Core.Native;

namespace FloVMP.Core.Vehicles;

/// <summary>Откуда машина взялась: от сервера (/car, API) или из трафика игры клиента.</summary>
public enum VehicleOrigin { Server, Traffic }

/// <summary>
/// Машина реестра. Поля разделены так, как советует аудит Sayonara (8.4 —
/// против «god-класса»): постоянные (что сохранит 8d), реплицируемые (что
/// видят клиенты, меняют <see cref="Version"/>) и runtime (места, таймеры) —
/// последние наружу только на чтение, меняет их только реестр.
/// </summary>
public sealed class RegisteredVehicle
{
    // --- постоянные ---
    public uint Id { get; }
    public uint Model { get; }
    public string Plate { get; internal set; }
    /// <summary>Переживает перезапуск сервера (8d). Трафик, поднятый на лету, — нет.</summary>
    public bool Persistent { get; set; }

    // --- реплицируемые ---
    public float X { get; internal set; }
    public float Y { get; internal set; }
    public float Z { get; internal set; }
    public float Rx { get; internal set; }
    public float Ry { get; internal set; }
    public float Rz { get; internal set; }
    public float Vx { get; internal set; }
    public float Vy { get; internal set; }
    public float Vz { get; internal set; }
    public int Dimension { get; }
    public bool EngineOn { get; internal set; }
    public bool SirenOn { get; internal set; }
    public bool Locked { get; internal set; }
    public float BodyHealth { get; internal set; } = NativeVehicleProtocol.MaxHealth;
    public float EngineHealth { get; internal set; } = NativeVehicleProtocol.MaxHealth;
    /// <summary>Растёт при каждом изменении того, что видят клиенты: по нему
    /// рассылка решает, слать ли VSTATE.</summary>
    public long Version { get; internal set; } = 1;

    // --- runtime ---
    public VehicleOrigin Origin { get; }
    /// <summary>Кто создал или зарегистрировал машину (0 — сервер).</summary>
    public uint RegisteredBy { get; }
    public long LastOccupiedMs { get; internal set; }
    internal readonly SortedDictionary<int, uint> Seats = new();
    public IReadOnlyDictionary<int, uint> Occupants => Seats;
    public uint Driver => Seats.TryGetValue(VehicleRegistry.DriverSeat, out var d) ? d : 0;
    public bool IsEmpty => Seats.Count == 0;

    internal RegisteredVehicle(uint id, uint model, string plate, int dimension, VehicleOrigin origin, uint registeredBy, long nowMs)
    {
        Id = id;
        Model = model;
        Plate = plate;
        Dimension = dimension;
        Origin = origin;
        RegisteredBy = registeredBy;
        LastOccupiedMs = nowMs;
    }

    public RegisteredVehicleView View() => new(Id, Model, X, Y, Z, Rx, Ry, Rz, Vx, Vy, Vz, Dimension,
        EngineOn, SirenOn, Locked, BodyHealth, EngineHealth, Plate);
}

/// <summary>Кто теперь сидит на месте: из этого рассылка собирает VOWN.</summary>
public readonly record struct SeatChange(uint VehicleId, int Seat, uint PlayerId);

public enum EnterResult { Ok, NoVehicle, BadSeat, OtherDimension, TooFar, Locked, SeatTaken }
public enum SyncResult { Applied, Unchanged, NoVehicle, NotDriver }

/// <summary>
/// Серверный реестр транспорта (пункт 8 roadmap). Машина — сущность сервера,
/// а не поля в STATE водителя: у неё свой ID, она переживает выход водителя,
/// её здоровье считает сервер.
///
/// Только главный поток сервера (как всё игровое состояние): блокировок нет
/// намеренно. Время — параметром nowMs, чтобы тесты шли без часов.
/// </summary>
public sealed class VehicleRegistry
{
    public const int DriverSeat = -1;
    public const int MaxSeat = 15;
    /// <summary>Сесть можно только рядом с машиной: дальше — это телепорт в чужую машину.</summary>
    public const float MaxEnterDistance = 10f;
    /// <summary>Одна регистрация трафика на игрока за столько мс (решение владельца).</summary>
    public const long TrafficRegisterIntervalMs = 2000;
    public const int MaxPlateLength = 8;

    private readonly Dictionary<uint, RegisteredVehicle> _vehicles = new();
    private readonly Dictionary<uint, (uint VehicleId, int Seat)> _playerSeats = new();
    private readonly Dictionary<uint, long> _lastTrafficRegister = new();
    private readonly List<SeatChange> _seatChanges = new();
    private readonly Random _random;
    private uint _nextId = 1;

    public VehicleRegistry(int maxRegistered = 1000, Random? random = null)
    {
        MaxRegistered = maxRegistered;
        _random = random ?? Random.Shared;
    }

    /// <summary>Потолок машин на сервер (vehicles.max_registered).</summary>
    public int MaxRegistered { get; set; }
    public int Count => _vehicles.Count;
    public IEnumerable<RegisteredVehicle> All => _vehicles.Values;

    public RegisteredVehicle? Get(uint id) => _vehicles.TryGetValue(id, out var v) ? v : null;

    /// <summary>Где сидит игрок; (0, 0) — не в машине реестра.</summary>
    public (uint VehicleId, int Seat) SeatOf(uint playerId) =>
        _playerSeats.TryGetValue(playerId, out var s) ? s : (0u, 0);

    public RegisteredVehicle? VehicleOf(uint playerId) =>
        _playerSeats.TryGetValue(playerId, out var s) ? Get(s.VehicleId) : null;

    /// <summary>Пересадки с последнего вызова — для рассылки VOWN.</summary>
    public List<SeatChange> DrainSeatChanges()
    {
        var list = new List<SeatChange>(_seatChanges);
        _seatChanges.Clear();
        return list;
    }

    /// <summary>Создать машину (/car, API). null — упёрлись в потолок или модель пустая.</summary>
    public RegisteredVehicle? Create(uint model, float x, float y, float z, float rx, float ry, float rz, int dimension,
        long nowMs, string? plate = null, bool persistent = false,
        VehicleOrigin origin = VehicleOrigin.Server, uint registeredBy = 0)
    {
        if (model == 0 || _vehicles.Count >= MaxRegistered) return null;
        var id = AllocateId();
        if (id == 0) return null;
        var v = new RegisteredVehicle(id, model, NormalizePlate(plate) ?? RandomPlate(), dimension, origin, registeredBy, nowMs)
        {
            X = x, Y = y, Z = z, Rx = rx, Ry = ry, Rz = rz,
            Persistent = persistent,
        };
        _vehicles[id] = v;
        return v;
    }

    /// <summary>
    /// Игрок сел за руль машины трафика и просит её зарегистрировать. Отказ —
    /// строкой причины (уйдёт в VREJ). Успех — игрок становится водителем;
    /// из прежней машины реестра он при этом высаживается.
    /// </summary>
    public (RegisteredVehicle? Vehicle, string? Refusal) RegisterTraffic(uint playerId, uint model,
        float x, float y, float z, float rx, float ry, float rz, int dimension, long nowMs)
    {
        if (model == 0) return (null, "неизвестная модель");
        if (_lastTrafficRegister.TryGetValue(playerId, out var last) && nowMs - last < TrafficRegisterIntervalMs)
            return (null, "слишком часто");
        if (_vehicles.Count >= MaxRegistered) return (null, "на сервере слишком много машин");
        // Отметка до проверок ниже: поток отказанных запросов тоже ограничен.
        _lastTrafficRegister[playerId] = nowMs;
        var v = Create(model, x, y, z, rx, ry, rz, dimension, nowMs, origin: VehicleOrigin.Traffic, registeredBy: playerId);
        if (v is null) return (null, "на сервере слишком много машин");
        Leave(playerId, nowMs);
        Seat(v, playerId, DriverSeat, nowMs);
        return (v, null);
    }

    /// <summary>Посадить игрока, если можно. Проверки — серверные: замок,
    /// дистанция, измерение, занятость места.</summary>
    public EnterResult TryEnter(uint playerId, uint vehicleId, int seat, float px, float py, float pz, int dimension, long nowMs)
    {
        if (!_vehicles.TryGetValue(vehicleId, out var v)) return EnterResult.NoVehicle;
        if (seat < DriverSeat || seat > MaxSeat) return EnterResult.BadSeat;
        if (v.Dimension != dimension) return EnterResult.OtherDimension;
        var dx = v.X - px; var dy = v.Y - py; var dz = v.Z - pz;
        if (dx * dx + dy * dy + dz * dz > MaxEnterDistance * MaxEnterDistance) return EnterResult.TooFar;
        if (_playerSeats.TryGetValue(playerId, out var current) && current == (vehicleId, seat)) return EnterResult.Ok;
        if (v.Seats.TryGetValue(seat, out var occupant) && occupant != playerId) return EnterResult.SeatTaken;
        // Пересадка внутри машины — не «вход снаружи»: замок её не запрещает.
        var alreadyInside = current.VehicleId == vehicleId;
        if (v.Locked && !alreadyInside) return EnterResult.Locked;
        Leave(playerId, nowMs);
        Seat(v, playerId, seat, nowMs);
        return EnterResult.Ok;
    }

    /// <summary>Сервер сам сажает игрока (/car, API) — без проверок замка и дистанции.
    /// Занятое место освобождается: решение сервера важнее.</summary>
    public void PutInto(uint playerId, uint vehicleId, int seat, long nowMs)
    {
        if (!_vehicles.TryGetValue(vehicleId, out var v) || seat < DriverSeat || seat > MaxSeat) return;
        if (v.Seats.TryGetValue(seat, out var occupant) && occupant != playerId) Leave(occupant, nowMs);
        Leave(playerId, nowMs);
        Seat(v, playerId, seat, nowMs);
    }

    /// <summary>Высадить игрока. Водитель вышел — машина останавливается: без
    /// водителя её физику никто не считает, а остальные должны видеть её стоящей.</summary>
    public bool Leave(uint playerId, long nowMs)
    {
        if (!_playerSeats.Remove(playerId, out var s)) return false;
        if (!_vehicles.TryGetValue(s.VehicleId, out var v)) return true;
        v.Seats.Remove(s.Seat);
        v.LastOccupiedMs = nowMs;
        _seatChanges.Add(new SeatChange(v.Id, s.Seat, 0));
        if (s.Seat == DriverSeat && (v.Vx != 0 || v.Vy != 0 || v.Vz != 0))
        {
            v.Vx = v.Vy = v.Vz = 0;
            v.Version++;
        }
        return true;
    }

    /// <summary>Игрок ушёл с сервера: освободить место и забыть его лимиты.</summary>
    public void RemovePlayer(uint playerId, long nowMs)
    {
        Leave(playerId, nowMs);
        _lastTrafficRegister.Remove(playerId);
    }

    /// <summary>Удалить машину. Сидящие в ней остаются без места; VDEL
    /// клиентам шлёт рассылка, VOWN не нужен — машины просто нет.</summary>
    public RegisteredVehicle? Remove(uint vehicleId)
    {
        if (!_vehicles.Remove(vehicleId, out var v)) return null;
        foreach (var occupant in v.Seats.Values) _playerSeats.Remove(occupant);
        v.Seats.Clear();
        return v;
    }

    /// <summary>
    /// VSYNC водителя. Позиция и скорость — его (он считает физику), здоровье
    /// — только вниз: поднять его может лишь сервер (<see cref="SetHealth"/>).
    /// </summary>
    public SyncResult ApplyDriverSync(uint playerId, in NativeVehicleSync s)
    {
        if (!_vehicles.TryGetValue(s.VehicleId, out var v)) return SyncResult.NoVehicle;
        if (v.Driver != playerId || playerId == 0) return SyncResult.NotDriver;
        var body = MathF.Min(v.BodyHealth, s.BodyHealth);
        var engine = MathF.Min(v.EngineHealth, s.EngineHealth);
        if (v.X == s.X && v.Y == s.Y && v.Z == s.Z && v.Rx == s.Rx && v.Ry == s.Ry && v.Rz == s.Rz &&
            v.Vx == s.Vx && v.Vy == s.Vy && v.Vz == s.Vz && v.EngineOn == s.EngineOn && v.SirenOn == s.SirenOn &&
            v.BodyHealth == body && v.EngineHealth == engine)
            return SyncResult.Unchanged;
        v.X = s.X; v.Y = s.Y; v.Z = s.Z;
        v.Rx = s.Rx; v.Ry = s.Ry; v.Rz = s.Rz;
        v.Vx = s.Vx; v.Vy = s.Vy; v.Vz = s.Vz;
        v.EngineOn = s.EngineOn;
        v.SirenOn = s.SirenOn;
        v.BodyHealth = body;
        v.EngineHealth = engine;
        v.Version++;
        return SyncResult.Applied;
    }

    // --- команды сервера: любое направление, водителю уйдёт VSET ---

    public bool SetHealth(uint vehicleId, float body, float engine)
    {
        if (!_vehicles.TryGetValue(vehicleId, out var v)) return false;
        v.BodyHealth = Math.Clamp(body, 0f, NativeVehicleProtocol.MaxHealth);
        v.EngineHealth = Math.Clamp(engine, NativeVehicleProtocol.MinEngineHealth, NativeVehicleProtocol.MaxHealth);
        v.Version++;
        return true;
    }

    public bool Repair(uint vehicleId) =>
        SetHealth(vehicleId, NativeVehicleProtocol.MaxHealth, NativeVehicleProtocol.MaxHealth);

    public bool SetEngine(uint vehicleId, bool on) => Mutate(vehicleId, v => v.EngineOn = on);
    public bool SetLocked(uint vehicleId, bool locked) => Mutate(vehicleId, v => v.Locked = locked);
    public bool SetSiren(uint vehicleId, bool on) => Mutate(vehicleId, v => v.SirenOn = on);

    public bool SetPlate(uint vehicleId, string plate)
    {
        var p = NormalizePlate(plate);
        return p is not null && Mutate(vehicleId, v => v.Plate = p);
    }

    /// <summary>
    /// Непостоянные машины, в которых никого нет дольше ttl, — удалить. Иначе
    /// каждая машина трафика, в которую кто-то садился, навсегда занимала бы
    /// место под потолком. ttl 0 — не удалять никогда.
    /// </summary>
    public List<RegisteredVehicle> CollectAbandoned(long nowMs, long ttlMs)
    {
        var removed = new List<RegisteredVehicle>();
        if (ttlMs <= 0) return removed;
        foreach (var v in _vehicles.Values)
            if (!v.Persistent && v.IsEmpty && nowMs - v.LastOccupiedMs >= ttlMs) removed.Add(v);
        foreach (var v in removed) _vehicles.Remove(v.Id);
        return removed;
    }

    private bool Mutate(uint vehicleId, Action<RegisteredVehicle> change)
    {
        if (!_vehicles.TryGetValue(vehicleId, out var v)) return false;
        change(v);
        v.Version++;
        return true;
    }

    private void Seat(RegisteredVehicle v, uint playerId, int seat, long nowMs)
    {
        v.Seats[seat] = playerId;
        v.LastOccupiedMs = nowMs;
        _playerSeats[playerId] = (v.Id, seat);
        _seatChanges.Add(new SeatChange(v.Id, seat, playerId));
    }

    private uint AllocateId()
    {
        // ID не переиспользуется сразу: клиент мог ещё не получить VDEL прежней
        // машины, и новая с тем же номером слилась бы со старой.
        for (var i = 0; i <= _vehicles.Count; i++)
        {
            var id = _nextId;
            _nextId = _nextId == uint.MaxValue ? 1 : _nextId + 1;
            if (!_vehicles.ContainsKey(id)) return id;
        }
        return 0;
    }

    private static string? NormalizePlate(string? plate)
    {
        if (plate is null) return null;
        var clean = new string(plate.Where(ch => ch is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or ' ')
            .ToArray()).ToUpperInvariant().Trim();
        if (clean.Length == 0) return null;
        return clean.Length > MaxPlateLength ? clean[..MaxPlateLength] : clean;
    }

    /// <summary>Номер как у машин в игре: 2 цифры, 3 буквы, 3 цифры.</summary>
    private string RandomPlate()
    {
        Span<char> c = stackalloc char[8];
        for (var i = 0; i < 8; i++)
            c[i] = i is >= 2 and <= 4 ? (char)('A' + _random.Next(26)) : (char)('0' + _random.Next(10));
        return new string(c);
    }
}
