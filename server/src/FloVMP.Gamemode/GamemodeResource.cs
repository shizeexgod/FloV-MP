using System.Diagnostics;
using System.IO;
using AltV.Net;
using AltV.Net.Elements.Entities;
using FloVMP.Core.Auth;
using FloVMP.Core.Licensing;
using FloVMP.Core.Logging;
using FloVMP.Gamemode.Systems.AntiCheat;

namespace FloVMP.Gamemode;

/// <summary>
/// Точка входа C#-геймода FloV:MP. alt:V находит этот класс (единственный
/// наследник <see cref="Resource"/> в сборке) и вызывает <see cref="OnStart"/>
/// при старте ресурса, <see cref="OnStop"/> — при остановке/перезагрузке.
/// <see cref="OnTick"/> — каждый тик сервера.
///
/// Здесь только проводка систем. Все обработчики событий alt:V обёрнуты в
/// <see cref="Safe"/>: исключение в одной системе не роняет сервер.
/// </summary>
public class GamemodeResource : Resource
{
    private readonly string ServerName = Environment.GetEnvironmentVariable("FLOVMP_SERVER_NAME") ?? "RolePlay Server";
    private const int AutoSaveIntervalMs = 60_000;

    private PlayerLifecycle? _playerLifecycle;
    private AuthSystem? _auth;
    private HudSystem? _hud;
    private InventorySystem? _inv;
    private ChatSystem? _chat;
    private ConsoleCommands? _console;
    private FloVMP.Core.Economy.EconomyService? _economy;
    private LicenseClient? _licenseClient;
    private TelemetryReporter? _telemetry;
    private AntiCheatSystem? _antiCheat;
    private FloVMP.Core.Factions.FactionService? _factions;
    private FloVMP.Core.Documents.DocumentService? _documents;
    private FloVMP.Core.Housing.HousingService? _housing;
    private FloVMP.Core.Characters.FactionUniformService? _uniforms;
    private RemoteServerAgent? _agent;
    private FloVMP.Gamemode.Systems.Api.HttpApiSystem? _httpApi;
    private FloVMP.Core.Auth.IAccountStore? _accountStore;
    /// <summary>
    /// Радиус слышимости пространственного голоса, метры. Совпадает с базовой
    /// платформой — иначе игроки, привыкшие к одному серверу, на другом
    /// сталкиваются с другой дальностью и считают это багом.
    /// </summary>
    private const float VoiceRangeMeters = 25.0f;

    private IVoiceChannel? _voiceChannel;
    private FloVMP.Core.Security.IBanStore? _banStore;
    private FloVMP.Core.Security.MultiTierBanService? _bans;
    private FloVMP.Core.Spatial.AdaptiveTickManager<uint>? _tickManager;
    private FloVMP.Core.Spatial.OcclusionCullingService? _occlusion;
    
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private long _lastAutoSaveMs;
    private long _lastArrestTickMs;
    private long _lastTickScaleMs;
    private long _lastVehTickMs;
    private int _lastPayDayHour = -1;
    private long _lastBanSyncMs;

    /// <summary>
    /// Когда гасить сервер по команде /restart, в миллисекундах часов ресурса.
    /// 0 = перезапуск не запланирован.
    /// </summary>
    private long _restartAtMs;
    private int _banSyncInFlight;

    /// <summary>
    /// Как часто инстанс перечитывает общий список блокировок. 30 секунд —
    /// компромисс: бан с соседнего сервера доходит достаточно быстро, но
    /// запрос к БД не превращается в постоянную фоновую нагрузку.
    /// </summary>
    private const int BanSyncIntervalMs = 30_000;
    private readonly List<(IPlayer Player, Account? Account, long RespawnAtMs)> _pendingRespawns = new();

