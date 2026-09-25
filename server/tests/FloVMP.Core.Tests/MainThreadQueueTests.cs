using System.Collections.Concurrent;
using FloVMP.Core.Async;
using Xunit;

namespace FloVMP.Core.Tests;

public class MainThreadQueueTests
{
    /// <summary>Поток «сервера»: крутит тики и разбирает очередь, как OnTick ресурса.</summary>
    private sealed class FakeServer : IDisposable
    {
        public readonly MainThreadQueue Queue;
        public readonly ConcurrentQueue<string> Log = new();
        private readonly Thread _thread;
        private readonly ConcurrentQueue<Action> _inTick = new();
        private volatile bool _stop;
        public int ThreadId;
        public long Tick;

        public FakeServer()
        {
            Queue = new MainThreadQueue(Log.Enqueue);
            _thread = new Thread(() =>
            {
                ThreadId = Environment.CurrentManagedThreadId;
                Queue.BindToCurrentThread();
                while (!_stop)
                {
                    Interlocked.Increment(ref Tick);
                    while (_inTick.TryDequeue(out var a)) a();
                    Queue.Pump();
                    Thread.Sleep(2);
                }
            }) { IsBackground = true };
            _thread.Start();
            SpinWait.SpinUntil(() => ThreadId != 0, 2000);
        }

        /// <summary>Выполнить в тике «сервера» (как обработчик события) и дождаться.</summary>
        public void InTick(Action a)
        {
            using var done = new ManualResetEventSlim();
            _inTick.Enqueue(() => { try { a(); } finally { done.Set(); } });
            Assert.True(done.Wait(3000));
        }

        public void Dispose() { _stop = true; _thread.Join(2000); }
    }

    [Fact]
    public async Task AfterAwait_CodeRunsOnMainThread_InLaterTick()
    {
        using var s = new FakeServer();
        var result = new TaskCompletionSource<(int Before, int After, long TickBefore, long TickAfter, bool CtxRestored)>();
        s.InTick(() => s.Queue.Start(async ct =>
        {
            var before = Environment.CurrentManagedThreadId;
            var tickBefore = Interlocked.Read(ref s.Tick);
            await Task.Run(() => Thread.Sleep(50), ct);      // «запрос в базу» в фоне
            result.SetResult((before, Environment.CurrentManagedThreadId, tickBefore, Interlocked.Read(ref s.Tick),
                SynchronizationContext.Current == s.Queue));
        }, "тест"));
        var r = await result.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(s.ThreadId, r.Before);
        Assert.Equal(s.ThreadId, r.After);                  // вернулись в главный поток
        Assert.True(r.TickAfter > r.TickBefore);             // тик не стоял, пока шёл запрос
        Assert.True(r.CtxRestored);
    }

    [Fact]
    public void ContextIsRestored_OtherResourcesAwaitsNotCaptured()
    {
        using var s = new FakeServer();
        SynchronizationContext? after = new SynchronizationContext();
        s.InTick(() =>
        {
            SynchronizationContext.SetSynchronizationContext(null);
            s.Queue.Start(async _ => await Task.Yield(), "тест");
            after = SynchronizationContext.Current;          // чужой код после нашего вызова
        });
        Assert.Null(after);
    }

    [Fact]
    public async Task HandlerError_IsLogged_NotThrown()
    {
        using var s = new FakeServer();
        s.InTick(() => s.Queue.Start(async _ => { await Task.Yield(); throw new InvalidOperationException("сломалось"); }, "мой обработчик"));
        s.InTick(() => s.Queue.Start(_ => throw new ArgumentException("сразу"), "синхронный"));
        await Task.Delay(300);
        Assert.Contains(s.Log, l => l.Contains("мой обработчик") && l.Contains("сломалось"));
        Assert.Contains(s.Log, l => l.Contains("синхронный") && l.Contains("сразу"));
    }

