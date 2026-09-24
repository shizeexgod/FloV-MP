using System.Globalization;
using AltV.Net;
using FloVMP.Core.Vehicles;

namespace FloVMP.Starter;

/// <summary>
/// Публичный API реестра транспорта для геймода (пункт 8c roadmap).
///
/// Геймод — отдельный ресурс: объектов платформы он не получает, поэтому,
/// как и <c>flovmp:native:*</c>, всё идёт событиями с ID. Машина — её ID в
/// реестре (тот же, что видят клиенты и по которому 8d будет её хранить),
/// игрок — его ID. Места — как в нативах GTA: −1 водитель, 0…15 пассажиры
/// (в RAGE:MP водитель 0, в alt:V 1 — см. docs/vehicle-registry-protocol.md).
///
/// Работает для игроков с клиентом 1.0.6+: у старых клиентов машины живут в
/// их игре, реестр ими управлять не может.
/// </summary>
public partial class StarterResource
{
    private void RegisterVehicleApi()
    {
        // --- создание и удаление ------------------------------------------------------
        // Ответ — flovmp:vehicle:created (ключ, ID машины или 0, причина отказа):
        // Alt.Emit не возвращает значений, ключ связывает ответ с запросом.
        Alt.OnServer<string, string, float, float, float, float, int, string, bool>("flovmp:vehicle:create",
            (key, model, x, y, z, heading, dimension, plate, persistent) =>
            {
                key ??= "";
                var name = (model ?? "").Trim();
                string? refusal = null;
                if (name.Length == 0) refusal = "не указана модель";
                else if (!Finite(x) || !Finite(y) || !Finite(z)) refusal = "недопустимые координаты";
                RegisteredVehicle? v = null;
                if (refusal is null)
                {
                    var hash = uint.TryParse(name, out var raw) ? raw : Alt.Hash(name.ToLowerInvariant());
                    v = Vehicles.Create(hash, x, y, z, Finite(heading) ? heading : 0, dimension,
                        string.IsNullOrWhiteSpace(plate) ? null : plate, persistent, _clock.ElapsedMilliseconds);
                    if (v is null) refusal = "на сервере слишком много машин";
                }
                if (refusal is not null) Alt.LogWarning($"[FloV:MP] flovmp:vehicle:create «{key}»: {refusal}");
                Alt.Emit("flovmp:vehicle:created", key, (int)(v?.Id ?? 0), refusal ?? "");
            });

        Alt.OnServer<int>("flovmp:vehicle:remove", id =>
            WithVehicle(id, "remove", v => Vehicles.Remove(v.Id, "api")));

        // --- посадка ------------------------------------------------------------------
        Alt.OnServer<int, int, int>("flovmp:vehicle:putInto", (playerId, id, seat) =>
        {
            var refusal = Vehicles.PutInto((uint)playerId, (uint)id, seat, _clock.ElapsedMilliseconds);
            if (refusal is not null)
                Alt.LogWarning($"[FloV:MP] flovmp:vehicle:putInto: игрок {playerId}, машина {id}: {refusal}");
        });

        Alt.OnServer<int>("flovmp:vehicle:removeFrom", playerId =>
            Vehicles.RemoveFrom((uint)playerId, _clock.ElapsedMilliseconds));

        // --- состояние ----------------------------------------------------------------
        Alt.OnServer<int, bool>("flovmp:vehicle:engine", (id, on) =>
            WithVehicle(id, "engine", v => Vehicles.SetEngine(v.Id, on)));
        Alt.OnServer<int, bool>("flovmp:vehicle:lock", (id, locked) =>
            WithVehicle(id, "lock", v => Vehicles.SetLocked(v.Id, locked)));
        Alt.OnServer<int>("flovmp:vehicle:repair", id =>
            WithVehicle(id, "repair", v => Vehicles.Repair(v.Id)));
        // Здоровье в любую сторону — это решение сервера (от игрока — только вниз).
        Alt.OnServer<int, float, float>("flovmp:vehicle:health", (id, body, engine) =>
            WithVehicle(id, "health", v =>
            {
                if (!Finite(body) || !Finite(engine)) { Alt.LogWarning("[FloV:MP] flovmp:vehicle:health: недопустимые значения"); return; }
                Vehicles.SetHealth(v.Id, body, engine);
            }));
        Alt.OnServer<int, string>("flovmp:vehicle:plate", (id, plate) =>
            WithVehicle(id, "plate", v =>
            {
                if (!Vehicles.SetPlate(v.Id, plate ?? ""))
                    Alt.LogWarning("[FloV:MP] flovmp:vehicle:plate: номер — латиница, цифры и пробел, до 8 символов");
            }));
        // Сохранять ли машину между перезапусками (само сохранение — 8d).
        Alt.OnServer<int, bool>("flovmp:vehicle:persistent", (id, on) =>
            WithVehicle(id, "persistent", v => Vehicles.SetPersistent(v.Id, on)));

        // --- чтение -------------------------------------------------------------------
        // Ответ — flovmp:vehicle:state (см. EmitVehicleState).
        Alt.OnServer<int>("flovmp:vehicle:query", id => WithVehicle(id, "query", EmitVehicleState));
        Alt.OnServer("flovmp:vehicle:queryAll", () =>
        {
            foreach (var v in Vehicles.Registry.All.ToList()) EmitVehicleState(v);
        });
    }

