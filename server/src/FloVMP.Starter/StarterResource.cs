using System;
using System.Collections.Concurrent;
using System.Linq;
using AltV.Net;
using AltV.Net.Data;
using AltV.Net.Elements.Entities;
using FloVMP.Core.Admin;

namespace FloVMP.Starter;

/// <summary>
/// Чистый ванильный стартер для клиентов FloV:MP с разграничением прав (RBAC):
/// - Обычные игроки: чистый спавн, чат, /pos, F8 консоль в режиме игрока (без админ-кнопок).
/// - Администраторы: F4 NoClip, /tpm, /car, /heal, /weather, /time, полная панель Дев-тулс в F8.
/// - 100% серверная валидация: любая попытка несанкционированного вызова админских событий
///   (teleportWaypoint, toggleNoClip, команды) строго блокируется на сервере.
/// - Pre-DB архитектура: автосохранение прав в config/admins.json, команды консоли сервера (setadmin/setfounder),
///   одноразовый токен первичной настройки (/claimowner).
/// </summary>
public class StarterResource : Resource
{
    // Точка появления (по умолчанию Легион-сквер). Свой ресурс меняет её событием
    //   Alt.Emit("flovmp:settings:spawn", x, y, z, heading)
    private Position _spawnPosition = new(198.8f, -935.6f, 30.7f);
    private float _spawnHeading = 140f;
    // false — возрождением после смерти управляет свой ресурс
    //   (Alt.Emit("flovmp:settings:respawn", false) + событие flovmp:player:died).
    private bool _platformRespawn = true;
    private static readonly uint DefaultPlayerModel = Alt.Hash("mp_m_freemode_01");

    // Пароль админ-дежурства. НЕТ небезопасного дефолта: раньше в исходниках
    // лежал "flovmp2026" — он попадает в поставляемую клиентам сборку (strings
    // на DLL) и открывает /alogin. Если переменная не задана — парольный путь
    // ОТКЛЮЧЁН (вход по назначенному рангу/токену остаётся).
    private static readonly string? AdminPassword = Environment.GetEnvironmentVariable("FLOVMP_ADMIN_PASSWORD");
    private readonly ConcurrentDictionary<uint, int> _adminLevels = new();
    // Права игрока без SocialClubId (0) — только на эту сессию, по ID
    // подключения; очищаются при выходе. Раньше здесь был словарь по НИКУ:
    // ник задаёт клиент, и любой, взявший ник администратора, получал его
    // уровень в обход AllowNameBasedAdmin=false.
    private readonly ConcurrentDictionary<uint, int> _sessionAdminRanks = new();

    // Фоновая сверка прав и банов с базой: правка таблицы admins/bans
    // применяется на работающем сервере без перезапуска.
    private const long StoreSyncIntervalMs = 30_000;
    private long _nextStoreSyncMs = StoreSyncIntervalMs;
    private int _storeSyncRunning;

    private IVoiceChannel? _spatialVoiceChannel;
    private AdminBootstrapManager _adminManager = null!;

    // Блокировки. В базовой платформе их не было вообще — только kick, после
    // которого нарушитель заходит обратно через пять секунд. Для продукта,
    // который ставят владельцам серверов, отсутствие бана — не «упрощение»,
    // а нерабочая модерация.
    // --- Защита чата ---------------------------------------------------------
    // Лимит совпадает с RP-режимом (ChatSystem): 4 сообщения за 3 секунды.
    // В базовой платформе его не было вовсе — один игрок со скриптом мог
    // заваливать чат без предела, а каждое сообщение уходит ВСЕМ игрокам, то
    // есть это был DoS одним подключением.
    private const int ChatMaxPerWindow = 4;
    private static readonly TimeSpan ChatWindow = TimeSpan.FromSeconds(3);
    private readonly System.Collections.Concurrent.ConcurrentDictionary<uint, (int Count, DateTime Start)> _chatRate = new();

    // Радиусы отыгровок — те же, что в RP-режиме, чтобы поведение не
    // расходилось между режимами. /me и /do — действие персонажа, их видят
    // рядом стоящие; крик слышно дальше.
    private const float RpActionRadius = 25.0f;
    private const float ShoutRadius = 55.0f;

    /// <summary>Не превысил ли игрок лимит сообщений. Возвращает true, если превысил.</summary>
    private bool ChatRateLimited(IPlayer player)
    {
        var now = DateTime.UtcNow;
        var window = _chatRate.AddOrUpdate(player.Id,
            _ => (1, now),
            (_, cur) => now - cur.Start > ChatWindow ? (1, now) : (cur.Count + 1, cur.Start));
        return window.Count > ChatMaxPerWindow;
    }

    /// <summary>Отправить сообщение тем, кто рядом (в том же измерении).</summary>
    private void SendNearby(IPlayer origin, float radius, string message, string kind, string author)
    {
        var pos = origin.Position;
        var dim = origin.Dimension;
        var radiusSq = radius * radius;

        foreach (var p in Alt.GetAllPlayers())
        {
            if (!p.Exists || p.Dimension != dim) continue;
            var dx = p.Position.X - pos.X;
            var dy = p.Position.Y - pos.Y;
            var dz = p.Position.Z - pos.Z;
            if (dx * dx + dy * dy + dz * dz > radiusSq) continue;
            p.Emit("flovmp:chat:msg", kind, author, message);
        }
    }

    /// <summary>
    /// Типы погоды GTA V, которые понимает нативная функция клиента.
    /// Команда /weather рассылает погоду всем игрокам, поэтому принимается
    /// только значение из этого списка.
    /// </summary>
    private static readonly HashSet<string> ValidWeatherTypes = new(StringComparer.Ordinal)
    {
        "EXTRASUNNY", "CLEAR", "NEUTRAL", "SMOG", "FOGGY", "OVERCAST", "CLOUDS",
        "CLEARING", "RAIN", "THUNDER", "SNOW", "BLIZZARD", "SNOWLIGHT", "XMAS", "HALLOWEEN",
    };

    /// <summary>
    /// Администраторы, подтвердившие пароль командой /alogin в этой сессии.
    ///
    /// Раньше /aduty ставила на дежурство БЕЗ пароля — то есть второй фактор
    /// /alogin обходился одной командой, хотя сервер при каждом входе прямо
    /// говорил админу «для входа на дежурство введите /alogin &lt;пароль&gt;».
    /// </summary>
    private readonly System.Collections.Concurrent.ConcurrentDictionary<uint, bool> _adminAuthed = new();

    /// <summary>Кто уже сообщил о готовности — защита от повторов от клиента.</summary>
    private readonly System.Collections.Concurrent.ConcurrentDictionary<uint, bool> _clientReady = new();

    /// <summary>
    /// Машина, заспавненная администратором командой /car. По одной на админа.
    ///
    /// Раньше каждый /car создавал новую машину и не удалял прежнюю. Проверка
    /// спавна тридцать раз оставляла в мире тридцать машин, число сущностей
    /// росло без предела и тянуло за собой стриминг и синхронизацию для всех
    /// игроков. Теперь новый /car убирает прежнюю машину этого же админа, а
    /// при отключении админа его машина удаляется.
    /// </summary>
    private readonly System.Collections.Concurrent.ConcurrentDictionary<uint, IVehicle> _adminVehicles = new();

    private FloVMP.Core.Security.IBanStore? _banStore;

    // Погода и время, выставленные администратором. Рассылка шла только тем, кто
    // был онлайн в момент команды: вошедший позже видел свою погоду и время.
    private string? _worldWeather;
    private (int Hour, int Minute)? _worldTime;

    // --- Команды модов -------------------------------------------------------
    // Свой ресурс владельца (папка gamemode) регистрирует чат-команды событием
    //   Alt.Emit("flovmp:commands:register", "имя", "описание", минУровеньАдмина)
    // и получает их вызовы:
    //   Alt.OnServer<IPlayer, string, string>("flovmp:command", (игрок, имя, аргументы) => ...)
    // Раньше платформа отвечала «Неизвестная команда» на всё, чего не знала сама,
    // и свои команды в отдельном ресурсе было не сделать.
    private static readonly HashSet<string> BuiltinCommands = new(StringComparer.Ordinal)
    {
        "a","admin","adminauth","aduty","alogin","armor","b","ban","banip","banlist","bans","car","claimowner","clear","cls","coords","delveh","destroyveh","dim","dimension","disarm","do","dv","engine","esp","fix","fly","freeze","gethere","givegun","god","godmode","goto","gun","hardban","heal","help","hwidban","kick","kill","lock","me","noclip","ooc","ped","pos","removeweapons","repair","revive","s","setadmin","setdim","shout","skin","speed","suicide","time","tp","tpm","unban","unfreeze","veh","vmute","voicemute","w","weapon","weather","whisper",
        "license",
    };
    private readonly ConcurrentDictionary<string, (string Description, int MinLevel)> _modCommands = new(StringComparer.Ordinal);

