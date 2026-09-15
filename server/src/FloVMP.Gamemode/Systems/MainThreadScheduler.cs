using System.Collections.Concurrent;
using AltV.Net;

namespace FloVMP.Gamemode.Systems;

/// <summary>
/// Отложенное выполнение НА ГЛАВНОМ ПОТОКЕ.
///
/// Зачем это есть. Сущности alt:V живут в нативной памяти C++ и трогать их
/// можно только из главного потока. Типовой соблазн — написать
/// <c>Task.Delay(300).ContinueWith(_ =&gt; player.Emit(...))</c>: продолжение
/// выполняется на произвольном потоке пула, и получаются сразу две беды.
///
/// Первая — гонки в нативной памяти движка. В проекте это уже разбирали:
/// реанимацию через <c>Task.Delay.ContinueWith</c> пришлось переделывать на
/// очередь, обрабатываемую в тике.
///
/// Вторая — проверка «жив ли игрок» ничего не гарантирует. Между
/// <c>player.Exists</c> и следующей строкой игрок успевает отключиться, и
/// обращение уходит в освобождённую нативную память. На главном потоке такого
/// окна нет: тик не прерывается отключением.
///
/// Здесь задача только ставится в очередь, а выполняется из <c>OnTick</c>.
/// </summary>
public static class MainThreadScheduler
{
    private sealed record Scheduled(long DueAtMs, Action Action, string Name);

    private static readonly ConcurrentQueue<Scheduled> _pending = new();
    private static readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();

    /// <summary>Сколько задач ждёт выполнения (для диагностики и тестов).</summary>
    public static int PendingCount => _pending.Count;

    /// <summary>
    /// Выполнить действие через указанную задержку на главном потоке.
    /// Задержка 0 означает «в ближайшем тике».
    /// </summary>
    public static void RunAfter(int delayMs, string name, Action action)
    {
        if (action is null) return;
        _pending.Enqueue(new Scheduled(_clock.ElapsedMilliseconds + Math.Max(0, delayMs), action, name));
    }

    /// <summary>
    /// Выполнить всё, чему подошёл срок. Вызывается из <c>OnTick</c>.
    ///
    /// Задачи, срок которых ещё не наступил, возвращаются в очередь. Порядок
    /// между разными задержками при этом не гарантируется — и не нужен: это
    /// отложенные одиночные действия, а не конвейер.
    /// </summary>
    public static void Pump()
    {
        if (_pending.IsEmpty) return;

        var now = _clock.ElapsedMilliseconds;
        var count = _pending.Count; // фиксируем, иначе перекладываемые задачи зациклят обход

        for (var i = 0; i < count; i++)
        {
            if (!_pending.TryDequeue(out var item)) break;

            if (item.DueAtMs > now)
            {
                _pending.Enqueue(item);
                continue;
            }

            try
            {
                item.Action();
            }
            catch (Exception ex)
            {
                // Одна упавшая отложенная задача не должна ронять тик.
                Alt.Log($"[FloV:MP] отложенная задача '{item.Name}' упала: {ex.Message}");
            }
        }
    }

    /// <summary>Сбросить очередь (остановка ресурса, тесты).</summary>
    public static void Clear()
    {
        while (_pending.TryDequeue(out _)) { }
    }
}
