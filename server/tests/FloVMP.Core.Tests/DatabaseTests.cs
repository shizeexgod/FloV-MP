using FloVMP.Core.Database;
using FloVMP.Core.Auth;
using Xunit;

namespace FloVMP.Core.Tests;

public sealed class DatabaseTests : IDisposable
{
    private readonly string _testDir;

    public DatabaseTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "flovmp-db-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, true); } catch { }
    }

    [Fact]
    public void DatabaseConfig_builds_valid_connection_string()
    {
        var cfg = new DatabaseConfig
        {
            Host = "188.127.229.224",
            Port = 3306,
            Database = "derzhava_rp",
            User = "flovmp",
            Password = "MySecretPassword123"
        };

        var cs = cfg.BuildConnectionString();
        Assert.Contains("Server=188.127.229.224", cs);
        Assert.Contains("Port=3306", cs);
        Assert.Contains("Database=derzhava_rp", cs);
        Assert.Contains("User ID=flovmp", cs);
        Assert.Contains("Password=MySecretPassword123", cs);
    }

    [Fact]
    public void Factory_falls_back_to_json_when_mysql_unreachable()
    {
        var jsonPath = Path.Combine(_testDir, "fallback_accounts.json");
        var badConnectionString = "Server=127.0.0.1;Port=59999;Database=fake_db;User ID=nobody;Password=bad;ConnectionTimeout=1;";

        var store = AccountStoreFactory.Create(badConnectionString, jsonPath);

        Assert.NotNull(store);
        Assert.IsType<JsonAccountStore>(store);

        var acc = store.Create("FallbackUser", "secret123");
        Assert.Equal("FallbackUser", acc.Username);
        Assert.True(store.Exists("FallbackUser"));
    }

    [Fact]
    public void Factory_uses_json_when_connection_string_empty()
    {
        var jsonPath = Path.Combine(_testDir, "empty_cs_accounts.json");
        var store = AccountStoreFactory.Create("", jsonPath);

        Assert.NotNull(store);
        Assert.IsType<JsonAccountStore>(store);
    }
}