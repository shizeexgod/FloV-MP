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
    // без базы сервер работает на файлах (права и баны).
    public string Host { get; set; } = Env("FLOVMP_DB_HOST", "127.0.0.1");
    public int Port { get; set; } = int.TryParse(Environment.GetEnvironmentVariable("FLOVMP_DB_PORT"), out var p) ? p : 3306;
    public string Database { get; set; } = Env("FLOVMP_DB_NAME", "flovmp_server");
    public string User { get; set; } = Env("FLOVMP_DB_USER", "flovmp");
    public string Password { get; set; } = Env("FLOVMP_DB_PASSWORD", "");
    public int ConnectionTimeoutSeconds { get; set; } = 5;

    // Пул ADO.NET (MySqlConnector). Дефолт 30 был рассчитан на демо-масштаб и
    // становится узким местом на 2000 слотов: при массовом наплыве (рестарт,
    // прайм-тайм) запросы встают в очередь за свободным соединением.
    // ВАЖНО: MaxPoolSize не должен превышать max_connections сервера MariaDB
    // (дефолт 151) с запасом на служебные подключения — иначе БД начнёт
    // отклонять соединения. Настраивается без пересборки.
    public int MinPoolSize { get; set; } =
        int.TryParse(Environment.GetEnvironmentVariable("FLOVMP_DB_POOL_MIN"), out var mn) ? mn : 8;
    public int MaxPoolSize { get; set; } =
        int.TryParse(Environment.GetEnvironmentVariable("FLOVMP_DB_POOL_MAX"), out var mx) ? mx : 120;

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