    private void OnRegisterModCommand(string name, string description, int minLevel)
    {
        name = (name ?? "").Trim().TrimStart('/').ToLowerInvariant();
        if (name.Length is 0 or > 32 || !name.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '_'))
        {
            Alt.LogWarning($"[FloV:MP] [Mods] команда '{name}' отклонена: только латиница, цифры и _, до 32 символов.");
            return;
        }
        if (BuiltinCommands.Contains(name))
        {
            Alt.LogWarning($"[FloV:MP] [Mods] команда /{name} уже есть в платформе — выберите другое имя.");
            return;
        }
        var entry = ((description ?? "").Trim(), Math.Clamp(minLevel, 0, 8));
        // Мод регистрирует команды и при своём старте, и по flovmp:platform:ready —
        // повтор той же команды не шумит в логе.
        if (_modCommands.TryGetValue(name, out var existing) && existing == entry) return;
        _modCommands[name] = entry;
        // Уже подключённым игрокам — обновлённые подсказки в чате.
        foreach (var online in Alt.GetAllPlayers())
            if (online.Exists && _clientReady.ContainsKey(online.Id)) SendChatCommands(online);
        Alt.Log($"[FloV:MP] [Mods] зарегистрирована команда /{name}" + (minLevel > 0 ? $" (администраторы {minLevel}+)" : ""));
    }

    // Лицензия (license.flv). Перепроверяется раз в час: срок может истечь на
    // работающем сервере. Без действующей лицензии сервер работает с лимитом игроков.
    private FloVMP.Core.Licensing.LicenseStatus _license =
        FloVMP.Core.Licensing.LicenseFile.Evaluate(null, DateTime.UtcNow);
    private const long LicenseRecheckMs = 60 * 60 * 1000;
    private long _nextLicenseCheckMs;

    private void CheckLicense(bool logAlways)
    {
        var previous = _license.State;
        try
        {
            var path = FloVMP.Core.Licensing.LicenseFile.Locate();
            _license = FloVMP.Core.Licensing.LicenseFile.Evaluate(
                path, DateTime.UtcNow, Environment.GetEnvironmentVariable("FLOVMP_LICENSE_KEY"));
        }
        catch (Exception ex)
        {
            Alt.LogWarning($"[FloV:MP] [License] ошибка проверки: {ex.Message}");
            return;
        }
        if (!logAlways && previous == _license.State) return;

        if (_license.State == FloVMP.Core.Licensing.LicenseState.Valid)
            Alt.Log($"[FloV:MP] [License] {_license.Message}");
        else
            Alt.LogWarning($"[FloV:MP] [License] {_license.Message}");
    }
    private FloVMP.Core.Security.MultiTierBanService? _bans;
    private readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();
    private readonly List<(IPlayer Player, long RespawnAtMs)> _pendingRespawns = new();

    /// <summary>
    /// Может ли администратор применять силовую команду к цели: к себе — да,
    /// к другому — только если цель ниже по назначенному рангу. Раньше /kick,
    /// /freeze, /vmute, /disarm и /gethere работали против кого угодно: админ
    /// второго уровня мог выкинуть с сервера владельца.
    /// </summary>
    private bool CanActOn(IPlayer admin, IPlayer target)
    {
        if (admin.Id == target.Id) return true;
        return GetAssignedAdminRank(target) < GetAssignedAdminRank(admin);
    }

    // Перебор пароля /alogin: без ограничения его сдерживал только лимит чата
    // (~80 попыток в минуту). После 5 ошибок — пауза 10 минут на SocialClubId
    // (или ID подключения, если SC нет); переподключение её не сбрасывает.
    private const int AloginMaxFailures = 5;
    private static readonly TimeSpan AloginLockout = TimeSpan.FromMinutes(10);
    private readonly ConcurrentDictionary<string, (int Failures, DateTime LockedUntil)> _aloginFailures = new();

    private static string AloginKey(IPlayer p) => p.SocialClubId > 0 ? "sc:" + p.SocialClubId : "id:" + p.Id;

    // Команды для подсказок в чате: (имя, описание, мин. уровень на дежурстве).
    // -1 — показывать тем, у кого есть назначенные права (даже не на дежурстве).
    private static readonly (string Cmd, string Desc, int Level)[] ChatCommandHints =
    {
        ("help", "список команд", 0), ("me", "действие персонажа", 0), ("do", "описание ситуации", 0),
        ("b", "OOC-сообщение", 0), ("s", "крикнуть", 0), ("w", "шёпот игроку: /w <id> <текст>", 0),
        ("pos", "координаты", 0), ("engine", "двигатель (2)", 0), ("lock", "замок транспорта (L)", 0),
        ("clear", "очистить чат", 0),
        ("alogin", "вход администратора: /alogin <пароль>", -1), ("aduty", "дежурство администратора", -1),
        ("a", "админ-чат", 1), ("tpm", "телепорт на метку (F5)", 1), ("noclip", "полёт (F4)", 1),
        ("esp", "админ-видение: /esp [0-3] (F3)", 1), ("car", "транспорт: /car [модель]", 1),
        ("fix", "починить транспорт", 1), ("dv", "удалить транспорт", 1), ("gun", "оружие: /gun <название> [патроны]", 1),
        ("disarm", "забрать оружие: /disarm [id]", 1), ("tp", "телепорт: /tp <x> <y> <z>", 1),
        ("goto", "к игроку: /goto <id>", 1), ("gethere", "игрока к себе: /gethere <id>", 1),
        ("freeze", "заморозить: /freeze <id>", 1), ("unfreeze", "разморозить: /unfreeze <id>", 1),
        ("revive", "реанимировать: /revive [id]", 1), ("heal", "здоровье и броня", 1), ("armor", "броня: /armor [0-100]", 1),
        ("god", "бессмертие", 1), ("kill", "умереть", 1), ("weather", "погода: /weather <тип>", 1),
        ("time", "время: /time <час> [мин]", 1), ("speed", "скорость бега: /speed <1.0-1.49>", 1),
        ("setdim", "измерение: /setdim <номер>", 1), ("skin", "скин: /skin <модель>", 1),
        ("kick", "исключить: /kick <id> [причина]", 2), ("bans", "список блокировок", 2), ("vmute", "голосовой мут: /vmute <id>", 2),
        ("ban", "блокировка: /ban <id> <дней> [причина]", 3),
        ("banip", "бан по IP: /banip <id> <дней> [причина]", 4), ("hwidban", "бан по железу: /hwidban <id> <дней> [причина]", 4),
        ("unban", "снять блокировку: /unban <ник|IP|HWID|ID>", 4),
        ("hardban", "навсегда: /hardban <id> <причина>", 6),
        ("setadmin", "права: /setadmin <id> <0-8>", 8),
    };

    /// <summary>Уровень на дежурстве — клиенту (консоль F8, NoClip/ESP), в метаданные и список подсказок чата.</summary>
    private void PushAdminLevel(IPlayer player, int level)
    {
        if (player is null || !player.Exists) return;
        player.Emit("flovmp:console:setAdmin", level);
        // Local meta видит только сам игрок (и сервер). Раньше уровень лежал в
        // stream synced meta, которую получают клиенты всех игроков рядом: читер
        // видел, кто вокруг администратор и кто на дежурстве, даже невидимый в NoClip.
        player.SetLocalMetaData("adminLevel", level);
        SendChatCommands(player);
        BroadcastAdminRoster();
    }

    /// <summary>
    /// Кто администратор — только администраторам на дежурстве (для ESP).
    /// Основатель (8) для младших уровней передаётся как -1: клиент его не рисует.
    /// </summary>
    private void BroadcastAdminRoster()
    {
        var admins = _adminLevels.Where(kv => kv.Value > 0).ToArray();
        foreach (var recipient in Alt.GetAllPlayers())
        {
            if (!recipient.Exists || !_clientReady.ContainsKey(recipient.Id)) continue;
            var own = _adminLevels.TryGetValue(recipient.Id, out var l) ? l : 0;
            var roster = new Dictionary<string, int>();
            if (own > 0)
            {
                foreach (var (id, level) in admins)
                    roster[id.ToString()] = level >= 8 && own < 8 && id != recipient.Id ? -1 : level;
            }
            recipient.Emit("flovmp:admin:roster", System.Text.Json.JsonSerializer.Serialize(roster));
        }
    }

    private void SendChatCommands(IPlayer player)
    {
        var assigned = GetAssignedAdminRank(player) > 0;
        var list = new List<object>();
        foreach (var (cmd, desc, level) in ChatCommandHints)
        {
            if (level == -1 ? assigned : level == 0 || IsAdmin(player, level))
                list.Add(new { cmd, desc });
        }
        if (!string.IsNullOrEmpty(_adminManager.CurrentSetupToken))
            list.Add(new { cmd = "claimowner", desc = "стать владельцем: /claimowner <токен>" });
        foreach (var (name, info) in _modCommands.OrderBy(kv => kv.Key))
        {
            if (info.MinLevel == 0 || IsAdmin(player, info.MinLevel))
                list.Add(new { cmd = name, desc = info.Description });
        }
        player.Emit("flovmp:chat:commands", System.Text.Json.JsonSerializer.Serialize(list));
    }

    public int GetAssignedAdminRank(IPlayer player)
    {
        // Источник правды — AdminBootstrapManager (admins.json или таблица
        // admins). Отдельного кэша по SocialClubId здесь нет: он пережил бы
        // снятие прав в базе.
        var fileRank = _adminManager.GetAssignedRank(player.SocialClubId, player.Name, player.Ip);
        if (fileRank > 0) return fileRank;

        if (player.SocialClubId == 0 && _sessionAdminRanks.TryGetValue(player.Id, out var sessionRank) && sessionRank > 0)
            return sessionRank;

        return 0;
    }

    public void SetAssignedAdminRank(IPlayer player, int rank)
    {
        rank = Math.Clamp(rank, 0, 8);
        if (player.SocialClubId > 0)
        {
            _adminManager.SetAdmin(player.SocialClubId.ToString(), rank);
            return;
        }

        // Без SocialClubId сохранить права надёжно не к чему — только сессия.
        if (rank > 0) _sessionAdminRanks[player.Id] = rank;
        else _sessionAdminRanks.TryRemove(player.Id, out _);
    }

    public override void OnStart()
    {
        try
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.InputEncoding = System.Text.Encoding.UTF8;
        }
        catch { }

        // Диагностика платформы (режим БД, накат миграций, выбор хранилища
        // блокировок) должна попадать в server.log. Обычный Console.WriteLine
        // из ресурса alt:V теряется — в лог идёт только то, что прошло через
        // Alt.Log. Проверено живым запуском: этих строк там не было ни разу.
        FloVMP.Core.CoreConsole.Out = msg => Alt.Log(msg);
        FloVMP.Core.CoreConsole.Warn = msg => Alt.LogWarning(msg);

        _adminManager = new AdminBootstrapManager();

        // Хранилище банов: общая таблица MariaDB, если база настроена, иначе
        // локальный файл. Фабрика сама печатает выбранный режим — владелец
        // сервера должен знать, действуют ли его баны на всех инстансах.
        var starterDataDir = Path.Combine(Directory.GetCurrentDirectory(), "flovmp-data");
        var starterDbConn = Environment.GetEnvironmentVariable("FLOVMP_DB_CONNECTION") ??
                            new FloVMP.Core.Database.DatabaseConfig().BuildConnectionString();
        // Миграции до обращения к таблицам: на свежей установке bans и admins
        // ещё не существуют. Раньше их накатывал только RP-режим (Core), а
        // базовая платформа молча работала без таблиц.
        var dbReachable = FloVMP.Core.Database.AccountStoreFactory.TryPrepareDatabase(starterDbConn);

        _banStore = FloVMP.Core.Security.BanStoreFactory.Create(
            starterDbConn, Path.Combine(starterDataDir, "bans.json"));
        _bans = new FloVMP.Core.Security.MultiTierBanService(_banStore);

        // Права администраторов в базе: владелец выдаёт себе уровень в таблице
        // admins, сервер подхватывает без перезапуска (фоновая сверка в OnTick
        // или reloadadmins в консоли).
        if (dbReachable)
            _adminManager.AttachStore(new FloVMP.Core.Admin.MySqlAdminStore(starterDbConn));

        CheckLicense(logAlways: true);

        Alt.Log("[FloV:MP Starter] Платформа запущена.");
        Alt.Log("[FloV:MP Starter] Права проверяются на сервере: админ-действия недоступны обычным игрокам.");

        if (!string.IsNullOrEmpty(_adminManager.CurrentSetupToken))
        {
            Alt.Log("=================================================================================");
            Alt.Log("[FloV:MP Setup] Администраторов ещё нет. Стать владельцем (уровень 8):");
            Alt.Log($"[FloV:MP Setup]  1. в игре:             /claimowner {_adminManager.CurrentSetupToken}");
            Alt.Log("[FloV:MP Setup]  2. в консоли сервера:  setadmin <ID игрока> 8");
            Alt.Log("[FloV:MP Setup]  3. в config/flovmp.env: FLOVMP_OWNER_SC=<ваш SocialClubId>");
            Alt.Log("=================================================================================");
        }

        try
        {
            // CreateVoiceChannel может вернуть null БЕЗ исключения, если в
            // server.toml нет секции [voice] — поэтому проверяем результат, а
            // не только отсутствие ошибки.
            _spatialVoiceChannel = Alt.CreateVoiceChannel(true, 25.0f);
            if (_spatialVoiceChannel is not null)
                Alt.Log("[FloV:MP Starter] Голосовой канал создан (пространственный, радиус 25 м).");
        }
        catch (Exception ex)
        {
            Alt.LogWarning($"[FloV:MP Starter] Голосовой канал НЕ создан: {ex.Message}");
        }

        // Молчаливое отсутствие голоса — самая частая жалоба при установке.
        // Причина почти всегда одна из двух, поэтому называем обе сразу.
        if (_spatialVoiceChannel is null)
        {
            Alt.LogWarning("[FloV:MP Starter] Голос работать НЕ будет. Проверьте по порядку:");
            Alt.LogWarning("  1) секция [voice] в server.toml (externalHost/externalPort/externalSecret);");
            Alt.LogWarning("  2) запущен ли altv-voice-server и совпадает ли externalSecret в его voice.toml.");
        }

        Alt.OnPlayerConnect += OnPlayerConnect;
        Alt.OnPlayerDisconnect += OnPlayerDisconnect;
        Alt.OnPlayerDead += OnPlayerDead;
        Alt.OnConsoleCommand += OnConsoleCommand;
        Alt.OnClient<IPlayer, string>("chat:message", OnChatMessage);
        Alt.OnClient<IPlayer, string>("flovmp:chat:say", OnChatMessage);
        Alt.OnClient<IPlayer>("flovmp:client:ready", OnClientReady);
        Alt.OnClient<IPlayer, float, float, float>("starter:teleportWaypoint", OnTeleportWaypoint);
        Alt.OnClient<IPlayer, bool>("starter:toggleNoClip", OnToggleNoClip);
        Alt.OnClient<IPlayer, bool>("flovmp:admin:noclip", OnToggleNoClip);
        Alt.OnServer<string, string, int>("flovmp:commands:register", OnRegisterModCommand);
        Alt.OnServer<float, float, float, float>("flovmp:settings:spawn", OnSpawnSetting);
        Alt.OnServer<bool>("flovmp:settings:respawn", OnRespawnSetting);

        // Ресурсы, стартовавшие раньше платформы, по этому событию повторяют регистрацию команд.
        Alt.Emit("flovmp:platform:ready");
    }

    public override void OnStop()
    {
        // Баны на диск до отписки от событий: выданный в последнюю секунду бан
        // обязан пережить перезапуск.
        try { (_banStore as IDisposable)?.Dispose(); }
        catch (Exception ex) { Alt.LogWarning($"[FloV:MP] Не удалось сохранить баны: {ex.Message}"); }

        Alt.OnPlayerConnect -= OnPlayerConnect;
        Alt.OnPlayerDisconnect -= OnPlayerDisconnect;
        Alt.OnPlayerDead -= OnPlayerDead;
        Alt.OnConsoleCommand -= OnConsoleCommand;
        Alt.Log("[FloV:MP Starter] Остановка ванильного стартера.");
    }

    public bool IsAdmin(IPlayer player, int minLevel = 1)
    {
        return _adminLevels.TryGetValue(player.Id, out var level) && level >= minLevel;
    }

    public void SendChatMessage(IPlayer player, string message, string kind = "system", string author = "")
    {
        if (player == null || !player.Exists) return;
        player.Emit("flovmp:chat:msg", kind, author, message);
    }

    public void BroadcastChatMessage(string message, string kind = "system", string author = "")
    {
        Alt.EmitAllClients("flovmp:chat:msg", kind, author, message);
    }

    /// <summary>
    /// Проверка входящего подключения по IP / Social Club / HWID / MAC.
    /// Возвращает true, если игрок отклонён.
    ///
    /// Проверка идёт по памяти сервиса, без похода в БД: она выполняется на
    /// главном потоке при каждом подключении, и запрос к базе здесь означал бы
    /// задержку тика на каждом входе.
    /// </summary>
    private bool RejectIfBanned(IPlayer player)
    {
        if (_bans is null) return false;

        try
        {
            var result = _bans.CheckConnection(
                accountId: 0,
                ip: player.Ip,
                socialClubId: player.SocialClubId.ToString(),
                hwidHash: player.HardwareIdHash.ToString("X16"),
                macAddress: player.HardwareIdExHash.ToString("X16"),
                policy: FloVMP.Core.Security.HwidPolicyMode.Strict);

            if (!result.IsBlocked) return false;

            var until = result.ExpiresAtUtc is null
                ? "навсегда"
                : $"до {result.ExpiresAtUtc:dd.MM.yyyy HH:mm} UTC";
            Alt.Log($"[FloV:MP] [Ban] отклонён вход {player.Name} ({player.Ip}): " +
                    $"{result.MatchedFlag} — {result.Reason}");
            player.Kick($"Доступ заблокирован ({until}). Причина: {result.Reason}");
            return true;
        }
        catch (Exception ex)
        {
            // Сломанная проверка не должна запирать вход всем подряд: в
            // сомнительной ситуации пускаем и пишем в лог, а не глушим сервер.
            Alt.LogWarning($"[FloV:MP] [Ban] ошибка проверки блокировки: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Общий обработчик /ban, /banip, /hwidban, /hardban.
    ///
    /// Разные команды отличаются только глубиной блокировки, поэтому логика
    /// одна: четыре почти одинаковые копии разъезжаются при первой же правке.
    /// Администратору явно сообщается, что именно заблокировано — обещать бан
    /// по железу и поставить флаг только на аккаунт хуже, чем не уметь его.
    /// </summary>
    private void HandleBanCommand(IPlayer admin, string cmd, string[] parts)
    {
        // Порог уровня растёт вместе с необратимостью: hardban — навсегда и по
        // железу, такое не должен уметь младший модератор.
        var required = cmd == "hardban" ? 6 : cmd == "ban" ? 3 : 4;
        if (!IsAdmin(admin, required))
        {
            SendChatMessage(admin, $"{{ef4444}}[FloV:MP Security] Доступ запрещен (требуется Уровень {required}+).");
            return;
        }

        if (_bans is null)
        {
            SendChatMessage(admin, "{ef4444}Сервис блокировок не подключён — блокировка невозможна.");
            return;
        }

        var permanent = cmd == "hardban";
        var minArgs = permanent ? 2 : 3;
        if (parts.Length < minArgs || !uint.TryParse(parts[1], out var targetId))
        {
            SendChatMessage(admin, permanent
                ? "{fde047}Использование: /hardban <ID> <причина>"
                : $"{{fde047}}Использование: /{cmd} <ID> <дней> [причина]");
            return;
        }

        var target = Alt.GetPlayerById(targetId);
        if (target == null)
        {
            SendChatMessage(admin, "{ef4444}Игрок с таким ID не найден.");
            return;
        }

        if (target.Id == admin.Id)
        {
            SendChatMessage(admin, "{ef4444}Себя забанить нельзя.");
            return;
        }

        // Администратор равного или большего уровня не банится: иначе двое
        // старших админов могут выбить друг друга с сервера.
        if (GetAssignedAdminRank(target) >= GetAssignedAdminRank(admin))
        {
            SendChatMessage(admin, "{ef4444}Нельзя заблокировать администратора равного или большего уровня.");
            return;
        }

        var days = 0;
        var reasonFrom = 2;
        if (!permanent)
        {
            if (!int.TryParse(parts[2], out days) || days <= 0)
            {
                SendChatMessage(admin, $"{{fde047}}Использование: /{cmd} <ID> <дней> [причина]");
                return;
            }
            reasonFrom = 3;
        }

        var banReason = parts.Length > reasonFrom
            ? string.Join(' ', parts[reasonFrom..])
            : "Нарушение правил сервера";

        // В базовой платформе нет аккаунтов, поэтому обычный бан — по SocialClub
        // (а если его нет — по железу). Раньше /ban создавал бан «аккаунта» без
        // аккаунта: нарушителя он не держал.
        var tier = cmd switch
        {
            "hardban" => FloVMP.Core.Security.BanTier.HardBan,
            "hwidban" => FloVMP.Core.Security.BanTier.HardwareBan,
            "banip" => FloVMP.Core.Security.BanTier.IpBan,
            _ => target.SocialClubId > 0
                ? FloVMP.Core.Security.BanTier.SocialClubBan
                : FloVMP.Core.Security.BanTier.HardwareBan,
        };

        FloVMP.Core.Security.BanRecord record;
        try
        {
            record = _bans.CreateBan(
                accountId: 0,
                username: target.Name,
                ip: target.Ip,
                socialClubId: target.SocialClubId.ToString(),
                hwidHash: target.HardwareIdHash.ToString("X16"),
                macAddress: target.HardwareIdExHash.ToString("X16"),
                tier: tier,
                adminUsername: admin.Name,
                reason: banReason,
                durationDays: days);
        }
        catch (Exception ex)
        {
            SendChatMessage(admin, $"{{ef4444}}Не удалось выдать блокировку: {ex.Message}");
            Alt.LogWarning($"[FloV:MP] [Ban] ошибка выдачи блокировки: {ex}");
            return;
        }

        var duration = permanent ? "навсегда" : $"на {days} дн.";
        BroadcastChatMessage($"{{ef4444}}[Бан] {target.Name} заблокирован {duration} администратором {admin.Name}. Причина: {banReason}");
        SendChatMessage(admin, $"{{34d399}}Блокировка {record.Id} выдана. Уровень: {record.Flags}.");
        Alt.Log($"[FloV:MP] [Ban] {admin.Name} забанил {target.Name} ({tier}, {duration}): {banReason}");

        target.Kick($"Вы заблокированы {duration}. Причина: {banReason}");
    }

    private void OnPlayerConnect(IPlayer player, string reason)
    {
        Alt.Log($"[FloV:MP] Игрок {player.Name} (ID: {player.Id}, SocialClub: {player.SocialClubId}) подключается...");

        // Блокировка проверяется ДО спавна: забаненный не должен появляться в
        // мире даже на мгновение, иначе остальные игроки видят «призрака», а
        // сам он успевает выстрелить или задавить кого-то перед киком.
        if (RejectIfBanned(player)) return;

        // Лимит игроков по лицензии. Администраторы проходят всегда: владелец
        // должен иметь возможность зайти на заполненный сервер.
        var online = Alt.GetAllPlayers().Count;
        if (online > _license.PlayerLimit && GetAssignedAdminRank(player) <= 0)
        {
            Alt.LogWarning($"[FloV:MP] [License] вход {player.Name} отклонён: достигнут предел {_license.PlayerLimit} игроков ({_license.State}).");
            player.Kick(_license.IsLicensed
                ? $"Сервер заполнен ({_license.PlayerLimit} игроков)."
                : $"Сервер работает без лицензии и пускает не больше {_license.PlayerLimit} игроков.");
            return;
        }

        // Чистый спавн игрока
        player.Model = DefaultPlayerModel;
        player.Spawn(_spawnPosition, 0);
        player.Rotation = new Rotation(0, 0, _spawnHeading);
        player.Health = 200;
        player.MaxHealth = 200;
        player.Armor = 100;

        // Автоматическое распознавание Основателя (8)
        // БЕЗ БЭКДОРОВ. Раньше здесь были: захардкоженный ник (любой
        // игрок, взявший этот ник, получал 8 уровень), захардкоженный SocialClubId
        // (бэкдор в коде, который поставляется клиентам по лицензии) и авто-выдача
        // прав по localhost-IP (за nginx/прокси IP схлопывается -> founder всем).
        // Легальные пути: /claimowner <токен> (токен печатается в консоли сервера)
        // и setadmin/setfounder из консоли сервера.
        var isOwner = _adminManager.IsFounder(player.SocialClubId, player.Name) ||
                      GetAssignedAdminRank(player) == 8;

        if (isOwner)
        {
            SetAssignedAdminRank(player, 8);
            _adminLevels[player.Id] = 8;
            Alt.Log($"[FloV:MP Admin] Владелец сервера {player.Name} (ID: {player.Id}, SC: {player.SocialClubId}) автоматически авторизован (Уровень 8 - Основатель).");
        }
        else
        {
            _adminLevels[player.Id] = 0;
        }

        // Активация 3D войс-канала
        try
        {
            _spatialVoiceChannel?.AddPlayer(player);
        }
        catch
        {
        }

        var initLvl = _adminLevels.TryGetValue(player.Id, out var curLvl) ? curLvl : 0;
        PushAdminLevel(player, initLvl);
    }

    private void OnClientReady(IPlayer player)
    {
        if (player == null || !player.Exists) return;

        // Событие приходит ОТ КЛИЕНТА, то есть его может слать модифицированный
        // клиент сколько угодно раз. Каждый вызов — работа на сервере и два
        // ответных события игроку, то есть усилитель для DoS. Готовность
        // осмысленна ровно один раз за подключение, повторы игнорируем.
        if (!_clientReady.TryAdd(player.Id, true)) return;

        player.Emit("starter:initClient", _spawnPosition.X, _spawnPosition.Y, _spawnPosition.Z);
        player.Emit("flovmp:client:welcome", player.Name, 0, _spawnPosition.X, _spawnPosition.Y, _spawnPosition.Z);

        // БЕЗ БЭКДОРОВ (см. комментарий в OnPlayerConnect): ни захардкоженного
        // ника, ни захардкоженного SocialClubId.
        var isOwner = _adminManager.IsFounder(player.SocialClubId, player.Name) ||
                      GetAssignedAdminRank(player) == 8;

        if (isOwner)
        {
            _adminLevels[player.Id] = 8;
            SetAssignedAdminRank(player, 8);
        }

        var lvl = _adminLevels.TryGetValue(player.Id, out var al) ? al : 0;
        PushAdminLevel(player, lvl);

        // Сигнал своим ресурсам (gamemode): клиент загружен, игрок заспавнен —
        // можно показывать свой интерфейс, телепортировать, выдавать данные.
        if (_worldWeather is not null) player.Emit("starter:setWeather", _worldWeather);
        if (_worldTime is { } wt) player.Emit("starter:setTime", wt.Hour, wt.Minute);

        Alt.Emit("flovmp:player:ready", player);

        // Приветствие — здесь, а не в OnPlayerConnect: при подключении
        // клиентский скрипт ещё не загружен, и отправленные туда сообщения
        // (включая подсказку /claimowner владельцу) терялись.
        SendChatMessage(player, "{ff3d8a}[FloV:MP]{ffffff} Добро пожаловать на сервер!");
        if (!_license.IsLicensed && GetAssignedAdminRank(player) > 0)
            SendChatMessage(player, $"{{f59e0b}}[Лицензия]{{ffffff}} {_license.Message}");
        else if (_license.State == FloVMP.Core.Licensing.LicenseState.Grace && GetAssignedAdminRank(player) > 0)
            SendChatMessage(player, $"{{f59e0b}}[Лицензия]{{ffffff}} {_license.Message}");

        var assigned = GetAssignedAdminRank(player);
        if (_adminLevels.TryGetValue(player.Id, out var activeLvl) && activeLvl == 8)
        {
            SendChatMessage(player, "{34d399}[FloV:MP Admin]{ffffff} Добро пожаловать, {fde047}Владелец сервера (" + player.Name + "){ffffff}! Все права администратора (Уровень 8) активированы автоматически.");
        }
        else if (assigned > 0)
        {
            SendChatMessage(player, "{34d399}[Admin]{ffffff} У вас есть права администратора (Уровень " + assigned + "). Для входа на дежурство введите: {fde047}/alogin <пароль>");
        }
        else if (!string.IsNullOrEmpty(_adminManager.CurrentSetupToken))
        {
            SendChatMessage(player, "{38bdf8}[Setup]{ffffff} Доступна команда {fde047}/claimowner <токен>{ffffff} для первичной активации прав.");
        }
        else
        {
            SendChatMessage(player, "{a1a1aa}Доступна команда /pos для координат.");
        }
    }

    private void OnConsoleCommand(string name, string[] args)
    {
        var cmd = name.ToLowerInvariant();
        switch (cmd)
        {
            case "setadmin":
                if (args.Length < 2)
                {
                    Alt.Log("[Console] Использование: setadmin <ID|Ник|SocialClub> <Уровень 0-8>");
                    return;
                }
                var targetArg = args[0];
                if (!int.TryParse(args[1], out var newLvl))
                {
                    Alt.Log("[Console] Уровень должен быть числом от 0 до 8.");
                    return;
                }
                newLvl = Math.Clamp(newLvl, 0, 8);

                IPlayer? matchedPlayer = null;
                if (uint.TryParse(targetArg, out var targetId))
                {
                    matchedPlayer = Alt.GetPlayerById(targetId);
                }
                if (matchedPlayer == null)
                {
                    matchedPlayer = Alt.GetAllPlayers().FirstOrDefault(p => string.Equals(p.Name, targetArg, StringComparison.OrdinalIgnoreCase));
                }

                if (matchedPlayer != null)
                {
                    SetAssignedAdminRank(matchedPlayer, newLvl);
                    _adminLevels[matchedPlayer.Id] = newLvl;
                    PushAdminLevel(matchedPlayer, newLvl);
                    SendChatMessage(matchedPlayer, $"{{34d399}}[Admin] Консоль сервера назначила вам уровень прав {newLvl}.");
                    Alt.Log($"[Console] Игроку [{matchedPlayer.Id}] {matchedPlayer.Name} успешно назначен уровень {newLvl}.");
                }
                else
                {
                    if (!targetArg.All(char.IsDigit))
                    {
                        // Игрок с таким ником не в сети, а права по нику отключены
                        // (ник подделывается). Раньше запись молча сохранялась и
                        // не действовала — владелец думал, что выдал права.
                        Alt.Log($"[Console] Игрок '{targetArg}' не в сети. Права выдаются по SocialClubId: setadmin <SocialClubId> {newLvl}");
                        return;
                    }
                    _adminManager.SetAdmin(targetArg, newLvl);
                    ApplyRevokedAdminRights();
                    Alt.Log(_adminManager.HasStore
                        ? $"[Console] SocialClubId {targetArg}: уровень {newLvl} сохранён в базу (таблица admins) и config/admins.json."
                        : $"[Console] SocialClubId {targetArg}: уровень {newLvl} сохранён в config/admins.json.");
                }
                break;

            case "setfounder":
                if (args.Length < 1)
                {
                    Alt.Log("[Console] Использование: setfounder <ID|Ник|SocialClub>");
                    return;
                }
                OnConsoleCommand("setadmin", new[] { args[0], "8" });
                break;

            case "adminlist":
                Alt.Log("─── СПИСОК АДМИНИСТРАТОРОВ (config/admins.json) ───");
                var allAdmins = _adminManager.GetAllAdmins();
                if (allAdmins.Count == 0)
                {
                    Alt.Log("  (нет зарегистрированных администраторов)");
                }
                else
                {
                    foreach (var kvp in allAdmins)
                    {
                        var isFound = _adminManager.IsFounder(0, kvp.Key);
                        Alt.Log($"  • {kvp.Key} -> Уровень {kvp.Value}{(isFound ? " [ОСНОВАТЕЛЬ]" : "")}");
                    }
                }
                Alt.Log("─── АДМИНИСТРАТОРЫ ОНЛАЙН ───");
                var onlineCount = 0;
                foreach (var p in Alt.GetAllPlayers())
                {
                    if (IsAdmin(p, 1))
                    {
                        onlineCount++;
                        var lvl = _adminLevels.TryGetValue(p.Id, out var l) ? l : 0;
                        Alt.Log($"  [{p.Id}] {p.Name} — Ур. {lvl} (На дежурстве)");
                    }
                }
                if (onlineCount == 0) Alt.Log("  (нет активных админов на дежурстве)");
                break;

            case "reloadadmins":
                _adminManager.Reload();
                ApplyRevokedAdminRights();
                Alt.Log(_adminManager.HasStore
                    ? "[Console] Права перечитаны из базы (таблица admins) и config/admins.json."
                    : "[Console] config/admins.json успешно перезагружен.");
                break;

            case "claimtoken":
                if (!string.IsNullOrEmpty(_adminManager.CurrentSetupToken))
                {
                    Alt.Log($"[Console] Активный токен настройки: {_adminManager.CurrentSetupToken}");
                    Alt.Log($"[Console] Введите в игре: /claimowner {_adminManager.CurrentSetupToken}");
                }
                else
                {
                    Alt.Log("[Console] Токен первичной настройки уже активирован или не задан.");
                }
                break;

            case "say":
                if (args.Length == 0)
                {
                    Alt.Log("[Console] Использование: say <текст>");
                    return;
                }
                var sayMsg = string.Join(' ', args);
                BroadcastChatMessage(sayMsg, "system", "СЕРВЕР");
                Alt.Log($"[Console] say -> {sayMsg}");
                break;

            case "kick":
                if (args.Length == 0)
                {
                    Alt.Log("[Console] Использование: kick <ID|Ник> [причина]");
                    return;
                }
                IPlayer? kickTarget = null;
                if (uint.TryParse(args[0], out var kId)) kickTarget = Alt.GetPlayerById(kId);
                if (kickTarget == null) kickTarget = Alt.GetAllPlayers().FirstOrDefault(p => string.Equals(p.Name, args[0], StringComparison.OrdinalIgnoreCase));
                if (kickTarget == null)
                {
                    Alt.Log($"[Console] Игрок '{args[0]}' не найден.");
                    return;
                }
                var kReason = args.Length > 1 ? string.Join(' ', args.Skip(1)) : "Исключен администратором консоли";
                BroadcastChatMessage($"{{ef4444}}[Kick] {kickTarget.Name} был исключен сервером. Причина: {kReason}");
                kickTarget.Kick(kReason);
                Alt.Log($"[Console] Игрок {kickTarget.Name} кикнут: {kReason}");
                break;

            // Блокировки из консоли — путь владельца сервера, когда он не в
            // игре: без него единственным способом забанить был бы вход в игру
            // администратором.
            case "ban":
            case "hardban":
                if (_bans is null) { Alt.Log("[Console] Сервис блокировок не подключён."); return; }
                if (args.Length == 0)
                {
                    Alt.Log("[Console] Использование: ban <ID|Ник> <дней> [причина]  |  hardban <ID|Ник> [причина]");
                    return;
                }
                IPlayer? banTarget = null;
                if (uint.TryParse(args[0], out var bId)) banTarget = Alt.GetPlayerById(bId);
                banTarget ??= Alt.GetAllPlayers().FirstOrDefault(
                    p => string.Equals(p.Name, args[0], StringComparison.OrdinalIgnoreCase));
                if (banTarget == null)
                {
                    Alt.Log($"[Console] Игрок '{args[0]}' не найден онлайн " +
                            "(заблокировать офлайн-игрока пока можно только по записи в БД).");
                    return;
                }

                var consolePermanent = name.Equals("hardban", StringComparison.OrdinalIgnoreCase);
                var consoleDays = 0;
                var consoleReasonFrom = 1;
                if (!consolePermanent)
                {
                    if (args.Length < 2 || !int.TryParse(args[1], out consoleDays) || consoleDays <= 0)
                    {
                        Alt.Log("[Console] Использование: ban <ID|Ник> <дней> [причина]");
                        return;
                    }
                    consoleReasonFrom = 2;
                }

                var consoleReason = args.Length > consoleReasonFrom
                    ? string.Join(' ', args.Skip(consoleReasonFrom))
                    : "Заблокирован администратором сервера";

                try
                {
                    var rec = _bans.CreateBan(
                        accountId: 0,
                        username: banTarget.Name,
                        ip: banTarget.Ip,
                        socialClubId: banTarget.SocialClubId.ToString(),
                        hwidHash: banTarget.HardwareIdHash.ToString("X16"),
                        macAddress: banTarget.HardwareIdExHash.ToString("X16"),
                        tier: consolePermanent
                            ? FloVMP.Core.Security.BanTier.HardBan
                            : banTarget.SocialClubId > 0
                                ? FloVMP.Core.Security.BanTier.SocialClubBan
                                : FloVMP.Core.Security.BanTier.HardwareBan,
                        adminUsername: "console",
                        reason: consoleReason,
                        durationDays: consoleDays);

                    var howLong = consolePermanent ? "навсегда" : $"на {consoleDays} дн.";
                    BroadcastChatMessage($"{{ef4444}}[Бан] {banTarget.Name} заблокирован {howLong}. Причина: {consoleReason}");
                    Alt.Log($"[Console] Блокировка {rec.Id}: {banTarget.Name} ({rec.Flags}, {howLong}) — {consoleReason}");
                    banTarget.Kick($"Вы заблокированы {howLong}. Причина: {consoleReason}");
                }
                catch (Exception ex)
                {
                    Alt.Log($"[Console] Не удалось выдать блокировку: {ex.Message}");
                }
                break;

            case "unban":
                if (_bans is null) { Alt.Log("[Console] Сервис блокировок не подключён."); return; }
                if (args.Length == 0)
                {
                    Alt.Log("[Console] Использование: unban <ник|IP|HWID|ID бана>");
                    return;
                }
                var consoleLifted = _bans.Unban(args[0]);
                Alt.Log(consoleLifted > 0
                    ? $"[Console] Снято блокировок: {consoleLifted} (запрос: {args[0]})"
                    : $"[Console] Блокировок по запросу '{args[0]}' не найдено.");
                break;

            case "bans":
            case "banlist":
                if (_bans is null) { Alt.Log("[Console] Сервис блокировок не подключён."); return; }
                var allBans = _bans.GetAllBans().Where(b => b.IsActive)
                                   .OrderByDescending(b => b.BannedAtUtc).ToList();
                Alt.Log($"─── ДЕЙСТВУЮЩИЕ БЛОКИРОВКИ: {allBans.Count} ───");
                foreach (var b in allBans)
                {
                    var until = b.ExpiresAtUtc is null ? "навсегда" : $"до {b.ExpiresAtUtc:u}";
                    Alt.Log($"  • {b.Id} | {b.Username} | {b.Flags} | {until} | выдал: {b.AdminUsername} | {b.Reason}");
                }
                if (allBans.Count == 0) Alt.Log("  (список пуст)");
                break;

            case "license":
                CheckLicense(logAlways: true);
                Alt.Log($"[Console] Лимит игроков: {_license.PlayerLimit}. Файл: {FloVMP.Core.Licensing.LicenseFile.Locate() ?? "не найден (license.flv в корне установки)"}");
                break;

            case "online":
                var players = Alt.GetAllPlayers();
                Alt.Log($"[Console] Онлайн: {players.Count} игроков");
                foreach (var p in players)
                {
                    var isAdm = _adminLevels.TryGetValue(p.Id, out var lv) && lv > 0;
                    Alt.Log($"  [{p.Id}] {p.Name} (IP: {p.Ip}, SC: {p.SocialClubId}) {(isAdm ? $"[Админ Ур.{lv}]" : "")}");
                }
                break;
        }
    }

    /// <summary>Удалить машину, заспавненную этим администратором, если она есть.</summary>
    private void DestroyAdminVehicle(uint playerId)
    {
        if (!_adminVehicles.TryRemove(playerId, out var old)) return;
        try
        {
            if (old != null && old.Exists) old.Destroy();
        }
        catch (Exception ex)
        {
            Alt.LogWarning($"[FloV:MP] Не удалось удалить машину администратора: {ex.Message}");
        }
    }

    private void OnPlayerDisconnect(IPlayer player, string reason)
    {
        Alt.Log($"[FloV:MP] Игрок {player.Name} (ID: {player.Id}) отключился ({reason}).");
        var wasAdmin = _adminLevels.TryGetValue(player.Id, out var leftLevel) && leftLevel > 0;
        _clientReady.TryRemove(player.Id, out _);
        _chatRate.TryRemove(player.Id, out _);
        // Подтверждение пароля живёт одну сессию: после переподключения —
        // заново /alogin. Иначе тот, кто занял освободившийся ID, унаследовал бы
        // чужое подтверждение.
        _adminAuthed.TryRemove(player.Id, out _);
        DestroyAdminVehicle(player.Id);
        _adminLevels.TryRemove(player.Id, out _);
        _sessionAdminRanks.TryRemove(player.Id, out _);
        _godModes.TryRemove(player.Id, out _);
        _pendingRespawns.RemoveAll(r => r.Player == player);

        try
        {
            _spatialVoiceChannel?.RemovePlayer(player);
        }
        catch
        {
        }

        if (wasAdmin) BroadcastAdminRoster();
    }

    private void OnPlayerDead(IPlayer player, IEntity killer, uint weapon)
    {
        if (player == null || !player.Exists) return;
        Alt.Log($"[FloV:MP Starter] Игрок {player.Name} (ID: {player.Id}) погиб.");
        Alt.Emit("flovmp:player:died", player, killer, weapon);
        if (_platformRespawn)
            _pendingRespawns.Add((player, _clock.ElapsedMilliseconds + 3000));
    }

    private void OnSpawnSetting(float x, float y, float z, float heading)
    {
        if (!float.IsFinite(x) || !float.IsFinite(y) || !float.IsFinite(z) || !float.IsFinite(heading) ||
            Math.Abs(x) > 25000f || Math.Abs(y) > 25000f || Math.Abs(z) > 5000f)
        {
            Alt.LogWarning("[FloV:MP] [Mods] flovmp:settings:spawn: недопустимые координаты — точка появления не изменена.");
            return;
        }
        _spawnPosition = new Position(x, y, z);
        _spawnHeading = heading;
        Alt.Log(FormattableString.Invariant($"[FloV:MP] [Mods] точка появления: {x:F1}, {y:F1}, {z:F1}"));
    }

    private void OnRespawnSetting(bool platformRespawn)
    {
        _platformRespawn = platformRespawn;
        if (!platformRespawn) _pendingRespawns.Clear();
        Alt.Log(platformRespawn
            ? "[FloV:MP] [Mods] возрождение после смерти — платформой"
            : "[FloV:MP] [Mods] возрождение после смерти — своим ресурсом (событие flovmp:player:died)");
    }

    /// <summary>
    /// Снять дежурство с тех, у кого права отозваны или понижены (в базе или
    /// консолью). Только главный поток: трогает сущности игроков.
    /// </summary>
    private void ApplyRevokedAdminRights()
    {
        foreach (var (playerId, onDuty) in _adminLevels.ToArray())
        {
            var p = Alt.GetPlayerById(playerId);
            if (p is null || !p.Exists) { _adminLevels.TryRemove(playerId, out _); continue; }

            var assigned = GetAssignedAdminRank(p);
            if (assigned >= onDuty) continue;

            if (assigned <= 0)
            {
                _adminLevels.TryRemove(playerId, out _);

                _adminAuthed.TryRemove(playerId, out _);
                DestroyAdminVehicle(playerId);
                if (_godModes.TryRemove(playerId, out var hadGod) && hadGod) p.Emit("starter:setGodMode", false);
                PushAdminLevel(p, 0);
                SendChatMessage(p, "{ef4444}[Admin] Ваши права администратора отозваны.");
                Alt.Log($"[FloV:MP Admin] Права отозваны: [{p.Id}] {p.Name} (SC {p.SocialClubId}).");
            }
            else
            {
                _adminLevels[playerId] = assigned;
                PushAdminLevel(p, assigned);
                SendChatMessage(p, $"{{f59e0b}}[Admin] Ваш уровень прав изменён: {assigned}.");
                Alt.Log($"[FloV:MP Admin] Уровень понижен до {assigned}: [{p.Id}] {p.Name}.");
            }
        }
    }

    /// <summary>
    /// Раз в 30 с сверяет права и баны с базой в фоне (сеть не на главном
    /// потоке), а применение к игрокам возвращает на главный поток.
    /// </summary>
    private void TickStoreSync()
    {
        var now = _clock.ElapsedMilliseconds;
        if (now < _nextStoreSyncMs) return;
        _nextStoreSyncMs = now + StoreSyncIntervalMs;

        if (!_adminManager.HasStore && _bans is null) return;
        if (System.Threading.Interlocked.CompareExchange(ref _storeSyncRunning, 1, 0) != 0) return;

        Task.Run(() =>
        {
            try
            {
                _adminManager.RefreshFromStore();
                _bans?.RefreshFromStore();
            }
            catch (Exception ex)
            {
                Alt.LogWarning($"[FloV:MP] Фоновая сверка с базой: {ex.Message}");
            }
            finally
            {
                System.Threading.Interlocked.Exchange(ref _storeSyncRunning, 0);
                _pendingAdminRecheck = 1;
            }
        });
    }

    private int _pendingAdminRecheck;

    public override void OnTick()
    {
        TickStoreSync();

        var nowMs = _clock.ElapsedMilliseconds;
        if (nowMs >= _nextLicenseCheckMs)
        {
            if (_nextLicenseCheckMs != 0) CheckLicense(logAlways: false);
            _nextLicenseCheckMs = nowMs + LicenseRecheckMs;
        }
        if (System.Threading.Interlocked.Exchange(ref _pendingAdminRecheck, 0) == 1)
        {
            try { ApplyRevokedAdminRights(); }
            catch (Exception ex) { Alt.LogWarning($"[FloV:MP Admin] Проверка отозванных прав: {ex.Message}"); }
        }

        if (_pendingRespawns.Count > 0)
        {
            var now = _clock.ElapsedMilliseconds;
            for (int i = _pendingRespawns.Count - 1; i >= 0; i--)
            {
                var item = _pendingRespawns[i];
                if (now >= item.RespawnAtMs)
                {
                    _pendingRespawns.RemoveAt(i);
                    var p = item.Player;
                    if (p == null || !p.Exists || p.Health > 0) continue;

                    try
                    {
                        p.Spawn(_spawnPosition, 0);
                        p.Health = 200;
                        p.Armor = 100;
                        p.Emit("starter:revive");
                        SendChatMessage(p, "{ef4444}Вы погибли и возродились на спавне.");
                    }
                    catch (Exception ex)
                    {
                        Alt.Log($"[FloV:MP Starter] respawn error: {ex.Message}");
                    }
                }
            }
        }
    }

    private readonly ConcurrentDictionary<uint, bool> _godModes = new();

    private void OnChatMessage(IPlayer player, string message)
    {
        if (player == null || !player.Exists) return;
        if (string.IsNullOrWhiteSpace(message)) return;

        // Лимит проверяется ДО всего остального, в том числе до команд:
        // заваливать сервер можно и командами, а не только текстом.
        if (ChatRateLimited(player))
        {
            SendChatMessage(player, "{fde047}Не так быстро.");
            return;
        }

        if (message.Length > 256)
            message = message[..256];

        // Раньше здесь стоял комментарий «удаление управляющих символов», но сам
        // код их не удалял — только обрезал длину. Очистка общая с RP-режимом
        // (FloVMP.Core.Chat.ChatSanitizer) и покрыта тестами.
        var cleaned = FloVMP.Core.Chat.ChatSanitizer.CleanPlayerText(message);
        if (cleaned is null) return;
        message = cleaned;

        if (message.StartsWith("/"))
        {
            HandleCommand(player, message[1..]);
            return;
        }

        BroadcastChatMessage(message, "player", $"[{player.Id}] {player.Name}");
    }

    private void HandleCommand(IPlayer player, string commandLine)
    {
        try
        {
            var parts = commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return;

            var cmd = parts[0].ToLowerInvariant();
        switch (cmd)
        {
            case "help":
                SendChatMessage(player, "{38bdf8}─── СПИСОК КОМАНД СЕРВЕРА ───");
                SendChatMessage(player, "{e4e4e7}Чат и отыгровки: {a1a1aa}/me, /do, /b (OOC), /s (крик), /w <id> (шепот), /clear");
                SendChatMessage(player, "{e4e4e7}Транспорт: {a1a1aa}/engine (2), /lock (L)");
                SendChatMessage(player, "{e4e4e7}Общие: {a1a1aa}/pos (координаты), /alogin <пароль>, /claimowner <токен>");
                if (IsAdmin(player, 1))
                {
                    SendChatMessage(player, "{34d399}Администрация: {a1a1aa}/tpm (F5), /noclip (F4), /esp [0-3] (F3), /car [модель], /fix, /dv, /gun [название], /disarm, /tp <x y z>, /goto <id>, /gethere <id>, /freeze <id>, /unfreeze <id>, /revive [id], /heal, /armor, /god, /kill, /weather, /time, /speed, /setdim, /skin, /kick, /a (админ-чат)");
                }
                if (IsAdmin(player, 2))
                {
                    SendChatMessage(player, "{f87171}Модерация (2+): {a1a1aa}/kick <id> [причина], /bans, /vmute <id>");
                    SendChatMessage(player, "{f87171}Блокировки: {a1a1aa}/bans (список), /ban <id> <дней> [причина] (3), /banip <id> <дней> (4), /hwidban <id> <дней> (4), /hardban <id> <причина> (6, навсегда), /unban <ник|IP|HWID|ID бана> (4)");
                    SendChatMessage(player, "{f87171}Голос: {a1a1aa}/vmute <id> — заглушить или вернуть голос игроку (переключатель)");
                }
                if (IsAdmin(player, 8))
                {
                    SendChatMessage(player, "{fde047}Главный Администратор: {a1a1aa}/setadmin <id> <lvl 0-8>");
                }
                var visibleModCommands = _modCommands
                    .Where(kv => kv.Value.MinLevel == 0 || IsAdmin(player, kv.Value.MinLevel))
                    .OrderBy(kv => kv.Key)
                    .Select(kv => string.IsNullOrEmpty(kv.Value.Description) ? "/" + kv.Key : $"/{kv.Key} — {kv.Value.Description}")
                    .ToList();
                if (visibleModCommands.Count > 0)
                    SendChatMessage(player, "{c4b5fd}Сервер: {a1a1aa}" + string.Join(", ", visibleModCommands));
                break;

            case "me":
                if (parts.Length < 2)
                {
                    SendChatMessage(player, "{fde047}Использование: /me <действие персонажа>");
                    return;
                }
                var meAction = string.Join(' ', parts.Skip(1));
                // Действие персонажа видят рядом стоящие. Раньше /me уходило
                // всему серверу: и по смыслу RP неверно, и при 2000 игроках
                // каждое /me превращалось в 2000 отправок.
                SendNearby(player, RpActionRadius, meAction, "me", player.Name);
                break;

            case "do":
                if (parts.Length < 2)
                {
                    SendChatMessage(player, "{fde047}Использование: /do <описание ситуации/окружения>");
                    return;
                }
                var doAction = string.Join(' ', parts.Skip(1));
                SendNearby(player, RpActionRadius, doAction, "do", player.Name);
                break;

            case "b":
            case "ooc":
                if (parts.Length < 2)
                {
                    SendChatMessage(player, "{fde047}Использование: /b <OOC сообщение>");
                    return;
                }
                var oocText = string.Join(' ', parts.Skip(1));
                BroadcastChatMessage(oocText, "ooc", $"[{player.Id}] {player.Name}");
                break;

            case "s":
            case "shout":
                if (parts.Length < 2)
                {
                    SendChatMessage(player, "{fde047}Использование: /s <крик>");
                    return;
                }
                var shoutText = string.Join(' ', parts.Skip(1));
                SendNearby(player, ShoutRadius, shoutText, "shout", $"[{player.Id}] {player.Name}");
                break;

            case "w":
            case "whisper":
                if (parts.Length < 3 || !uint.TryParse(parts[1], out var wId))
                {
                    SendChatMessage(player, "{fde047}Использование: /w <ID игрока> <сообщение>");
                    return;
                }
                var wTarget = Alt.GetPlayerById(wId);
                if (wTarget == null)
                {
                    SendChatMessage(player, "{ef4444}Игрок с таким ID не найден.");
                    return;
                }
                var wMsg = string.Join(' ', parts.Skip(2));
                SendChatMessage(wTarget, wMsg, "whisper", $"[{player.Id}] {player.Name}");
                SendChatMessage(player, $"[для [{wTarget.Id}] {wTarget.Name}]: {wMsg}", "whisper", "Вы");
                break;

            case "a":
            case "admin":
                if (!IsAdmin(player, 1))
                {
                    SendChatMessage(player, "{ef4444}[FloV:MP Security] У вас нет прав для использования админ-чата.");
                    return;
                }
                if (parts.Length < 2)
                {
                    SendChatMessage(player, "{fde047}Использование: /a <сообщение для администрации>");
                    return;
                }
                var aMsg = string.Join(' ', parts.Skip(1));
                var adminLvl = _adminLevels.TryGetValue(player.Id, out var al) ? al : 1;
                foreach (var p in Alt.GetAllPlayers())
                {
                    if (IsAdmin(p, 1))
                    {
                        SendChatMessage(p, aMsg, "admin", $"[{player.Id}] {player.Name} (Ур.{adminLvl})");
                    }
                }
                break;

            case "pos":
            case "coords":
                SendChatMessage(player, $"{{38bdf8}}Координаты: X: {player.Position.X:F2}, Y: {player.Position.Y:F2}, Z: {player.Position.Z:F2}, Yaw: {player.Rotation.Yaw:F2}");
                player.Emit("starter:copyCoords", player.Position.X, player.Position.Y, player.Position.Z, player.Rotation.Yaw);
                break;

            case "claimowner":
                if (parts.Length < 2)
                {
                    SendChatMessage(player, "{fde047}Использование: /claimowner <токен>");
                    return;
                }
                var claimToken = parts[1];
                if (_adminManager.TryClaimOwner(claimToken, player.Name, player.SocialClubId, out var claimMsg))
                {
                    SetAssignedAdminRank(player, 8);
                    _adminLevels[player.Id] = 8;
                    PushAdminLevel(player, 8);
                    SendChatMessage(player, $"{{34d399}}[FloV:MP Security] {claimMsg}");
                    Alt.Log($"[Security Alert] Игрок {player.Name} (ID: {player.Id}) успешно активировал права Основателя через токен.");
                }
                else
                {
                    SendChatMessage(player, $"{{ef4444}}[FloV:MP Security] {claimMsg}");
                }
                break;

            case "alogin":
            case "adminauth":
                var assignedRank = GetAssignedAdminRank(player);
                if (assignedRank <= 0)
                {
                    SendChatMessage(player, "{ef4444}[FloV:MP Security] У вас нет прав администратора на этом сервере.");
                    return;
                }
                var aKey = AloginKey(player);
                if (assignedRank != 8 && _aloginFailures.TryGetValue(aKey, out var af) && af.LockedUntil > DateTime.UtcNow)
                {
                    var left = (int)Math.Ceiling((af.LockedUntil - DateTime.UtcNow).TotalMinutes);
                    SendChatMessage(player, $"{{ef4444}}[FloV:MP Security] Слишком много неверных попыток. Повторите через {left} мин.");
                    return;
                }
                var pwdOk = !string.IsNullOrEmpty(AdminPassword)
                            && parts.Length > 1
                            && string.Equals(parts[1], AdminPassword, StringComparison.Ordinal);
                if (assignedRank == 8 || pwdOk)
                {
                    _aloginFailures.TryRemove(aKey, out _);
                    _adminAuthed[player.Id] = true;
                    _adminLevels[player.Id] = assignedRank;
                    PushAdminLevel(player, assignedRank);
                    SendChatMessage(player, $"{{34d399}}[FloV:MP Security] Авторизация успешна! Вход на дежурство выполнен (Уровень {assignedRank}). Админ-функции и F8 разблокированы.");
                    Alt.Log($"[Security] Администратор {player.Name} (ID: {player.Id}, Уровень: {assignedRank}) заступил на дежурство.");
                }
                else
                {
                    _aloginFailures.AddOrUpdate(aKey,
                        _ => (1, DateTime.MinValue),
                        (_, cur) => cur.LockedUntil != DateTime.MinValue && cur.LockedUntil <= DateTime.UtcNow
                            ? (1, DateTime.MinValue)
                            : (cur.Failures + 1, cur.Failures + 1 >= AloginMaxFailures ? DateTime.UtcNow + AloginLockout : DateTime.MinValue));
                    SendChatMessage(player, string.IsNullOrEmpty(AdminPassword)
                        ? "{ef4444}[FloV:MP Security] Пароль администратора на сервере не задан (FLOVMP_ADMIN_PASSWORD) — заступайте на дежурство командой /aduty."
                        : "{ef4444}[FloV:MP Security] Неверный пароль администратора!");
                    Alt.LogWarning($"[Security Alert] Неудачная попытка авторизации /alogin от {player.Name} (ID: {player.Id})");
                }
                break;

            case "aduty":
                var dRank = GetAssignedAdminRank(player);
                if (dRank <= 0)
                {
                    SendChatMessage(player, "{ef4444}[FloV:MP Security] У вас нет прав администратора.");
                    return;
                }
                var isDuty = _adminLevels.TryGetValue(player.Id, out var curDuty) && curDuty > 0;
                if (isDuty)
                {
                    _adminLevels[player.Id] = 0;
                    PushAdminLevel(player, 0);
                    SendChatMessage(player, "{fde047}[Admin]{ffffff} Вы вышли с дежурства администрации.");
                }
                else
                {
                    // Выйти с дежурства можно всегда, а ЗАСТУПИТЬ — только после
                    // /alogin, если пароль администратора настроен. Исключения:
                    //   * владелец (уровень 8) — /alogin и сам пускает его без пароля;
                    //   * пароль не задан вовсе — тогда второго фактора нет, и
                    //     закрыть /aduty значило бы запереть младших админов.
                    var passwordConfigured = !string.IsNullOrEmpty(AdminPassword);
                    var mayGoOnDuty = !passwordConfigured
                                      || dRank == 8
                                      || _adminAuthed.ContainsKey(player.Id);
                    if (!mayGoOnDuty)
                    {
                        SendChatMessage(player, "{fde047}[Admin]{ffffff} Сначала подтвердите пароль: {fde047}/alogin <пароль>");
                        Alt.LogWarning($"[Security Alert] Попытка заступить на дежурство без пароля: {player.Name} (ID: {player.Id})");
                        return;
                    }

                    _adminLevels[player.Id] = dRank;
                    PushAdminLevel(player, dRank);
                    SendChatMessage(player, $"{{34d399}}[Admin]{{ffffff}} Вы заступили на дежурство (Уровень {dRank}).");
                }
                break;

            case "setadmin":
                if (!IsAdmin(player, 8))
                {
                    SendChatMessage(player, "{ef4444}[FloV:MP Security] Доступ запрещен (требуется Уровень 8).");
                    return;
                }
                if (parts.Length < 3 || !uint.TryParse(parts[1], out var targetId) || !int.TryParse(parts[2], out var targetLvl))
                {
                    SendChatMessage(player, "{fde047}Использование: /setadmin <ID> <Уровень 0-8>");
                    return;
                }
                var target = Alt.GetPlayerById(targetId);
                if (target == null)
                {
                    SendChatMessage(player, "{ef4444}Игрок с таким ID не найден.");
                    return;
                }
                targetLvl = Math.Clamp(targetLvl, 0, 8);
                if (target.Id != player.Id && (GetAssignedAdminRank(target) >= GetAssignedAdminRank(player)
                                               || _adminManager.IsFounder(target.SocialClubId, target.Name)))
                {
                    SendChatMessage(player, "{ef4444}Нельзя менять права администратора равного или большего уровня (и Основателя). Это делается из консоли сервера.");
                    return;
                }
                SetAssignedAdminRank(target, targetLvl);
                _adminLevels[target.Id] = targetLvl;
                PushAdminLevel(target, targetLvl);
                SendChatMessage(target, $"{{34d399}}[Admin] Администратор {player.Name} установил вам уровень доступа {targetLvl}.");
                SendChatMessage(player, $"{{34d399}}Установлен уровень {targetLvl} для {target.Name}.");
                break;

            case "esp":
                if (!IsAdmin(player, 1))
                {
                    SendChatMessage(player, "{ef4444}[FloV:MP Security] У вас нет прав для включения ESP.");
                    return;
                }
                if (parts.Length > 1 && int.TryParse(parts[1], out var targetMode))
                {
                    player.Emit("flovmp:admin:toggleEsp", Math.Clamp(targetMode, 0, 3));
                }
                else
                {
                    player.Emit("flovmp:admin:toggleEsp");
                }
                break;

            case "tpm":
                if (!IsAdmin(player, 1))
                {
                    SendChatMessage(player, "{ef4444}[FloV:MP Security] У вас нет прав для телепортации.");
                    return;
                }
                player.Emit("starter:requestWaypointTp");
                break;

            case "tp":
                if (!IsAdmin(player, 1))
                {
                    SendChatMessage(player, "{ef4444}[FloV:MP Security] У вас нет прав для телепортации.");
                    return;
                }
                if (parts.Length < 4 || !float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var tpx)
                    || !float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var tpy)
                    || !float.TryParse(parts[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var tpz))
                {
                    SendChatMessage(player, "{fde047}Использование: /tp <X> <Y> <Z>");
                    return;
                }
                if (float.IsNaN(tpx) || float.IsNaN(tpy) || float.IsNaN(tpz) ||
                    float.IsInfinity(tpx) || float.IsInfinity(tpy) || float.IsInfinity(tpz) ||
                    Math.Abs(tpx) > 25000f || Math.Abs(tpy) > 25000f || Math.Abs(tpz) > 5000f)
                {
                    SendChatMessage(player, "{ef4444}Недопустимые координаты.");
                    return;
                }
                player.Position = new Position(tpx, tpy, tpz + 0.5f);
                SendChatMessage(player, $"{{34d399}}Телепортирован на координаты: {tpx:F1}, {tpy:F1}, {tpz:F1}");
                break;

            case "goto":
                if (!IsAdmin(player, 1))
                {
                    SendChatMessage(player, "{ef4444}[FloV:MP Security] У вас нет прав.");
                    return;
                }
                if (parts.Length < 2 || !uint.TryParse(parts[1], out var gotoId))
                {
                    SendChatMessage(player, "{fde047}Использование: /goto <ID игрока>");
                    return;
                }
                var gotoTarget = Alt.GetPlayerById(gotoId);
                if (gotoTarget == null || !gotoTarget.Exists)
                {
                    SendChatMessage(player, "{ef4444}Игрок с таким ID не найден.");
                    return;
                }
                if (gotoTarget.Id == player.Id)
                {
                    SendChatMessage(player, "{fde047}Это вы.");
                    return;
                }
                // Измерение переносится вместе с координатами. Раньше менялась
                // только позиция: если игрок был в интерьере (своё измерение),
                // администратор прилетал в те же координаты, но в общий мир —
                // в пустоту, и цели не видел.
                player.Dimension = gotoTarget.Dimension;
                player.Position = new Position(gotoTarget.Position.X, gotoTarget.Position.Y + 1.0f, gotoTarget.Position.Z);
                SendChatMessage(player, $"{{34d399}}Вы телепортировались к {gotoTarget.Name} (ID: {gotoTarget.Id})");
                break;

            case "gethere":
                if (!IsAdmin(player, 1))
                {
                    SendChatMessage(player, "{ef4444}[FloV:MP Security] У вас нет прав.");
                    return;
                }
                if (parts.Length < 2 || !uint.TryParse(parts[1], out var gethereId))
                {
                    SendChatMessage(player, "{fde047}Использование: /gethere <ID игрока>");
                    return;
                }
                var gethereTarget = Alt.GetPlayerById(gethereId);
                if (gethereTarget == null || !gethereTarget.Exists)
                {
                    SendChatMessage(player, "{ef4444}Игрок с таким ID не найден.");
                    return;
                }
                if (gethereTarget.Id == player.Id)
                {
                    SendChatMessage(player, "{fde047}Это вы.");
                    return;
                }
                // Измерение администратора переносится на игрока — иначе игрок
                // из интерьера оказывался бы рядом по координатам, но в другом
                // мире, и они друг друга не видели.
                if (!CanActOn(player, gethereTarget))
                {
                    SendChatMessage(player, "{ef4444}Нельзя телепортировать администратора равного или большего уровня.");
                    return;
                }
                gethereTarget.Dimension = player.Dimension;
                gethereTarget.Position = new Position(player.Position.X + 1.0f, player.Position.Y, player.Position.Z);
                SendChatMessage(player, $"{{34d399}}Игрок {gethereTarget.Name} телепортирован к вам.");
                SendChatMessage(gethereTarget, $"{{34d399}}Администратор {player.Name} телепортировал вас к себе.");
                break;

            case "freeze":
            case "unfreeze":
                if (!IsAdmin(player, 1))
                {
                    SendChatMessage(player, "{ef4444}[FloV:MP Security] У вас нет прав.");
                    return;
                }
                if (parts.Length < 2 || !uint.TryParse(parts[1], out var frzId))
                {
                    SendChatMessage(player, $"{{fde047}}Использование: /{cmd} <ID игрока>");
                    return;
                }
                var frzTarget = Alt.GetPlayerById(frzId);
                if (frzTarget == null)
                {
                    SendChatMessage(player, "{ef4444}Игрок не найден.");
                    return;
                }
                if (!CanActOn(player, frzTarget))
                {
                    SendChatMessage(player, "{ef4444}Нельзя заморозить администратора равного или большего уровня.");
                    return;
                }
                var isFreeze = cmd == "freeze";
                frzTarget.Emit("starter:setFrozen", isFreeze);
                SendChatMessage(player, isFreeze ? $"{{34d399}}Игрок {frzTarget.Name} заморожен." : $"{{34d399}}Игрок {frzTarget.Name} разморожен.");
                SendChatMessage(frzTarget, isFreeze ? "{ef4444}Вы были заморожены администратором." : "{34d399}Вы были разморожены администратором.");
                break;

            case "car":
            case "veh":
                if (!IsAdmin(player, 1))
                {
                    SendChatMessage(player, "{ef4444}[FloV:MP Security] У вас нет прав для спавна транспорта.");
                    return;
                }
                var modelName = parts.Length > 1 ? parts[1] : "adder";

                // Имя модели уходит и в Alt.Hash, и обратно в чат. Фигурные
                // скобки в чате — это цветовые коды, поэтому их из ввода убираем:
                // иначе «/car {ff0000}…» раскрашивал бы сообщение.
                modelName = new string(modelName.Where(ch => char.IsLetterOrDigit(ch) || ch == '_').ToArray());
                if (modelName.Length == 0 || modelName.Length > 32)
                {
                    SendChatMessage(player, "{fde047}Использование: /car <модель, например adder>");
                    return;
                }

                try
                {
                    DestroyAdminVehicle(player.Id);

                    var spawnPos = new Position(player.Position.X + 2f, player.Position.Y + 2f, player.Position.Z);
                    var veh = Alt.CreateVehicle(Alt.Hash(modelName), spawnPos, player.Rotation);
                    veh.Dimension = player.Dimension;
                    veh.NumberplateText = "FLOVMP";
                    veh.EngineOn = true;
                    _adminVehicles[player.Id] = veh;
                    SendChatMessage(player, $"{{34d399}}Создан транспорт: {modelName} (Гос. номер: FLOVMP). Прежняя ваша машина убрана.");
                }
                catch (Exception ex)
                {
                    SendChatMessage(player, $"{{ef4444}}Ошибка спавна транспорта: {ex.Message}");
                }
                break;

            case "fix":
            case "repair":
                if (!IsAdmin(player, 1))
                {
                    SendChatMessage(player, "{ef4444}[FloV:MP Security] У вас нет прав.");
                    return;
                }
                IVehicle? targetVeh = player.Vehicle;
                if (targetVeh == null || !targetVeh.Exists)
                {
                    var pPos = player.Position;
                    var pDim = player.Dimension;
                    targetVeh = Alt.GetAllVehicles().FirstOrDefault(v => v.Exists && v.Dimension == pDim && v.Position.Distance(pPos) <= 6.0f);
                }
                if (targetVeh != null && targetVeh.Exists)
                {
                    targetVeh.Repair();
                    SendChatMessage(player, "{34d399}Транспорт отремонтирован.");
                }
                else
                {
                    SendChatMessage(player, "{fde047}Вы должны находиться в транспорте или рядом с ним (до 6м) для починки.");
                }
                break;

            case "dv":
            case "delveh":
            case "destroyveh":
                if (!IsAdmin(player, 1))
                {
                    SendChatMessage(player, "{ef4444}[FloV:MP Security] У вас нет прав.");
                    return;
                }
                IVehicle? delTarget = player.Vehicle;
                if (delTarget == null || !delTarget.Exists)
                {
                    var pPos = player.Position;
                    var pDim = player.Dimension;
                    delTarget = Alt.GetAllVehicles().FirstOrDefault(v => v.Exists && v.Dimension == pDim && v.Position.Distance(pPos) <= 6.0f);
                }
                if (delTarget != null && delTarget.Exists)
                {
                    delTarget.Destroy();
                    SendChatMessage(player, "{34d399}[Транспорт] Транспортное средство успешно удалено.");
                }
                else
                {
                    SendChatMessage(player, "{fde047}[Транспорт] Вы должны находиться в транспорте или рядом с ним (до 6м).");
                }
                break;

            case "weapon":
            case "gun":
            case "givegun":
                if (!IsAdmin(player, 1))
                {
                    SendChatMessage(player, "{ef4444}[FloV:MP Security] У вас нет прав.");
                    return;
                }
                var wepName = parts.Length > 1 ? parts[1] : "weapon_pistol";
                // Имя уходит обратно в чат: фигурные скобки там — цветовые коды.
                wepName = new string(wepName.Where(ch => char.IsLetterOrDigit(ch) || ch == '_').ToArray());
                if (wepName.Length == 0 || wepName.Length > 40)
                {
                    SendChatMessage(player, "{fde047}Использование: /gun <weapon_pistol> [патроны 1-9999]");
                    return;
                }
                if (!wepName.StartsWith("weapon_", StringComparison.OrdinalIgnoreCase)) wepName = "weapon_" + wepName;
                // Отрицательное или огромное число патронов уходило в игру как есть.
                var ammo = parts.Length > 2 && int.TryParse(parts[2], out var a) ? Math.Clamp(a, 1, 9999) : 250;
                try
                {
                    var wepHash = Alt.Hash(wepName);
                    player.GiveWeapon(wepHash, ammo, true);
                    SendChatMessage(player, $"{{34d399}}Выдано оружие: {wepName} (патронов: {ammo})");
                }
                catch (Exception ex)
                {
                    SendChatMessage(player, $"{{ef4444}}Ошибка выдачи оружия: {ex.Message}");
                }
                break;

            case "disarm":
            case "removeweapons":
                if (!IsAdmin(player, 1))
                {
                    SendChatMessage(player, "{ef4444}[FloV:MP Security] У вас нет прав.");
                    return;
                }
                IPlayer disarmTarget = player;
                if (parts.Length > 1 && uint.TryParse(parts[1], out var dId))
                {
                    var foundD = Alt.GetPlayerById(dId);
                    if (foundD == null)
                    {
                        SendChatMessage(player, "{ef4444}Игрок с таким ID не найден.");
                        return;
                    }
                    disarmTarget = foundD;
                }
                if (!CanActOn(player, disarmTarget))
                {
                    SendChatMessage(player, "{ef4444}Нельзя разоружить администратора равного или большего уровня.");
                    return;
                }
                disarmTarget.RemoveAllWeapons(true);
                SendChatMessage(player, $"{{34d399}}Все оружие у игрока {disarmTarget.Name} изъято.");
                break;

            case "engine":
                if (player.Vehicle != null && player.Vehicle.Exists)
                {
                    if (player.Vehicle.Driver != player)
                    {
                        SendChatMessage(player, "{fde047}[Транспорт] Управлять зажиганием может только водитель.");
                        return;
                    }
                    player.Vehicle.EngineOn = !player.Vehicle.EngineOn;
                    var engStatus = player.Vehicle.EngineOn ? "Двигатель заведён." : "Двигатель заглушен.";
                    SendChatMessage(player, $"{{34d399}}[Транспорт] {engStatus}");
                }
                else
                {
                    SendChatMessage(player, "{fde047}[Транспорт] Вы должны находиться в транспортном средстве.");
                }
                break;

            case "lock":
                IVehicle? lockVeh = player.Vehicle;
                if (lockVeh == null || !lockVeh.Exists)
                {
                    var pPos = player.Position;
                    var pDim = player.Dimension;
                    lockVeh = Alt.GetAllVehicles().FirstOrDefault(v => v.Exists && v.Dimension == pDim && v.Position.Distance(pPos) <= 6.0f);
                }
                if (lockVeh != null && lockVeh.Exists)
                {
                    lockVeh.LockState = lockVeh.LockState == AltV.Net.Enums.VehicleLockState.Locked
                        ? AltV.Net.Enums.VehicleLockState.Unlocked
                        : AltV.Net.Enums.VehicleLockState.Locked;
                    var isLocked = lockVeh.LockState == AltV.Net.Enums.VehicleLockState.Locked;
                    SendChatMessage(player, isLocked ? "{f87171}[Транспорт] Двери заблокированы." : "{34d399}[Транспорт] Двери разблокированы.");
                }
                else
                {
                    SendChatMessage(player, "{fde047}[Транспорт] Поблизости нет транспортного средства.");
                }
                break;

            case "noclip":
            case "fly":
                if (!IsAdmin(player, 1))
                {
                    SendChatMessage(player, "{ef4444}[FloV:MP Security] У вас нет прав для включения NoClip.");
                    return;
                }
                player.Emit("starter:toggleNoClip");
                break;

            case "heal":
                if (!IsAdmin(player, 1))
                {
                    SendChatMessage(player, "{ef4444}[FloV:MP Security] У вас нет прав для лечения.");
                    return;
                }
                player.Health = 200;
                player.Armor = 100;
                SendChatMessage(player, "{34d399}Здоровье и броня восстановлены до 100%.");
                break;

            case "revive":
                if (!IsAdmin(player, 1))
                {
                    SendChatMessage(player, "{ef4444}[FloV:MP Security] У вас нет прав для реанимации.");
                    return;
                }
                IPlayer targetRevive = player;
                if (parts.Length > 1 && uint.TryParse(parts[1], out var revId))
                {
                    var foundTarget = Alt.GetPlayerById(revId);
                    if (foundTarget == null)
                    {
                        SendChatMessage(player, "{ef4444}Игрок с указанным ID не найден.");
                        return;
                    }
                    targetRevive = foundTarget;
                }
                _pendingRespawns.RemoveAll(r => r.Player == targetRevive);
                targetRevive.Spawn(targetRevive.Position, 0);
                targetRevive.Health = 200;
                targetRevive.Armor = 100;
                targetRevive.Emit("starter:revive");
                SendChatMessage(player, $"{{34d399}}Игрок {targetRevive.Name} успешно реанимирован.");
                if (targetRevive != player)
                {
                    SendChatMessage(targetRevive, $"{{34d399}}Администратор {player.Name} реанимировал вас.");
                }
                break;

            case "armor":
                if (!IsAdmin(player, 1))
                {
                    SendChatMessage(player, "{ef4444}[FloV:MP Security] У вас нет прав.");
                    return;
                }
                // Броня в GTA — 0..100; раньше принималось до 65535.
                var armorVal = parts.Length > 1 && ushort.TryParse(parts[1], out var arm) ? Math.Min(arm, (ushort)100) : (ushort)100;
                player.Armor = armorVal;
                SendChatMessage(player, $"{{34d399}}Броня установлена на {armorVal}.");
                break;

            case "god":
            case "godmode":
                if (!IsAdmin(player, 1))
                {
                    SendChatMessage(player, "{ef4444}[FloV:MP Security] У вас нет прав.");
                    return;
                }
                var currentGod = _godModes.GetOrAdd(player.Id, false);
                var newGod = !currentGod;
                _godModes[player.Id] = newGod;
                player.Emit("starter:setGodMode", newGod);
                SendChatMessage(player, newGod ? "{34d399}Режим бога (GodMode) ВКЛЮЧЕН." : "{fde047}Режим бога (GodMode) ВЫКЛЮЧЕН.");
                break;

            case "kill":
            case "suicide":
                if (!IsAdmin(player, 1))
                {
                    SendChatMessage(player, "{ef4444}[FloV:MP Security] У вас нет прав.");
                    return;
                }
                player.Health = 0;
                SendChatMessage(player, "{ef4444}Вы погибли.");
                break;

            case "speed":
                if (!IsAdmin(player, 1))
                {
                    SendChatMessage(player, "{ef4444}[FloV:MP Security] У вас нет прав.");
                    return;
                }
                var speedMult = parts.Length > 1 && float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var sm) ? sm : 1.0f;
                // GTA принимает множитель бега только в пределах 1.0–1.49; клиент
                // и так его зажимает. Но сервер раньше отвечал админу исходным
                // числом — «множитель: 1000.00» при реально применённом 1.49, —
                // то есть сообщал неправду о том, что произошло.
                if (float.IsNaN(speedMult) || float.IsInfinity(speedMult)) speedMult = 1.0f;
                var appliedSpeed = Math.Clamp(speedMult, 1.0f, 1.49f);
                player.Emit("starter:setSpeed", appliedSpeed);
                SendChatMessage(player, Math.Abs(appliedSpeed - speedMult) > 0.001f
                    ? $"{{fde047}}Множитель скорости бега: {appliedSpeed:F2} (игра допускает только 1.00–1.49)"
                    : $"{{38bdf8}}Множитель скорости бега: {appliedSpeed:F2}");
                break;

            case "weather":
                if (!IsAdmin(player, 1))
                {
                    SendChatMessage(player, "{ef4444}[FloV:MP Security] У вас нет прав для смены погоды.");
                    return;
                }
                if (parts.Length > 1)
                {
                    var weatherType = parts[1].ToUpperInvariant();
                    // Погода рассылается ВСЕМ игрокам и объявляется в общий чат,
                    // поэтому проверяется по списку. Раньше любая строка уходила
                    // каждому клиенту в нативную функцию: одна опечатка админа
                    // ломала погоду всему серверу, а фигурные скобки во вводе
                    // раскрашивали сообщение в общем чате.
                    if (!ValidWeatherTypes.Contains(weatherType))
                    {
                        SendChatMessage(player, "{ef4444}Неизвестная погода. Доступно: " +
                                                string.Join(", ", ValidWeatherTypes));
                        return;
                    }
                    _worldWeather = weatherType;
                    Alt.EmitAllClients("starter:setWeather", weatherType);
                    BroadcastChatMessage($"{{38bdf8}}[Погода] Администратор установил погоду: {weatherType}");
                }
                else
                {
                    SendChatMessage(player, "{fde047}Использование: /weather <CLEAR|EXTRASUNNY|CLOUDS|RAIN|THUNDER|SNOW|XMAS>");
                }
                break;

            case "time":
                if (!IsAdmin(player, 1))
                {
                    SendChatMessage(player, "{ef4444}[FloV:MP Security] У вас нет прав для смены времени.");
                    return;
                }
                if (parts.Length > 1 && int.TryParse(parts[1], out var hour))
                {
                    var minute = parts.Length > 2 && int.TryParse(parts[2], out var m) ? m : 0;
                    // Время рассылается всем игрокам. Без проверки «/time 99 99»
                    // уходило каждому клиенту и объявлялось в общий чат как 99:99.
                    if (hour < 0 || hour > 23 || minute < 0 || minute > 59)
                    {
                        SendChatMessage(player, "{ef4444}Час должен быть от 0 до 23, минута — от 0 до 59.");
                        return;
                    }
                    _worldTime = (hour, minute);
                    Alt.EmitAllClients("starter:setTime", hour, minute);
                    BroadcastChatMessage($"{{38bdf8}}[Время] Администратор установил время: {hour:D2}:{minute:D2}");
                }
                else
                {
                    SendChatMessage(player, "{fde047}Использование: /time <час 0-23> [минута 0-59]");
                }
                break;

            case "setdim":
            case "dim":
            case "dimension":
                if (!IsAdmin(player, 1))
                {
                    SendChatMessage(player, "{ef4444}[FloV:MP Security] У вас нет прав.");
                    return;
                }
                if (parts.Length > 1 && int.TryParse(parts[1], out var dim))
                {
                    player.Dimension = dim;
                    SendChatMessage(player, $"{{38bdf8}}Измерение изменено на: {dim}");
                }
                else
                {
                    SendChatMessage(player, "{fde047}Использование: /setdim <номер измерения>");
                }
                break;

            case "skin":
            case "ped":
                if (!IsAdmin(player, 1))
                {
                    SendChatMessage(player, "{ef4444}[FloV:MP Security] У вас нет прав.");
                    return;
                }
                if (parts.Length > 1)
                {
                    var skinModel = new string(parts[1].Where(ch => char.IsLetterOrDigit(ch) || ch == '_').ToArray());
                    if (skinModel.Length == 0 || skinModel.Length > 40)
                    {
                        SendChatMessage(player, "{fde047}Использование: /skin <модель, напр. mp_m_freemode_01>");
                        return;
                    }
                    try
                    {
                        player.Model = Alt.Hash(skinModel);
                        SendChatMessage(player, $"{{34d399}}Скин изменен на: {skinModel}");
                    }
                    catch (Exception ex)
                    {
                        SendChatMessage(player, $"{{ef4444}}Ошибка смены скина: {ex.Message}");
                    }
                }
                else
                {
                    SendChatMessage(player, "{fde047}Использование: /skin <модель, напр. mp_m_freemode_01>");
                }
                break;

            case "kick":
                if (!IsAdmin(player, 2))
                {
                    SendChatMessage(player, "{ef4444}[FloV:MP Security] Доступ запрещен (требуется Уровень 2+).");
                    return;
                }
                if (parts.Length < 2 || !uint.TryParse(parts[1], out var kickId))
                {
                    SendChatMessage(player, "{fde047}Использование: /kick <ID> [причина]");
                    return;
                }
                var kickTarget = Alt.GetPlayerById(kickId);
                if (kickTarget == null)
                {
                    SendChatMessage(player, "{ef4444}Игрок с таким ID не найден.");
                    return;
                }
                if (!CanActOn(player, kickTarget))
                {
                    SendChatMessage(player, "{ef4444}Нельзя исключить администратора равного или большего уровня.");
                    return;
                }
                var reason = parts.Length > 2 ? string.Join(' ', parts[2..]) : "Исключен администратором";
                BroadcastChatMessage($"{{ef4444}}[Kick] {kickTarget.Name} был исключен администратором {player.Name}. Причина: {reason}");
                kickTarget.Kick(reason);
                break;

            // Блокировки. До их появления в платформе был только /kick, после
            // которого нарушитель возвращается через пять секунд.
            case "ban":
            case "banip":
            case "hwidban":
            case "hardban":
                HandleBanCommand(player, cmd, parts);
                break;

            // Голосовой мут. Раньше заглушить голос было нечем вообще: игрок,
            // кричащий в микрофон, останавливался только киком или баном.
            case "vmute":
            case "voicemute":
                if (!IsAdmin(player, 2))
                {
                    SendChatMessage(player, "{ef4444}[FloV:MP Security] Доступ запрещен (требуется Уровень 2+).");
                    return;
                }
                if (_spatialVoiceChannel is null)
                {
                    SendChatMessage(player, "{ef4444}Голосовой канал не создан — глушить нечего (см. [voice] в server.toml).");
                    return;
                }
                if (parts.Length < 2 || !uint.TryParse(parts[1], out var vmuteId))
                {
                    SendChatMessage(player, "{fde047}Использование: /vmute <ID>  (повторный вызов снимает мут)");
                    return;
                }
                var vmuteTarget = Alt.GetPlayerById(vmuteId);
                if (vmuteTarget == null)
                {
                    SendChatMessage(player, "{ef4444}Игрок с таким ID не найден.");
                    return;
                }
                if (!CanActOn(player, vmuteTarget))
                {
                    SendChatMessage(player, "{ef4444}Нельзя заглушить администратора равного или большего уровня.");
                    return;
                }
                try
                {
                    // Переключатель, а не отдельные /vmute и /vunmute: админу
                    // проще нажать одно и то же, чем помнить текущее состояние.
                    if (_spatialVoiceChannel.IsPlayerMuted(vmuteTarget))
                    {
                        _spatialVoiceChannel.UnmutePlayer(vmuteTarget);
                        SendChatMessage(player, $"{{34d399}}Голос игрока {vmuteTarget.Name} восстановлен.");
                        Alt.Log($"[FloV:MP] [Voice] {player.Name} снял голосовой мут с {vmuteTarget.Name}");
                    }
                    else
                    {
                        _spatialVoiceChannel.MutePlayer(vmuteTarget);
                        SendChatMessage(player, $"{{fde047}}Голос игрока {vmuteTarget.Name} заглушён.");
                        Alt.Log($"[FloV:MP] [Voice] {player.Name} заглушил голос {vmuteTarget.Name}");
                    }
                }
                catch (Exception ex)
                {
                    SendChatMessage(player, $"{{ef4444}}Не удалось изменить мут голоса: {ex.Message}");
                }
                break;

            case "unban":
                if (!IsAdmin(player, 4))
                {
                    SendChatMessage(player, "{ef4444}[FloV:MP Security] Доступ запрещен (требуется Уровень 4+).");
                    return;
                }
                if (parts.Length < 2)
                {
                    SendChatMessage(player, "{fde047}Использование: /unban <ник|IP|HWID|ID бана>");
                    return;
                }
                if (_bans is null)
                {
                    SendChatMessage(player, "{ef4444}Сервис блокировок не подключён.");
                    return;
                }
                var liftedCount = _bans.Unban(parts[1]);
                SendChatMessage(player, liftedCount > 0
                    ? $"{{34d399}}Снято блокировок: {liftedCount} (запрос: {parts[1]})."
                    : $"{{fde047}}Блокировок по запросу '{parts[1]}' не найдено.");
                if (liftedCount > 0)
                    Alt.Log($"[FloV:MP] [Ban] {player.Name} снял блокировок: {liftedCount} по запросу '{parts[1]}'");
                break;

            case "bans":
            case "banlist":
                if (!IsAdmin(player, 2))
                {
                    SendChatMessage(player, "{ef4444}[FloV:MP Security] Доступ запрещен (требуется Уровень 2+).");
                    return;
                }
                if (_bans is null)
                {
                    SendChatMessage(player, "{ef4444}Сервис блокировок не подключён.");
                    return;
                }
                var active = _bans.GetAllBans().Where(b => b.IsActive).ToList();
                SendChatMessage(player, $"{{60a5fa}}=== Действующие блокировки: {active.Count} ===");
                // Выводим не весь список: сотни строк в чате бесполезны и
                // забивают историю. Полный список — командой bans в консоли.
                foreach (var b in active.OrderByDescending(b => b.BannedAtUtc).Take(10))
                {
                    var until = b.ExpiresAtUtc is null ? "навсегда" : $"до {b.ExpiresAtUtc:dd.MM.yyyy HH:mm}";
                    SendChatMessage(player, $"{{d1d5db}}{b.Id} | {b.Username} | {b.Flags} | {until} | {b.Reason}");
                }
                if (active.Count > 10)
                    SendChatMessage(player, $"{{9ca3af}}...и ещё {active.Count - 10}. Полный список: команда bans в консоли сервера.");
                break;

            case "clear":
            case "cls":
                player.Emit("flovmp:chat:clear");
                SendChatMessage(player, "{a1a1aa}Чат очищен.");
                break;

            default:
                if (_modCommands.TryGetValue(cmd, out var modCommand))
                {
                    if (modCommand.MinLevel > 0 && !IsAdmin(player, modCommand.MinLevel))
                    {
                        SendChatMessage(player, $"{{ef4444}}[FloV:MP Security] Доступ запрещен (требуется Уровень {modCommand.MinLevel}+).");
                        break;
                    }
                    var argsLine = parts.Length > 1 ? string.Join(' ', parts.Skip(1)) : "";
                    Alt.Emit("flovmp:command", player, cmd, argsLine);
                    break;
                }
                SendChatMessage(player, $"{{a1a1aa}}Неизвестная команда: /{cmd}. Введите /help для списка доступных команд.");
                break;
        }
    }
    catch (Exception ex)
    {
        Alt.LogError($"[FloV:MP Command Error] {player?.Name}: {ex.Message}");
        if (player != null && player.Exists)
        {
            SendChatMessage(player, "{ef4444}[Ошибка] Произошла ошибка при обработке команды.");
        }
    }
}

    private void OnTeleportWaypoint(IPlayer player, float x, float y, float z)
    {
        if (!IsAdmin(player, 1))
        {
            Alt.LogWarning($"[Security Violation] Неавторизованный запрос teleportWaypoint от {player.Name} (ID: {player.Id})");
            SendChatMessage(player, "{ef4444}[FloV:MP Security] Телепортация отклонена сервером (недостаточно прав).");
            return;
        }

        if (float.IsNaN(x) || float.IsNaN(y) || float.IsNaN(z) ||
            float.IsInfinity(x) || float.IsInfinity(y) || float.IsInfinity(z) ||
            Math.Abs(x) > 25000f || Math.Abs(y) > 25000f || Math.Abs(z) > 5000f)
        {
            SendChatMessage(player, "{ef4444}[FloV:MP Security] Недопустимые координаты телепортации.");
            return;
        }

        player.Position = new Position(x, y, z + 1.0f);
        SendChatMessage(player, $"{{34d399}}Телепортация по метке: {x:F1}, {y:F1}, {z:F1}");
    }

    private void OnToggleNoClip(IPlayer player, bool enabled)
    {
        if (!IsAdmin(player, 1))
        {
            Alt.LogWarning($"[Security Violation] Неавторизованная попытка toggleNoClip от {player.Name} (ID: {player.Id})");
            SendChatMessage(player, "{ef4444}[FloV:MP Security] Полет NoClip отклонен сервером (недостаточно прав).");
            return;
        }

        SendChatMessage(player, enabled ? "{34d399}Админ-полет (NoClip) ВКЛЮЧЕН" : "{fde047}Админ-полет (NoClip) ВЫКЛЮЧЕН");
    }
}
