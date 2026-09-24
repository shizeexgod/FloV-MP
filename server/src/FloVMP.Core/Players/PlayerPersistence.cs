using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using FloVMP.Core.Database;
using MySqlConnector;

namespace FloVMP.Core.Players;

/// <summary>Оружие игрока по учёту сервера; Ammo −1 — без учёта (холодное).</summary>
public sealed record SavedWeapon(uint Hash, int Ammo);

/// <summary>
/// То, что платформа сама восстанавливает при входе (пункт 10 roadmap).
/// Identity — постоянный ID игрока (player.SocialClubId): у клиента 3889 — ID
/// его ключа, у клиента alt:V — SocialClub.
/// </summary>
public sealed record PersistedPlayer(
    string Identity, string Name, float X, float Y, float Z, float Heading, int Dimension,
    int Health, int Armor, uint Model, IReadOnlyList<SavedWeapon> Weapons);

/// <summary>Хранилище игроков одного мира: состояние и данные геймода «ключ → значение».</summary>
public interface IPlayerStore
{
    string Describe { get; }
    IReadOnlyList<PersistedPlayer> LoadAllStates();
    void UpsertState(PersistedPlayer player);
    IReadOnlyDictionary<string, string> LoadData(string identity);
    /// <summary>value null — удалить ключ.</summary>
    void SetData(string identity, string key, string? value);
    void Flush();
}

/// <summary>Файл flovmp-data/players.json — когда базы нет. Атомарная замена, запись раз в секунду.</summary>
public sealed class JsonPlayerStore : IPlayerStore, IDisposable
{
    private sealed class Record
    {
        public PersistedPlayer? State { get; set; }
        public Dictionary<string, string> Data { get; set; } = new();
    }

    private readonly string _path;
    private readonly object _lock = new();
    private Dictionary<string, Record> _records = new();
    private readonly Timer _timer;
    private bool _dirty;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    public JsonPlayerStore(string path)
    {
        _path = path;
        Load();
        _timer = new Timer(_ => Flush(), null, 1000, 1000);
    }

    public string Describe => $"локальный файл {_path}";

    public IReadOnlyList<PersistedPlayer> LoadAllStates()
    {
        lock (_lock) return _records.Values.Where(r => r.State is not null).Select(r => r.State!).ToList();
    }

    public void UpsertState(PersistedPlayer player)
    {
        lock (_lock) { RecordOf(player.Identity).State = player; _dirty = true; }
    }

    public IReadOnlyDictionary<string, string> LoadData(string identity)
    {
        lock (_lock) return _records.TryGetValue(identity, out var r) ? new Dictionary<string, string>(r.Data) : new();
    }

    public void SetData(string identity, string key, string? value)
    {
        lock (_lock)
        {
            var r = RecordOf(identity);
            if (value is null) r.Data.Remove(key);
            else r.Data[key] = value;
            _dirty = true;
        }
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
                File.WriteAllText(tmp, JsonSerializer.Serialize(_records, JsonOpts));
                if (File.Exists(_path)) File.Replace(tmp, _path, null);
                else File.Move(tmp, _path);
            }
            catch (Exception ex)
            {
                _dirty = true;
                CoreConsole.Warning($"[FloV:MP] players.json: ошибка записи: {ex.Message}");
            }
        }
    }

    public void Dispose()
    {
        try { _timer.Dispose(); } catch { }
        Flush();
    }

    private Record RecordOf(string identity)
    {
        if (!_records.TryGetValue(identity, out var r)) _records[identity] = r = new Record();
        return r;
    }

    private void Load()
    {
        if (!File.Exists(_path)) return;
        try
        {
            _records = JsonSerializer.Deserialize<Dictionary<string, Record>>(File.ReadAllText(_path)) ?? new();
        }
        catch (Exception ex)
        {
            var moved = StoreFiles.QuarantineCorrupt(_path);
            CoreConsole.Warning($"[FloV:MP] players.json не прочитан ({ex.Message}); файл отложен: {moved ?? "не удалось"}.");
        }
    }
}

/// <summary>MariaDB/MySQL: таблицы players и player_data (миграция 004).</summary>
public sealed class MySqlPlayerStore : IPlayerStore
{
    private readonly string _conn;
    private readonly string _world;

    public MySqlPlayerStore(string connectionString, string world)
    {
        _conn = connectionString;
        _world = world;
    }

    public string Describe => $"таблицы players и player_data MariaDB, мир «{_world}»";

