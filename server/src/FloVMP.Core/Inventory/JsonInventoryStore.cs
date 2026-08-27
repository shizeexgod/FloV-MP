using System.Text.Json;

namespace FloVMP.Core.Items;

/// <summary>Персист инвентарей по accountId. JSON-файл, потокобезопасно.</summary>
public interface IInventoryStore
{
    Inventory Load(int accountId);
    void Save(int accountId, Inventory inv);
}

public sealed class JsonInventoryStore : IInventoryStore
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

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public JsonInventoryStore(string path)
    {
        _path = path;
        Load();
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
            Persist();
        }
    }

    private void Load()
    {
        if (!File.Exists(_path)) return;
        try
        {
            _data = JsonSerializer.Deserialize<Dictionary<int, Record>>(File.ReadAllText(_path)) ?? new();
        }
        catch
        {
            _data = new();
        }
    }

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
