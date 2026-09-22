using MySqlConnector;

namespace FloVMP.LicenseAuthority;

/// <summary>
/// Ключи, активации и журнал в MariaDB. Время хранится в UTC; при чтении
/// помечается как UTC, иначе в JSON оно ушло бы без «Z» и сервер клиента
/// сдвинул бы срок на свой часовой пояс.
/// </summary>
public sealed class MySqlStore : ILicenseStore
{
    private readonly string _cs;

    public MySqlStore(string connectionString) => _cs = connectionString;

    private MySqlConnection Open()
    {
        var c = new MySqlConnection(_cs);
        c.Open();
        return c;
    }

    private static DateTime Utc(object v) => DateTime.SpecifyKind((DateTime)v, DateTimeKind.Utc);
    private static DateTime? UtcOrNull(object v) => v is DBNull ? null : Utc(v);

    public void EnsureSchema()
    {
        using var c = Open();
        foreach (var sql in new[]
        {
            """
            CREATE TABLE IF NOT EXISTS licenses (
              id BIGINT AUTO_INCREMENT PRIMARY KEY,
              license_key VARCHAR(64) NOT NULL UNIQUE,
              project VARCHAR(128) NOT NULL,
              owner VARCHAR(128) NOT NULL,
              contact VARCHAR(256) NOT NULL DEFAULT '',
              plan VARCHAR(32) NOT NULL DEFAULT 'business',
              max_players INT NOT NULL DEFAULT 1000,
              max_servers INT NOT NULL DEFAULT 1,
              status VARCHAR(16) NOT NULL DEFAULT 'issued',
              status_reason VARCHAR(256) NOT NULL DEFAULT '',
              created_at DATETIME NOT NULL,
              activated_at DATETIME NULL,
              expires_at DATETIME NULL,
              note VARCHAR(512) NOT NULL DEFAULT '',
              INDEX ix_status (status)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
            """,
            """
            CREATE TABLE IF NOT EXISTS activations (
              id BIGINT AUTO_INCREMENT PRIMARY KEY,
              license_id BIGINT NOT NULL,
              server_id VARCHAR(64) NOT NULL,
              ip VARCHAR(64) NOT NULL DEFAULT '',
              version VARCHAR(32) NOT NULL DEFAULT '',
              slots INT NOT NULL DEFAULT 0,
              first_seen DATETIME NOT NULL,
              last_seen DATETIME NOT NULL,
              UNIQUE KEY ux_license_server (license_id, server_id),
              CONSTRAINT fk_act_license FOREIGN KEY (license_id) REFERENCES licenses(id) ON DELETE CASCADE
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
            """,
            """
            CREATE TABLE IF NOT EXISTS license_events (
              id BIGINT AUTO_INCREMENT PRIMARY KEY,
              at DATETIME NOT NULL,
              license_id BIGINT NULL,
              key_mark VARCHAR(16) NOT NULL DEFAULT '',
              type VARCHAR(32) NOT NULL,
              ip VARCHAR(64) NOT NULL DEFAULT '',
              server_id VARCHAR(64) NOT NULL DEFAULT '',
              detail VARCHAR(512) NOT NULL DEFAULT '',
              INDEX ix_license_at (license_id, at),
              INDEX ix_at (at)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
            """,
        })
        {
            using var cmd = new MySqlCommand(sql, c);
            cmd.ExecuteNonQuery();
        }
    }

    private const string LicenseColumns =
        "id, license_key, project, owner, contact, plan, max_players, max_servers, status, status_reason, created_at, activated_at, expires_at, note";

    private static LicenseRecord ReadLicense(MySqlDataReader r) => new()
    {
        Id = r.GetInt64(0), Key = r.GetString(1), Project = r.GetString(2), Owner = r.GetString(3),
        Contact = r.GetString(4), Plan = r.GetString(5), MaxPlayers = r.GetInt32(6), MaxServers = r.GetInt32(7),
        Status = r.GetString(8), StatusReason = r.GetString(9), CreatedAt = Utc(r.GetValue(10)),
        ActivatedAt = UtcOrNull(r.GetValue(11)), ExpiresAt = UtcOrNull(r.GetValue(12)), Note = r.GetString(13),
    };

