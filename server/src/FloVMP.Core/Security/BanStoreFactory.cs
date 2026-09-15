using MySqlConnector;

using FloVMP.Core;
namespace FloVMP.Core.Security;

/// <summary>
/// Выбор хранилища блокировок: общая таблица в MariaDB, если база настроена,
/// иначе локальный файл.
///
/// Разница принципиальная и её надо понимать при установке: с файловым
/// хранилищем баны живут на конкретной машине, и второй инстанс о них не
/// знает. Горизонталь возможна только на общей БД, поэтому выбранный режим
/// печатается в консоль при старте — чтобы это не выяснялось уже после того,
/// как забаненный зашёл со второго сервера.
/// </summary>
public static class BanStoreFactory
{
    public static IBanStore Create(string? connectionString, string jsonFallbackPath)
    {
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            try
            {
                using (var conn = new MySqlConnection(connectionString))
                {
                    conn.Open();
                }
                var store = new MySqlBanStore(connectionString);
                var loaded = store.LoadAll().Count;
                CoreConsole.Write($"[FloV:MP] [Ban] Блокировки: общая таблица MariaDB (записей: {loaded}). " +
                                  "Баны действуют на всех инстансах.");
                return store;
            }
            catch (Exception ex)
            {
                CoreConsole.Write($"[FloV:MP] [Ban] MariaDB недоступна ({ex.Message}). " +
                                  $"Блокировки уходят в локальный файл: {jsonFallbackPath}");
                CoreConsole.Write("[FloV:MP] [Ban] ВНИМАНИЕ: локальные баны не действуют на других инстансах.");
            }
        }
        else
        {
            CoreConsole.Write($"[FloV:MP] [Ban] Блокировки: локальный файл {jsonFallbackPath} " +
                              "(база не настроена — на нескольких инстансах баны не разойдутся).");
        }

        return new JsonBanStore(jsonFallbackPath);
    }
}