    public override void OnStart()
    {
        Alt.Log($"[FloV:MP] core: gamemode start (v{BuildInfo.Version})");

        // Диагностика платформы (режим БД, миграции, хранилище блокировок)
        // обязана попадать в server.log: обычный Console.WriteLine из ресурса
        // alt:V теряется, туда идёт только прошедшее через Alt.Log.
        FloVMP.Core.CoreConsole.Out = msg => Alt.Log(msg);
        FloVMP.Core.CoreConsole.Warn = msg => Alt.LogWarning(msg);

        var dataDir = Path.Combine(Directory.GetCurrentDirectory(), "flovmp-data");
        GameLog.Configure(new FileLogSink(Path.Combine(dataDir, "logs")));
        GameLog.System("gamemode_start", ("version", BuildInfo.Version));

        var dbConn = Environment.GetEnvironmentVariable("FLOVMP_DB_CONNECTION") ??
                     new FloVMP.Core.Database.DatabaseConfig().BuildConnectionString();
        var accountStore = FloVMP.Core.Database.AccountStoreFactory.Create(dbConn, Path.Combine(dataDir, "accounts.json"));
        _accountStore = accountStore; // нужен для гарантированной записи на OnStop

        // Многоуровневые блокировки (IP / Social Club / HWID / MAC).
        // Хранилище общее с аккаунтами: на MariaDB баны действуют на всех
        // инстансах, на локальном файле — только на этом (об этом фабрика
        // предупреждает в консоли при старте).
        _banStore = FloVMP.Core.Security.BanStoreFactory.Create(
            dbConn, Path.Combine(dataDir, "bans.json"));
        _bans = new FloVMP.Core.Security.MultiTierBanService(_banStore);

        // Пространственный голос. В RP-режиме его не было ВООБЩЕ: канал
        // создавался только в базовой платформе (FloVMP.Starter), поэтому при
        // переключении сервера в полный режим голос молча пропадал.
        // Радиус 25 м — как в платформе, чтобы поведение не расходилось.
        Safe.Run("core.voice.create", () =>
        {
            // Может вернуть null БЕЗ исключения, если в server.toml нет
            // секции [voice] — проверяем результат, а не отсутствие ошибки.
            _voiceChannel = Alt.CreateVoiceChannel(true, VoiceRangeMeters);
            if (_voiceChannel is not null)
                Alt.Log($"[FloV:MP] core: голосовой канал создан (радиус {VoiceRangeMeters} м)");
        });
        if (_voiceChannel is null)
            Alt.LogWarning("[FloV:MP] core: голосовой канал НЕ создан — проверьте секцию [voice] в server.toml " +
                           "и запущен ли altv-voice-server.");

        _auth = new AuthSystem(accountStore, OnPlayerAuthed, ServerName, _bans);
        _auth.Attach();

        // Встроенный HTTP-API (:7799) — авторизация для лаунчера + живой /info.
        // На Linux отдельного FloVMP.ServerLauncher нет, поэтому API здесь.
        int apiPort = int.TryParse(Environment.GetEnvironmentVariable("FLOVMP_API_PORT"), out var pp) ? pp : 7799;
        _httpApi = new FloVMP.Gamemode.Systems.Api.HttpApiSystem(
            store: accountStore,
            playerCount: () => Alt.GetAllPlayers().Count,
            maxPlayers: 2000,
            serverName: ServerName,
            gamemode: "RolePlay",
            log: msg => Alt.Log(msg),
            port: apiPort);
        _httpApi.Start();

        // Режим гейммода: base = ПЛАТФОРМА (спавн + чат + анти-чит + модерация,
        // без готового геймплея); full = RP-мод со всеми системами.
        // Платформа поставляется в base; full — опционально для RP.
        var gamemodeMode = (Environment.GetEnvironmentVariable("FLOVMP_MODE") ?? "base").Trim().ToLowerInvariant();
        bool fullMode = gamemodeMode == "full";
        Alt.Log($"[FloV:MP] core: режим гейммода = {(fullMode ? "full (RP-мод)" : "base (платформа)")}");

        if (fullMode)
        {
            _inv = new InventorySystem(Path.Combine(dataDir, "inventories.json"));
            _inv.Attach();
        }

        _antiCheat = new AntiCheatSystem(
            accountOf: p => _auth?.AccountOf(p),
            inventoryWeaponsOf: p => _inv?.GetAllowedWeapons(p));

        _playerLifecycle = new PlayerLifecycle(notifyTeleport: (p, pos) => _antiCheat?.NotifyAdminTeleport(p, pos));
        _playerLifecycle.Attach();

        // RP-геймплей (HUD денег, экономика, фракции, документы, жильё, униформы)
        // — только в full-режиме. В base их нет: сервер-владелец строит своё.
        // Все обращения к ним в OnTick/OnPlayerAuthed уже под null-guard.
        if (fullMode)
        {
            _hud = new HudSystem(ServerName);

            _economy = new FloVMP.Core.Economy.EconomyService();
            _factions = new FloVMP.Core.Factions.FactionService();
            Presets.DefaultFactions.RegisterAll(_factions);
            if (_inv != null)
            {
                _inv.IsCuffed = accId => _factions.IsCuffed(accId);
            }
            if (_antiCheat != null)
            {
                _antiCheat.IsCuffed = accId => _factions.IsCuffed(accId);
            }
            _documents = new FloVMP.Core.Documents.DocumentService();
            _housing = new FloVMP.Core.Housing.HousingService();
            Presets.DefaultHousing.RegisterAll(_housing);
            _uniforms = new FloVMP.Core.Characters.FactionUniformService();
            Presets.DefaultUniforms.RegisterAll(_uniforms);
            Presets.DefaultUniforms.RegisterAll(FloVMP.Core.Characters.FactionUniformService.Default);
        }

        _chat = new ChatSystem(
            accountOf: p => _auth.AccountOf(p),
            saveAccount: acc => _auth.SaveAccount(acc),
            findAccountByName: name => _auth.FindByIdentifier(name),
            economy: _economy,
            factions: _factions,
            documents: _documents,
            housing: _housing,
            inventory: _inv,
            notifyTeleport: (p, pos) => _antiCheat?.NotifyAdminTeleport(p, pos),
            setAdminExempt: (accId, exempt) => _antiCheat?.Service.SetAdminExemption(accId, exempt),
            // Остановка сервера обязана происходить на главном потоке.
            // Раньше это делал Task.Run: Alt.StopServer() вызывался из пула
            // потоков, пока главный крутил тик, — ровно тот же класс гонок в
            // нативной памяти движка, из-за которого уже переделывали респавн.
            // Теперь команда только ставит отметку времени, а гасит сервер
            // OnTick.
            restartServer: sec => _restartAtMs = _clock.ElapsedMilliseconds + Math.Max(0, sec) * 1000L,
            platformMode: !fullMode,
            bans: _bans,
            voiceChannel: () => _voiceChannel);
        _chat.Attach();

        _console = new ConsoleCommands(
            saveAll: () => _inv?.SaveAll(),
            broadcast: text => _chat?.Broadcast(text),
            nameOf: p => _auth?.AccountOf(p)?.Username);
        _console.Attach();
        Alt.Log($"[FloV:MP] core: data dir -> {dataDir}");

        // Лицензирование и телеметрия платформы
        var licConfig = LicenseConfig.FromEnvironment();
        _licenseClient = new LicenseClient(licConfig, cacheDir: dataDir);
        var licResult = _licenseClient.VerifyAsync().GetAwaiter().GetResult();
        if (licResult.IsValid)
        {
            Alt.Log($"[FloV:MP] [Security] Лицензия активна: {licResult.Plan.ToUpper()} (Слоты: {licResult.MaxPlayers})");
            if (licResult.IsCachedOffline)
            {
                Alt.Log($"[FloV:MP] [Security] Автономный режим: {licResult.ErrorMessage}");
            }
        }
        else
        {
            Alt.Log($"[FloV:MP] [Security] ОШИБКА ЛИЦЕНЗИИ: {licResult.ErrorMessage}");
            if (licConfig.StrictMode)
            {
                Alt.Log("[FloV:MP] [Security] Сервер остановлен из-за ошибки лицензии.");
                System.Environment.Exit(1);
            }
        }

        _tickManager = new FloVMP.Core.Spatial.AdaptiveTickManager<uint>();
        _occlusion = new FloVMP.Core.Spatial.OcclusionCullingService();

        _telemetry = new TelemetryReporter(licConfig)
        {
            GetPlayerCount = () => Alt.GetAllPlayers().Count,
            GetMaxPlayers = () => licResult.MaxPlayers,
            GetTickRate = () =>
            {
                var players = Alt.GetAllPlayers();
                if (players.Count == 0) return 60;
                int sum = 0;
                foreach (var p in players)
                {
                    if (p.Exists)
                        sum += _tickManager?.GetEffectiveTickRate(p.Id) ?? 60;
                }
                return Math.Max(10, sum / players.Count);
            },
            GetFps = () => 60,
            GetMemoryMb = () => System.GC.GetTotalMemory(false) / (1024 * 1024)
        };
        _telemetry.Start();

        var agentToken = Environment.GetEnvironmentVariable("FLOVMP_AGENT_TOKEN") ?? licConfig.LicenseKey;
        var commandApiUrl = Environment.GetEnvironmentVariable("FLOVMP_COMMAND_URL") ?? "http://localhost:3000/api/v1/agent/command";
        _agent = new RemoteServerAgent(agentToken, commandApiUrl);
        _agent.OnBroadcastRequested += async msg =>
        {
            _chat?.Broadcast($"[ОБЪЯВЛЕНИЕ /o] {msg}");
            Alt.Log($"[FloV:MP] [Dashboard] Broadcast: {msg}");
            return await Task.FromResult("OK");
        };
        _agent.Start();

        Alt.OnPlayerDisconnect += OnPlayerDisconnect;
        Alt.OnPlayerDead += OnPlayerDead;
        Alt.OnServerStarted += OnServerStarted;

        Alt.Log("[FloV:MP] core: systems attached, waiting for players");
    }

