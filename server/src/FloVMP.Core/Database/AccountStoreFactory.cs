using FloVMP.Core.Auth;
using FloVMP.Core.Logging;
using MySqlConnector;

namespace FloVMP.Core.Database;

/// <summary>
/// Фабрика создания хранилища аккаунтов.
/// Проверяет доступность MariaDB/MySQL; при сбое или отсутствии строки
/// прозрачно переключается на надёжное локальное файловое хранилище (JsonAccountStore).
/// </summary>
public static class AccountStoreFactory
{
    public static IAccountStore Create(string? connectionString, string jsonFallbackPath)
    {
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            // Схема накатывается ДО проверки соединения: MigrationRunner сам
            // создаёт базу, если её ещё нет. Иначе первый запуск у клиента
            // спотыкался бы об «Unknown database» и молча уходил в JSON —
            // с виду сервер работает, а данные не там, где ждут.
            RunMigrations(connectionString);

            try
            {
                using var conn = new MySqlConnection(connectionString);
                conn.Open();
                GameLog.System("db_connected", ("provider", "MariaDB/MySQL"), ("database", conn.Database));
                Console.WriteLine($"[FloV:MP] [DB] Успешное подключение к MariaDB ({conn.Database})");
                return new MySqlAccountStore(connectionString);
            }
            catch (Exception ex)
            {
                GameLog.System("db_fallback", ("error", ex.Message), ("fallback", jsonFallbackPath));
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[FloV:MP] [DB] Внимание: MariaDB недоступна ({ex.Message}). Переход на локальное хранилище: {jsonFallbackPath}");
                Console.ResetColor();
            }
        }

        return new JsonAccountStore(jsonFallbackPath);
    }

    /// <summary>
    /// Накат миграций перед первым обращением к базе.
    /// Отключается <c>FLOVMP_DB_MIGRATE=0</c> — на случай, когда схемой
    /// управляет DBA клиента и приложению права на DDL не выданы.
    /// </summary>
    private static void RunMigrations(string connectionString)
    {
        if (Environment.GetEnvironmentVariable("FLOVMP_DB_MIGRATE") == "0")
        {
            Console.WriteLine("[FloV:MP] [DB] Миграции отключены (FLOVMP_DB_MIGRATE=0).");
            return;
        }

        var report = MigrationRunner.Run(connectionString, log: Console.WriteLine);

        if (report.Skipped)
        {
            Console.WriteLine($"[FloV:MP] [DB] Миграции пропущены: {report.SkipReason}");
            return;
        }

        Console.WriteLine($"[FloV:MP] [DB] Миграции: применено {report.Applied}, " +
                          $"уже актуально {report.AlreadyUpToDate}" +
                          (report.HasDrift ? $", РАСХОЖДЕНИЙ {report.Drift.Count}" : "") + ".");
    }
}