    /// <summary>События реестра → события ресурсам. Подписка — один раз при создании сервиса.</summary>
    private void WireVehicleEvents(NativeVehicleService service)
    {
        service.PlayerEnteredVehicle += (player, vehicle, seat) =>
            Alt.Emit("flovmp:vehicle:enter", (int)player, (int)vehicle, seat);
        service.PlayerLeftVehicle += (player, vehicle, seat) =>
            Alt.Emit("flovmp:vehicle:leave", (int)player, (int)vehicle, seat);
        service.Damaged += (vehicle, bodyLoss, engineLoss, driver) =>
            Alt.Emit("flovmp:vehicle:damage", (int)vehicle, bodyLoss, engineLoss, (int)driver);
        service.Destroyed += vehicle => Alt.Emit("flovmp:vehicle:destroyed", (int)vehicle);
        service.Removed += (vehicle, reason) =>
        {
            _vehicleProxies.Remove(vehicle);
            Alt.Emit("flovmp:vehicle:removed", (int)vehicle, reason);
        };
    }

    private void WithVehicle(int id, string what, Action<RegisteredVehicle> action)
    {
        var v = id > 0 ? Vehicles.Registry.Get((uint)id) : null;
        if (v is null)
        {
            Alt.LogWarning($"[FloV:MP] flovmp:vehicle:{what}: машины с ID {id} нет");
            return;
        }
        try { action(v); }
        catch (Exception ex) { Alt.LogError($"[FloV:MP] flovmp:vehicle:{what} для машины {id}: {ex.Message}"); }
    }

    /// <summary>
    /// flovmp:vehicle:state — ID, модель, позиция, курс (градусы), измерение,
    /// номер, двигатель, закрыта, сирена, кузов, двигатель (здоровье), водитель
    /// (0 — нет), сохраняемая, из трафика ли. Ровно 16 значений: больше
    /// типизированный Alt.OnServer в ресурсе не примет — наклон машины сюда
    /// поэтому не входит (он есть у IVehicle.Rotation).
    /// </summary>
    private void EmitVehicleState(RegisteredVehicle v) =>
        Alt.Emit("flovmp:vehicle:state", (int)v.Id, v.Model.ToString(CultureInfo.InvariantCulture),
            v.X, v.Y, v.Z, v.Rz, v.Dimension, v.Plate,
            v.EngineOn, v.Locked, v.SirenOn, v.BodyHealth, v.EngineHealth,
            (int)v.Driver, v.Persistent, v.Origin == VehicleOrigin.Traffic);

    // Один объект IVehicle на машину: ресурс может сравнивать их по ссылке.
    private readonly Dictionary<uint, AltV.Net.Elements.Entities.IVehicle> _vehicleProxies = new();

    /// <summary>IPlayer.Vehicle игрока 3889 — машина реестра, в которой он сидит, или null.</summary>
    internal AltV.Net.Elements.Entities.IVehicle? VehicleProxyFor(uint playerId)
    {
        var v = _vehicles?.Registry.VehicleOf(playerId);
        if (v is null) return null;
        if (!_vehicleProxies.TryGetValue(v.Id, out var proxy))
        {
            proxy = System.Reflection.DispatchProxy.Create<AltV.Net.Elements.Entities.IVehicle, NativeVehicleProxy>();
            ((NativeVehicleProxy)(object)proxy).Init(v.Id, this);
            _vehicleProxies[v.Id] = proxy;
        }
        return proxy;
    }

    /// <summary>IPlayer.Seat в нумерации alt:V (водитель — 1), 0 — не в машине реестра.</summary>
    internal byte AltSeatFor(uint playerId)
    {
        var (vehicleId, seat) = _vehicles?.Registry.SeatOf(playerId) ?? (0u, 0);
        return vehicleId == 0 ? (byte)0 : (byte)(seat + 2);
    }

    internal RegisteredVehicle? RegisteredVehicleById(uint id) => _vehicles?.Registry.Get(id);
    internal AltV.Net.Elements.Entities.IPlayer? PlayerForProxy(uint id) => id == 0 ? null : PlayerById(id);

    internal void VehicleApiSetEngine(uint id, bool on) => Vehicles.SetEngine(id, on);
    internal void VehicleApiSetLocked(uint id, bool locked) => Vehicles.SetLocked(id, locked);
    internal void VehicleApiSetHealth(uint id, float body, float engine) => Vehicles.SetHealth(id, body, engine);
    internal void VehicleApiSetPlate(uint id, string plate) => Vehicles.SetPlate(id, plate);
    internal void VehicleApiRepair(uint id) => Vehicles.Repair(id);
    internal void VehicleApiRemove(uint id) => Vehicles.Remove(id, "api");
}
