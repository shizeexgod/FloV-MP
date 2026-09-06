namespace FloVMP.Core.Vehicles;

/// <summary>
/// Сервис управления активным автопарком сервера «Держава Онлайн».
/// </summary>
public sealed class VehicleService
{
    private readonly object _lock = new();
    private readonly Dictionary<int, VehicleData> _byId = new();
    private readonly Dictionary<string, VehicleData> _byPlate = new(StringComparer.OrdinalIgnoreCase);

    public void RegisterVehicle(VehicleData vehicle)
    {
        lock (_lock)
        {
            _byId[vehicle.Id] = vehicle;
            if (!string.IsNullOrWhiteSpace(vehicle.Plate))
            {
                _byPlate[vehicle.Plate] = vehicle;
            }
        }
    }

    public VehicleData? FindById(int id)
    {
        lock (_lock)
        {
            return _byId.TryGetValue(id, out var v) ? v : null;
        }
    }

    public VehicleData? FindByPlate(string plate)
    {
        lock (_lock)
        {
            return _byPlate.TryGetValue(plate, out var v) ? v : null;
        }
    }

    public IReadOnlyList<VehicleData> GetVehiclesByOwner(int ownerAccountId)
    {
        lock (_lock)
        {
            return _byId.Values.Where(v => v.OwnerAccountId == ownerAccountId).ToList();
        }
    }

    public bool UnregisterVehicle(int id)
    {
        lock (_lock)
        {
            if (_byId.Remove(id, out var v))
            {
                if (!string.IsNullOrWhiteSpace(v.Plate))
                {
                    _byPlate.Remove(v.Plate);
                }
                return true;
            }
            return false;
        }
    }

    public IReadOnlyList<VehicleData> GetAll()
    {
        lock (_lock)
        {
            return _byId.Values.ToList();
        }
    }
}