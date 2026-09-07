using System.Data;
using FloVMP.Core.Auth;
using FloVMP.Core.Logging;
using MySqlConnector;

namespace FloVMP.Core.Database;

/// <summary>
/// Реализация <see cref="IAccountStore"/> поверх MariaDB / MySQL.
/// Обеспечивает персистентность данных игроков в production 24/7.
/// </summary>
public sealed class MySqlAccountStore : IAccountStore
{
    private readonly string _connectionString;

    public MySqlAccountStore(string connectionString)
    {
        _connectionString = connectionString;
    }

    private MySqlConnection OpenConnection()
    {
        var conn = new MySqlConnection(_connectionString);
        conn.Open();
        return conn;
    }

    public Account? FindByUsername(string username)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, username, password_hash, cash, bank, admin_level,
                   is_banned, ban_reason, ban_until_utc, mute_until_utc,
                   created_at, last_login_at,
                   email, totp_secret, two_fa_enabled
            FROM accounts
            WHERE username = @username
            LIMIT 1;";
        cmd.Parameters.AddWithValue("@username", username);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;

        return ReadAccount(reader);
    }

    public bool Exists(string username)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM accounts WHERE username = @username LIMIT 1;";
        cmd.Parameters.AddWithValue("@username", username);

        var res = cmd.ExecuteScalar();
        return res != null;
    }

    public Account Create(string username, string passwordHash)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO accounts (username, password_hash, cash, bank, admin_level, is_banned, created_at)
            VALUES (@username, @password_hash, @cash, @bank, @admin_level, 0, NOW());
            SELECT LAST_INSERT_ID();";
        
        cmd.Parameters.AddWithValue("@username", username);
        cmd.Parameters.AddWithValue("@password_hash", passwordHash);
        cmd.Parameters.AddWithValue("@cash", Account.StartingCash);
        cmd.Parameters.AddWithValue("@bank", Account.StartingBank);
        cmd.Parameters.AddWithValue("@admin_level", 0);

        try
        {
            var id = Convert.ToInt32(cmd.ExecuteScalar());
            return new Account
            {
                Id = id,
                Username = username,
                PasswordHash = passwordHash,
                Cash = Account.StartingCash,
                Bank = Account.StartingBank,
                AdminLevel = 0,
                CreatedUtc = DateTime.UtcNow.ToString("O")
            };
        }
        catch (MySqlException ex) when (ex.Number == 1062) // Duplicate entry
        {
            throw new InvalidOperationException($"имя занято: {username}");
        }
    }

    public void Update(Account account)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = account.Id > 0
            ? @"UPDATE accounts
                SET password_hash = @pw,
                    cash = @cash,
                    bank = @bank,
                    admin_level = @admin_level,
                    is_banned = @is_banned,
                    ban_reason = @ban_reason,
                    ban_until_utc = @ban_until,
                    mute_until_utc = @mute_until,
                    last_login_at = @last_login,
                    email = @email,
                    totp_secret = @totp_secret,
                    two_fa_enabled = @two_fa_enabled
                WHERE id = @id;"
            : @"UPDATE accounts
                SET password_hash = @pw,
                    cash = @cash,
                    bank = @bank,
                    admin_level = @admin_level,
                    is_banned = @is_banned,
                    ban_reason = @ban_reason,
                    ban_until_utc = @ban_until,
                    mute_until_utc = @mute_until,
                    last_login_at = @last_login,
                    email = @email,
                    totp_secret = @totp_secret,
                    two_fa_enabled = @two_fa_enabled
                WHERE username = @username;";

        cmd.Parameters.AddWithValue("@id", account.Id);
        cmd.Parameters.AddWithValue("@username", account.Username);
        cmd.Parameters.AddWithValue("@pw", account.PasswordHash);
        cmd.Parameters.AddWithValue("@cash", account.Cash);
        cmd.Parameters.AddWithValue("@bank", account.Bank);
        cmd.Parameters.AddWithValue("@admin_level", account.AdminLevel);
        cmd.Parameters.AddWithValue("@is_banned", account.IsBanned ? 1 : 0);
        cmd.Parameters.AddWithValue("@ban_reason", string.IsNullOrEmpty(account.BanReason) ? (object)DBNull.Value : account.BanReason);
        
        object banUntilVal = DBNull.Value;
        if (!string.IsNullOrEmpty(account.BanUntilUtc) && DateTime.TryParse(account.BanUntilUtc, out var bdt))
            banUntilVal = bdt;
        cmd.Parameters.AddWithValue("@ban_until", banUntilVal);

        object muteUntilVal = DBNull.Value;
        if (!string.IsNullOrEmpty(account.MuteUntilUtc) && DateTime.TryParse(account.MuteUntilUtc, out var mdt))
            muteUntilVal = mdt;
        cmd.Parameters.AddWithValue("@mute_until", muteUntilVal);

        object lastLoginVal = DBNull.Value;
        if (!string.IsNullOrEmpty(account.LastLoginUtc) && DateTime.TryParse(account.LastLoginUtc, out var ldt))
            lastLoginVal = ldt;
        cmd.Parameters.AddWithValue("@last_login", lastLoginVal);

        cmd.Parameters.AddWithValue("@email", string.IsNullOrEmpty(account.Email) ? (object)DBNull.Value : account.Email);
        cmd.Parameters.AddWithValue("@totp_secret", string.IsNullOrEmpty(account.TotpSecret) ? (object)DBNull.Value : account.TotpSecret);
        cmd.Parameters.AddWithValue("@two_fa_enabled", account.TwoFaEnabled ? 1 : 0);

        cmd.ExecuteNonQuery();
    }

    private static Account ReadAccount(IDataRecord r)
    {
        var acc = new Account
        {
            Id = r.GetInt32(r.GetOrdinal("id")),
            Username = r.GetString(r.GetOrdinal("username")),
            PasswordHash = r.GetString(r.GetOrdinal("password_hash")),
            Cash = r.GetInt64(r.GetOrdinal("cash")),
            Bank = r.GetInt64(r.GetOrdinal("bank")),
            AdminLevel = r.GetByte(r.GetOrdinal("admin_level")),
            IsBanned = r.GetBoolean(r.GetOrdinal("is_banned")),
            BanReason = r.IsDBNull(r.GetOrdinal("ban_reason")) ? "" : r.GetString(r.GetOrdinal("ban_reason")),
        };

        var banIdx = r.GetOrdinal("ban_until_utc");
        if (!r.IsDBNull(banIdx))
            acc.BanUntilUtc = r.GetDateTime(banIdx).ToString("O");

        var muteIdx = r.GetOrdinal("mute_until_utc");
        if (!r.IsDBNull(muteIdx))
            acc.MuteUntilUtc = r.GetDateTime(muteIdx).ToString("O");

        var createdIdx = r.GetOrdinal("created_at");
        if (!r.IsDBNull(createdIdx))
            acc.CreatedUtc = r.GetDateTime(createdIdx).ToString("O");

        var loginIdx = r.GetOrdinal("last_login_at");
        if (!r.IsDBNull(loginIdx))
            acc.LastLoginUtc = r.GetDateTime(loginIdx).ToString("O");

        var emailIdx = r.GetOrdinal("email");
        if (!r.IsDBNull(emailIdx))
            acc.Email = r.GetString(emailIdx);

        var totpIdx = r.GetOrdinal("totp_secret");
        if (!r.IsDBNull(totpIdx))
            acc.TotpSecret = r.GetString(totpIdx);

        var twoFaIdx = r.GetOrdinal("two_fa_enabled");
        if (!r.IsDBNull(twoFaIdx))
            acc.TwoFaEnabled = r.GetBoolean(twoFaIdx);

        return acc;
    }
}