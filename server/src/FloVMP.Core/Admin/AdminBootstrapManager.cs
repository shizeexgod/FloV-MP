using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FloVMP.Core.Admin;

/// <summary>
/// Структура файла конфигурации администраторов (config/admins.json).
/// Обеспечивает персистентность прав без базы данных.
/// </summary>
public class AdminConfigFile
{
    [JsonPropertyName("founders")]
    public List<string> Founders { get; set; } = new();

    [JsonPropertyName("admins")]
    public Dictionary<string, int> Admins { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("autoClaimFirstPlayer")]
    // ВАЖНО: по умолчанию ВЫКЛЮЧЕНО. При true первый подключившийся игрок
    // автоматически становился Основателем (8) без токена — на публичном
    // запуске это значит, что права заберёт случайный человек. Безопасный
    // путь первичной настройки: токен из консоли сервера (/claimowner <токен>)
    // либо setadmin/setfounder из консоли.
    public bool AutoClaimFirstPlayer { get; set; } = false;

    /// <summary>
    /// Разрешать ли выдачу прав по НИКУ игрока. По умолчанию ВЫКЛЮЧЕНО:
    /// ник в alt:V задаётся клиентом (altv.toml), поэтому любой желающий может
    /// взять ник администратора и получить его уровень. Безопасная привязка —
    /// по SocialClubId. Включать только осознанно (напр. локальная отладка).
    /// </summary>
    public bool AllowNameBasedAdmin { get; set; } = false;

    [JsonPropertyName("setupToken")]
    public string SetupToken { get; set; } = "";
}

/// <summary>
/// Сервис управления администраторами на чистом сервере без базы данных (Pre-DB Architecture):
/// - Читает и автоматически сохраняет config/admins.json при любых изменениях.
/// - Генерирует одноразовый криптографический токен настройки (setupToken) при первом запуске.
/// - Поддерживает автоматическое присвоение прав Основателя (8) первому подключившемуся игроку.
/// - Поддерживает назначение прав из серверной консоли (setadmin / setfounder).
/// </summary>
public class AdminBootstrapManager
{
    private readonly string _configFilePath;
    private readonly object _lock = new();
    private AdminConfigFile _config = new();

    // --- Защита токена владельца от перебора -------------------------------
    // Токен даёт уровень 8 — полный контроль над сервером. Подсказка о команде
    // /claimowner показывается каждому, кто зашёл на свежий сервер, то есть
    // перебор токена открыто предлагается любому посетителю.
    //
    // Блокировка ОБЩАЯ на сервер, а не на игрока: лимит на одно подключение
    // обходится сотней подключений, перебирающих параллельно. После серии
    // неудач приём токена закрывается на время у всех сразу.
    public const int ClaimMaxFailures = 10;
    public static readonly TimeSpan ClaimLockout = TimeSpan.FromMinutes(10);

    private int _claimFailures;
    private DateTime _claimLockedUntilUtc = DateTime.MinValue;
    private readonly Func<DateTime> _clock;

    // Хранилище прав в базе. Необязательное: без него всё работает как раньше,
    // по config/admins.json. С ним база — источник правды для прав по
    // SocialClubId, а память — быстрый кэш для проверки при входе игрока.
    private IAdminStore? _store;

    // Владелец из FLOVMP_OWNER_SC. Задан окружением сервера, поэтому действует
    // всегда: сверка с базой не должна отбирать у него права, даже если в
    // таблице admins его нет (установщик выдаёт права именно так).
    private string? _envOwnerSc;

    /// <summary>Подключено ли хранилище прав в базе.</summary>
    public bool HasStore { get { lock (_lock) return _store != null; } }

    /// <summary>Сколько неудачных попыток накоплено к текущему моменту.</summary>
    public int ClaimFailures { get { lock (_lock) return _claimFailures; } }

    /// <summary>Закрыт ли сейчас приём токена владельца.</summary>
    public bool IsClaimLocked { get { lock (_lock) return _clock() < _claimLockedUntilUtc; } }

    public AdminBootstrapManager(string? configFilePath = null)
        : this(configFilePath, () => DateTime.UtcNow)
    {
    }

    /// <summary>Конструктор с подменяемыми часами — для проверки блокировки в тестах.</summary>
    public AdminBootstrapManager(string? configFilePath, Func<DateTime> clock)
    {
        _clock = clock ?? (() => DateTime.UtcNow);
        _configFilePath = configFilePath ?? ResolveDefaultConfigPath();
        LoadOrCreate();
    }

    public string ConfigFilePath => _configFilePath;

    public string CurrentSetupToken
    {
        get
        {
            lock (_lock) return _config.SetupToken;
        }
    }

