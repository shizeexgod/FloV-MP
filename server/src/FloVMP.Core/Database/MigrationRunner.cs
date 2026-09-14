using System.Diagnostics;
using System.Text.RegularExpressions;
using FloVMP.Core.Logging;
using MySqlConnector;

namespace FloVMP.Core.Database;

/// <summary>Итог одной миграции в отчёте.</summary>
public sealed record MigrationOutcome(string Name, string Status, long DurationMs, string? Detail = null);

/// <summary>Отчёт о прогоне миграций.</summary>
public sealed class MigrationReport
{
    public List<MigrationOutcome> Items { get; } = new();
    public List<string> Drift { get; } = new();
    public bool Skipped { get; init; }
    public string? SkipReason { get; init; }

    public int Applied => Items.Count(i => i.Status == "applied");
    public int AlreadyUpToDate => Items.Count(i => i.Status == "up-to-date");
    public bool HasDrift => Drift.Count > 0;
}

/// <summary>
/// Накат схемы БД из каталога <c>sql/migrations</c>.
///
/// Зачем: до этого схема жила только в <c>sql/schema.sql</c> и накатывалась
/// руками. При установке платформы клиенту это означало «забыл выполнить файл →
/// сервер молча свалился на JSON-хранилище», а при обновлении — расхождение
/// схемы у разных клиентов. Раннер делает накат частью старта сервера.
///
/// Горизонтальное масштабирование: несколько инстансов, стартующих
/// одновременно на одну базу, договариваются через именованный
/// <c>GET_LOCK</c> — мигрирует ровно один, остальные ждут и затем видят, что
/// всё уже применено.
///
/// Транзакций здесь нет намеренно: DDL в MySQL/MariaDB вызывает неявный commit,
/// «откатить половину CREATE TABLE» физически невозможно. Поэтому миграция
/// записывается как применённая только после успеха ВСЕХ её выражений, а
/// повторный накат после падения посередине делается безопасным через
/// <c>IF NOT EXISTS</c> и директиву <c>flovmp:ignore-errors</c>.
/// </summary>
public static class MigrationRunner
{
    public const string HistoryTable = "schema_migrations";

    /// <summary>Сколько ждать чужую миграцию, прежде чем сдаться.</summary>
    public static int LockTimeoutSeconds { get; set; } =
        int.TryParse(Environment.GetEnvironmentVariable("FLOVMP_DB_MIGRATE_LOCK_TIMEOUT"), out var t) ? t : 120;

    /// <summary>
    /// Считать ли расхождение контрольной суммы применённой миграции фатальным.
    /// По умолчанию — нет (громкое предупреждение): уронить боевой сервер из-за
    /// правки комментария в старом .sql хуже, чем о ней сообщить. Включается
    /// через <c>FLOVMP_DB_MIGRATE_STRICT=1</c> для CI и приёмки.
    /// </summary>
    public static bool Strict =>
        Environment.GetEnvironmentVariable("FLOVMP_DB_MIGRATE_STRICT") == "1";

    private static readonly Regex SafeIdentifier = new("^[A-Za-z0-9_$]+$", RegexOptions.Compiled);

    private static string EngineVersion =>
        typeof(MigrationRunner).Assembly.GetName().Version?.ToString() ?? "0.0.0";

