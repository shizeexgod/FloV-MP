namespace FloVMP.Core.Database;

/// <summary>
/// Настройки подключения к MariaDB / MySQL серверу FloV:MP.
/// </summary>
public sealed class DatabaseConfig
{
    // Значения по умолчанию берутся из окружения — секрета в исходниках нет.
    //   FLOVMP_DB_HOST / FLOVMP_DB_PORT / FLOVMP_DB_NAME / FLOVMP_DB_USER / FLOVMP_DB_PASSWORD
    // Либо целиком: FLOVMP_DB_CONNECTION (её читает GamemodeResource раньше).
    // Пароля нет → BuildConnectionString вернёт пустую строку, и
    // AccountStoreFactory прозрачно уйдёт на локальный JsonAccountStore.
    public string Host { get; set; } = Env("FLOVMP_DB_HOST", "127.0.0.1");
    public int Port { get; set; } = int.TryParse(Environment.GetEnvironmentVariable("FLOVMP_DB_PORT"), out var p) ? p : 3306;
    public string Database { get; set; } = Env("FLOVMP_DB_NAME", "flovmp_server");
    public string User { get; set; } = Env("FLOVMP_DB_USER", "flovmp");
    public string Password { get; set; } = Env("FLOVMP_DB_PASSWORD", "");
    public int ConnectionTimeoutSeconds { get; set; } = 5;
    public int MinPoolSize { get; set; } = 2;
    public int MaxPoolSize { get; set; } = 30;

    private static string Env(string key, string fallback)
    {
        var v = Environment.GetEnvironmentVariable(key);
        return string.IsNullOrWhiteSpace(v) ? fallback : v;
    }

    public string BuildConnectionString()
    {
        if (string.IsNullOrEmpty(Password))
            return ""; // нет пароля в окружении — фабрика уйдёт на JSON-хранилище
        return $"Server={Host};Port={Port};Database={Database};User ID={User};Password={Password};" +
               $"ConnectionTimeout={ConnectionTimeoutSeconds};MinPoolSize={MinPoolSize};MaxPoolSize={MaxPoolSize};" +
               $"Character Set=utf8mb4;AllowUserVariables=True;";
    }
}