    [Fact]
    public async Task Stop_CancelsToken_AndDropsPendingContinuations()
    {
        using var s = new FakeServer();
        var ran = false;
        var tokenCancelled = new TaskCompletionSource<bool>();
        var gate = new TaskCompletionSource();
        s.InTick(() => s.Queue.Start(async ct =>
        {
            ct.Register(() => tokenCancelled.TrySetResult(true));
            await gate.Task;                                   // ждёт «базу»
            ran = true;                                        // после остановки — не должно выполниться
        }, "тест"));
        s.InTick(() => s.Queue.Stop());
        gate.SetResult();
        Assert.True(await tokenCancelled.Task.WaitAsync(TimeSpan.FromSeconds(2)));
        await Task.Delay(200);
        Assert.False(ran);
        Assert.DoesNotContain(s.Log, l => l.Contains("ошибка"));   // отмена при остановке — не ошибка
    }

    [Fact]
    public void Stop_LetsCancelledHandlersFinish_TheirFinallyRuns()
    {
        // Живой прогон: без доработки finally ожидающего обработчика при
        // остановке ресурса молча пропадал.
        var q = new MainThreadQueue(_ => { });
        q.BindToCurrentThread();
        var cleaned = false;
        q.Start(async ct =>
        {
            try { await Task.Delay(5000, ct); }
            finally { cleaned = true; }
        }, "тест");
        var sw = System.Diagnostics.Stopwatch.StartNew();
        q.Stop();                                              // из «OnStop», в главном потоке
        Assert.True(cleaned);
        Assert.True(sw.ElapsedMilliseconds < 400);             // не ждём зря весь срок
        Assert.Equal(0, q.InFlight);

        // Живой прогон: успешно отработавшие обработчики не должны числиться
        // незаконченными — иначе остановка ждёт весь срок.
        var q2 = new MainThreadQueue(_ => { });
        q2.BindToCurrentThread();
        var done = false;
        q2.Start(async _ => { await Task.Delay(10); done = true; }, "успешный");
        for (var i = 0; i < 200 && !done; i++) { q2.Pump(); Thread.Sleep(5); }
        Assert.True(done);
        SpinWait.SpinUntil(() => q2.InFlight == 0, 1000);
        Assert.Equal(0, q2.InFlight);
        sw.Restart(); q2.Stop();
        Assert.True(sw.ElapsedMilliseconds < 100);

        // Без работы остановка мгновенная.
        var idle = new MainThreadQueue(_ => { });
        sw.Restart(); idle.Stop();
        Assert.True(sw.ElapsedMilliseconds < 50);
    }

    [Fact]
    public async Task Invoke_FromBackground_RunsOnMain()
    {
        using var s = new FakeServer();
        var id = await Task.Run(() => s.Queue.Invoke(() => Environment.CurrentManagedThreadId)).WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(s.ThreadId, id);
        var tick0 = Interlocked.Read(ref s.Tick);
        await s.Queue.NextTick().WaitAsync(TimeSpan.FromSeconds(3));
        Assert.True(Interlocked.Read(ref s.Tick) >= tick0);
    }

    [Fact]
    public void Pump_RespectsBudget_AndSelfReschedulingDoesNotHangTick()
    {
        var q = new MainThreadQueue(_ => { }) { BudgetMs = 5 };
        q.BindToCurrentThread();
        for (var i = 0; i < 100; i++) q.Post(_ => Thread.Sleep(1), null);
        var first = q.Pump();
        Assert.InRange(first, 1, 20);                         // 5 мс бюджета — не все 100
        Assert.True(q.Pending > 0);

        // Продолжение, которое каждый раз ставит себя снова, не держит тик вечно.
        var q2 = new MainThreadQueue(_ => { });
        q2.BindToCurrentThread();
        SendOrPostCallback again = null!;
        again = _ => q2.Post(again, null);
        q2.Post(again, null);
        Assert.Equal(1, q2.Pump());
        Assert.Equal(1, q2.Pending);
    }

    [Fact]
    public void PostAfterStop_IsIgnored()
    {
        var q = new MainThreadQueue(_ => { });
        q.Stop();
        q.Post(_ => throw new Exception("не должно"), null);
        Assert.Equal(0, q.Pending);
        Assert.True(q.Invoke(() => 1).IsCanceled);
    }
}