    public LicenseRecord? FindByKey(string key)
    {
        using var c = Open();
        using var cmd = new MySqlCommand($"SELECT {LicenseColumns} FROM licenses WHERE license_key = @k", c);
        cmd.Parameters.AddWithValue("@k", key);
        using var r = cmd.ExecuteReader();
        return r.Read() ? ReadLicense(r) : null;
    }

    public List<LicenseRecord> List(string? status)
    {
        using var c = Open();
        using var cmd = new MySqlCommand($"SELECT {LicenseColumns} FROM licenses" +
                                         (status is null ? "" : " WHERE status = @s") + " ORDER BY id", c);
        if (status is not null) cmd.Parameters.AddWithValue("@s", status);
        using var r = cmd.ExecuteReader();
        var list = new List<LicenseRecord>();
        while (r.Read()) list.Add(ReadLicense(r));
        return list;
    }

    private static void Bind(MySqlCommand cmd, LicenseRecord l)
    {
        cmd.Parameters.AddWithValue("@key", l.Key);
        cmd.Parameters.AddWithValue("@project", l.Project);
        cmd.Parameters.AddWithValue("@owner", l.Owner);
        cmd.Parameters.AddWithValue("@contact", l.Contact);
        cmd.Parameters.AddWithValue("@plan", l.Plan);
        cmd.Parameters.AddWithValue("@players", l.MaxPlayers);
        cmd.Parameters.AddWithValue("@servers", l.MaxServers);
        cmd.Parameters.AddWithValue("@status", l.Status);
        cmd.Parameters.AddWithValue("@reason", l.StatusReason);
        cmd.Parameters.AddWithValue("@created", l.CreatedAt);
        cmd.Parameters.AddWithValue("@activated", (object?)l.ActivatedAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@expires", (object?)l.ExpiresAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@note", l.Note);
    }

    public void Insert(LicenseRecord l)
    {
        using var c = Open();
        using var cmd = new MySqlCommand(
            "INSERT INTO licenses (license_key, project, owner, contact, plan, max_players, max_servers, status, status_reason, created_at, activated_at, expires_at, note) " +
            "VALUES (@key, @project, @owner, @contact, @plan, @players, @servers, @status, @reason, @created, @activated, @expires, @note)", c);
        Bind(cmd, l);
        cmd.ExecuteNonQuery();
        l.Id = cmd.LastInsertedId;
    }

    public void Update(LicenseRecord l)
    {
        using var c = Open();
        using var cmd = new MySqlCommand(
            "UPDATE licenses SET project=@project, owner=@owner, contact=@contact, plan=@plan, max_players=@players, " +
            "max_servers=@servers, status=@status, status_reason=@reason, activated_at=@activated, expires_at=@expires, note=@note " +
            "WHERE id=@id", c);
        Bind(cmd, l);
        cmd.Parameters.AddWithValue("@id", l.Id);
        cmd.ExecuteNonQuery();
    }

    private static ActivationRecord ReadActivation(MySqlDataReader r) => new()
    {
        Id = r.GetInt64(0), LicenseId = r.GetInt64(1), ServerId = r.GetString(2), Ip = r.GetString(3),
        Version = r.GetString(4), Slots = r.GetInt32(5), FirstSeen = Utc(r.GetValue(6)), LastSeen = Utc(r.GetValue(7)),
    };

    private const string ActivationColumns = "id, license_id, server_id, ip, version, slots, first_seen, last_seen";

    public List<ActivationRecord> Activations(long licenseId)
    {
        using var c = Open();
        using var cmd = new MySqlCommand($"SELECT {ActivationColumns} FROM activations WHERE license_id=@l ORDER BY id", c);
        cmd.Parameters.AddWithValue("@l", licenseId);
        using var r = cmd.ExecuteReader();
        var list = new List<ActivationRecord>();
        while (r.Read()) list.Add(ReadActivation(r));
        return list;
    }