    public bool CanAutoClaim
    {
        get
        {
            lock (_lock) return _config.AutoClaimFirstPlayer && _config.Admins.Count == 0 && _config.Founders.Count == 0;
        }
    }

    public static string ResolveDefaultConfigPath()
    {
        var cwd = Directory.GetCurrentDirectory();

        var candidates = new[]
        {
            Path.Combine(cwd, "config", "admins.json"),
            Path.Combine(cwd, "flovmp-data", "admins.json"),
            Path.Combine(cwd, "admins.json")
        };

        foreach (var c in candidates)
        {
            if (File.Exists(c)) return c;
        }

        // По умолчанию создаём в config/admins.json
        var defaultDir = Path.Combine(cwd, "config");
        if (!Directory.Exists(defaultDir))
        {
            try { Directory.CreateDirectory(defaultDir); } catch { }
        }
        return Path.Combine(defaultDir, "admins.json");
    }

    public void LoadOrCreate()
    {
        lock (_lock)
        {
            try
            {
                if (File.Exists(_configFilePath))
                {
                    var json = File.ReadAllText(_configFilePath);
                    var loaded = JsonSerializer.Deserialize<AdminConfigFile>(json);
                    if (loaded != null)
                    {
                        _config = loaded;
                        _config.Founders ??= new List<string>();
                        _config.Admins = new Dictionary<string, int>(_config.Admins ?? new(), StringComparer.OrdinalIgnoreCase);
                    }
                }
                else
                {
                    _config = new AdminConfigFile();
                }
            }
            catch
            {
                _config = new AdminConfigFile();
            }

            // Владелец из окружения — только по SocialClubId: ник задаёт сам
            // игрок, и прежний FLOVMP_OWNER_NAME лишь засорял admins.json записью,
            // которая при выключенных правах по нику ничего не давала.
            var envOwnerSc = Environment.GetEnvironmentVariable("FLOVMP_OWNER_SC");
            _envOwnerSc = !string.IsNullOrWhiteSpace(envOwnerSc) && IsSocialClubKey(envOwnerSc.Trim())
                ? envOwnerSc.Trim()
                : null;
            if (!string.IsNullOrWhiteSpace(envOwnerSc))
            {
                _config.Admins[envOwnerSc.Trim()] = 8;
                if (!_config.Founders.Contains(envOwnerSc.Trim()))
                    _config.Founders.Add(envOwnerSc.Trim());
            }

            // Диагностика: записи, привязанные к НИКУ, больше не дают прав
            // (AllowNameBasedAdmin=false), т.к. ник подделывается клиентом.
            // Громко предупреждаем, чтобы владелец не остался без доступа.
            if (!_config.AllowNameBasedAdmin)
            {
                var nameKeyed = new List<string>();
                foreach (var f in _config.Founders)
                    if (!ulong.TryParse(f, out _)) nameKeyed.Add($"founder:{f}");
                foreach (var kv in _config.Admins)
                    if (!ulong.TryParse(kv.Key, out _)) nameKeyed.Add($"admin:{kv.Key}({kv.Value})");

                if (nameKeyed.Count > 0)
                {
                    CoreConsole.Write("========================================================");
                    CoreConsole.Write("[FloV:MP Security] ВНИМАНИЕ: права по НИКУ отключены.");
                    CoreConsole.Write("  Ник в alt:V задаётся клиентом — любой мог взять ник");
                    CoreConsole.Write("  админа и получить его уровень. Эти записи НЕ действуют:");
                    foreach (var n in nameKeyed) CoreConsole.Write($"    - {n}");
                    CoreConsole.Write("  Как восстановить доступ (любой способ):");
                    CoreConsole.Write("    1) В консоли сервера:  setadmin <SocialClubId> 8");
                    CoreConsole.Write("    2) В игре:             /claimowner <токен из консоли>");
                    CoreConsole.Write("    3) Переменная окружения FLOVMP_OWNER_SC=<SocialClubId>");
                    CoreConsole.Write("  (Осознанно вернуть привязку по нику: AllowNameBasedAdmin=true");
                    CoreConsole.Write("   в config/admins.json — НЕ рекомендуется на публичном сервере.)");
                    CoreConsole.Write("========================================================");
                }
            }

            // Если список пуст и токена нет — сгенерировать токен настройки
            if (_config.Admins.Count == 0 && _config.Founders.Count == 0 && string.IsNullOrWhiteSpace(_config.SetupToken))
            {
                var envToken = Environment.GetEnvironmentVariable("FLOVMP_SETUP_TOKEN");
                _config.SetupToken = !string.IsNullOrWhiteSpace(envToken) ? envToken.Trim() : GenerateSetupToken();
            }

            SaveInternal();
        }
    }

