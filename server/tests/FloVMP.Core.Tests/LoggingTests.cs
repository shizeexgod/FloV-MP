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
            Category = LogCategory.Admin,
            Action = "kick",
            Actor = LogActor.Admin(7, "Nick"),
            Target = "12",
            Details = new Dictionary<string, object?> { ["reason"] = "afk" },
        });
        await sink.FlushAsync();

        var file = Path.Combine(_dir, "admin", "2026-03-04.jsonl");
        Assert.True(File.Exists(file));

        var line = File.ReadAllLines(file).Single();
        using var doc = JsonDocument.Parse(line);
        Assert.Equal("kick", doc.RootElement.GetProperty("Action").GetString());
        Assert.Equal("afk", doc.RootElement.GetProperty("Details").GetProperty("reason").GetString());
    }

    [Fact]
    public async Task Many_writes_all_land()
    {
        await using var sink = new FileLogSink(_dir);
        for (var i = 0; i < 500; i++)
            sink.Write(new LogEntry { Category = LogCategory.Admin, Action = "kick", Target = i.ToString() });

        await sink.FlushAsync();

        var files = Directory.GetFiles(Path.Combine(_dir, "admin"));
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
        GameLog.System("boot");
        GameLog.Admin("kick", LogActor.Admin(2, "root"), "1", ("reason", "afk"));
    }

    [Fact]
    public async Task Category_helpers_route_to_files()
    {
        GameLog.Configure(new FileLogSink(_dir));

        GameLog.Account("login", LogActor.Player(10, "Nick"), "1.2.3.4");
        GameLog.Admin("tp", LogActor.Admin(1, "root"), "10", ("x", 250));
        GameLog.Punishment("ban", LogActor.Admin(1, "root"), "10", "cheat", 86400);

        await GameLog.FlushAsync();

        Assert.True(Directory.Exists(Path.Combine(_dir, "account")));
        Assert.True(Directory.Exists(Path.Combine(_dir, "admin")));
        Assert.True(Directory.Exists(Path.Combine(_dir, "punishment")));

        var admin = Directory.GetFiles(Path.Combine(_dir, "admin")).Single();
        using var doc = JsonDocument.Parse(File.ReadAllLines(admin).Single());
        Assert.Equal("tp", doc.RootElement.GetProperty("Action").GetString());
        Assert.Equal(250, doc.RootElement.GetProperty("Details").GetProperty("x").GetInt32());
    }
}
