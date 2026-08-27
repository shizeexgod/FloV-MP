using System.IO;
using AltV.Net;

namespace FloVMP.Gamemode;

/// <summary>
/// Точка входа C#-геймода FloV:MP. alt:V находит этот класс (единственный
/// наследник <see cref="Resource"/> в сборке) и вызывает <see cref="OnStart"/>
/// при старте ресурса, <see cref="OnStop"/> — при остановке/перезагрузке.
///
/// Здесь только проводка систем.
/// </summary>
public class GamemodeResource : Resource
{
    private PlayerLifecycle? _playerLifecycle;
    private AuthSystem? _auth;

    public override void OnStart()
    {
        Alt.Log($"[FloV:MP] core: gamemode start (v{BuildInfo.Version})");

        _playerLifecycle = new PlayerLifecycle();
        _playerLifecycle.Attach();

        var accountsPath = Path.Combine(Directory.GetCurrentDirectory(), "flovmp-data", "accounts.json");
        _auth = new AuthSystem(accountsPath, (player, accountId) => _playerLifecycle.SpawnAuthed(player, accountId));
        _auth.Attach();
        Alt.Log($"[FloV:MP] core: auth store -> {accountsPath}");

        Alt.OnServerStarted += OnServerStarted;

        Alt.Log("[FloV:MP] core: systems attached, waiting for players");
    }

    public override void OnStop()
    {
        Alt.OnServerStarted -= OnServerStarted;
        _auth?.Detach();
        _auth = null;
        _playerLifecycle?.Detach();
        _playerLifecycle = null;

        Alt.Log("[FloV:MP] core: gamemode stopped");
    }

    private static void OnServerStarted()
    {
        Alt.Log("[FloV:MP] core: server fully started");
    }
}
