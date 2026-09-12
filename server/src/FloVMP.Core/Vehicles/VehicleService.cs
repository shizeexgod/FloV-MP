namespace FloVMP.Core.Vehicles;

/// <summary>
/// Сервис управления активным автопарком сервера FloV:MP.
/// </summary>
public sealed class VehicleService
{
    private readonly object _lock = new();
    private readonly Dictionary<int, VehicleData> _byId = new();
    private readonly Dictionary<string, VehicleData> _byPlate = new(StringComparer.OrdinalIgnoreCase);

    public void RegisterVehicle(VehicleData vehicle)
    {
        if (vehicle == null) return;
        lock (_lock)
        {
            if (_byId.TryGetValue(vehicle.Id, out var existing) && !string.IsNullOrWhiteSpace(existing.Plate))
            {
                _byPlate.Remove(existing.Plate);
            }

            _byId[vehicle.Id] = vehicle;
            if (!string.IsNullOrWhiteSpace(vehicle.Plate))
            {
                _byPlate[vehicle.Plate] = vehicle;
            }
        }
    }

    public void UpdatePlate(int id, string newPlate)
    {
        lock (_lock)
        {
            if (!_byId.TryGetValue(id, out var vehicle)) return;
            if (!string.IsNullOrWhiteSpace(vehicle.Plate))
            {
                _byPlate.Remove(vehicle.Plate);
            }
            vehicle.Plate = newPlate ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(newPlate))
            {
                _byPlate[newPlate] = vehicle;
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
        if (string.IsNullOrWhiteSpace(plate)) return null;
        lock (_lock)
        {
            return _byPlate.TryGetValue(plate.Trim(), out var v) ? v : null;
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