    public IReadOnlyList<PersistedPlayer> LoadAllStates()
    {
        var list = new List<PersistedPlayer>();
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT `identity`, `name`, `pos_x`, `pos_y`, `pos_z`, `heading`, `dimension`, " +
                          "`health`, `armor`, `model`, `weapons` FROM `players` WHERE `world` = @w";
        cmd.Parameters.AddWithValue("@w", _world);
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new PersistedPlayer(r.GetString(0), r.GetString(1), r.GetFloat(2), r.GetFloat(3), r.GetFloat(4),
                r.GetFloat(5), r.GetInt32(6), r.GetInt32(7), r.GetInt32(8), r.GetUInt32(9),
                PlayerWeaponsJson.Parse(r.IsDBNull(10) ? null : r.GetString(10))));
        return list;
    }

    public void UpsertState(PersistedPlayer p)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText =
            "INSERT INTO `players` (`world`, `identity`, `name`, `pos_x`, `pos_y`, `pos_z`, `heading`, `dimension`, " +
            "  `health`, `armor`, `model`, `weapons`) " +
            "VALUES (@w, @id, @name, @x, @y, @z, @h, @dim, @hp, @ar, @model, @weapons) " +
            "ON DUPLICATE KEY UPDATE `name` = VALUES(`name`), `pos_x` = VALUES(`pos_x`), `pos_y` = VALUES(`pos_y`), " +
            "  `pos_z` = VALUES(`pos_z`), `heading` = VALUES(`heading`), `dimension` = VALUES(`dimension`), " +
            "  `health` = VALUES(`health`), `armor` = VALUES(`armor`), `model` = VALUES(`model`), `weapons` = VALUES(`weapons`)";
        cmd.Parameters.AddWithValue("@w", _world);
        cmd.Parameters.AddWithValue("@id", p.Identity);
        cmd.Parameters.AddWithValue("@name", p.Name);
        cmd.Parameters.AddWithValue("@x", p.X);
        cmd.Parameters.AddWithValue("@y", p.Y);
        cmd.Parameters.AddWithValue("@z", p.Z);
        cmd.Parameters.AddWithValue("@h", p.Heading);
        cmd.Parameters.AddWithValue("@dim", p.Dimension);
        cmd.Parameters.AddWithValue("@hp", p.Health);
        cmd.Parameters.AddWithValue("@ar", p.Armor);
        cmd.Parameters.AddWithValue("@model", p.Model);
        cmd.Parameters.AddWithValue("@weapons", PlayerWeaponsJson.Format(p.Weapons));
        cmd.ExecuteNonQuery();
    }

    public IReadOnlyDictionary<string, string> LoadData(string identity)
    {
        var data = new Dictionary<string, string>(StringComparer.Ordinal);
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT `key`, `value` FROM `player_data` WHERE `world` = @w AND `identity` = @id";
        cmd.Parameters.AddWithValue("@w", _world);
        cmd.Parameters.AddWithValue("@id", identity);
        using var r = cmd.ExecuteReader();
        while (r.Read()) data[r.GetString(0)] = r.GetString(1);
        return data;
    }

    public void SetData(string identity, string key, string? value)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        if (value is null)
        {
            cmd.CommandText = "DELETE FROM `player_data` WHERE `world` = @w AND `identity` = @id AND `key` = @k";
        }
        else
        {
            cmd.CommandText = "INSERT INTO `player_data` (`world`, `identity`, `key`, `value`) VALUES (@w, @id, @k, @v) " +
                              "ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)";
            cmd.Parameters.AddWithValue("@v", value);
        }
        cmd.Parameters.AddWithValue("@w", _world);
        cmd.Parameters.AddWithValue("@id", identity);
        cmd.Parameters.AddWithValue("@k", key);
        cmd.ExecuteNonQuery();
    }

    public void Flush() { }

    private MySqlConnection Open()
    {
        var c = new MySqlConnection(_conn);
        c.Open();
        return c;
    }
}

public static class PlayerWeaponsJson
{
    public static string Format(IReadOnlyList<SavedWeapon> weapons) => JsonSerializer.Serialize(weapons);

    public static IReadOnlyList<SavedWeapon> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<SavedWeapon>();
        try { return JsonSerializer.Deserialize<List<SavedWeapon>>(json) ?? new(); }
        catch (JsonException) { return Array.Empty<SavedWeapon>(); }
    }
}

public static class PlayerStoreFactory
{
    public static IPlayerStore Create(string? connectionString, string world, string jsonFallbackPath)
    {
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            try
            {
                var store = new MySqlPlayerStore(connectionString, world);
                store.LoadData("__probe__"); // таблицы есть, база отвечает
                return store;
            }
            catch (Exception ex)
            {
                CoreConsole.Warning($"[FloV:MP] [Игроки] MariaDB недоступна ({ex.Message}) — игроки сохраняются в файл {jsonFallbackPath}.");
            }
        }
        return new JsonPlayerStore(jsonFallbackPath);
    }
}

/// <summary>
/// Сохранение игроков: состояние — в памяти целиком (оно маленькое и нужно
/// в момент входа, ждать базу вход не должен), данные геймода — грузятся в
/// фоне при входе, готовность сообщается <see cref="PumpLoaded"/> в главном
/// потоке. Запись — в фоне, «последнее побеждает».
/// </summary>
public sealed class PlayerPersistence : IDisposable
{
    public const int MaxKeyLength = 64;
    public const int MaxValueLength = 65_536;
    public const int MaxKeysPerPlayer = 200;
    private static readonly Regex KeyPattern = new("^[A-Za-z0-9_.:-]{1,64}$", RegexOptions.Compiled);

