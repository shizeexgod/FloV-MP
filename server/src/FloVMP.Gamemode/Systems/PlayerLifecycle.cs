using System.Threading;
using AltV.Net;
using AltV.Net.Elements.Entities;
using AltV.Net.Enums;
using FloVMP.Core.Auth;

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
    private readonly Action<IPlayer, AltV.Net.Data.Position>? _notifyTeleport;

    public PlayerLifecycle(Action<IPlayer, AltV.Net.Data.Position>? notifyTeleport = null)
    {
        _notifyTeleport = notifyTeleport;
    }

    public void Attach()
    {
        Alt.OnPlayerDisconnect += OnPlayerDisconnect;
    }

    public void Detach()
    {
        Alt.OnPlayerDisconnect -= OnPlayerDisconnect;
    }

    /// <summary>Заспавнить уже авторизованного игрока.</summary>
    public void SpawnAuthed(IPlayer player, int accountId, Account? account = null, int? arrestRemainingSeconds = null, string? arrestReason = null)
    {
        if (!player.Exists) return;

        var index = Interlocked.Increment(ref _spawnCounter);
        AltV.Net.Data.Position position;
        int dimension = 0;
        bool stripWeapons = false;
        string? statusMessage = null;

        var now = DateTime.UtcNow;
        if (account != null && account.IsJailed(now))
        {
            dimension = FloVMP.Core.World.DimensionManager.AdminJailDimension;
            position = new AltV.Net.Data.Position(1651.2f, 2570.3f, 45.5f);
            stripWeapons = true;
            statusMessage = $"[Деморган] Вы отбываете административное наказание до {account.JailUntilUtc}.";
        }
        else if (arrestRemainingSeconds.HasValue && arrestRemainingSeconds.Value > 0)
        {
            dimension = 0;
            position = new AltV.Net.Data.Position(459.4f, -997.8f, 24.9f);
            stripWeapons = true;
            statusMessage = $"[ГУ МВД] Вы находитесь в камере КПЗ. Осталось: {arrestRemainingSeconds.Value} сек. Причина: {arrestReason}";
        }
        else
        {
            position = SpawnPoints.Scattered(SpawnPoints.DefaultSpawn, index);
        }

        player.Model = (uint)PedModel.FreemodeMale01;
        player.Dimension = dimension;
        player.Spawn(position, 0);

        if (stripWeapons)
        {
            player.RemoveAllWeapons(true);
        }

        _notifyTeleport?.Invoke(player, position);

        player.Emit("flovmp:client:welcome", player.Name, index, position.X, position.Y, position.Z);

        if (statusMessage != null)
        {
            ChatSystem.SendSystem(player, statusMessage);
        }

        Alt.Log($"[FloV:MP] spawn: {player.Name} (acc {accountId}, dim {dimension}) -> #{index} @ {position.X:0.0}/{position.Y:0.0}/{position.Z:0.0}");

        // Страховочная отправка событий через 300мс после инициализации сетевого педа движком alt:V
        var p = player;
        var pName = player.Name;
        var posX = position.X;
        var posY = position.Y;
        var posZ = position.Z;
        // Раньше это был Task.Delay(...).ContinueWith(...): продолжение уходило
        // на произвольный поток пула и трогало сущность alt:V, живущую в
        // нативной памяти. Между p.Exists и p.Emit игрок успевает отключиться,
        // и обращение уходит в освобождённую память. На главном потоке такого
        // окна нет — тик не прерывается отключением.
        Systems.MainThreadScheduler.RunAfter(300, "post-spawn.welcome", () =>
        {
            if (!p.Exists) return;
            p.Emit("flovmp:auth:hide");
            p.Emit("flovmp:client:welcome", pName, index, posX, posY, posZ);
        });
    }

    private static void OnPlayerDisconnect(IPlayer player, string reason)
    {
        Alt.Log($"[FloV:MP] disconnect: {player.Name} — {reason}");
    }
}
