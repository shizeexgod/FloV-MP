using System.Collections.Concurrent;

namespace FloVMP.Gamemode.Systems;

/// <summary>
/// Лёгкий анти-спам для клиентских событий: не более N срабатываний на игрока
/// за скользящее окно. Клиент недоверенный — он может слать
/// flovmp:inv:use / :move / :drop сотни раз в секунду, а каждый обработчик
/// пишет JSON инвентаря на диск, шлёт sync и лог. Без гейта это дешёвый
/// DoS диска/сети гейм-сервера.
///
/// Потокобезопасно (ConcurrentDictionary). Память чистится по <see cref="Sweep"/>
/// либо когда словарь перерастает лимит.
/// </summary>
public sealed class ClientRateGate
{
    private readonly int _maxPerWindow;
    private readonly TimeSpan _window;
    private readonly ConcurrentDictionary<uint, (int count, long windowStartTicks)> _hits = new();

    public ClientRateGate(int maxPerWindow = 12, int windowMs = 1000)
    {
        _maxPerWindow = maxPerWindow;
        _window = TimeSpan.FromMilliseconds(windowMs);
    }

    /// <summary>true — можно обрабатывать; false — превышен лимит, событие игнорируем.</summary>
    public bool Allow(uint playerId)
    {
        var now = DateTime.UtcNow.Ticks;
        var windowTicks = _window.Ticks;

        var e = _hits.AddOrUpdate(playerId,
            _ => (1, now),
            (_, cur) => (now - cur.windowStartTicks) > windowTicks ? (1, now) : (cur.count + 1, cur.windowStartTicks));

        if (_hits.Count > 4096)
        {
            foreach (var kv in _hits)
                if ((now - kv.Value.windowStartTicks) > windowTicks * 8) _hits.TryRemove(kv.Key, out _);
        }

        return e.count <= _maxPerWindow;
    }

    /// <summary>Убрать игрока при выходе.</summary>
    public void Forget(uint playerId) => _hits.TryRemove(playerId, out _);
}
