using FloVMP.Core.Database;
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
            Database = "flovmp_server",
            User = "flovmp",
            Password = "MySecretPassword123"
        };

        var cs = cfg.BuildConnectionString();
        Assert.Contains("Server=188.127.229.224", cs);
        Assert.Contains("Port=3306", cs);
        Assert.Contains("Database=flovmp_server", cs);
        Assert.Contains("User ID=flovmp", cs);
        Assert.Contains("Password=MySecretPassword123", cs);
    }

    [Fact]
    public void PrepareDatabase_returns_false_when_mysql_unreachable()
    {
        var badConnectionString = "Server=127.0.0.1;Port=59999;Database=fake_db;User ID=nobody;Password=bad;ConnectionTimeout=1;";
        Assert.False(AccountStoreFactory.TryPrepareDatabase(badConnectionString));
    }

    [Fact]
    public void PrepareDatabase_returns_false_when_connection_string_empty()
    {
        Assert.False(AccountStoreFactory.TryPrepareDatabase(""));
        Assert.False(AccountStoreFactory.TryPrepareDatabase(null));
    }
}
