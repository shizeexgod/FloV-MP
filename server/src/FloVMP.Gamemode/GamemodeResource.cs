using System.Diagnostics;
using System.IO;
using AltV.Net;
using AltV.Net.Elements.Entities;
using FloVMP.Core.Auth;
using FloVMP.Core.Logging;

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
    private const string ServerName = "FloV:MP Dev";
    private const int AutoSaveIntervalMs = 60_000;

    private PlayerLifecycle? _playerLifecycle;
    private AuthSystem? _auth;
    private HudSystem? _hud;
    private InventorySystem? _inv;
    private ChatSystem? _chat;
    private ConsoleCommands? _console;

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private long _lastAutoSaveMs;

    public override void OnStart()
    {
        Alt.Log($"[FloV:MP] core: gamemode start (v{BuildInfo.Version})");

        _playerLifecycle = new PlayerLifecycle();
        _playerLifecycle.Attach();

        _hud = new HudSystem(ServerName);

        var dataDir = Path.Combine(Directory.GetCurrentDirectory(), "flovmp-data");
        GameLog.Configure(new FileLogSink(Path.Combine(dataDir, "logs")));
        GameLog.System("gamemode_start", ("version", BuildInfo.Version));

        _auth = new AuthSystem(Path.Combine(dataDir, "accounts.json"), OnPlayerAuthed);
        _auth.Attach();

        _inv = new InventorySystem(Path.Combine(dataDir, "inventories.json"));
        _inv.Attach();

        _chat = new ChatSystem(p => _auth.AccountOf(p));
        _chat.Attach();

        _console = new ConsoleCommands(
            saveAll: () => _inv?.SaveAll(),
            broadcast: text => _chat?.Broadcast(text),
            nameOf: p => _auth?.AccountOf(p)?.Username);
        _console.Attach();
        Alt.Log($"[FloV:MP] core: data dir -> {dataDir}");

        Alt.OnPlayerDisconnect += OnPlayerDisconnect;
        Alt.OnServerStarted += OnServerStarted;

        Alt.Log("[FloV:MP] core: systems attached, waiting for players");
    }

    public override void OnStop()
    {
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

        Alt.Log("[FloV:MP] core: gamemode stopped");
    }

    public override void OnTick()
    {
        _hud?.Tick();

        var now = _clock.ElapsedMilliseconds;
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
    });

    private void OnPlayerDisconnect(IPlayer player, string reason) => Safe.Run("core.OnPlayerDisconnect", () =>
    {
        _hud?.OnDisconnect(player);
    });

    private static void OnServerStarted() => Safe.Run("core.OnServerStarted", () =>
    {
        Alt.Log("[FloV:MP] core: server fully started");
    });
}
