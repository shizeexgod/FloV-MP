using MySqlConnector;

namespace FloVMP.Core.Admin;

/// <summary>
/// Права администраторов в таблице admins (миграция 004).
///
/// Чтение идёт редко — при старте и при фоновой синхронизации, — а проверка
/// прав при входе игрока выполняется по памяти AdminBootstrapManager. Ходить
/// в базу на каждое подключение, да ещё на главном потоке, было бы нельзя.
/// </summary>
public sealed class MySqlAdminStore : IAdminStore
{
    private readonly string _connectionString;

    public MySqlAdminStore(string connectionString)
    {
        _connectionString = connectionString;
    }

    public IReadOnlyList<AdminRecord> LoadAll()
    {
        var result = new List<AdminRecord>();

        using var conn = new MySqlConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        // Уровень ограничен и здесь, а не только ограничением таблицы: старые
        // MySQL (до 8.0.16) CHECK разбирают, но не применяют, и опечатка вроде
        // 80 иначе дошла бы до сервера.
        cmd.CommandText =
            "SELECT `social_club`, `level`, `is_founder` FROM `admins` " +
            "WHERE `level` BETWEEN 1 AND 8";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var sc = reader.GetString(0).Trim();
            // SocialClubId — только цифры. Мусор в ключе (пробел, буква,
            // ник по ошибке) пропускаем, а не превращаем в чьи-то права.
            if (sc.Length == 0 || !sc.All(char.IsDigit)) continue;

            var level = reader.GetInt32(1);
            var founder = reader.GetBoolean(2) || level == 8;
            result.Add(new AdminRecord(sc, level, founder));
        }

        return result;
    }

    public void Upsert(string socialClub, int level, bool isFounder, string? grantedBy)
    {
        if (string.IsNullOrWhiteSpace(socialClub)) return;
        socialClub = socialClub.Trim();
        if (!socialClub.All(char.IsDigit)) return;
        level = Math.Clamp(level, 0, 8);

        using var conn = new MySqlConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();

        if (level == 0)
        {
            // Снятие прав — удаление строки, а не level = 0: так таблица
            // остаётся списком действующих администраторов, и её удобно читать
            // глазами при ручной правке.
            cmd.CommandText = "DELETE FROM `admins` WHERE `social_club` = @sc";
            cmd.Parameters.AddWithValue("@sc", socialClub);
            cmd.ExecuteNonQuery();
            return;
        }

        cmd.CommandText =
            "INSERT INTO `admins` (`social_club`, `level`, `is_founder`, `granted_by`) " +
            "VALUES (@sc, @level, @founder, @by) " +
            "ON DUPLICATE KEY UPDATE `level` = VALUES(`level`), " +
            "  `is_founder` = VALUES(`is_founder`), `granted_by` = VALUES(`granted_by`)";
        cmd.Parameters.AddWithValue("@sc", socialClub);
        cmd.Parameters.AddWithValue("@level", level);
        cmd.Parameters.AddWithValue("@founder", isFounder || level == 8);
        cmd.Parameters.AddWithValue("@by", (object?)Trim(grantedBy, 64) ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    private static string? Trim(string? value, int max) =>
        string.IsNullOrEmpty(value) ? value : (value.Length <= max ? value : value[..max]);
}