    public override void OnStop()
    {
        // Аккаунты пишутся в фоне с дебаунсом (чтобы не блокировать игровой тик),
        // поэтому на остановке обязаны принудительно сбросить их на диск.
        Safe.Run("core.OnStop.scheduler", Systems.MainThreadScheduler.Clear);
        Safe.Run("core.OnStop.accounts", () => (_accountStore as IDisposable)?.Dispose());
        // Баны обязаны попасть на диск при остановке: иначе выданный за
        // последнюю секунду бан не переживёт перезапуск.
        Safe.Run("core.OnStop.bans", () => (_banStore as IDisposable)?.Dispose());
        _accountStore = null;

        _httpApi?.Stop();
        _httpApi = null;

        _agent?.Stop();
        _agent?.Dispose();
        _agent = null;

        _telemetry?.Stop();
        _telemetry?.Dispose();
        _telemetry = null;
        _licenseClient?.Dispose();
        _licenseClient = null;

        // Dispose сбрасывает инвентари на диск И гасит фоновый таймер записи:
        // без этого таймер тикает после остановки ресурса, а при перезагрузке
        // ресурса их становится два.
        Safe.Run("core.OnStop.flush", () => _inv?.Dispose());
        Safe.Run("core.OnStop.log", () =>
        {
            GameLog.System("gamemode_stop");
            GameLog.ShutdownAsync().GetAwaiter().GetResult();
        });

        Alt.OnServerStarted -= OnServerStarted;
        Alt.OnPlayerDisconnect -= OnPlayerDisconnect;
        Alt.OnPlayerDead -= OnPlayerDead;
        _console?.Detach();
        _console = null;
        _chat?.Detach();
        _chat = null;
        _inv?.Detach();
        _inv = null;
        _auth?.Detach();
        _auth = null;
        _playerLifecycle?.Detach();
        _playerLifecycle = null;
        _hud = null;
        _economy = null;
        _antiCheat?.Detach();
        _antiCheat = null;
        _tickManager = null;
        _occlusion = null;
        _factions = null;
        _documents = null;
        _housing = null;

        Alt.Log("[FloV:MP] core: gamemode stopped");
    }

