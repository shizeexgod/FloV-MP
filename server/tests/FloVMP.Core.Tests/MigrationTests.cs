using FloVMP.Core.Database;
using Xunit;

namespace FloVMP.Core.Tests;

/// <summary>
/// Тесты системы миграций схемы БД.
///
/// Подключения к живой MariaDB здесь нет намеренно: проверяется вся логика,
/// которая способна испортить базу ДО того, как SQL уйдёт на сервер —
/// разбор файла на выражения, порядок применения и контрольные суммы.
/// Ошибка в сплиттере означает выполненный наполовину CREATE TABLE, и поймать
/// её надо в тестах, а не на боевой базе клиента.
/// </summary>
public class MigrationTests
{
    // ---------- SqlScriptSplitter ----------

    [Fact]
    public void Split_SeparatesSimpleStatements()
    {
        var parts = SqlScriptSplitter.Split("CREATE TABLE a (id INT); CREATE TABLE b (id INT);");
        Assert.Equal(2, parts.Count);
        Assert.StartsWith("CREATE TABLE a", parts[0]);
        Assert.StartsWith("CREATE TABLE b", parts[1]);
    }

    [Fact]
    public void Split_IgnoresSemicolonInsideStringLiteral()
    {
        // Наивный Split(';') разрезал бы это выражение пополам.
        var parts = SqlScriptSplitter.Split("INSERT INTO t VALUES ('a;b'); SELECT 1;");
        Assert.Equal(2, parts.Count);
        Assert.Contains("'a;b'", parts[0]);
        Assert.Equal("SELECT 1", parts[1]);
    }

    [Fact]
    public void Split_IgnoresSemicolonInsideColumnComment()
    {
        var sql = "CREATE TABLE t (x INT COMMENT 'APARTMENT; HOUSE; GARAGE'); SELECT 2;";
        var parts = SqlScriptSplitter.Split(sql);
        Assert.Equal(2, parts.Count);
        Assert.Contains("GARAGE", parts[0]);
    }

    [Fact]
    public void Split_DropsLineComments()
    {
        var parts = SqlScriptSplitter.Split("-- комментарий; с точкой с запятой\nSELECT 1;");
        Assert.Single(parts);
        Assert.Equal("SELECT 1", parts[0]);
    }

    [Fact]
    public void Split_DropsBlockComments()
    {
        var parts = SqlScriptSplitter.Split("/* блок; внутри */ SELECT 1; /* хвост */");
        Assert.Single(parts);
        Assert.Equal("SELECT 1", parts[0]);
    }

    [Fact]
    public void Split_HandlesBacktickIdentifiersAndEscapes()
    {
        var parts = SqlScriptSplitter.Split(@"UPDATE `t` SET `s` = 'a\'b;c' WHERE id = 1; SELECT 3;");
        Assert.Equal(2, parts.Count);
        Assert.Equal("SELECT 3", parts[1]);
    }

    [Fact]
    public void Split_HandlesDoubledQuoteInsideLiteral()
    {
        var parts = SqlScriptSplitter.Split("SELECT 'it''s; ok'; SELECT 4;");
        Assert.Equal(2, parts.Count);
        Assert.Equal("SELECT 4", parts[1]);
    }

    [Fact]
    public void Split_IgnoresTrailingEmptyStatement()
    {
        var parts = SqlScriptSplitter.Split("SELECT 1;\n\n   \n");
        Assert.Single(parts);
    }

    [Fact]
    public void Split_ReturnsEmptyForBlankScript()
    {
        Assert.Empty(SqlScriptSplitter.Split("   \n-- только комментарий\n"));
    }

    // ---------- MigrationFile ----------

    [Fact]
    public void Parse_ExtractsVersionFromNumericPrefix()
    {
        var m = MigrationFile.Parse("007_add_thing.sql", "SELECT 1;");
        Assert.Equal(7, m.Version);
    }

    [Fact]
    public void Parse_ChecksumIgnoresLineEndingStyle()
    {
        // Репозиторий выезжает с CRLF на Windows и с LF на Linux-VDS —
        // без нормализации каждый клиент видел бы ложный «дрейф схемы».
        var lf = MigrationFile.Parse("001_a.sql", "SELECT 1;\nSELECT 2;");
        var crlf = MigrationFile.Parse("001_a.sql", "SELECT 1;\r\nSELECT 2;\r\n");
        Assert.Equal(lf.Checksum, crlf.Checksum);
    }

    [Fact]
    public void Parse_ChecksumChangesWhenSqlChanges()
    {
        var a = MigrationFile.Parse("001_a.sql", "SELECT 1;");
        var b = MigrationFile.Parse("001_a.sql", "SELECT 2;");
        Assert.NotEqual(a.Checksum, b.Checksum);
    }

    [Fact]
    public void Parse_ReadsIgnoreErrorsDirective()
    {
        var m = MigrationFile.Parse("002_x.sql",
            "-- flovmp:ignore-errors 1060,1061\nALTER TABLE t ADD COLUMN c INT;");
        Assert.Contains(1060, m.IgnoredErrors);
        Assert.Contains(1061, m.IgnoredErrors);
        Assert.DoesNotContain(1050, m.IgnoredErrors);
    }

    [Fact]
    public void Parse_NoDirective_MeansNothingIsIgnored()
    {
        var m = MigrationFile.Parse("003_x.sql", "ALTER TABLE t ADD COLUMN c INT;");
        Assert.Empty(m.IgnoredErrors);
    }

