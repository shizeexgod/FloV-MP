using System.Text.Json;
using MySqlConnector;

namespace FloVMP.Core.Vehicles;

/// <summary>
/// Сохраняемая часть машины реестра (8d): то, без чего машина после
/// перезапуска не встанет на место. Двигатель не хранится — после рестарта
/// машина стоит заглушённой, как припаркованная.
/// </summary>
public sealed record PersistedVehicle(
    uint Id, uint Model, string Plate,
    float X, float Y, float Z, float Rx, float Ry, float Rz,
    int Dimension, bool Locked, float BodyHealth, float EngineHealth);

/// <summary>Хранилище сохраняемых машин одного мира.</summary>
public interface IVehicleStore
{
    string Describe { get; }
    IReadOnlyList<PersistedVehicle> LoadAll();
    void Upsert(PersistedVehicle vehicle);
    void Delete(uint id);
    void Flush();
}

/// <summary>
/// Файловое хранилище — когда MariaDB не настроена. Запись отложенная (раз в
/// секунду и на остановке), файл заменяется атомарно: оборванная запись не
/// оставит пустой файл вместо всех машин сервера.
/// </summary>
public sealed class JsonVehicleStore : IVehicleStore, IDisposable
{
    private readonly string _path;
    private readonly object _lock = new();
    private readonly Dictionary<uint, PersistedVehicle> _records = new();
    private readonly Timer _timer;
    private bool _dirty;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public JsonVehicleStore(string path)
    {
        _path = path;
        Load();
        _timer = new Timer(_ => Flush(), null, 1000, 1000);
    }

    public string Describe => $"локальный файл {_path}";

    public IReadOnlyList<PersistedVehicle> LoadAll()
    {
        lock (_lock) return _records.Values.OrderBy(v => v.Id).ToList();
    }

    public void Upsert(PersistedVehicle vehicle)
    {
        lock (_lock) { _records[vehicle.Id] = vehicle; _dirty = true; }
    }

    public void Delete(uint id)
    {
        lock (_lock) { if (_records.Remove(id)) _dirty = true; }
    }

    public void Flush()
    {
        lock (_lock)
        {
            if (!_dirty) return;
            _dirty = false;
            try
            {
                var dir = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                var tmp = _path + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(_records.Values.OrderBy(v => v.Id), JsonOpts));
                if (File.Exists(_path)) File.Replace(tmp, _path, null);
                else File.Move(tmp, _path);
            }
            catch (Exception ex)
            {
                _dirty = true; // повторим на следующем тике таймера
                CoreConsole.Warning($"[FloV:MP] vehicles.json: ошибка записи: {ex.Message}");
            }
        }
    }

    public void Dispose()
    {
        try { _timer.Dispose(); } catch { }
        Flush();
    }

    private void Load()
    {
        if (!File.Exists(_path)) return;
        try
        {
            var list = JsonSerializer.Deserialize<List<PersistedVehicle>>(File.ReadAllText(_path)) ?? new();
            foreach (var v in list) _records[v.Id] = v;
        }
        catch (Exception ex)
        {
            // Битый файл не должен молча превратиться в «машин нет» при
            // следующей записи: откладываем его в сторону для разбора.
            var moved = StoreFiles.QuarantineCorrupt(_path);
            CoreConsole.Warning($"[FloV:MP] vehicles.json не прочитан ({ex.Message}); файл отложен: {moved ?? "не удалось"}.");
        }
    }
}

/// <summary>Хранилище в MariaDB/MySQL (таблица vehicles, миграция 003).</summary>
public sealed class MySqlVehicleStore : IVehicleStore
{
    private readonly string _connectionString;
    private readonly string _world;

    public MySqlVehicleStore(string connectionString, string world)
    {
        _connectionString = connectionString;
        _world = world;
    }

    public string Describe => $"таблица vehicles MariaDB, мир «{_world}»";

    public IReadOnlyList<PersistedVehicle> LoadAll()
    {
        var result = new List<PersistedVehicle>();
        using var conn = new MySqlConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT `id`, `model`, `plate`, `pos_x`, `pos_y`, `pos_z`, `rot_x`, `rot_y`, `rot_z`, " +
            "       `dimension`, `locked`, `body_health`, `engine_health` " +
            "FROM `vehicles` WHERE `world` = @world ORDER BY `id`";
        cmd.Parameters.AddWithValue("@world", _world);
        using var r = cmd.ExecuteReader();
        while (r.Read())
            result.Add(new PersistedVehicle(r.GetUInt32(0), r.GetUInt32(1), r.GetString(2),
                r.GetFloat(3), r.GetFloat(4), r.GetFloat(5), r.GetFloat(6), r.GetFloat(7), r.GetFloat(8),
                r.GetInt32(9), r.GetBoolean(10), r.GetFloat(11), r.GetFloat(12)));
        return result;
    }

