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
    private const string ServerName = "Держава RP";
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
    private FloVMP.Core.Spatial.AdaptiveTickManager<uint>? _tickManager;
    private FloVMP.Core.Spatial.OcclusionCullingService? _occlusion;
    
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private long _lastAutoSaveMs;
    private long _lastArrestTickMs;
    private long _lastTickScaleMs;

    public override void OnStart()
    {
        Alt.Log($"[FloV:MP] core: gamemode start (v{BuildInfo.Version})");

        _playerLifecycle = new PlayerLifecycle();
        _playerLifecycle.Attach();

        _hud = new HudSystem(ServerName);

        var dataDir = Path.Combine(Directory.GetCurrentDirectory(), "flovmp-data");
        GameLog.Configure(new FileLogSink(Path.Combine(dataDir, "logs")));
        GameLog.System("gamemode_start", ("version", BuildInfo.Version));

        var dbConn = Environment.GetEnvironmentVariable("FLOVMP_DB_CONNECTION") ??
                     new FloVMP.Core.Database.DatabaseConfig().BuildConnectionString();
        var accountStore = FloVMP.Core.Database.AccountStoreFactory.Create(dbConn, Path.Combine(dataDir, "accounts.json"));

        _auth = new AuthSystem(accountStore, OnPlayerAuthed);
        _auth.Attach();

        _economy = new FloVMP.Core.Economy.EconomyService();
        _factions = new FloVMP.Core.Factions.FactionService();
        Presets.DerzhavaFactions.RegisterAll(_factions);
        _documents = new FloVMP.Core.Documents.DocumentService();
        _housing = new FloVMP.Core.Housing.HousingService();
        Presets.DerzhavaHousing.RegisterAll(_housing);
        _uniforms = new FloVMP.Core.Characters.FactionUniformService();
        Presets.DerzhavaUniforms.RegisterAll(_uniforms);
        Presets.DerzhavaUniforms.RegisterAll(FloVMP.Core.Characters.FactionUniformService.Default);

        _inv = new InventorySystem(Path.Combine(dataDir, "inventories.json"));
        _inv.Attach();

        _chat = new ChatSystem(
            accountOf: p => _auth.AccountOf(p),
            saveAccount: acc => _auth.SaveAccount(acc),
            findAccountByName: name => _auth.FindByName(name),
            economy: _economy,
            factions: _factions,
            documents: _documents,
            housing: _housing);
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

        _antiCheat = new AntiCheatSystem(p => _auth?.AccountOf(p));

        Alt.OnPlayerDisconnect += OnPlayerDisconnect;
        Alt.OnServerStarted += OnServerStarted;

        Alt.Log("[FloV:MP] core: systems attached, waiting for players");
    }

    public override void OnStop()
    {
        _agent?.Stop();
        _agent?.Dispose();
        _agent = null;

        _telemetry?.Stop();
        _telemetry?.Dispose();
        _telemetry = null;
        _licenseClient?.Dispose();
        _licenseClient = null;

        Safe.Run("core.OnStop.flush", () => _inv?.SaveAll());
        Safe.Run("core.OnStop.log", () =>
        {
            GameLog.System("gamemode_stop");
            GameLog.ShutdownAsync().GetAwaiter().GetResult();
        });

        Alt.OnServerStarted -= OnServerStarted;
        Alt.OnPlayerDisconnect -= OnPlayerDisconnect;
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
        _hud?.Tick();
        _antiCheat?.Tick();

        var now = _clock.ElapsedMilliseconds;

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
                foreach (var accId in released)
                {
                    var p = Alt.GetAllPlayers().FirstOrDefault(pl => pl.Exists && _auth?.AccountOf(pl)?.Id == accId);
                    if (p != null && p.Exists)
                    {
                        p.Position = new AltV.Net.Data.Position(425.1f, -979.5f, 30.7f);
                        p.Emit("flovmp:chat:system", "[ГУ МВД] Срок вашего ареста истёк. Вы освобождены из камеры предварительного заключения.");
                    }
                }
            }
        }

        if (now - _lastAutoSaveMs >= AutoSaveIntervalMs)
        {
            _lastAutoSaveMs = now;
            Safe.Run("core.autosave", () => _inv?.SaveAll());
            Safe.Run("core.autosave.log", () => _ = GameLog.FlushAsync());
        }
    }

    private void OnPlayerAuthed(IPlayer player, Account account) => Safe.Run("core.OnPlayerAuthed", () =>
    {
        _playerLifecycle?.SpawnAuthed(player, account.Id);
        _hud?.OnAuthed(player, account);
        _inv?.OnAuthed(player, account);
        _chat?.OnPlayerAuthed(player, account);
        _antiCheat?.OnAuthed(player, account);
    });

    private void OnPlayerDisconnect(IPlayer player, string reason) => Safe.Run("core.OnPlayerDisconnect", () =>
    {
        _tickManager?.UnregisterEntity(player.Id);
        _antiCheat?.OnDisconnect(player);
        _hud?.OnDisconnect(player);
    });

    private static void OnServerStarted() => Safe.Run("core.OnServerStarted", () =>
    {
        Alt.Log("[FloV:MP] core: server fully started");
    });
}

