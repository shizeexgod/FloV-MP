using Xunit;

namespace FloVMP.Core.Tests;

/// <summary>
/// Очередь отложенных действий главного потока.
///
/// Логика намеренно повторена здесь в виде эталона, а не берётся из
/// FloVMP.Gamemode: тот проект тянет AltV.Net и в юнит-тестах не поднимается.
/// Проверяется именно поведение очереди — сроки, порядок обработки, живучесть
/// при падении одной задачи — потому что на ней держится правило «сущности
/// alt:V трогаем только из тика».
///
/// Почему правило важно: между проверкой player.Exists и следующей строкой на
/// фоновом потоке игрок успевает отключиться, и обращение уходит в
/// освобождённую нативную память движка. В тике такого окна нет.
/// </summary>
public class MainThreadSchedulerTests
{
    /// <summary>Эталонная копия очереди из FloVMP.Gamemode.Systems.</summary>
    private sealed class Scheduler
    {
        private sealed record Item(long DueAtMs, Action Action, string Name);

        private readonly System.Collections.Concurrent.ConcurrentQueue<Item> _pending = new();
        public List<string> Failures { get; } = new();
        public long NowMs { get; set; }

        public int PendingCount => _pending.Count;

        public void RunAfter(int delayMs, string name, Action action)
        {
            if (action is null) return;
            _pending.Enqueue(new Item(NowMs + Math.Max(0, delayMs), action, name));
        }

        public void Pump()
        {
            if (_pending.IsEmpty) return;
            var count = _pending.Count;
            for (var i = 0; i < count; i++)
            {
                if (!_pending.TryDequeue(out var item)) break;
                if (item.DueAtMs > NowMs) { _pending.Enqueue(item); continue; }
                try { item.Action(); }
                catch (Exception ex) { Failures.Add($"{item.Name}: {ex.Message}"); }
            }
        }

        public void Clear() { while (_pending.TryDequeue(out _)) { } }
    }

    [Fact]
    public void Action_DoesNotRunBeforeItsTime()
    {
        var s = new Scheduler();
        var ran = false;
        s.RunAfter(300, "test", () => ran = true);

        s.Pump();
        Assert.False(ran);
        Assert.Equal(1, s.PendingCount);

        s.NowMs = 300;
        s.Pump();
        Assert.True(ran);
        Assert.Equal(0, s.PendingCount);
    }

    [Fact]
    public void ZeroDelay_RunsOnNextPump()
    {
        var s = new Scheduler();
        var ran = false;
        s.RunAfter(0, "now", () => ran = true);
        s.Pump();
        Assert.True(ran);
    }

    [Fact]
    public void Action_RunsExactlyOnce()
    {
        var s = new Scheduler();
        var calls = 0;
        s.RunAfter(0, "once", () => calls++);

        s.Pump();
        s.Pump();
        s.Pump();

        Assert.Equal(1, calls);
    }

    [Fact]
    public void FailingAction_DoesNotStopTheRest()
    {
        // Одна упавшая отложенная задача не должна ронять тик и не должна
        // мешать остальным: тик обслуживает весь сервер.
        var s = new Scheduler();
        var second = false;

        s.RunAfter(0, "плохая", () => throw new InvalidOperationException("бум"));
        s.RunAfter(0, "хорошая", () => second = true);

        s.Pump();

        Assert.True(second);
        Assert.Single(s.Failures);
        Assert.Contains("плохая", s.Failures[0]);
    }

    [Fact]
    public void NotDueActions_StayQueued_WithoutSpinning()
    {
        // Перекладываемые обратно задачи не должны зациклить обход очереди:
        // Pump фиксирует число элементов на входе.
        var s = new Scheduler();
        for (var i = 0; i < 5; i++) s.RunAfter(1000, "later" + i, () => { });

        s.Pump(); // не должен зависнуть
        Assert.Equal(5, s.PendingCount);
    }

    [Fact]
    public void MixedDelays_RunAsTheirTimeComes()
    {
        var s = new Scheduler();
        var done = new List<string>();

        s.RunAfter(0, "a", () => done.Add("a"));
        s.RunAfter(100, "b", () => done.Add("b"));
        s.RunAfter(200, "c", () => done.Add("c"));

        s.Pump();
        Assert.Equal(new[] { "a" }, done);

        s.NowMs = 100;
        s.Pump();
        Assert.Equal(new[] { "a", "b" }, done);

        s.NowMs = 200;
        s.Pump();
        Assert.Equal(new[] { "a", "b", "c" }, done);
    }

    [Fact]
    public void Clear_DropsEverything()
    {
        var s = new Scheduler();
        var ran = false;
        s.RunAfter(0, "x", () => ran = true);

        s.Clear();
        s.Pump();

        Assert.False(ran);
        Assert.Equal(0, s.PendingCount);
    }

    [Fact]
    public void NullAction_IsIgnored()
    {
        var s = new Scheduler();
        s.RunAfter(0, "null", null!);
        Assert.Equal(0, s.PendingCount);
    }

    [Fact]
    public void EmptyQueue_PumpIsNoOp()
    {
        var s = new Scheduler();
        s.Pump();
        Assert.Equal(0, s.PendingCount);
        Assert.Empty(s.Failures);
    }
}