    /// <summary>
    /// Ищет каталог миграций относительно рабочей папки сервера. Сервер
    /// запускается из <c>runtime/</c>, а репозиторий лежит выше — поэтому
    /// проверяем несколько уровней вверх, а не один жёстко заданный путь.
    /// </summary>
    public static string? LocateMigrationsDirectory(string? startDir = null)
    {
        var explicitDir = Environment.GetEnvironmentVariable("FLOVMP_DB_MIGRATIONS_DIR");
        if (!string.IsNullOrWhiteSpace(explicitDir))
            return Directory.Exists(explicitDir) ? explicitDir : null;

        var dir = new DirectoryInfo(startDir ?? Directory.GetCurrentDirectory());
        for (var depth = 0; dir != null && depth < 6; depth++, dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "sql", "migrations");
            if (Directory.Exists(candidate)) return candidate;
        }
        return null;
    }

    /// <summary>
    /// Прогон миграций. Не бросает при недоступной БД — возвращает отчёт со
    /// <see cref="MigrationReport.Skipped"/>, потому что старт сервера не должен
    /// падать из-за временно лежащей базы: фабрика хранилища всё равно уйдёт на
    /// локальный JSON и сервер останется играбельным.
    /// </summary>
    public static MigrationReport Run(string? connectionString, string? migrationsDir = null, Action<string>? log = null)
    {
        log ??= Console.WriteLine;

        if (string.IsNullOrWhiteSpace(connectionString))
            return new MigrationReport { Skipped = true, SkipReason = "нет строки подключения (режим JSON-хранилища)" };

        migrationsDir ??= LocateMigrationsDirectory();
        if (string.IsNullOrWhiteSpace(migrationsDir) || !Directory.Exists(migrationsDir))
            return new MigrationReport { Skipped = true, SkipReason = "каталог sql/migrations не найден" };

        var migrations = MigrationFile.LoadAll(migrationsDir);
        if (migrations.Count == 0)
            return new MigrationReport { Skipped = true, SkipReason = "в каталоге нет .sql-файлов" };

        try
        {
            EnsureDatabaseExists(connectionString, log);
            return Apply(connectionString, migrations, log);
        }
        catch (Exception ex)
        {
            var code = ex is MySqlException my ? $" (код {my.Number})" : "";
            log($"[FloV:MP] [DB] Миграции не выполнены: {ex.Message}{code}");
            GameLog.System("db_migrate_failed", ("error", ex.Message));
            if (Strict) throw;
            return new MigrationReport { Skipped = true, SkipReason = ex.Message };
        }
    }

    /// <summary>
    /// Создаёт базу, если её ещё нет. Без этого первый запуск у клиента падал
    /// бы на «Unknown database» и уходил в JSON-режим, хотя MariaDB поднята и
    /// доступна — типовая ошибка установки.
    /// </summary>
    private static void EnsureDatabaseExists(string connectionString, Action<string> log)
    {
        var builder = new MySqlConnectionStringBuilder(connectionString);
        var dbName = builder.Database;
        if (string.IsNullOrWhiteSpace(dbName)) return;

        // Имя базы приходит из окружения и подставляется в DDL как идентификатор
        // (параметризовать идентификатор нельзя). Поэтому — строгий allow-list.
        if (!SafeIdentifier.IsMatch(dbName))
            throw new InvalidOperationException(
                $"Недопустимое имя базы данных '{dbName}': разрешены только буквы, цифры, _ и $.");

        builder.Database = "";
        using var conn = new MySqlConnection(builder.ConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"CREATE DATABASE IF NOT EXISTS `{dbName}` " +
                          "CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci";
        var created = cmd.ExecuteNonQuery();
        if (created > 0) log($"[FloV:MP] [DB] База '{dbName}' создана.");
    }

    private static MigrationReport Apply(string connectionString, IReadOnlyList<MigrationFile> migrations, Action<string> log)
    {
        var report = new MigrationReport();

        using var conn = new MySqlConnection(connectionString);
        conn.Open();

        EnsureHistoryTable(conn);

        var lockName = $"flovmp_migrate_{conn.Database}";
        if (!TryAcquireLock(conn, lockName))
            throw new TimeoutException(
                $"Не удалось получить блокировку миграций за {LockTimeoutSeconds}с — " +
                "другой инстанс сервера мигрирует ту же базу слишком долго.");

        try
        {
            var applied = ReadApplied(conn);

            foreach (var migration in migrations)
            {
                if (applied.TryGetValue(migration.Name, out var knownChecksum))
                {
                    if (!string.Equals(knownChecksum, migration.Checksum, StringComparison.OrdinalIgnoreCase))
                    {
                        var msg = $"{migration.Name}: файл изменён после наката " +
                                  $"(в базе {Short(knownChecksum)}, на диске {Short(migration.Checksum)})";
                        report.Drift.Add(msg);
                        report.Items.Add(new MigrationOutcome(migration.Name, "drift", 0, msg));
                        continue;
                    }
                    report.Items.Add(new MigrationOutcome(migration.Name, "up-to-date", 0));
                    continue;
                }

                var sw = Stopwatch.StartNew();
                var skippedStatements = ApplyOne(conn, migration, log);
                sw.Stop();

                RecordApplied(conn, migration, sw.ElapsedMilliseconds);
                report.Items.Add(new MigrationOutcome(
                    migration.Name, "applied", sw.ElapsedMilliseconds,
                    skippedStatements > 0 ? $"пропущено выражений (уже существует): {skippedStatements}" : null));

                log($"[FloV:MP] [DB] Миграция {migration.Name} применена за {sw.ElapsedMilliseconds} мс " +
                    $"({migration.Statements.Count} выражений).");
                GameLog.System("db_migration_applied",
                    ("name", migration.Name), ("ms", sw.ElapsedMilliseconds.ToString()));
            }
        }
        finally
        {
            try
            {
                using var release = conn.CreateCommand();
                release.CommandText = "SELECT RELEASE_LOCK(@n)";
                release.Parameters.AddWithValue("@n", lockName);
                release.ExecuteScalar();
            }
            catch (Exception ex)
            {
                // Блокировка всё равно снимется при закрытии соединения —
                // это диагностика, а не ошибка наката.
                log($"[FloV:MP] [DB] Не удалось снять блокировку миграций: {ex.Message}");
            }
        }

        if (report.HasDrift)
        {
            foreach (var d in report.Drift)
                log($"[FloV:MP] [DB] ВНИМАНИЕ: расхождение схемы — {d}");
            GameLog.System("db_migration_drift", ("count", report.Drift.Count.ToString()));
            if (Strict)
                throw new InvalidOperationException(
                    "FLOVMP_DB_MIGRATE_STRICT=1 и обнаружено расхождение миграций: " +
                    string.Join("; ", report.Drift));
        }

        return report;
    }

    private static bool TryAcquireLock(MySqlConnection conn, string lockName)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT GET_LOCK(@n, @t)";
        cmd.Parameters.AddWithValue("@n", lockName);
        cmd.Parameters.AddWithValue("@t", LockTimeoutSeconds);
        cmd.CommandTimeout = LockTimeoutSeconds + 30;
        var result = cmd.ExecuteScalar();
        return result is not null && result != DBNull.Value && Convert.ToInt32(result) == 1;
    }

    private static void EnsureHistoryTable(MySqlConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "CREATE TABLE IF NOT EXISTS `" + HistoryTable + "` (" +
            "  `name` VARCHAR(190) NOT NULL PRIMARY KEY," +
            "  `checksum` CHAR(64) NOT NULL," +
            "  `applied_at_utc` DATETIME NOT NULL," +
            "  `duration_ms` INT NOT NULL DEFAULT 0," +
            "  `statements` INT NOT NULL DEFAULT 0," +
            "  `engine_version` VARCHAR(32) DEFAULT NULL" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci";
        cmd.ExecuteNonQuery();
    }

    private static Dictionary<string, string> ReadApplied(MySqlConnection conn)
    {
        var applied = new Dictionary<string, string>(StringComparer.Ordinal);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT `name`, `checksum` FROM `" + HistoryTable + "`";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            applied[reader.GetString(0)] = reader.GetString(1);
        return applied;
    }

    private static int ApplyOne(MySqlConnection conn, MigrationFile migration, Action<string> log)
    {
        var skipped = 0;
        for (var i = 0; i < migration.Statements.Count; i++)
        {
            var sql = migration.Statements[i];
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                cmd.CommandTimeout = 300; // ALTER на большой таблице — это минуты
                cmd.ExecuteNonQuery();
            }
            catch (MySqlException ex) when (migration.IgnoredErrors.Contains(ex.Number))
            {
                skipped++;
                log($"[FloV:MP] [DB] {migration.Name}: выражение #{i + 1} пропущено " +
                    $"(код {ex.Number}: {FirstLine(ex.Message)})");
            }
            catch (MySqlException ex)
            {
                throw new InvalidOperationException(
                    $"Миграция {migration.Name}, выражение #{i + 1} упало " +
                    $"(код {ex.Number}): {ex.Message}. SQL: {Excerpt(sql)}", ex);
            }
        }
        return skipped;
    }

    private static void RecordApplied(MySqlConnection conn, MigrationFile migration, long durationMs)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "INSERT INTO `" + HistoryTable + "` " +
            "(`name`, `checksum`, `applied_at_utc`, `duration_ms`, `statements`, `engine_version`) " +
            "VALUES (@name, @sum, @at, @ms, @st, @ver) " +
            "ON DUPLICATE KEY UPDATE `checksum` = VALUES(`checksum`)";
        cmd.Parameters.AddWithValue("@name", migration.Name);
        cmd.Parameters.AddWithValue("@sum", migration.Checksum);
        cmd.Parameters.AddWithValue("@at", DateTime.UtcNow);
        cmd.Parameters.AddWithValue("@ms", (int)Math.Min(durationMs, int.MaxValue));
        cmd.Parameters.AddWithValue("@st", migration.Statements.Count);
        cmd.Parameters.AddWithValue("@ver", EngineVersion);
        cmd.ExecuteNonQuery();
    }

    private static string FirstLine(string text)
    {
        var idx = text.IndexOf('\n');
        return idx < 0 ? text : text[..idx].TrimEnd('\r');
    }

    private static string Short(string checksum) => checksum.Length <= 12 ? checksum : checksum[..12];

    private static string Excerpt(string sql) => sql.Length <= 400 ? sql : sql[..400] + " ...";
}
