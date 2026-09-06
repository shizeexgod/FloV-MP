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
}