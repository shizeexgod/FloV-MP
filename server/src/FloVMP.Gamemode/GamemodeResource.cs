using System.IO;
using AltV.Net;
using AltV.Net.Elements.Entities;
using FloVMP.Core.Auth;

namespace FloVMP.Gamemode;

/// <summary>
/// Точка входа C#-геймода FloV:MP. alt:V находит этот класс (единственный
/// наследник <see cref="Resource"/> в сборке) и вызывает <see cref="OnStart"/>
/// при старте ресурса, <see cref="OnStop"/> — при остановке/перезагрузке.
/// <see cref="OnTick"/> — каждый тик сервера.
///
/// Здесь только проводка систем.
/// </summary>
public class GamemodeResource : Resource
{
    private const string ServerName = "FloV:MP Dev";

    private PlayerLifecycle? _playerLifecycle;
    private AuthSystem? _auth;
    private HudSystem? _hud;
    private InventorySystem? _inv;

    public override void OnStart()
    {
        Alt.Log($"[FloV:MP] core: gamemode start (v{BuildInfo.Version})");

        _playerLifecycle = new PlayerLifecycle();
        _playerLifecycle.Attach();

        _hud = new HudSystem(ServerName);

        var dataDir = Path.Combine(Directory.GetCurrentDirectory(), "flovmp-data");
        _auth = new AuthSystem(Path.Combine(dataDir, "accounts.json"), OnPlayerAuthed);
        _auth.Attach();

        _inv = new InventorySystem(Path.Combine(dataDir, "inventories.json"), p => _auth.AccountOf(p));
        _inv.Attach();
        Alt.Log($"[FloV:MP] core: data dir -> {dataDir}");

        Alt.OnPlayerDisconnect += OnPlayerDisconnect;
        Alt.OnServerStarted += OnServerStarted;

        Alt.Log("[FloV:MP] core: systems attached, waiting for players");
    }

    public override void OnStop()
    {
        Alt.OnServerStarted -= OnServerStarted;
        Alt.OnPlayerDisconnect -= OnPlayerDisconnect;
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
    }

    private void OnPlayerAuthed(IPlayer player, Account account)
    {
        _playerLifecycle?.SpawnAuthed(player, account.Id);
        _hud?.OnAuthed(player, account);
        _inv?.OnAuthed(player, account);
    }

    private void OnPlayerDisconnect(IPlayer player, string reason)
    {
        _hud?.OnDisconnect(player);
    }

    private static void OnServerStarted()
    {
        Alt.Log("[FloV:MP] core: server fully started");
    }
}
