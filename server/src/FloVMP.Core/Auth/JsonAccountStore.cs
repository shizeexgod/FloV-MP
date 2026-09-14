using System.Text.Json;

namespace FloVMP.Core.Auth;

/// <summary>
/// Потокобезопасное JSON-файловое хранилище учёток. Для скелета Фазы 3.
/// Запись durable: во временный файл + атомарная замена.
/// </summary>
public sealed class JsonAccountStore : IAccountStore, IDisposable
{
    private readonly string _path;
    private readonly string _journalPath;
    private readonly object _lock = new();
    private readonly Dictionary<string, Account> _byName = new(StringComparer.OrdinalIgnoreCase);

    // Индекс по номеру счёта. Без него FindByBankAccount делал линейный перебор
    // всех аккаунтов на КАЖДЫЙ перевод денег, прямо в игровом потоке: на
    // десятках тысяч учёток это заметный фриз на ровном месте.
    private readonly Dictionary<string, string> _bankToName = new(StringComparer.OrdinalIgnoreCase);

    private int _nextId = 1;

    // ПРОИЗВОДИТЕЛЬНОСТЬ: раньше Save() вызывался синхронно на КАЖДОЕ изменение
    // и сериализовал ВЕСЬ список аккаунтов + переписывал файл целиком. Это шло
    // из обработчиков alt:V, т.е. на главном (игровом) потоке: при тысячах
    // аккаунтов каждая операция = многомегабайтная запись и фриз тика.
    // Теперь изменение только помечает состояние «грязным», а фактическая
    // запись идёт в фоне с дебаунсом. Тик не блокируется.
    private readonly System.Threading.Timer _flushTimer;
    private volatile bool _dirty;
    private volatile bool _disposed;
    private const int FlushIntervalMs = 1000; // маленькое окно потери при аварии

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public JsonAccountStore(string path)
    {
        _path = path;
        _journalPath = path + ".journal";
        Load();
        _flushTimer = new System.Threading.Timer(_ => FlushIfDirty(), null, FlushIntervalMs, FlushIntervalMs);
    }

    /// <summary>Немедленно записать на диск, если есть несохранённые изменения.</summary>
    public void Flush() => FlushIfDirty();

    private void FlushIfDirty()
    {
        if (!_dirty) return;
        lock (_lock) { FlushToDiskLocked(); }
    }