    /// <summary>
    /// Перечитать права. С подключённой базой — заново из неё: так владелец,
    /// поправивший таблицу admins руками, получает права сразу, командой
    /// reloadadmins в консоли, не дожидаясь фоновой синхронизации.
    /// </summary>
    public void Reload()
    {
        LoadOrCreate();
        RefreshFromStore();
    }

    /// <summary>
    /// Подключить хранилище прав в базе.
    ///
    /// При первом подключении права из admins.json переносятся в базу, если в
    /// ней ещё пусто. Без этого обновление сервера до версии с базой молча
    /// отобрало бы права у всех существующих администраторов.
    /// </summary>
    public void AttachStore(IAdminStore store)
    {
        if (store is null) throw new ArgumentNullException(nameof(store));

        IReadOnlyList<AdminRecord> existing;
        try
        {
            existing = store.LoadAll();
        }
        catch (Exception ex)
        {
            // База недоступна — остаёмся на admins.json и честно об этом говорим.
            CoreConsole.Warning($"[FloV:MP Admin] Права в базе недоступны ({ex.Message}). " +
                                "Используется config/admins.json.");
            return;
        }

        // Что переносить — снимаем под блокировкой, а пишем в базу без неё.
        List<(string Key, int Level, bool Founder)> toImport;
        lock (_lock)
        {
            _store = store;
            toImport = existing.Count == 0
                ? _config.Admins
                    .Where(kv => kv.Value > 0 && IsSocialClubKey(kv.Key))
                    .Select(kv => (kv.Key, kv.Value, _config.Founders.Contains(kv.Key)))
                    .ToList()
                : new List<(string, int, bool)>();
        }

        var imported = 0;
        foreach (var (key, level, founder) in toImport)
        {
            try
            {
                store.Upsert(key, level, founder, "import:admins.json");
                imported++;
            }
            catch (Exception ex)
            {
                CoreConsole.Warning($"[FloV:MP Admin] Не удалось перенести права {key} в базу: {ex.Message}");
            }
        }

        if (imported > 0)
            CoreConsole.Write($"[FloV:MP Admin] Перенесено в базу администраторов из admins.json: {imported}");

        RefreshFromStore();
        CoreConsole.Write("[FloV:MP Admin] Права администраторов берутся из базы (таблица admins).");
    }

    /// <summary>
    /// Подтянуть права из базы. Возвращает число администраторов по
    /// SocialClubId после синхронизации, либо -1, если хранилища нет или база
    /// недоступна (тогда память остаётся как была).
    ///
    /// Для ключей SocialClubId база авторитетна: снятие прав в базе снимает их
    /// и на сервере. Записи по нику (они и так отключены по умолчанию) живут
    /// только в admins.json и здесь не трогаются.
    /// </summary>
    public int RefreshFromStore()
    {
        IAdminStore? store;
        lock (_lock) store = _store;
        if (store is null) return -1;

        IReadOnlyList<AdminRecord> records;
        try
        {
            records = store.LoadAll();
        }
        catch (Exception ex)
        {
            // Сбой чтения не должен отбирать права у всех: память не трогаем.
            CoreConsole.Warning($"[FloV:MP Admin] Синхронизация прав с базой не удалась: {ex.Message}");
            return -1;
        }

        lock (_lock)
        {
            foreach (var key in _config.Admins.Keys.Where(IsSocialClubKey).ToList())
                _config.Admins.Remove(key);
            _config.Founders.RemoveAll(IsSocialClubKey);

            foreach (var r in records)
            {
                if (r.Level <= 0 || !IsSocialClubKey(r.SocialClub)) continue;
                _config.Admins[r.SocialClub] = Math.Clamp(r.Level, 1, 8);
                if ((r.IsFounder || r.Level == 8) && !_config.Founders.Contains(r.SocialClub))
                    _config.Founders.Add(r.SocialClub);
            }

            if (_envOwnerSc != null)
            {
                _config.Admins[_envOwnerSc] = 8;
                if (!_config.Founders.Contains(_envOwnerSc)) _config.Founders.Add(_envOwnerSc);
            }

            // Появился хотя бы один администратор — авто-выдача первому
            // зашедшему больше неуместна.
            if (_config.Admins.Count > 0) _config.AutoClaimFirstPlayer = false;

            return _config.Admins.Keys.Count(IsSocialClubKey);
        }
    }

    /// <summary>Ключ прав — SocialClubId (только цифры), а не ник.</summary>
    private static bool IsSocialClubKey(string key) =>
        !string.IsNullOrEmpty(key) && key.All(char.IsDigit);