    public ActivationRecord? FindActivation(long licenseId, string serverId)
    {
        using var c = Open();
        using var cmd = new MySqlCommand($"SELECT {ActivationColumns} FROM activations WHERE license_id=@l AND server_id=@s", c);
        cmd.Parameters.AddWithValue("@l", licenseId);
        cmd.Parameters.AddWithValue("@s", serverId);
        using var r = cmd.ExecuteReader();
        return r.Read() ? ReadActivation(r) : null;
    }

    public void InsertActivation(ActivationRecord a)
    {
        using var c = Open();
        using var cmd = new MySqlCommand(
            "INSERT INTO activations (license_id, server_id, ip, version, slots, first_seen, last_seen) " +
            "VALUES (@l, @s, @ip, @v, @slots, @first, @last)", c);
        cmd.Parameters.AddWithValue("@l", a.LicenseId);
        cmd.Parameters.AddWithValue("@s", a.ServerId);
        cmd.Parameters.AddWithValue("@ip", a.Ip);
        cmd.Parameters.AddWithValue("@v", a.Version);
        cmd.Parameters.AddWithValue("@slots", a.Slots);
        cmd.Parameters.AddWithValue("@first", a.FirstSeen);
        cmd.Parameters.AddWithValue("@last", a.LastSeen);
        cmd.ExecuteNonQuery();
        a.Id = cmd.LastInsertedId;
    }

    public void TouchActivation(long id, string ip, string version, int slots, DateTime at)
    {
        using var c = Open();
        using var cmd = new MySqlCommand(
            "UPDATE activations SET ip=@ip, version=IF(@v='', version, @v), slots=IF(@slots=0, slots, @slots), last_seen=@at WHERE id=@id", c);
        cmd.Parameters.AddWithValue("@ip", ip);
        cmd.Parameters.AddWithValue("@v", version);
        cmd.Parameters.AddWithValue("@slots", slots);
        cmd.Parameters.AddWithValue("@at", at);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    public int RemoveActivations(long licenseId, string? serverId)
    {
        using var c = Open();
        using var cmd = new MySqlCommand("DELETE FROM activations WHERE license_id=@l" + (serverId is null ? "" : " AND server_id=@s"), c);
        cmd.Parameters.AddWithValue("@l", licenseId);
        if (serverId is not null) cmd.Parameters.AddWithValue("@s", serverId);
        return cmd.ExecuteNonQuery();
    }

    public void AddEvent(EventRecord e)
    {
        using var c = Open();
        using var cmd = new MySqlCommand(
            "INSERT INTO license_events (at, license_id, key_mark, type, ip, server_id, detail) VALUES (@at, @l, @k, @t, @ip, @s, @d)", c);
        cmd.Parameters.AddWithValue("@at", e.At);
        cmd.Parameters.AddWithValue("@l", (object?)e.LicenseId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@k", e.Key.Length > 16 ? e.Key[..16] : e.Key);
        cmd.Parameters.AddWithValue("@t", e.Type.Length > 32 ? e.Type[..32] : e.Type);
        cmd.Parameters.AddWithValue("@ip", e.Ip);
        cmd.Parameters.AddWithValue("@s", e.ServerId);
        cmd.Parameters.AddWithValue("@d", e.Detail.Length > 512 ? e.Detail[..512] : e.Detail);
        cmd.ExecuteNonQuery();
    }

    public List<EventRecord> Events(long? licenseId, int limit)
    {
        using var c = Open();
        using var cmd = new MySqlCommand(
            "SELECT at, license_id, key_mark, type, ip, server_id, detail FROM license_events" +
            (licenseId is null ? "" : " WHERE license_id=@l") + " ORDER BY id DESC LIMIT @n", c);
        if (licenseId is not null) cmd.Parameters.AddWithValue("@l", licenseId);
        cmd.Parameters.AddWithValue("@n", Math.Clamp(limit, 1, 1000));
        using var r = cmd.ExecuteReader();
        var list = new List<EventRecord>();
        while (r.Read())
            list.Add(new EventRecord
            {
                At = Utc(r.GetValue(0)), LicenseId = r.IsDBNull(1) ? null : r.GetInt64(1), Key = r.GetString(2),
                Type = r.GetString(3), Ip = r.GetString(4), ServerId = r.GetString(5), Detail = r.GetString(6),
            });
        return list;
    }
}
