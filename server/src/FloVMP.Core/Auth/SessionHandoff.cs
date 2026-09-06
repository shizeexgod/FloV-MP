using System.Text.Json;
using System.Text.Json.Serialization;

namespace FloVMP.Core.Auth;

/// <summary>
/// Передача аккаунта из игры в лаунчер. Если игрок не вошёл в лаунчере,
/// сразу зашёл в игру и авторизовался там — сервер пишет
/// <c>%LOCALAPPDATA%\FloridaV\session.json</c>, а лаунчер (Electron main.js,
/// <c>native:readSession</c>) его подхватывает и тоже становится
/// авторизованным под тем же аккаунтом.
///
/// Формат файла — camelCase, ровно те поля, что читает лаунчер:
/// <c>{ "username", "createdUtc", "email", "twoFa", "updatedUtc" }</c>.
/// </summary>
public static class SessionHandoff
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    /// <summary>Путь к общему с лаунчером session.json (та же папка, что и settings.json).</summary>
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FloridaV", "session.json");

    public sealed record SessionInfo(
        [property: JsonPropertyName("username")] string Username,
        [property: JsonPropertyName("createdUtc")] string CreatedUtc,
        [property: JsonPropertyName("email")] string Email,
        [property: JsonPropertyName("twoFa")] bool TwoFa,
        [property: JsonPropertyName("updatedUtc")] string UpdatedUtc);

    /// <summary>Записать аккаунт вошедшего в игре игрока для лаунчера. Не бросает.</summary>
    public static void Write(Account account, string? path = null)
    {
        try
        {
            path ??= DefaultPath;
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var info = new SessionInfo(
                account.Username,
                account.CreatedUtc,
                account.Email ?? "",
                account.TwoFaEnabled,
                DateTime.UtcNow.ToString("O"));

            var json = JsonSerializer.Serialize(info, JsonOpts);
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, json);
            if (File.Exists(path)) File.Replace(tmp, path, null);
            else File.Move(tmp, path);
        }
        catch
        {
            // Хэндофф — удобство, не критичный путь. Молча пропускаем
            // (нет прав на папку, диск занят и т.п.).
        }
    }

    /// <summary>Прочитать session.json, либо null (нет файла / битый / без username).</summary>
    public static SessionInfo? Read(string? path = null)
    {
        try
        {
            path ??= DefaultPath;
            if (!File.Exists(path)) return null;
            var info = JsonSerializer.Deserialize<SessionInfo>(File.ReadAllText(path), JsonOpts);
            return info is not null && !string.IsNullOrEmpty(info.Username) ? info : null;
        }
        catch { return null; }
    }

    /// <summary>Удалить session.json (выход из аккаунта). Не бросает.</summary>
    public static void Clear(string? path = null)
    {
        try { File.Delete(path ?? DefaultPath); } catch { }
    }
}
