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

    public AdminBootstrapManager(string? configFilePath = null)
    {
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

            // Проверка переменных окружения для явного назначения владельца
            var envOwner = Environment.GetEnvironmentVariable("FLOVMP_OWNER_NAME");
            if (!string.IsNullOrWhiteSpace(envOwner))
            {
                _config.Admins[envOwner.Trim()] = 8;
                if (!_config.Founders.Contains(envOwner.Trim()))
                    _config.Founders.Add(envOwner.Trim());
            }

            var envOwnerSc = Environment.GetEnvironmentVariable("FLOVMP_OWNER_SC");
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
                    Console.WriteLine("========================================================");
                    Console.WriteLine("[FloV:MP Security] ВНИМАНИЕ: права по НИКУ отключены.");
                    Console.WriteLine("  Ник в alt:V задаётся клиентом — любой мог взять ник");
                    Console.WriteLine("  админа и получить его уровень. Эти записи НЕ действуют:");
                    foreach (var n in nameKeyed) Console.WriteLine($"    - {n}");
                    Console.WriteLine("  Как восстановить доступ (любой способ):");
                    Console.WriteLine("    1) В консоли сервера:  setadmin <SocialClubId> 8");
                    Console.WriteLine("    2) В игре:             /claimowner <токен из консоли>");
                    Console.WriteLine("    3) Переменная окружения FLOVMP_OWNER_SC=<SocialClubId>");
                    Console.WriteLine("  (Осознанно вернуть привязку по нику: AllowNameBasedAdmin=true");
                    Console.WriteLine("   в config/admins.json — НЕ рекомендуется на публичном сервере.)");
                    Console.WriteLine("========================================================");
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

    public void Reload() => LoadOrCreate();

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
        lock (_lock)
        {
            inputToken = inputToken.Trim();

            bool isTokenValid = !string.IsNullOrEmpty(_config.SetupToken) &&
                                string.Equals(inputToken, _config.SetupToken, StringComparison.OrdinalIgnoreCase);

            bool isAutoClaimWithoutToken = string.IsNullOrWhiteSpace(inputToken) &&
                                          _config.AutoClaimFirstPlayer &&
                                          _config.Admins.Count == 0 &&
                                          _config.Founders.Count == 0;

            if (!isTokenValid && !isAutoClaimWithoutToken)
            {
                message = "Неверный токен первичной настройки администратора.";
                return false;
            }

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
            return true;
        }
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
            Console.Error.WriteLine($"[FloV:MP Admin] Ошибка сохранения config/admins.json: {ex.Message}");
        }
    }

    private static string GenerateSetupToken()
    {
        var bytes = new byte[4];
        RandomNumberGenerator.Fill(bytes);
        var part1 = Convert.ToHexString(bytes[..2]);
        var part2 = Convert.ToHexString(bytes[2..]);
        return $"FLV-{part1}-{part2}";
    }
}
