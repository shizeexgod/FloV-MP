using MySqlConnector;

namespace FloVMP.Core.Security;

/// <summary>
/// Хранилище блокировок в MariaDB/MySQL — рабочий путь для нескольких
/// инстансов: таблица одна на всех, поэтому бан, выданный на любом сервере,
/// действует везде.
///
/// Запись синхронная и это осознанно: бан выдаётся редко (не горячий путь), а
/// терять его нельзя. Чтение всех банов делается один раз при старте, дальше
/// проверки идут по памяти сервиса — запрос к БД на каждый вход игрока был бы
/// как раз тем, чего допускать нельзя.
/// </summary>
public sealed class MySqlBanStore : IBanStore
{
    private readonly string _connectionString;

    public MySqlBanStore(string connectionString)
    {
        _connectionString = connectionString;
    }

    public IReadOnlyList<BanRecord> LoadAll() => LoadChangedSince(null);

    /// <summary>
    /// Записи, изменённые после указанного момента. Нужна для горизонтали:
    /// инстанс периодически спрашивает «что поменялось», вместо того чтобы
    /// перечитывать всю таблицу банов целиком.
    /// </summary>
    public IReadOnlyList<BanRecord> LoadChangedSince(DateTime? sinceUtc)
    {
        var result = new List<BanRecord>();

        using var conn = new MySqlConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT `id`, `account_id`, `username`, `ip`, `social_club`, `hwid_hash`, `mac_address`, " +
            "       `flags`, `admin_username`, `reason`, `banned_at_utc`, `expires_at_utc`, `is_active` " +
            "FROM `bans`" +
            (sinceUtc.HasValue ? " WHERE `updated_at_utc` >= @since" : "");
        if (sinceUtc.HasValue) cmd.Parameters.AddWithValue("@since", sinceUtc.Value);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new BanRecord(
                Id: reader.GetString(0),
                AccountId: reader.GetInt32(1),
                Username: reader.GetString(2),
                Ip: reader.IsDBNull(3) ? null : reader.GetString(3),
                SocialClubId: reader.IsDBNull(4) ? null : reader.GetString(4),
                HwidHash: reader.IsDBNull(5) ? null : reader.GetString(5),
                MacAddress: reader.IsDBNull(6) ? null : reader.GetString(6),
                Flags: (BanFlags)reader.GetInt32(7),
                AdminUsername: reader.GetString(8),
                Reason: reader.GetString(9),
                BannedAtUtc: DateTime.SpecifyKind(reader.GetDateTime(10), DateTimeKind.Utc),
                ExpiresAtUtc: reader.IsDBNull(11)
                    ? null
                    : DateTime.SpecifyKind(reader.GetDateTime(11), DateTimeKind.Utc),
                IsActive: reader.GetBoolean(12)));
        }

        return result;
    }

    public void Upsert(BanRecord record)
    {
        if (record is null || string.IsNullOrWhiteSpace(record.Id)) return;

        using var conn = new MySqlConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "INSERT INTO `bans` (`id`, `account_id`, `username`, `ip`, `social_club`, `hwid_hash`, " +
            "  `mac_address`, `flags`, `admin_username`, `reason`, `banned_at_utc`, `expires_at_utc`, `is_active`) " +
            "VALUES (@id, @account_id, @username, @ip, @social_club, @hwid, @mac, @flags, @admin, " +
            "        @reason, @banned_at, @expires_at, @is_active) " +
            "ON DUPLICATE KEY UPDATE " +
            "  `is_active` = VALUES(`is_active`), `reason` = VALUES(`reason`), " +
            "  `expires_at_utc` = VALUES(`expires_at_utc`), `flags` = VALUES(`flags`)";

        cmd.Parameters.AddWithValue("@id", record.Id);
        cmd.Parameters.AddWithValue("@account_id", record.AccountId);
        cmd.Parameters.AddWithValue("@username", Trim(record.Username, 64));
        cmd.Parameters.AddWithValue("@ip", (object?)Trim(record.Ip, 45) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@social_club", (object?)Trim(record.SocialClubId, 128) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@hwid", (object?)Trim(record.HwidHash, 128) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@mac", (object?)Trim(record.MacAddress, 64) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@flags", (int)record.Flags);
        cmd.Parameters.AddWithValue("@admin", Trim(record.AdminUsername, 64));
        cmd.Parameters.AddWithValue("@reason", Trim(record.Reason, 255));
        cmd.Parameters.AddWithValue("@banned_at", record.BannedAtUtc);
        cmd.Parameters.AddWithValue("@expires_at", (object?)record.ExpiresAtUtc ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@is_active", record.IsActive);

        cmd.ExecuteNonQuery();
    }

    /// <summary>Записи уходят в БД сразу — отдельного сброса не требуется.</summary>
    public void Flush() { }

    /// <summary>
    /// Обрезка под длину колонки. Причина ника/бана приходит от админа и может
    /// быть длиннее поля; молча падать на «Data too long» посреди выдачи бана
    /// хуже, чем сохранить усечённый текст.
    /// </summary>
    private static string? Trim(string? value, int max)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= max ? value : value[..max];
    }
}