    public override void OnTick()
    {
        // Результаты входа/регистрации считаются в фоне (БД + PBKDF2), а
        // применяются здесь — на главном потоке, где только и можно трогать
        // сущности alt:V.
        _auth?.Pump();

        // Отложенные действия с сущностями alt:V — строго здесь, на главном
        // потоке (см. MainThreadScheduler).
        Systems.MainThreadScheduler.Pump();

        _hud?.Tick();
        _antiCheat?.Tick();

        var now = _clock.ElapsedMilliseconds;

        // Запланированный /restart. Выполняется здесь, на главном потоке:
        // гасить сервер из фонового потока — гонка в нативной памяти движка.
        if (_restartAtMs != 0 && now >= _restartAtMs)
        {
            _restartAtMs = 0;
            Alt.Log("[FloV:MP] core: плановый перезапуск, сохраняем состояние");
            Safe.Run("core.restart.save", () => _inv?.SaveAll());
            Safe.Run("core.restart.accounts", () => (_accountStore as IDisposable)?.Dispose());
            Safe.Run("core.restart.bans", () => (_banStore as IDisposable)?.Dispose());
            Alt.StopServer();
            return;
        }

        // Адаптивное масштабирование тикрейта каждые 100 мс
        if (now - _lastTickScaleMs >= 100)
        {
            _lastTickScaleMs = now;
            if (_tickManager != null)
            {
                var allPlayers = Alt.GetAllPlayers();
                _tickManager.ServerLoadFactor = Math.Clamp(allPlayers.Count / 1000f, 0f, 1f);
                foreach (var p in allPlayers)
                {
                    if (!p.Exists) continue;
                    var pos = new FloVMP.Core.AntiCheat.Vector3D(p.Position.X, p.Position.Y, p.Position.Z);
                    var vel = p.IsInVehicle && p.Vehicle != null && p.Vehicle.Exists
                        ? new FloVMP.Core.AntiCheat.Vector3D(p.Vehicle.Velocity.X, p.Vehicle.Velocity.Y, p.Vehicle.Velocity.Z)
                        : FloVMP.Core.AntiCheat.Vector3D.Zero;
                    bool inCombat = p.IsShooting || p.EntityAimingAt != null;
                    _tickManager.UpdateEntityState(p.Id, pos, vel, inCombat, p.Dimension);
                }
            }
        }

        // Ежесекундный тик арестов и освобождение заключённых
        if (now - _lastArrestTickMs >= 1000)
        {
            _lastArrestTickMs = now;
            var released = _factions?.TickArrests(1);
            if (released != null && released.Count > 0)
            {
                // Карта аккаунт -> игрок строится ОДИН раз. Раньше на каждого
                // освобождённого вызывался Alt.GetAllPlayers() с линейным
                // поиском: при массовом освобождении это O(освобождённых x
                // онлайна) плюс аллокация полного списка игроков на каждой
                // итерации — на двух тысячах онлайна заметный провал тика.
                var onlineByAccount = new Dictionary<int, IPlayer>();
                foreach (var pl in Alt.GetAllPlayers())
                {
                    if (!pl.Exists) continue;
                    var a = _auth?.AccountOf(pl);
                    if (a != null) onlineByAccount[a.Id] = pl;
                }

                foreach (var accId in released)
                {
                    onlineByAccount.TryGetValue(accId, out var p);
                    if (p != null && p.Exists)
                    {
                        p.Dimension = 0;
                        p.Position = new AltV.Net.Data.Position(425.1f, -979.5f, 30.7f);
                        _antiCheat?.NotifyAdminTeleport(p, p.Position);
                        ChatSystem.SendSystem(p, "[ГУ МВД] Срок вашего ареста истёк. Вы освобождены из камеры предварительного заключения.");
                    }
                }
            }

            // Проверка истечения срока деморгана (Admin Jail) для онлайн-игроков
            var nowUtc = DateTime.UtcNow;
            foreach (var p in Alt.GetAllPlayers())
            {
                if (!p.Exists) continue;
                var acc = _auth?.AccountOf(p);
                if (acc != null && !string.IsNullOrEmpty(acc.JailUntilUtc) && !acc.IsJailed(nowUtc))
                {
                    acc.JailUntilUtc = "";
                    _auth?.SaveAccount(acc);
                    if (p.Dimension == FloVMP.Core.World.DimensionManager.AdminJailDimension)
                    {
                        p.Dimension = 0;
                        p.Position = SpawnPoints.MoscowRedSquare;
                        _antiCheat?.NotifyAdminTeleport(p, p.Position);
                        ChatSystem.SendSystem(p, "[Деморган] Срок вашего административного наказания истёк. Вы возвращены на свободу.");
                    }
                }
            }
        }

        // Расход топлива транспорта и контроль двигателей каждые 2 сек
        if (now - _lastVehTickMs >= 2000)
        {
            _lastVehTickMs = now;
            foreach (var veh in Alt.GetAllVehicles())
            {
                if (!veh.Exists || !veh.EngineOn) continue;

                float currentFuel = 100.0f;
                if (veh.GetStreamSyncedMetaData("fuel", out float fVal))
                {
                    currentFuel = fVal;
                }

                // Расход: холостой ход 0.02%, при движении пропорционально скорости
                float speed = (float)Math.Sqrt(veh.Velocity.X * veh.Velocity.X + veh.Velocity.Y * veh.Velocity.Y + veh.Velocity.Z * veh.Velocity.Z);
                float consumption = 0.02f + (speed * 0.003f);
                float newFuel = Math.Max(0.0f, currentFuel - consumption);

                veh.SetStreamSyncedMetaData("fuel", newFuel);

                if (newFuel <= 0.05f)
                {
                    veh.EngineOn = false;
                    if (veh.Driver != null && veh.Driver.Exists)
                    {
                        ChatSystem.SendSystem(veh.Driver, "[Транспорт] В баке закончилось топливо! Двигатель заглох.");
                    }
                }
            }
        }

        // Дозагрузка блокировок, выданных на других инстансах. Уходит в фон:
        // чтение из БД на главном потоке задержало бы тик, а результат
        // применяется в потокобезопасный словарь сервиса.
        if (_bans is not null && now - _lastBanSyncMs >= BanSyncIntervalMs)
        {
            _lastBanSyncMs = now;
            // Защита от наложения: если прошлая синхронизация ещё идёт
            // (медленная БД), новую не запускаем — иначе запросы копятся.
            if (System.Threading.Interlocked.CompareExchange(ref _banSyncInFlight, 1, 0) == 0)
            {
                var bans = _bans;
                Task.Run(() =>
                {
                    try
                    {
                        var changed = bans.RefreshFromStore();
                        if (changed > 0)
                            Alt.Log($"[FloV:MP] [Ban] синхронизировано блокировок с другими инстансами: {changed}");
                    }
                    catch (Exception ex)
                    {
                        Alt.Log($"[FloV:MP] [Ban] синхронизация не удалась: {ex.Message}");
                    }
                    finally
                    {
                        System.Threading.Interlocked.Exchange(ref _banSyncInFlight, 0);
                    }
                });
            }
        }

        if (now - _lastAutoSaveMs >= AutoSaveIntervalMs)
        {
            _lastAutoSaveMs = now;
            Safe.Run("core.autosave", () => _inv?.SaveAll());
            Safe.Run("core.drops.cleanup", () => _inv?.AtomicTransactions.CleanupExpiredDrops(TimeSpan.FromMinutes(30)));
            Safe.Run("core.autosave.log", () => _ = GameLog.FlushAsync());
        }

        // Ежечасный PayDay (в 00 минут каждого часа)
        var utcNow = DateTime.UtcNow;
        if (utcNow.Minute == 0 && _lastPayDayHour != utcNow.Hour)
        {
            _lastPayDayHour = utcNow.Hour;
            TriggerPayDay();
        }

        // Обработка запланированных возрождений игроков (на главном потоке alt:V)
        if (_pendingRespawns.Count > 0)
        {
            for (int i = _pendingRespawns.Count - 1; i >= 0; i--)
            {
                var item = _pendingRespawns[i];
                if (now >= item.RespawnAtMs)
                {
                    _pendingRespawns.RemoveAt(i);
                    var p = item.Player;
                    var pAcc = item.Account;
                    if (p == null || !p.Exists || p.Health > 0) continue;

                    try
                    {
                        var nowUtcRespawn = DateTime.UtcNow;
                        if (pAcc != null && pAcc.IsJailed(nowUtcRespawn))
                        {
                            p.Dimension = FloVMP.Core.World.DimensionManager.AdminJailDimension;
                            var jailPos = new AltV.Net.Data.Position(1651.2f, 2570.3f, 45.5f);
                            p.Spawn(jailPos, 0);
                            p.Health = 200;
                            p.Armor = 0;
                            _antiCheat?.NotifyAdminTeleport(p, jailPos);
                            ChatSystem.SendSystem(p, "[Деморган] Вы вернулись в камеру деморгана после оказания медицинской помощи.");
                        }
                        else if (pAcc != null && _factions != null && _factions.IsArrested(pAcc.Id, out var rem, out _))
                        {
                            p.Dimension = 0;
                            var arrestPos = new AltV.Net.Data.Position(459.4f, -997.8f, 24.9f);
                            p.Spawn(arrestPos, 0);
                            p.Health = 200;
                            p.Armor = 0;
                            _antiCheat?.NotifyAdminTeleport(p, arrestPos);
                            ChatSystem.SendSystem(p, $"[ГУ МВД] Вы возвращены в КПЗ. Осталось времени: {rem} сек.");
                        }
                        else
                        {
                            p.Dimension = 0;
                            p.Spawn(SpawnPoints.MoscowHospital, 0);
                            p.Health = 200;
                            p.Armor = 0;
                            _antiCheat?.NotifyAdminTeleport(p, SpawnPoints.MoscowHospital);
                            ChatSystem.SendSystem(p, "[Скорая помощь] Вас доставили в приёмное отделение Городской больницы.");
                        }

                        p.Emit("flovmp:hud:respawned");
                    }
                    catch (Exception ex)
                    {
                        Alt.Log($"[FloV:MP] hospital respawn error: {ex.Message}");
                    }
                }
            }
        }
    }