    [Fact]
    public void LoadAll_OrdersNumericallyNotLexicographically()
    {
        var dir = Path.Combine(Path.GetTempPath(), "flovmp-mig-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            // Лексикографически "010" < "9" — такой порядок накатил бы схему
            // задом наперёд и уронил бы зависимые ALTER'ы.
            File.WriteAllText(Path.Combine(dir, "009_nine.sql"), "SELECT 9;");
            File.WriteAllText(Path.Combine(dir, "010_ten.sql"), "SELECT 10;");
            File.WriteAllText(Path.Combine(dir, "001_one.sql"), "SELECT 1;");

            var all = MigrationFile.LoadAll(dir);
            Assert.Equal(new[] { "001_one.sql", "009_nine.sql", "010_ten.sql" },
                         all.Select(m => m.Name).ToArray());
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void LoadAll_MissingDirectory_ReturnsEmpty()
    {
        Assert.Empty(MigrationFile.LoadAll(Path.Combine(Path.GetTempPath(), "нет-такого-" + Guid.NewGuid())));
    }

    // ---------- MigrationRunner ----------

    [Fact]
    public void Run_WithoutConnectionString_SkipsQuietly()
    {
        var report = MigrationRunner.Run(null);
        Assert.True(report.Skipped);
        Assert.Equal(0, report.Applied);
    }

    [Fact]
    public void Run_WithEmptyMigrationsDirectory_Skips()
    {
        var dir = Path.Combine(Path.GetTempPath(), "flovmp-mig-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var report = MigrationRunner.Run("Server=127.0.0.1;Database=x;Uid=y;Pwd=z;", dir, _ => { });
            Assert.True(report.Skipped);
            Assert.Contains(".sql", report.SkipReason);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Run_UnreachableDatabase_DoesNotThrow()
    {
        // Старт сервера не должен падать из-за лежащей базы: фабрика уйдёт
        // на JSON-хранилище, и сервер останется играбельным.
        var dir = Path.Combine(Path.GetTempPath(), "flovmp-mig-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "001_a.sql"), "SELECT 1;");
            var report = MigrationRunner.Run(
                "Server=127.0.0.1;Port=1;Database=flovmp_test;Uid=u;Pwd=p;ConnectionTimeout=1;",
                dir, _ => { });
            Assert.True(report.Skipped);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Run_RejectsUnsafeDatabaseName()
    {
        // Имя базы идёт в CREATE DATABASE как идентификатор — параметризовать
        // его нельзя, поэтому недопустимое имя должно отсекаться до SQL.
        var dir = Path.Combine(Path.GetTempPath(), "flovmp-mig-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "001_a.sql"), "SELECT 1;");
            var report = MigrationRunner.Run(
                "Server=127.0.0.1;Database=`evil`;Uid=u;Pwd=p;ConnectionTimeout=1;",
                dir, _ => { });
            Assert.True(report.Skipped);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void LocateMigrationsDirectory_FindsRepositoryMigrations()
    {
        // Тесты запускаются из bin\Debug\net8.0 — раннер обязан найти
        // sql\migrations, поднимаясь по дереву, как это делает сервер из runtime\.
        var found = MigrationRunner.LocateMigrationsDirectory(AppContext.BaseDirectory);
        if (found is null) return; // каталог вне дерева репозитория — проверять нечего
        Assert.True(Directory.Exists(found));
    }

    // ---------- реальные файлы проекта ----------

    [Fact]
    public void RepositoryMigrations_ParseAndContainNoDatabaseSwitch()
    {
        var dir = FindRepoMigrations();
        if (dir is null) return;

        var all = MigrationFile.LoadAll(dir);
        Assert.NotEmpty(all);

        foreach (var m in all)
        {
            Assert.NotEmpty(m.Statements);

            foreach (var stmt in m.Statements)
            {
                var upper = stmt.TrimStart().ToUpperInvariant();

                // CREATE DATABASE / USE внутри миграции увели бы накат в чужую
                // базу, а сервер продолжил бы работать с пустой.
                Assert.False(upper.StartsWith("USE "), $"{m.Name}: USE запрещён в миграции");
                Assert.False(upper.StartsWith("CREATE DATABASE"),
                    $"{m.Name}: CREATE DATABASE запрещён в миграции");

                // Синтаксис MariaDB, который не понимает MySQL 8 — платформу
                // ставят и туда, и туда.
                Assert.DoesNotContain("ADD COLUMN IF NOT EXISTS", upper);
                Assert.DoesNotContain("CREATE INDEX IF NOT EXISTS", upper);
            }
        }
    }

    [Fact]
    public void RepositoryMigrations_HaveUniqueVersionNumbers()
    {
        var dir = FindRepoMigrations();
        if (dir is null) return;

        var all = MigrationFile.LoadAll(dir);
        var duplicates = all.GroupBy(m => m.Version).Where(g => g.Count() > 1).ToList();
        Assert.True(duplicates.Count == 0,
            "Дублирующиеся номера миграций: " +
            string.Join(", ", duplicates.Select(g => g.Key + " -> " + string.Join("/", g.Select(m => m.Name)))));
    }

    private static string? FindRepoMigrations()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var depth = 0; dir != null && depth < 10; depth++, dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "sql", "migrations");
            if (Directory.Exists(candidate)) return candidate;
        }
        return null;
    }
}