    /// <summary>Записать права в базу, если она подключена. Ошибка не роняет вызов.</summary>
    private void PersistToStore(string socialClub, int level, bool isFounder, string? grantedBy)
    {
        IAdminStore? store;
        lock (_lock) store = _store;
        if (store is null || !IsSocialClubKey(socialClub)) return;
        try
        {
            store.Upsert(socialClub, level, isFounder, grantedBy);
        }
        catch (Exception ex)
        {
            // Права уже действуют в памяти этого сервера. Администратор должен
            // знать, что после перезапуска они пропадут.
            CoreConsole.Warning($"[FloV:MP Admin] ВНИМАНИЕ: права {socialClub} не сохранены в базу " +
                                $"({ex.Message}). Они действуют до перезапуска сервера.");
        }
    }

    public int GetAssignedRank(ulong socialClubId, string playerName, string? ip = null)
    {
        // ВАЖНО: раньше любой игрок с localhost-IP безусловно получал 8 уровень.
        // За nginx/прокси/NAT реальный IP схлопывается в 127.0.0.1 — и права
        // Основателя получали ВСЕ подключившиеся. Теперь этот путь выключен по
        // умолчанию и включается только явным FLOVMP_ALLOW_LOCAL_OWNER=1
        // (для локальной отладки на своей машине, не для прода).
        if (!string.IsNullOrEmpty(ip) && (ip == "127.0.0.1" || ip == "::1" || ip == "localhost")
            && string.Equals(Environment.GetEnvironmentVariable("FLOVMP_ALLOW_LOCAL_OWNER"), "1", StringComparison.Ordinal))
            return 8;

        lock (_lock)
        {
            var scStr = socialClubId > 0 ? socialClubId.ToString() : null;

            // Привязка по SocialClubId — основная и безопасная.
            if (scStr != null && _config.Founders.Contains(scStr)) return 8;
            if (scStr != null && _config.Admins.TryGetValue(scStr, out var scRank) && scRank > 0)
                return scRank;

            // Привязка по НИКУ — только если явно разрешена (ник подделывается
            // клиентом, см. AllowNameBasedAdmin).
            if (_config.AllowNameBasedAdmin && !string.IsNullOrEmpty(playerName))
            {
                if (_config.Founders.Contains(playerName)) return 8;
                if (_config.Admins.TryGetValue(playerName, out var nameRank) && nameRank > 0)
                    return nameRank;
            }

            return 0;
        }
    }

    public bool IsFounder(ulong socialClubId, string playerName)
    {
        lock (_lock)
        {
            var scStr = socialClubId > 0 ? socialClubId.ToString() : null;
            if (scStr != null && _config.Founders.Contains(scStr)) return true;
            if (_config.AllowNameBasedAdmin && !string.IsNullOrEmpty(playerName)
                && _config.Founders.Contains(playerName)) return true;
            return false;
        }
    }

    public bool TryClaimOwner(string inputToken, string playerName, ulong socialClubId, out string message)
    {
        string? claimedSocialClub = null;
        try
        {
            return TryClaimOwnerLocked(inputToken, playerName, socialClubId, out message, ref claimedSocialClub);
        }
        finally
        {
            if (claimedSocialClub != null)
                PersistToStore(claimedSocialClub, 8, true, "claimowner");
        }
    }

