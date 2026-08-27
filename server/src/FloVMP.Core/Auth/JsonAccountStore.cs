using System.Text.Json;

namespace FloVMP.Core.Auth;

/// <summary>
/// Потокобезопасное JSON-файловое хранилище учёток. Для скелета Фазы 3.
/// Запись durable: во временный файл + атомарная замена.
/// </summary>
public sealed class JsonAccountStore : IAccountStore
{
    private readonly string _path;
    private readonly object _lock = new();
    private readonly Dictionary<string, Account> _byName = new(StringComparer.OrdinalIgnoreCase);
    private int _nextId = 1;

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public JsonAccountStore(string path)
    {
        _path = path;
        Load();
    }

    public Account? FindByUsername(string username)
    {
        lock (_lock)
        {
            return _byName.TryGetValue(username, out var a) ? Clone(a) : null;
        }
    }

    public bool Exists(string username)
    {
        lock (_lock) { return _byName.ContainsKey(username); }
    }

    public Account Create(string username, string passwordHash)
    {
        lock (_lock)
        {
            if (_byName.ContainsKey(username))
                throw new InvalidOperationException($"имя занято: {username}");

            var acc = new Account
            {
                Id = _nextId++,
                Username = username,
                PasswordHash = passwordHash,
                CreatedUtc = DateTime.UtcNow.ToString("O"),
            };
            _byName[username] = acc;
            Save();
            return Clone(acc);
        }
    }

    public void Update(Account account)
    {
        lock (_lock)
        {
            if (!_byName.ContainsKey(account.Username))
                throw new InvalidOperationException($"нет учётки: {account.Username}");
            _byName[account.Username] = Clone(account);
            Save();
        }
    }

    private void Load()
    {
        if (!File.Exists(_path)) return;
        try
        {
            var list = JsonSerializer.Deserialize<List<Account>>(File.ReadAllText(_path)) ?? new();
            foreach (var a in list)
            {
                _byName[a.Username] = a;
                _nextId = Math.Max(_nextId, a.Id + 1);
            }
        }
        catch
        {
            // битый файл — стартуем пустыми, не роняем сервер
        }
    }

    private void Save()
    {
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(_byName.Values.OrderBy(a => a.Id), JsonOpts);
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, json);
        if (File.Exists(_path)) File.Replace(tmp, _path, null);
        else File.Move(tmp, _path);
    }

    private static Account Clone(Account a) => new()
    {
        Id = a.Id,
        Username = a.Username,
        PasswordHash = a.PasswordHash,
        CreatedUtc = a.CreatedUtc,
        LastLoginUtc = a.LastLoginUtc,
    };
}