    public void Upsert(PersistedVehicle v)
    {
        using var conn = new MySqlConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "INSERT INTO `vehicles` (`world`, `id`, `model`, `plate`, `pos_x`, `pos_y`, `pos_z`, " +
            "  `rot_x`, `rot_y`, `rot_z`, `dimension`, `locked`, `body_health`, `engine_health`) " +
            "VALUES (@world, @id, @model, @plate, @x, @y, @z, @rx, @ry, @rz, @dim, @locked, @body, @engine) " +
            "ON DUPLICATE KEY UPDATE `model` = VALUES(`model`), `plate` = VALUES(`plate`), " +
            "  `pos_x` = VALUES(`pos_x`), `pos_y` = VALUES(`pos_y`), `pos_z` = VALUES(`pos_z`), " +
            "  `rot_x` = VALUES(`rot_x`), `rot_y` = VALUES(`rot_y`), `rot_z` = VALUES(`rot_z`), " +
            "  `dimension` = VALUES(`dimension`), `locked` = VALUES(`locked`), " +
            "  `body_health` = VALUES(`body_health`), `engine_health` = VALUES(`engine_health`)";
        cmd.Parameters.AddWithValue("@world", _world);
        cmd.Parameters.AddWithValue("@id", v.Id);
        cmd.Parameters.AddWithValue("@model", v.Model);
        cmd.Parameters.AddWithValue("@plate", v.Plate);
        cmd.Parameters.AddWithValue("@x", v.X);
        cmd.Parameters.AddWithValue("@y", v.Y);
        cmd.Parameters.AddWithValue("@z", v.Z);
        cmd.Parameters.AddWithValue("@rx", v.Rx);
        cmd.Parameters.AddWithValue("@ry", v.Ry);
        cmd.Parameters.AddWithValue("@rz", v.Rz);
        cmd.Parameters.AddWithValue("@dim", v.Dimension);
        cmd.Parameters.AddWithValue("@locked", v.Locked);
        cmd.Parameters.AddWithValue("@body", v.BodyHealth);
        cmd.Parameters.AddWithValue("@engine", v.EngineHealth);
        cmd.ExecuteNonQuery();
    }

    public void Delete(uint id)
    {
        using var conn = new MySqlConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM `vehicles` WHERE `world` = @world AND `id` = @id";
        cmd.Parameters.AddWithValue("@world", _world);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    public void Flush() { } // пишет сразу; очередь — в VehiclePersistence
}

/// <summary>Выбор хранилища: MariaDB, если база настроена и отвечает, иначе файл.</summary>
public static class VehicleStoreFactory
{
    public static IVehicleStore Create(string? connectionString, string world, string jsonFallbackPath)
    {
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            try
            {
                var store = new MySqlVehicleStore(connectionString, world);
                store.LoadAll(); // проверка: таблица есть, база отвечает
                return store;
            }
            catch (Exception ex)
            {
                CoreConsole.Warning($"[FloV:MP] [Транспорт] MariaDB недоступна для машин ({ex.Message}) — " +
                                    $"машины сохраняются в файл {jsonFallbackPath}.");
            }
        }
        return new JsonVehicleStore(jsonFallbackPath);
    }
}

/// <summary>
/// Запись машин мимо игрового потока (<see cref="Database.LatestWinsWriter{TKey}"/>):
/// сохранения идут из тика, а запрос к базе — десятки миллисекунд.
/// </summary>
public sealed class VehiclePersistence : IDisposable
{
    private readonly IVehicleStore _store;
    private readonly Database.LatestWinsWriter<uint> _writer;

    public VehiclePersistence(IVehicleStore store, Action<string> warn)
    {
        _store = store;
        _writer = new Database.LatestWinsWriter<uint>("flovmp-vehicle-store", "[Транспорт] машины", warn);
    }

    public string Describe => _store.Describe;
    public IReadOnlyList<PersistedVehicle> LoadAll() => _store.LoadAll();

    /// <summary>Сколько изменений ещё не записано (для диагностики и тестов).</summary>
    public int Pending => _writer.Pending;

    public void Save(PersistedVehicle vehicle) => _writer.Enqueue(vehicle.Id, () => _store.Upsert(vehicle));
    public void Delete(uint id) => _writer.Enqueue(id, () => _store.Delete(id));

    /// <summary>Дождаться записи всего накопленного (остановка сервера).</summary>
    public bool FlushBlocking(TimeSpan timeout)
    {
        var ok = _writer.FlushBlocking(timeout);
        _store.Flush();
        return ok;
    }

    public void Dispose()
    {
        _writer.Dispose();
        (_store as IDisposable)?.Dispose();
    }
}
