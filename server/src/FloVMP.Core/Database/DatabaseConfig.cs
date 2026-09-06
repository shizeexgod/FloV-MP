namespace FloVMP.Core.Database;

/// <summary>
/// Настройки подключения к MariaDB / MySQL серверу «Держава Онлайн».
/// </summary>
public sealed class DatabaseConfig
{
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 3306;
    public string Database { get; set; } = "derzhava_rp";
    public string User { get; set; } = "flovmp";
    public string Password { get; set; } = "DerzhavaFloVMP2026!Secure";
    public int ConnectionTimeoutSeconds { get; set; } = 5;
    public int MinPoolSize { get; set; } = 2;
    public int MaxPoolSize { get; set; } = 30;

    public string BuildConnectionString()
    {
        return $"Server={Host};Port={Port};Database={Database};User ID={User};Password={Password};" +
               $"ConnectionTimeout={ConnectionTimeoutSeconds};MinPoolSize={MinPoolSize};MaxPoolSize={MaxPoolSize};" +
               $"Character Set=utf8mb4;AllowUserVariables=True;";
    }
}