    private void TriggerPayDay()
    {
        Safe.Run("core.payday", () =>
        {
            var players = Alt.GetAllPlayers().Where(p => p.Exists && _auth?.AccountOf(p) != null).ToList();
            if (players.Count == 0) return;

            Alt.Log($"[FloV:MP PayDay] Расчёт государственной зарплаты и пособий для {players.Count} игроков...");
            _chat?.Broadcast("====== [ ВРЕМЯ РАСЧЁТА: PAYDAY ] ======");

            foreach (var p in players)
            {
                var acc = _auth?.AccountOf(p);
                if (acc == null) continue;

                long totalGain = 0;
                // 1. Базовое пособие гражданина РФ
                const long citizenAllowance = 1500;
                totalGain += citizenAllowance;

                // 2. Фракционная заработная плата
                string factionSalaryInfo = "";
                if (_factions != null)
                {
                    var mem = _factions.GetMember(acc.Id);
                    if (mem != null)
                    {
                        var fac = _factions.GetFaction(mem.FactionId);
                        var rank = fac?.GetRank(mem.RankLevel);
                        if (rank != null && rank.Salary > 0)
                        {
                            totalGain += rank.Salary;
                            factionSalaryInfo = $" | Зарплата ({fac?.Tag}): +{rank.Salary:N0} руб.";
                        }
                    }
                }

                if (long.MaxValue - acc.Bank >= totalGain)
                {
                    acc.Bank += totalGain;
                }
                _auth?.SaveAccount(acc);

                ChatSystem.SendSystem(p, $"[Банк Держава] Начислено в PayDay: +{totalGain:N0} руб. (Пособие: +{citizenAllowance:N0} руб.{factionSalaryInfo}). Баланс счёта: {acc.Bank:N0} руб.");
            }

            _chat?.Broadcast("Все выплаты успешно зачислены на банковские счета граждан.");
            GameLog.System("payday", ("players_paid", players.Count), ("hour", DateTime.UtcNow.Hour));
        });
    }