    private bool TryClaimOwnerLocked(string inputToken, string playerName, ulong socialClubId,
                                     out string message, ref string? claimedSocialClub)
    {
        lock (_lock)
        {
            var now = _clock();
            if (now < _claimLockedUntilUtc)
            {
                var left = _claimLockedUntilUtc - now;
                message = $"Приём токена временно закрыт из-за серии неверных попыток. " +
                          $"Повторите через {Math.Ceiling(left.TotalMinutes)} мин.";
                return false;
            }

            inputToken = (inputToken ?? string.Empty).Trim();

            // Сравнение за постоянное время: обычное сравнение строк прерывается
            // на первом несовпавшем символе, и по времени ответа можно было бы
            // подбирать токен посимвольно.
            bool isTokenValid = !string.IsNullOrEmpty(_config.SetupToken) &&
                                FixedTimeEqualsIgnoreCase(inputToken, _config.SetupToken);

            bool isAutoClaimWithoutToken = string.IsNullOrWhiteSpace(inputToken) &&
                                          _config.AutoClaimFirstPlayer &&
                                          _config.Admins.Count == 0 &&
                                          _config.Founders.Count == 0;

            if (!isTokenValid && !isAutoClaimWithoutToken)
            {
                _claimFailures++;
                if (_claimFailures >= ClaimMaxFailures)
                {
                    _claimLockedUntilUtc = now + ClaimLockout;
                    _claimFailures = 0;
                    CoreConsole.Warning(
                        $"[FloV:MP] [Security] ВНИМАНИЕ: {ClaimMaxFailures} неверных попыток токена владельца. " +
                        $"Приём токена закрыт на {ClaimLockout.TotalMinutes} мин. " +
                        $"Последняя попытка: игрок «{playerName}», SocialClub {socialClubId}.");
                    message = "Слишком много неверных попыток. Приём токена временно закрыт.";
                    return false;
                }

                message = "Неверный токен первичной настройки администратора.";
                return false;
            }

            _claimFailures = 0;

            // Назначаем Основателя (Уровень 8)
            if (!string.IsNullOrEmpty(playerName))
            {
                _config.Admins[playerName] = 8;
                if (!_config.Founders.Contains(playerName))
                    _config.Founders.Add(playerName);
            }

            if (socialClubId > 0)
            {
                var scStr = socialClubId.ToString();
                _config.Admins[scStr] = 8;
                if (!_config.Founders.Contains(scStr))
                    _config.Founders.Add(scStr);
            }

            // Инвалидация токена и выключение авто-клейма
            _config.SetupToken = "";
            _config.AutoClaimFirstPlayer = false;
            SaveInternal();

            // Запись владельца в базу откладывается до выхода из блокировки
            // (см. finally ниже) — по той же причине, что и в SetAdmin.
            claimedSocialClub = socialClubId > 0 ? socialClubId.ToString() : null;

            message = $"Владение сервером успешно подтверждено! Игроку {playerName} присвоен статус Основателя (Уровень 8).";
            return true;
        }
    }

    public bool SetAdmin(string identifier, int level)
    {
        if (string.IsNullOrWhiteSpace(identifier)) return false;
        identifier = identifier.Trim();
        level = Math.Clamp(level, 0, 8);

        lock (_lock)
        {
            if (level == 0)
            {
                _config.Admins.Remove(identifier);
                _config.Founders.Remove(identifier);
            }
            else
            {
                _config.Admins[identifier] = level;
                if (level == 8 && !_config.Founders.Contains(identifier))
                {
                    _config.Founders.Add(identifier);
                }
                else if (level < 8)
                {
                    _config.Founders.Remove(identifier);
                }
            }

            _config.AutoClaimFirstPlayer = false;
            SaveInternal();
        }

        // В базу пишем ПОСЛЕ снятия блокировки. Проверка прав при входе игрока
        // берёт ту же блокировку на главном потоке: медленная или зависшая база
        // под блокировкой заморозила бы вход всех игроков.
        PersistToStore(identifier, level, level == 8, "console:setadmin");
        return true;
    }

    public IReadOnlyDictionary<string, int> GetAllAdmins()
    {
        lock (_lock)
        {
            return new Dictionary<string, int>(_config.Admins, StringComparer.OrdinalIgnoreCase);
        }
    }

    public IReadOnlyList<string> GetFounders()
    {
        lock (_lock)
        {
            return _config.Founders.ToArray();
        }
    }

    private void SaveInternal()
    {
        try
        {
            var dir = Path.GetDirectoryName(_configFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            var json = JsonSerializer.Serialize(_config, options);
            File.WriteAllText(_configFilePath, json);
        }
        catch (Exception ex)
        {
            CoreConsole.Warning($"[FloV:MP Admin] Ошибка сохранения config/admins.json: {ex.Message}");
        }
    }

    /// <summary>
    /// Одноразовый токен владельца: 64 бита из криптографического генератора.
    ///
    /// Раньше было 32 бита (FLV-XXXX-XXXX) — около четырёх миллиардов вариантов.
    /// При сотнях параллельных подключений это дни перебора, а токен на свежем
    /// сервере живёт, пока владелец его не заберёт. 64 бита перебором
    /// недостижимы при любом числе подключений.
    /// </summary>
    private static string GenerateSetupToken()
    {
        var bytes = new byte[8];
        RandomNumberGenerator.Fill(bytes);
        var hex = Convert.ToHexString(bytes);
        return $"FLV-{hex[..4]}-{hex[4..8]}-{hex[8..12]}-{hex[12..16]}";
    }

    private static bool FixedTimeEqualsIgnoreCase(string a, string b)
    {
        var ba = System.Text.Encoding.UTF8.GetBytes(a.ToUpperInvariant());
        var bb = System.Text.Encoding.UTF8.GetBytes(b.ToUpperInvariant());
        return CryptographicOperations.FixedTimeEquals(ba, bb);
    }
}
