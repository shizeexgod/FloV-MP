using System.Text.Json;
using FloVMP.Core.Logging;
using Xunit;

namespace FloVMP.Core.Tests;

public sealed class FileLogSinkTests : IDisposable
{
    private readonly string _dir;

    public FileLogSinkTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "flovmp-log-tests", Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    [Fact]
    public async Task Writes_jsonl_partitioned_by_category_and_date()
    {
        var fixedNow = new DateTime(2026, 3, 4, 10, 0, 0, DateTimeKind.Utc);
        await using var sink = new FileLogSink(_dir, utcNow: () => fixedNow);

        sink.Write(new LogEntry
        {
            TsUtc = fixedNow.ToString("O"),
            Category = LogCategory.Money,
            Action = "delta",
            Actor = LogActor.Player(7, "Nick"),
            Target = "7",
            Details = new Dictionary<string, object?> { ["delta"] = -500, ["source"] = "shop" },
        });
        await sink.FlushAsync();

        var file = Path.Combine(_dir, "money", "2026-03-04.jsonl");
        Assert.True(File.Exists(file));

        var line = File.ReadAllLines(file).Single();
        using var doc = JsonDocument.Parse(line);
        Assert.Equal("delta", doc.RootElement.GetProperty("Action").GetString());
        Assert.Equal("shop", doc.RootElement.GetProperty("Details").GetProperty("source").GetString());
    }

    [Fact]
    public async Task Many_writes_all_land()
    {
        await using var sink = new FileLogSink(_dir);
        for (var i = 0; i < 500; i++)
            sink.Write(new LogEntry { Category = LogCategory.Item, Action = "add", Target = i.ToString() });

        await sink.FlushAsync();

        var files = Directory.GetFiles(Path.Combine(_dir, "item"));
        var total = files.Sum(f => File.ReadAllLines(f).Length);
        Assert.Equal(500, total);
    }

    [Fact]
    public async Task Flush_and_dispose_drain_remaining()
    {
        var sink = new FileLogSink(_dir);
        sink.Write(new LogEntry { Category = LogCategory.System, Action = "boot" });
        await sink.DisposeAsync(); // без явного Flush — Dispose добивает остаток

        var file = Directory.GetFiles(Path.Combine(_dir, "system")).Single();
        Assert.Single(File.ReadAllLines(file));
    }
}

public sealed class GameLogTests : IDisposable
{
    private readonly string _dir;

    public GameLogTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "flovmp-gamelog-tests", Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        GameLog.ShutdownAsync().GetAwaiter().GetResult();
        try { Directory.Delete(_dir, true); } catch { }
    }

    [Fact]
    public void No_sink_configured_is_silent_noop()
    {
        // не должно бросать
        GameLog.Money(LogActor.Player(1, "x"), 100, "test", 100);
        GameLog.Admin("kick", LogActor.Admin(2, "root"), "1", ("reason", "afk"));
    }

    [Fact]
    public async Task Category_helpers_route_to_files()
    {
        GameLog.Configure(new FileLogSink(_dir));

        GameLog.Account("login", LogActor.Player(10, "Nick"), "1.2.3.4");
        GameLog.Money(LogActor.Player(10, "Nick"), 250, "job:taxi", 1250);
        GameLog.Item("use", LogActor.Player(10, "Nick"), "bandage", 1);
        GameLog.Punishment("ban", LogActor.Admin(1, "root"), "10", "cheat", 86400);

        await GameLog.FlushAsync();

        Assert.True(Directory.Exists(Path.Combine(_dir, "account")));
        Assert.True(Directory.Exists(Path.Combine(_dir, "money")));
        Assert.True(Directory.Exists(Path.Combine(_dir, "item")));
        Assert.True(Directory.Exists(Path.Combine(_dir, "punishment")));

        var money = Directory.GetFiles(Path.Combine(_dir, "money")).Single();
        using var doc = JsonDocument.Parse(File.ReadAllLines(money).Single());
        Assert.Equal("job:taxi", doc.RootElement.GetProperty("Details").GetProperty("source").GetString());
        Assert.Equal(1250, doc.RootElement.GetProperty("Details").GetProperty("after").GetInt64());
    }
}
