using System.Collections.Concurrent;
using System.Diagnostics;

namespace FloVMP.Core.Async;

/// <summary>
/// Главный поток ресурса для асинхронного кода геймода (пункт 23 roadmap).
///
/// Разработчик геймода пишет <c>await db.QueryAsync(...)</c> прямо в
/// обработчике события — запрос идёт в фоне, тик сервера не стоит, а код
/// после <c>await</c> продолжается снова в главном потоке сервера, где можно
/// звать API движка. Продолжения не выполняются «когда придётся» из чужого
/// потока: они ложатся в эту очередь, а ресурс разбирает её в своём OnTick
/// (<see cref="Pump"/>).
///
/// Почему очередь, а не Alt.Emit: события между ресурсами alt:V доставляются
/// после выхода отправителя из тика (проверено живым запуском, 7d932ab), и
/// «ответить в том же вызове» на них нельзя. Очередь в тике ресурса — простой
/// и проверяемый путь назад в главный поток.
///
/// Главный поток alt:V общий для всех C#-ресурсов. Поэтому контекст
/// синхронизации ставится только на время своих вызовов и потом
/// возвращается прежний — иначе чужие <c>await</c> ушли бы в нашу очередь.
/// </summary>
public sealed class MainThreadQueue : SynchronizationContext
{
    private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> _queue = new();
    private readonly Action<string> _log;
    private readonly CancellationTokenSource _stopping = new();
    // Отмена (_stopping) и закрытие — разные моменты: между ними Stop даёт
    // отменённым обработчикам доработать (finally, закрыть соединение).
    private volatile bool _closed;
    private int _inFlight;   // запущенные через Start и ещё не закончившиеся
    private int _mainThreadId;
    private long _lastWarnMs;

    public MainThreadQueue(Action<string> log) => _log = log;

    /// <summary>Сколько миллисекунд тика можно тратить на продолжения (остальное — в следующем тике).</summary>
    public double BudgetMs { get; set; } = 4;

    /// <summary>Отменяется при остановке ресурса: долгие запросы должны его слушать.</summary>
    public CancellationToken Stopping => _stopping.Token;
    public bool Stopped => _stopping.IsCancellationRequested;
    /// <summary>Сколько даётся отменённым обработчикам на доработку при остановке.</summary>
    public TimeSpan StopDrain { get; set; } = TimeSpan.FromMilliseconds(500);
    public int Pending => _queue.Count;
    /// <summary>Сколько асинхронных обработчиков ещё не закончилось.</summary>
    public int InFlight => Volatile.Read(ref _inFlight);
    /// <summary>Главный поток известен (Attach или первый Pump) — очередь кто-то разбирает.</summary>
    public bool Bound => _mainThreadId != 0;
    /// <summary>Сейчас главный поток ресурса (тот, что зовёт Pump).</summary>
    public bool IsMainThread => _mainThreadId != 0 && Environment.CurrentManagedThreadId == _mainThreadId;

    /// <summary>Запомнить главный поток — из OnStart ресурса, до первого Pump.</summary>
    public void BindToCurrentThread() => _mainThreadId = Environment.CurrentManagedThreadId;

    public override void Post(SendOrPostCallback d, object? state)
    {
        // После закрытия ресурса его код больше не выполняется: сборки
        // выгружаются, а продолжение звало бы API уже без ресурса.
        if (_closed) return;
        _queue.Enqueue((d, state));
    }

    public override void Send(SendOrPostCallback d, object? state)
    {
        if (IsMainThread) { Run(d, state); return; }
        // Синхронная отправка из фона: ждём выполнения в следующем тике.
        using var done = new ManualResetEventSlim(false);
        Exception? error = null;
        Post(_ => { try { d(state); } catch (Exception ex) { error = ex; } finally { done.Set(); } }, null);
        if (!done.Wait(TimeSpan.FromSeconds(30)))
            throw new TimeoutException("главный поток ресурса не разобрал очередь за 30 с (ресурс остановлен или тик завис)");
        if (error is not null) throw new InvalidOperationException("ошибка в главном потоке ресурса", error);
    }

    public override SynchronizationContext CreateCopy() => this;

