using System.Threading.Tasks;
using FloVMP.Core.Database;
using Xunit;

namespace FloVMP.Core.Tests;

public class WriteBehindQueueTests
{
    [Fact]
    public async Task Basic_Flush_Persists_And_Clears()
    {
        var q = new WriteBehindQueue<string, int>();
        q.MarkDirty("a", 10);
        q.MarkDirty("b", 20);

        int persisted = 0;
        var n = await q.FlushAsync(batch => { persisted = batch.Count; return Task.FromResult(true); });

        Assert.Equal(2, n);
        Assert.Equal(2, persisted);
        Assert.Equal(0, q.PendingCount);
    }

    [Fact]
    public async Task Failed_Persist_Keeps_Entities_Dirty()
    {
        var q = new WriteBehindQueue<string, int>();
        q.MarkDirty("a", 10);

        var n = await q.FlushAsync(_ => Task.FromResult(false)); // persist не удался
        Assert.Equal(0, n);
        Assert.Equal(1, q.PendingCount); // осталось грязным для повтора
    }

    [Fact]
    public async Task Redirty_During_Flush_Is_Not_Lost()
    {
        // Регресс: во время async-сброса запись перегрязняется новым значением.
        // Раньше сравнение current<=record (один объект) всегда истинно -> запись
        // удалялась и НОВОЕ значение терялось. Теперь через Version не теряется.
        var q = new WriteBehindQueue<string, int>();
        q.MarkDirty("k", 1);

        var res = await q.FlushAsync(async batch =>
        {
            // имитируем конкурентную запись V2 во время "сохранения" V1
            q.MarkDirty("k", 2);
            await Task.Yield();
            return true;
        });

        Assert.Equal(1, res);              // один батч обработан
        Assert.Equal(1, q.PendingCount);   // V2 НЕ потерян — остался грязным

        // следующий сброс должен сохранить именно V2
        int got = -1;
        await q.FlushAsync(batch => { got = batch[0].Entity; return Task.FromResult(true); });
        Assert.Equal(2, got);
        Assert.Equal(0, q.PendingCount);
    }

    [Fact]
    public async Task FlushEntityImmediately_Redirty_During_Save_Is_Not_Lost()
    {
        var q = new WriteBehindQueue<string, int>();
        q.MarkDirty("k", 1);

        var ok = await q.FlushEntityImmediatelyAsync("k", async rec =>
        {
            q.MarkDirty("k", 2); // перегрязнили во время сохранения
            await Task.Yield();
            return true;
        });

        Assert.True(ok);
        Assert.Equal(1, q.PendingCount); // V2 не потерян
    }

    [Fact]
    public async Task MarkDirty_Same_Key_Coalesces_To_Latest()
    {
        var q = new WriteBehindQueue<string, int>();
        q.MarkDirty("k", 1);
        q.MarkDirty("k", 2);
        q.MarkDirty("k", 3);

        Assert.Equal(1, q.PendingCount); // коалесинг: одна запись на ключ

        int got = -1;
        await q.FlushAsync(batch => { got = batch[0].Entity; return Task.FromResult(true); });
        Assert.Equal(3, got); // последнее значение
    }
}
