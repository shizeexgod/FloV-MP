using FloVMP.Core.Auth;
using FloVMP.Core.Logging;
using MySqlConnector;

using FloVMP.Core;
namespace FloVMP.Core.Database;

/// <summary>
/// Подготовка MariaDB при старте сервера: накат миграций и проверка соединения.
/// </summary>
public static class AccountStoreFactory
{

    /// <summary>
    /// Подготовка базы: накат
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