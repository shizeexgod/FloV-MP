using System.Threading;
using AltV.Net;
using AltV.Net.Elements.Entities;
using AltV.Net.Enums;

namespace FloVMP.Gamemode;

/// <summary>
/// Появление игрока в мире — теперь ПОСЛЕ авторизации (<see cref="AuthSystem"/>
/// вызывает <see cref="SpawnAuthed"/>). До входа игрок не спавнится: клиент
/// висит на чёрном экране с NUI логина.
///
/// Взаимную видимость и синхронизацию перемещения дальше обеспечивает
/// сетевой движок alt:V сам.
/// </summary>
public sealed class PlayerLifecycle
{
    private int _spawnCounter = -1;

    public void Attach()
    {
        Alt.OnPlayerDisconnect += OnPlayerDisconnect;
    }

    public void Detach()
    {
        Alt.OnPlayerDisconnect -= OnPlayerDisconnect;
    }

    /// <summary>Заспавнить уже авторизованного игрока.</summary>
    public void SpawnAuthed(IPlayer player, int accountId)
    {
        if (!player.Exists) return;

        var index = Interlocked.Increment(ref _spawnCounter);
        var position = SpawnPoints.Scattered(SpawnPoints.LegionSquare, index);

        player.Model = (uint)PedModel.FreemodeMale01;
        player.Dimension = 0;
        player.Spawn(position, 0);

        player.Emit("flovmp:client:welcome", player.Name, index);

        Alt.Log($"[FloV:MP] spawn: {player.Name} (acc {accountId}) -> #{index} @ {position.X:0.0}/{position.Y:0.0}/{position.Z:0.0}");
    }

    private static void OnPlayerDisconnect(IPlayer player, string reason)
    {
        Alt.Log($"[FloV:MP] disconnect: {player.Name} — {reason}");
    }
}