    private void OnPlayerAuthed(IPlayer player, Account account) => Safe.Run("core.OnPlayerAuthed", () =>
    {
        _antiCheat?.OnAuthed(player, account);
        int? arrestSec = null;
        string? arrestReason = null;
        if (_factions != null && _factions.IsArrested(account.Id, out var rem, out var rsn))
        {
            arrestSec = rem;
            arrestReason = rsn;
        }
        _playerLifecycle?.SpawnAuthed(player, account.Id, account, arrestSec, arrestReason);
        _hud?.OnAuthed(player, account);
        _inv?.OnAuthed(player, account);
        _chat?.OnPlayerAuthed(player, account);

        // В голосовой канал игрок попадает только ПОСЛЕ авторизации: иначе
        // висящий на экране входа слышал бы происходящее в игре и мог бы
        // говорить, не войдя в аккаунт.
        Safe.Run("core.voice.add", () => _voiceChannel?.AddPlayer(player));
    });

    private void OnPlayerDisconnect(IPlayer player, string reason) => Safe.Run("core.OnPlayerDisconnect", () =>
    {
        _tickManager?.UnregisterEntity(player.Id);
        // Голосовой канал держит ссылку на игрока: без явного удаления
        // отключившиеся накапливаются в канале — утечка и лишний трафик.
        Safe.Run("core.voice.remove", () => _voiceChannel?.RemovePlayer(player));
        _antiCheat?.OnDisconnect(player);
        _hud?.OnDisconnect(player);
        _pendingRespawns.RemoveAll(r => r.Player == player);
        var acc = _auth?.AccountOf(player);
        if (acc != null && _factions != null)
        {
            if (_factions.IsCuffed(acc.Id))
            {
                // Защита от Off-from-Arrest / Quit-in-Cuffs:
                // Если игрок намеренно вышел из игры в наручниках, сажаем в КПЗ на 30 минут
                _factions.TryArrest(0, acc.Id, 1800, "Выход из игры при аресте (/q от ареста)", out _);
                acc.JailUntilUtc = DateTime.UtcNow.AddMinutes(30).ToString("O");
                _auth?.SaveAccount(acc);
                Alt.Log($"[FloV:MP RP] Игрок {acc.Username} (acc:{acc.Id}) вышел из игры в наручниках! Автоматически посажен в КПЗ на 30 мин.");
            }
        }
    });

