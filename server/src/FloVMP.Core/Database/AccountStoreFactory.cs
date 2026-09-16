using FloVMP.Core.Auth;
using FloVMP.Core.Logging;
using MySqlConnector;

using FloVMP.Core;
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
                CoreConsole.Write($"[FloV:MP] [DB] Успешное подключение к MariaDB ({conn.Database})");
                return new MySqlAccountStore(connectionString);
            }
            catch (Exception ex)
            {
                GameLog.System("db_fallback", ("error", ex.Message), ("fallback", jsonFallbackPath));
                CoreConsole.Write($"[FloV:MP] [DB] Внимание: MariaDB недоступна ({ex.Message}). Переход на локальное хранилище: {jsonFallbackPath}");
            }
        }

        return new JsonAccountStore(jsonFallbackPath);
    }

    /// <summary>
    /// Подготовка базы для режима без аккаунтов (базовая платформа): накат
    /// миграций и проверка соединения. true — база доступна и таблицы на месте.
    /// Никогда не бросает: недоступная база — штатный режим «на файлах».
    /// </summary>
    public static bool TryPrepareDatabase(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return false;
        try
        {
            RunMigrations(connectionString);
            using var conn = new MySqlConnection(connectionString);
            conn.Open();
            CoreConsole.Write($"[FloV:MP] [DB] Успешное подключение к MariaDB ({conn.Database})");
            return true;
        }
        catch (Exception ex)
        {
            CoreConsole.Warning($"[FloV:MP] [DB] MariaDB недоступна ({ex.Message}). " +
                                "Права и баны хранятся в файлах (config/admins.json, flovmp-data/bans.json).");
            return false;
        }
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
            CoreConsole.Write("[FloV:MP] [DB] Миграции отключены (FLOVMP_DB_MIGRATE=0).");
            return;
        }

        var report = MigrationRunner.Run(connectionString, log: CoreConsole.Write);

        if (report.Skipped)
        {
            CoreConsole.Write($"[FloV:MP] [DB] Миграции пропущены: {report.SkipReason}");
            return;
        }

        CoreConsole.Write($"[FloV:MP] [DB] Миграции: применено {report.Applied}, " +
                          $"уже актуально {report.AlreadyUpToDate}" +
                          (report.HasDrift ? $", РАСХОЖДЕНИЙ {report.Drift.Count}" : "") + ".");
    }
}