using FloVMP.Core.Economy;
using FloVMP.Core.Logging;
using MySqlConnector;

namespace FloVMP.Core.Database;

/// <summary>
/// Хранилище логов наказаний, действий администрации и финансовых транзакций в MariaDB.
/// BUGFIX: Все методы вынесены в Task.Run, чтобы синхронные conn.Open() + ExecuteNonQuery()
/// не блокировали главный поток alt:V при вызове из ChatSystem/EconomyService.
/// </summary>
public sealed class MySqlAuditStore
{
    private readonly string _connectionString;

    public MySqlAuditStore(string connectionString)
    {
        _connectionString = connectionString;
    }

    public void LogPunishment(int? targetAccountId, string targetName, int? adminAccountId, string adminName, string type, string reason, int durationSeconds)
    {
        // Fire-and-forget с логированием ошибок — аудит не должен блокировать игру
        _ = Task.Run(() =>
        {
            try
            {
                using var conn = new MySqlConnection(_connectionString);
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO punishments (target_account_id, target_name, admin_account_id, admin_name, type, reason, duration_seconds, is_active, created_at, expires_at)
                    VALUES (@tid, @tname, @aid, @aname, @type, @reason, @dur, 1, NOW(), 
                            IF(@dur > 0, DATE_ADD(NOW(), INTERVAL @dur SECOND), NULL));";
                cmd.Parameters.AddWithValue("@tid", targetAccountId ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@tname", targetName);
                cmd.Parameters.AddWithValue("@aid", adminAccountId ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@aname", adminName);
                cmd.Parameters.AddWithValue("@type", type);
                cmd.Parameters.AddWithValue("@reason", reason);
                cmd.Parameters.AddWithValue("@dur", durationSeconds);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                GameLog.System("audit_store_error", ("table", "punishments"), ("error", ex.Message));
            }
        });
    }

    public void LogAdminCommand(int? adminAccountId, string adminName, string adminRank, string command, string args, string targetPlayer, string ipAddress)
    {
        _ = Task.Run(() =>
        {
            try
            {
                using var conn = new MySqlConnection(_connectionString);
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO admin_audit_logs (admin_account_id, admin_name, admin_rank, command, args, target_player, ip_address, created_at)
                    VALUES (@aid, @aname, @arank, @cmd, @args, @target, @ip, NOW());";
                cmd.Parameters.AddWithValue("@aid", adminAccountId ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@aname", adminName);
                cmd.Parameters.AddWithValue("@arank", adminRank);
                cmd.Parameters.AddWithValue("@cmd", command);
                cmd.Parameters.AddWithValue("@args", args ?? "");
                cmd.Parameters.AddWithValue("@target", targetPlayer ?? "");
                cmd.Parameters.AddWithValue("@ip", ipAddress ?? "");
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                GameLog.System("audit_store_error", ("table", "admin_audit_logs"), ("error", ex.Message));
            }
        });
    }

    public void LogTransaction(TransactionRecord tx)
    {
        _ = Task.Run(() =>
        {
            try
            {
                using var conn = new MySqlConnection(_connectionString);
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO bank_transactions (sender_character_id, receiver_character_id, amount, type, description, created_at)
                    VALUES (@sender, @receiver, @amount, @type, @desc, NOW());";
                cmd.Parameters.AddWithValue("@sender", tx.SenderAccountId ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@receiver", tx.ReceiverAccountId ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@amount", tx.Amount);
                cmd.Parameters.AddWithValue("@type", tx.Type.ToString());
                cmd.Parameters.AddWithValue("@desc", tx.Description);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                GameLog.System("audit_store_error", ("table", "bank_transactions"), ("error", ex.Message));
            }
        });
    }
}