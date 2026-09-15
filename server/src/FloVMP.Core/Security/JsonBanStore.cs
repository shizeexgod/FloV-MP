using System.Text.Json;
using FloVMP.Core;

namespace FloVMP.Core.Security;

/// <summary>
/// Файловое хранилище блокировок — резервный путь, когда MariaDB не настроена.
///
/// Запись отложенная, как и у хранилища аккаунтов: бан выдаётся из обработчика
/// команды, то есть с игрового потока, и синхронная перезапись файла на каждый
/// бан тормозила бы тик. Окно потери — секунда, и оно закрывается немедленным
/// сбросом в Flush()/Dispose().
///
/// ВАЖНО для нескольких инстансов: файл лежит локально, поэтому баны НЕ
/// расходятся между серверами. Горизонталь требует MySqlBanStore — там общая
/// таблица и периодическая дозагрузка чужих банов.
/// </summary>
public sealed class JsonBanStore : IBanStore, IDisposable
{
    private readonly string _path;
    private readonly object _lock = new();
    private readonly Dictionary<string, BanRecord> _records = new(StringComparer.Ordinal);
    private readonly System.Threading.Timer _timer;
    private volatile bool _dirty;
    private volatile bool _disposed;
    private const int FlushIntervalMs = 1000;

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public JsonBanStore(string path)
    {
        _path = path;
        Load();
        _timer = new System.Threading.Timer(_ => Flush(), null, FlushIntervalMs, FlushIntervalMs);
    }

    public IReadOnlyList<BanRecord> LoadAll()
    {
        lock (_lock) return _records.Values.ToList();
    }

    public void Upsert(BanRecord record)
    {
        if (record is null || string.IsNullOrWhiteSpace(record.Id)) return;
        lock (_lock)
        {
            _records[record.Id] = record;
            _dirty = true;
        }
    }

    public void Flush()
    {
        if (!_dirty) return;
        lock (_lock)
        {
            if (!_dirty) return;
            _dirty = false;
            try
            {
                var dir = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                var json = JsonSerializer.Serialize(_records.Values.OrderBy(r => r.BannedAtUtc), JsonOpts);
                var tmp = _path + ".tmp";
                File.WriteAllText(tmp, json);
                if (File.Exists(_path)) File.Replace(tmp, _path, null);
                else File.Move(tmp, _path);
            }
            catch (Exception ex)
            {
                _dirty = true; // не теряем: повторим на следующем тике таймера
                Console.Error.WriteLine($"[FloV:MP] bans.json: ошибка записи: {ex.Message}");
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _timer.Dispose(); } catch { }
        Flush(); // на остановке сервера баны обязаны попасть на диск
    }

    private void Load()
    {
        if (!File.Exists(_path)) return;
        try
        {
            var list = JsonSerializer.Deserialize<List<BanRecord>>(File.ReadAllText(_path)) ?? new();
            foreach (var r in list)
                if (!string.IsNullOrWhiteSpace(r.Id)) _records[r.Id] = r;
        }
        catch (Exception ex)
        {
            // Битый файл банов не должен мешать серверу подняться, но и тихо
            // разбаниваться нельзя: файл уходит в карантин с явным сообщением.
            var quarantined = StoreFiles.QuarantineCorrupt(_path);
            Console.Error.WriteLine(
                $"[FloV:MP] ВНИМАНИЕ: bans.json повреждён ({ex.Message}); карантин: {quarantined ?? "не удалось"}. " +
                "Действующие блокировки сброшены — проверьте список банов.");
            _records.Clear();
        }
    }
}