    /// <summary>Записать на диск. Вызывать только под взятым _lock.</summary>
    private void FlushToDiskLocked()
    {
        if (!_dirty) return;
        _dirty = false;
        try { SaveToDisk(); }
        catch (Exception ex)
        {
            _dirty = true; // не теряем изменения — повторим на следующем тике
            Console.Error.WriteLine($"[FloV:MP] accounts.json: ошибка записи: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _flushTimer.Dispose(); } catch { }
        FlushIfDirty(); // на остановке сервера данные обязаны попасть на диск
    }

    public Account? FindByUsername(string username)
    {
        lock (_lock)
        {
            return _byName.TryGetValue(username, out var a) ? Clone(a) : null;
        }
    }

    public Account? FindByBankAccount(string bankAccountNumber)
    {
        if (string.IsNullOrWhiteSpace(bankAccountNumber)) return null;
        var trimmed = bankAccountNumber.Trim();
        lock (_lock)
        {
            if (!_bankToName.TryGetValue(trimmed, out var name)) return null;
            return _byName.TryGetValue(name, out var acc) ? Clone(acc) : null;
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
                Cash = Account.StartingCash,
                Bank = Account.StartingBank,
                BankAccountNumber = $"40817810{_nextId:D8}",
            };
            _byName[username] = acc;
            Index(acc);

            // Терять только что зарегистрированный аккаунт нельзя, поэтому
            // запись немедленная — но НЕ переписыванием всего файла.
            // Полный сброс стоит O(числа аккаунтов): нагрузочный стенд намерял
            // 9 мс на регистрацию уже при 1500 учётках, а на десятках тысяч это
            // сотни миллисекунд. Вместо этого дописываем одну строку в журнал
            // (O(1)), а полный файл перезаписывает фоновый флаш.
            AppendToJournalLocked(acc);
            _dirty = true;
            return Clone(acc);
        }
    }

    public void Update(Account account)
    {
        lock (_lock)
        {
            if (!_byName.TryGetValue(account.Username, out var existing))
                throw new InvalidOperationException($"нет учётки: {account.Username}");

            // Номер счёта может смениться (выдача/перевыпуск) — старый ключ
            // обязан уйти из индекса, иначе перевод продолжит находить учётку
            // по номеру, которого у неё уже нет.
            if (!string.IsNullOrWhiteSpace(existing.BankAccountNumber))
                _bankToName.Remove(existing.BankAccountNumber.Trim());

            var clone = Clone(account);
            _byName[account.Username] = clone;
            Index(clone);
            Save();
        }
    }

    private void Load()
    {
        if (File.Exists(_path))
        {
        try
        {
            var list = JsonSerializer.Deserialize<List<Account>>(File.ReadAllText(_path)) ?? new();
            foreach (var a in list)
            {
                _byName[a.Username] = a;
                Index(a);
                _nextId = Math.Max(_nextId, a.Id + 1);
            }
        }
        catch (Exception ex)
        {
            // битый файл — не роняем сервер: отодвигаем в карантин, стартуем пустыми
            var quarantined = StoreFiles.QuarantineCorrupt(_path);
            Console.Error.WriteLine(
                $"[FloV:MP] accounts.json повреждён ({ex.Message}); карантин: {quarantined ?? "не удалось"}");
            _byName.Clear();
            _bankToName.Clear();
        }
        }

        LoadJournal();
    }

    /// <summary>
    /// Догружает аккаунты, зарегистрированные после последнего полного сброса.
    ///
    /// ВАЖНО: запись из журнала применяется ТОЛЬКО если такого имени ещё нет.
    /// Порядок сброса — сначала полный файл, потом удаление журнала; если
    /// сервер умрёт между этими шагами, журнал переживёт файл. Перезапись им
    /// уже сохранённой учётки откатила бы её к состоянию на момент регистрации
    /// (деньги, уровень админа). Добавление недостающих — безопасно.
    /// </summary>
    private void LoadJournal()
    {
        if (!File.Exists(_journalPath)) return;

        var restored = 0;
        try
        {
            foreach (var line in File.ReadAllLines(_journalPath))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                Account? acc;
                try { acc = JsonSerializer.Deserialize<Account>(line); }
                catch { continue; } // строка, оборванная аварией — пропускаем

                if (acc is null || string.IsNullOrWhiteSpace(acc.Username)) continue;
                if (_byName.ContainsKey(acc.Username)) continue;

                _byName[acc.Username] = acc;
                Index(acc);
                _nextId = Math.Max(_nextId, acc.Id + 1);
                restored++;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[FloV:MP] accounts.json.journal: ошибка чтения: {ex.Message}");
        }

        if (restored > 0)
        {
            Console.WriteLine($"[FloV:MP] Восстановлено из журнала регистраций: {restored}");
            _dirty = true; // вернём их в основной файл при первом же сбросе
        }
    }

    /// <summary>Дописать одну учётку в журнал. Вызывать только под взятым _lock.</summary>
    private void AppendToJournalLocked(Account acc)
    {
        try
        {
            var dir = Path.GetDirectoryName(_journalPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.AppendAllText(_journalPath, JsonSerializer.Serialize(acc) + "\n");
        }
        catch (Exception ex)
        {
            // Журнал — страховка, а не источник истины: учётка уже в памяти и
            // уйдёт на диск фоновым сбросом. Регистрацию из-за этого не рушим.
            Console.Error.WriteLine($"[FloV:MP] accounts.json.journal: ошибка записи: {ex.Message}");
        }
    }

    /// <summary>Поддержание индекса по номеру банковского счёта.</summary>
    private void Index(Account acc)
    {
        if (!string.IsNullOrWhiteSpace(acc.BankAccountNumber))
            _bankToName[acc.BankAccountNumber.Trim()] = acc.Username;
    }

    /// <summary>Пометить, что состояние изменилось. Запись выполнит фоновый флаш.</summary>
    private void Save() => _dirty = true;

    private void SaveToDisk()
    {
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(_byName.Values.OrderBy(a => a.Id), JsonOpts);
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, json);
        if (File.Exists(_path)) File.Replace(tmp, _path, null);
        else File.Move(tmp, _path);

        // Журнал обнуляется ТОЛЬКО после того, как полный файл лёг на диск:
        // всё, что в нём было, теперь есть в основном файле. Обратный порядок
        // означал бы окно, в котором регистрации нет ни там, ни там.
        try
        {
            if (File.Exists(_journalPath)) File.Delete(_journalPath);
        }
        catch (Exception ex)
        {
            // Не страшно: повторное чтение журнала добавит только недостающие
            // учётки и ничего не перезапишет.
            Console.Error.WriteLine($"[FloV:MP] accounts.json.journal: не удалось очистить: {ex.Message}");
        }
    }

    private static Account Clone(Account a) => new()
    {
        Id = a.Id,
        Username = a.Username,
        PasswordHash = a.PasswordHash,
        CreatedUtc = a.CreatedUtc,
        LastLoginUtc = a.LastLoginUtc,
        Cash = a.Cash,
        Bank = a.Bank,
        BankAccountNumber = a.BankAccountNumber,
        Email = a.Email,
        TotpSecret = a.TotpSecret,
        TwoFaEnabled = a.TwoFaEnabled,
        AdminLevel = a.AdminLevel,
        IsBanned = a.IsBanned,
        BanReason = a.BanReason,
        BanUntilUtc = a.BanUntilUtc,
        MuteUntilUtc = a.MuteUntilUtc,
    };
}
