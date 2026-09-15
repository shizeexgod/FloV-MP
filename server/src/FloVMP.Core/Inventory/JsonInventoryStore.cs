using System.Text.Json;
using FloVMP.Core;

namespace FloVMP.Core.Items;

/// <summary>Персист инвентарей по accountId. JSON-файл, потокобезопасно.</summary>
public interface IInventoryStore
{
    Inventory Load(int accountId);
    void Save(int accountId, Inventory inv);

    /// <summary>
    /// Немедленно записать накопленные изменения. Нужен потому, что Save()
    /// у файловой реализации только помечает состояние грязным: полная
    /// перезапись файла на каждое сохранение давала O(n^2) в игровом тике.
    /// </summary>
    void Flush();
}

/// <summary>
/// Файловое хранилище инвентарей.
///
/// ПРОИЗВОДИТЕЛЬНОСТЬ — почему здесь отложенная запись.
/// Раньше каждый Save() сериализовал СЛОВАРЬ ЦЕЛИКОМ (инвентари всех
/// аккаунтов) и переписывал весь файл. А автосейв вызывает SaveAll(), то есть
/// Save() на каждого игрока онлайн — и получает N полных перезаписей файла,
/// в котором лежат все N инвентарей. Это O(n²) прямо в игровом тике: при
/// тысяче онлайна — тысяча сериализаций тысячи инвентарей подряд.
///
/// Симптом на боевом сервере выглядел так:
///   [Warning] resourceManager.Update() took: 240988 ms
/// — сервер замер на четыре минуты. Подбор предмета (TryGiveItem) тоже звал
/// Save() и тоже переписывал весь файл.
///
/// Теперь Save() только обновляет запись в памяти и помечает состояние
/// грязным; на диск пишет фоновый таймер, один раз за интервал, независимо от
/// числа изменений. Тик не блокируется, а окно потери закрывается явным
/// Flush() на автосейве и Dispose() на остановке сервера.
/// </summary>
public sealed class JsonInventoryStore : IInventoryStore, IDisposable
{
    private sealed class Record
    {
        public int SlotCount { get; set; } = 24;
        public double MaxWeight { get; set; } = 40.0;
        public List<ItemStack?> Slots { get; set; } = new();
    }

    private readonly string _path;
    private readonly object _lock = new();
    private Dictionary<int, Record> _data = new();

    private readonly System.Threading.Timer _flushTimer;
    private volatile bool _dirty;
    private volatile bool _disposed;
    private const int FlushIntervalMs = 1000;

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public JsonInventoryStore(string path)
    {
        _path = path;
        Load();
        _flushTimer = new System.Threading.Timer(_ => Flush(), null, FlushIntervalMs, FlushIntervalMs);
    }

    public Inventory Load(int accountId)
    {
        lock (_lock)
        {
            if (!_data.TryGetValue(accountId, out var rec))
                return new Inventory();

            var inv = new Inventory(rec.SlotCount, rec.MaxWeight);
            inv.LoadSnapshot(rec.Slots);
            return inv;
        }
    }

    public void Save(int accountId, Inventory inv)
    {
        lock (_lock)
        {
            _data[accountId] = new Record
            {
                SlotCount = inv.SlotCount,
                MaxWeight = inv.MaxWeight,
                Slots = inv.Snapshot().Select(s => s is null ? null : s).ToList(),
            };
            // Файл перепишет фоновый таймер — здесь только отметка.
            _dirty = true;
        }
    }

    /// <summary>
    /// Немедленно записать накопленные изменения. Вызывается на автосейве и
    /// при остановке сервера — там задержка допустима, а потеря данных нет.
    /// </summary>
    public void Flush()
    {
        if (!_dirty) return;
        lock (_lock)
        {
            if (!_dirty) return;
            _dirty = false;
            try
            {
                Persist();
            }
            catch (Exception ex)
            {
                _dirty = true; // не теряем изменения — повторим на следующем тике таймера
                CoreConsole.Warning($"[FloV:MP] inventories.json: ошибка записи: {ex.Message}");
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _flushTimer.Dispose(); } catch { }
        Flush(); // на остановке сервера инвентари обязаны попасть на диск
    }

    private void Load()
    {
        if (!File.Exists(_path)) return;
        try
        {
            _data = JsonSerializer.Deserialize<Dictionary<int, Record>>(File.ReadAllText(_path)) ?? new();
        }
        catch (Exception ex)
        {
            var quarantined = StoreFiles.QuarantineCorrupt(_path);
            CoreConsole.Warning(
                $"[FloV:MP] inventories.json повреждён ({ex.Message}); карантин: {quarantined ?? "не удалось"}");
            _data = new();
        }
    }

    /// <summary>Запись на диск. Вызывать только под взятым _lock.</summary>
    private void Persist()
    {
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(_data, JsonOpts));
        if (File.Exists(_path)) File.Replace(tmp, _path, null);
        else File.Move(tmp, _path);
    }
}
