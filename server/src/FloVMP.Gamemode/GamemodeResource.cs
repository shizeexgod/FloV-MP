using AltV.Net;

namespace FloVMP.Gamemode;

/// <summary>
/// Точка входа C#-геймода FloV:MP. alt:V находит этот класс (единственный
/// наследник <see cref="Resource"/> в сборке) и вызывает <see cref="OnStart"/>
/// при старте ресурса, <see cref="OnStop"/> — при остановке/перезагрузке.
///
/// Здесь только проводка: создаём системы и отдаём им подписку на события.
/// Никакой игровой логики в самом классе-точке входа — так проще тестировать
/// системы по отдельности и не превращать точку входа в свалку.
/// </summary>
public class GamemodeResource : Resource
{
    private PlayerLifecycle? _playerLifecycle;

    public override void OnStart()
    {
        Alt.Log($"[FloV:MP] core: gamemode start (v{BuildInfo.Version})");

        _playerLifecycle = new PlayerLifecycle();
        _playerLifecycle.Attach();

        Alt.OnServerStarted += OnServerStarted;

        Alt.Log("[FloV:MP] core: systems attached, waiting for players");
    }

    public override void OnStop()
    {
        Alt.OnServerStarted -= OnServerStarted;
        _playerLifecycle?.Detach();
        _playerLifecycle = null;

        Alt.Log("[FloV:MP] core: gamemode stopped");
    }

    private static void OnServerStarted()
    {
        Alt.Log("[FloV:MP] core: server fully started");
    }
}
