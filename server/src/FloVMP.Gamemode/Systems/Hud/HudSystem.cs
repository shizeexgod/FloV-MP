using System.Collections.Concurrent;
using System.Diagnostics;
using AltV.Net;
using AltV.Net.Elements.Entities;
using FloVMP.Core.Auth;

namespace FloVMP.Gamemode;

/// <summary>
/// HUD игрока (каркас Фазы 3). Сервер — источник истины: раз в секунду
/// шлёт каждому вошедшему игроку срез состояния, клиент только рисует NUI.
///
/// Тик берётся из <see cref="GamemodeResource.OnTick"/> (поток тика alt:V —
/// эмитить оттуда безопасно). Аккумулятор отсекает лишние отправки.
/// </summary>
public sealed class HudSystem
{
    private const int PushIntervalMs = 1000;

    private readonly string _serverName;
    private readonly ConcurrentDictionary<uint, Account> _players = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private long _lastPushMs;

    public HudSystem(string serverName)
    {
        _serverName = serverName;
    }

    /// <summary>Игрок вошёл и заспавнен — включаем ему HUD.</summary>
    public void OnAuthed(IPlayer player, Account account) => Safe.Run("hud.OnAuthed", () =>
    {
        _players[player.Id] = account;
        if (player.Exists)
            player.Emit("flovmp:hud:init", _serverName);
    });

    public void OnDisconnect(IPlayer player)
    {
        _players.TryRemove(player.Id, out _);
    }

    /// <summary>Вызывается каждый тик ресурса.</summary>
    public void Tick()
    {
        var now = _clock.ElapsedMilliseconds;
        if (now - _lastPushMs < PushIntervalMs) return;
        _lastPushMs = now;

        if (_players.IsEmpty) return;

        Safe.Run("hud.Tick", () =>
        {
            var online = _players.Count;
            var (hour, minute) = ServerClock();

            foreach (var player in Alt.GetAllPlayers())
            {
                if (!player.Exists || !_players.TryGetValue(player.Id, out var acc)) continue;

                // alt:V/GTA: здоровье игрока 100..200 (100 = смерть). HUD: 0..100.
                var health = Math.Clamp((int)player.Health - 100, 0, 100);
                var armor = Math.Clamp((int)player.Armor, 0, 100);

                player.Emit("flovmp:hud:tick", health, armor, acc.Cash, online, hour, minute);
            }
        });
    }

    private static (int hour, int minute) ServerClock()
    {
        var t = DateTime.Now;
        return (t.Hour, t.Minute);
    }
}
