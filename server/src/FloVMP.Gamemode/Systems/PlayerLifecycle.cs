using AltV.Net;
using AltV.Net.Elements.Entities;
using AltV.Net.Enums;

namespace FloVMP.Gamemode;

/// <summary>
/// Жизненный цикл игрока для Фазы 1: подключился → выдать модель и заспавнить,
/// отключился → просто залогировать. Ничего лишнего — задача этой фазы в том,
/// чтобы два клиента увидели друг друга и подвигались; вся синхронизация
/// позиций/анимаций дальше идёт штатным сетевым движком alt:V сама.
/// </summary>
public sealed class PlayerLifecycle
{
    private int _spawnCounter;

    public void Attach()
    {
        Alt.OnPlayerConnect += OnPlayerConnect;
        Alt.OnPlayerDisconnect += OnPlayerDisconnect;
    }

    public void Detach()
    {
        Alt.OnPlayerConnect -= OnPlayerConnect;
        Alt.OnPlayerDisconnect -= OnPlayerDisconnect;
    }

    private void OnPlayerConnect(IPlayer player, string reason)
    {
        if (!player.Exists)
        {
            return;
        }

        var index = _spawnCounter++;
        var position = SpawnPoints.Scattered(SpawnPoints.LegionSquare, index);

        player.Model = (uint)PedModel.FreemodeMale01;
        player.Dimension = 0;
        player.Spawn(position, 0);

        // C# → JS round-trip: клиентский ресурс ловит это событие и рисует
        // приветствие. Заодно проверяем, что js-module жив и связь работает.
        player.Emit("flovmp:client:welcome", player.Name, index);

        Alt.Log($"[FloV:MP] connect: {player.Name} (id {player.Id}) -> spawn #{index} @ {position.X:0.0}/{position.Y:0.0}/{position.Z:0.0}");
    }

    private static void OnPlayerDisconnect(IPlayer player, string reason)
    {
        Alt.Log($"[FloV:MP] disconnect: {player.Name} — {reason}");
    }
}