    private readonly IPlayerStore _store;
    private readonly LatestWinsWriter<string> _writer;
    private readonly Dictionary<string, PersistedPlayer> _states = new();
    private readonly Dictionary<string, Dictionary<string, string>> _data = new();
    private readonly ConcurrentQueue<(string Identity, IReadOnlyDictionary<string, string>? Data, string? Error)> _loaded = new();
    // Чьи данные сейчас грузятся и чьи уже в памяти. Отдельно от _data: запись
    // геймода до окончания загрузки не должна выглядеть как «загружено».
    private readonly HashSet<string> _loading = new();
    private readonly HashSet<string> _ready = new();

    public PlayerPersistence(IPlayerStore store, Action<string> warn)
    {
        _store = store;
        _writer = new LatestWinsWriter<string>("flovmp-player-store", "[Игроки] данные игроков", warn);
    }

    public string Describe => _store.Describe;
    public int Pending => _writer.Pending;

    /// <summary>Поднять состояния всех игроков мира (при старте сервера).</summary>
    public int LoadStates()
    {
        _states.Clear();
        foreach (var p in _store.LoadAllStates()) _states[p.Identity] = p;
        return _states.Count;
    }

    public PersistedPlayer? SavedState(string identity) => _states.TryGetValue(identity, out var p) ? p : null;

    public void SaveState(PersistedPlayer p)
    {
        _states[p.Identity] = p;
        _writer.Enqueue("s:" + p.Identity, () => _store.UpsertState(p));
    }

    // --- данные геймода ---

    /// <summary>Начать фоновую загрузку данных игрока (вход). Итог — в <see cref="PumpLoaded"/>.</summary>
    public void BeginLoadData(string identity)
    {
        if (_ready.Contains(identity) || !_loading.Add(identity)) return;
        ThreadPool.QueueUserWorkItem(_ =>
        {
            try { _loaded.Enqueue((identity, _store.LoadData(identity), null)); }
            catch (Exception ex) { _loaded.Enqueue((identity, null, ex.Message)); }
        });
    }

    /// <summary>Главный поток: чьи данные загрузились (или не загрузились — с причиной).</summary>
    public List<(string Identity, string? Error)> PumpLoaded()
    {
        var done = new List<(string, string?)>();
        while (_loaded.TryDequeue(out var item))
        {
            // Игрок успел уйти, пока грузилось, — данные не нужны.
            if (!_loading.Remove(item.Identity)) continue;
            if (item.Data is not null)
            {
                // Пока грузилось, геймод мог уже что-то записать — его значения свежее.
                var merged = new Dictionary<string, string>(item.Data, StringComparer.Ordinal);
                if (_data.TryGetValue(item.Identity, out var early))
                    foreach (var (k, v) in early) merged[k] = v;
                _data[item.Identity] = merged;
                _ready.Add(item.Identity);
            }
            done.Add((item.Identity, item.Error));
        }
        return done;
    }

    public bool IsLoaded(string identity) => _ready.Contains(identity);

    public string? GetData(string identity, string key) =>
        _data.TryGetValue(identity, out var d) && d.TryGetValue(key, out var v) ? v : null;

    public IReadOnlyCollection<string> DataKeys(string identity) =>
        _data.TryGetValue(identity, out var d) ? d.Keys.ToList() : Array.Empty<string>();

    /// <summary>Записать (value null или пусто — удалить). null — записано, иначе причина отказа.</summary>
    public string? SetData(string identity, string key, string? value)
    {
        if (!KeyPattern.IsMatch(key ?? "")) return "ключ — латиница, цифры, _ . : -, до 64 символов";
        if (value is { Length: > MaxValueLength }) return $"значение длиннее {MaxValueLength} символов";
        if (!_data.TryGetValue(identity, out var d)) _data[identity] = d = new(StringComparer.Ordinal);
        var delete = string.IsNullOrEmpty(value);
        if (!delete && !d.ContainsKey(key!) && d.Count >= MaxKeysPerPlayer) return $"у игрока уже {MaxKeysPerPlayer} ключей";
        if (delete) d.Remove(key!);
        else d[key!] = value!;
        var k = key!;
        var v = delete ? null : value;
        _writer.Enqueue("d:" + identity + "\n" + k, () => _store.SetData(identity, k, v));
        return null;
    }

    /// <summary>Игрок ушёл: его данные в памяти больше не нужны (в хранилище они остаются).</summary>
    public void Unload(string identity)
    {
        _data.Remove(identity);
        _loading.Remove(identity);
        _ready.Remove(identity);
    }

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