    private void OnPlayerDead(IPlayer player, IEntity killer, uint weapon) => Safe.Run("core.OnPlayerDead", () =>
    {
        if (player == null || !player.Exists) return;

        var acc = _auth?.AccountOf(player);
        var victimName = acc?.Username ?? player.Name;

        string killerName = "окружающая среда / суицид";
        if (killer is IPlayer killerPlayer && killerPlayer.Exists)
        {
            var kAcc = _auth?.AccountOf(killerPlayer);
            killerName = kAcc?.Username ?? killerPlayer.Name;
            GameLog.System("player_killed", ("victim", victimName), ("killer", killerName), ("weapon", weapon));
        }
        else
        {
            GameLog.System("player_died", ("victim", victimName), ("weapon", weapon));
        }

        Alt.Log($"[FloV:MP RP] Игрок {victimName} (ID: {player.Id}) погиб. Убийца: {killerName}, Оружие: 0x{weapon:X}");

        if (acc != null && _factions != null && _factions.IsCuffed(acc.Id))
        {
            _factions.TryUncuff(0, acc.Id, out _);
        }

        player.Emit("flovmp:hud:dead", 5);

        // Регистрация запланированного возрождения на основном потоке сервера (через 5 секунд)
        _pendingRespawns.Add((player, acc, _clock.ElapsedMilliseconds + 5000));
    });

    private static void OnServerStarted() => Safe.Run("core.OnServerStarted", () =>
    {
        Alt.Log("[FloV:MP] core: server fully started");
    });
}