    /// <summary>
    /// Из OnTick ресурса: выполнить накопленные продолжения в пределах бюджета.
    /// Возвращает, сколько выполнено.
    /// </summary>
    public int Pump()
    {
        if (_mainThreadId == 0) BindToCurrentThread();
        if (_queue.IsEmpty || _closed) return 0;
        var started = Stopwatch.GetTimestamp();
        var done = 0;
        // Своё — только то, что было в очереди на начало разбора: продолжение,
        // поставившее новое (await Task.Yield() в цикле), не держит тик вечно.
        var limit = _queue.Count;
        while (done < limit && _queue.TryDequeue(out var item))
        {
            Run(item.Callback, item.State);
            done++;
            if (Stopwatch.GetElapsedTime(started).TotalMilliseconds >= BudgetMs) break;
        }
        if (!_queue.IsEmpty && Stopwatch.GetElapsedTime(started).TotalMilliseconds >= BudgetMs)
        {
            var now = Environment.TickCount64;
            if (now - _lastWarnMs > 30_000)
            {
                _lastWarnMs = now;
                _log($"[FloV:MP] [Async] продолжений больше, чем влезает в тик ({BudgetMs:0.#} мс): осталось {_queue.Count}, " +
                     "разберутся в следующих тиках. Тяжёлую работу — в Task.Run, а не в главный поток.");
            }
        }
        return done;
    }

    private void Run(SendOrPostCallback d, object? state)
    {
        var previous = Current;
        SetSynchronizationContext(this);
        try { d(state); }
        catch (Exception ex) { _log("[FloV:MP] [Async] ошибка в продолжении обработчика: " + ex); }
        finally { SetSynchronizationContext(previous); }
    }

    /// <summary>
    /// Запустить асинхронную работу из главного потока: синхронная часть —
    /// сразу, продолжения после await — через очередь. Ошибка работы пишется
    /// в журнал (не роняет сервер); отмена при остановке ресурса — молча.
    /// </summary>
    public void Start(Func<CancellationToken, Task> work, string what)
    {
        if (_closed || Stopped) return;
        var previous = Current;
        SetSynchronizationContext(this);
        Task task;
        try { task = work(_stopping.Token); }
        catch (Exception ex) { task = Task.FromException(ex); }
        finally { SetSynchronizationContext(previous); }
        if (task.IsCompletedSuccessfully) return;
        Interlocked.Increment(ref _inFlight);
        // Продолжение — на любой исход: живой прогон показал, что с
        // NotOnRanToCompletion успешно закончившийся обработчик не уменьшал
        // счётчик, и остановка ресурса зря ждала весь StopDrain.
        task.ContinueWith(t =>
        {
            Interlocked.Decrement(ref _inFlight);
            if (t.IsCompletedSuccessfully || t.IsCanceled) return;
            if (t.Exception?.InnerException is OperationCanceledException && Stopped) return;
            if (t.Exception is { } ex) _log($"[FloV:MP] [Async] {what}: {ex.InnerException ?? ex}");
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    /// <summary>Выполнить в главном потоке из любого (результат — задачей).</summary>
    public Task<T> Invoke<T>(Func<T> func)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (_closed) { tcs.SetCanceled(); return tcs.Task; }
        Post(_ =>
        {
            try { tcs.SetResult(func()); } catch (Exception ex) { tcs.SetException(ex); }
        }, null);
        return tcs.Task;
    }

    /// <summary><c>await queue.NextTick()</c> — продолжить в следующем тике ресурса.</summary>
    public Task NextTick() => Invoke(() => true);

    /// <summary>
    /// Из OnStop ресурса: отменить токен и дать отменённым обработчикам
    /// доработать (<see cref="StopDrain"/>) — их finally и уборка выполнятся,
    /// пока ресурс ещё жив. Что не успело — не выполнится: сборки ресурса
    /// выгружаются. Живым прогоном найдено: без доработки finally ожидающего
    /// обработчика при остановке ресурса молча пропадал.
    /// </summary>
    public void Stop()
    {
        if (_closed) return;
        _stopping.Cancel();   // отмена будит ожидания — их продолжения встают в очередь
        var deadline = Stopwatch.GetTimestamp() + (long)(StopDrain.TotalSeconds * Stopwatch.Frequency);
        // Ждём, пока все запущенные обработчики не закончатся (или срок): их
        // продолжения после отмены приходят в очередь, иногда из пула чуть позже.
        while (Stopwatch.GetTimestamp() < deadline)
        {
            if (_queue.TryDequeue(out var item)) { Run(item.Callback, item.State); continue; }
            if (InFlight == 0) break;
            Thread.Sleep(1);
        }
        _closed = true;
        var dropped = 0;
        while (_queue.TryDequeue(out _)) dropped++;
        if (dropped > 0) _log($"[FloV:MP] [Async] ресурс остановлен: {dropped} продолжений не выполнено.");
    }
}
