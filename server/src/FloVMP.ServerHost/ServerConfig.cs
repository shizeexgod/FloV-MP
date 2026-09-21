using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace FloVMP.ServerHost;

/// <summary>
/// Файлы настроек владельца сервера: config/flovmp.env, server/server.toml,
/// voice/voice.toml. Создаются из шаблонов при первом запуске; существующие
/// никогда не перезаписываются.
/// </summary>
public static class ServerConfig
{
    private static readonly UTF8Encoding Utf8 = new(false);

    public sealed record InitResult(bool CreatedEnv, bool CreatedToml);

    /// <summary>Сколько потоков синхронизации отдать серверу: половина ядер, 1..4.</summary>
    private static int SyncThreads() => Math.Clamp(Environment.ProcessorCount / 2, 1, 4);

    public static InitResult Initialize(string root)
    {
        var createdEnv = false;
        var envPath = Path.Combine(root, "config", "flovmp.env");
        if (!File.Exists(envPath))
        {
            var example = Path.Combine(root, "config", "flovmp.env.example");
            var text = File.Exists(example) ? File.ReadAllText(example, Utf8) : "";
            text = SetEnvValue(text, "FLOVMP_SETUP_TOKEN", NewSetupToken());
            Directory.CreateDirectory(Path.GetDirectoryName(envPath)!);
            File.WriteAllText(envPath, text, Utf8);
            createdEnv = true;
        }

        var serverToml = Path.Combine(root, "server", "server.toml");
        var voiceToml = Path.Combine(root, "voice", "voice.toml");
        var createdToml = false;
        if (!File.Exists(serverToml) || !File.Exists(voiceToml))
        {
            // Секрет голоса общий для обоих файлов — пересоздаём пару целиком,
            // но только отсутствующие файлы: существующий не трогаем.
            var secret = RandomNumberGenerator.GetInt32(1, int.MaxValue);
            var map = new Dictionary<string, string>
            {
                ["__FLOVMP_NAME__"] = "FloV:MP Server",
                ["__FLOVMP_PORT__"] = "7788",
                ["__FLOVMP_PLAYERS__"] = "100",
                ["__FLOVMP_VOICE_SECRET__"] = secret.ToString(),
                ["__FLOVMP_VOICE_PORT__"] = "7896",
                ["__FLOVMP_VOICE_PUBLIC_HOST__"] = "127.0.0.1",
                ["__FLOVMP_VOICE_PUBLIC_PORT__"] = "7895",
                // Потоки синхронизации по ядрам машины: на одном потоке
                // отправка упирается в несколько сотен игроков раньше, чем
                // в процессор. Половина ядер, но не больше четырёх — остальное
                // нужно стримеру и голосовому серверу.
                ["__FLOVMP_SYNC_SEND__"] = SyncThreads().ToString(),
                ["__FLOVMP_SYNC_RECEIVE__"] = SyncThreads().ToString(),
            };
            if (File.Exists(serverToml) != File.Exists(voiceToml))
            {
                // Один из файлов уже есть — берём секрет из него, иначе голос
                // не соединится с игровым сервером.
                var existing = File.Exists(serverToml) ? File.ReadAllText(serverToml) : File.ReadAllText(voiceToml);
                var m = Regex.Match(existing, @"(?m)^\s*(?:externalSecret|secret)\s*=\s*(\d+)");
                if (m.Success) map["__FLOVMP_VOICE_SECRET__"] = m.Groups[1].Value;
            }

            foreach (var (template, target) in new[]
                     {
                         (Path.Combine(root, "server", "server.toml.example"), serverToml),
                         (Path.Combine(root, "voice", "voice.toml.example"), voiceToml),
                     })
            {
                if (File.Exists(target)) continue;
                if (!File.Exists(template))
                    throw new FileNotFoundException("нет шаблона настроек — распакуйте архив полностью", template);
                var text = File.ReadAllText(template, Utf8);
                foreach (var (k, v) in map) text = text.Replace(k, v);
                File.WriteAllText(target, text, Utf8);
            }
            createdToml = true;
        }

        return new InitResult(createdEnv, createdToml);
    }

    /// <summary>Пары КЛЮЧ=значение из flovmp.env. Строки не исполняются.</summary>
    public static Dictionary<string, string> LoadEnv(string root)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var path = Path.Combine(root, "config", "flovmp.env");
        if (!File.Exists(path)) return result;
        foreach (var raw in File.ReadAllLines(path, Utf8))
        {
            var m = Regex.Match(raw, @"^\s*([A-Za-z_][A-Za-z0-9_]*)=(.*)$");
            if (!m.Success) continue;
            var val = m.Groups[2].Value.Trim();
            if (val.Length >= 2 && (val[0] == '"' && val[^1] == '"' || val[0] == '\'' && val[^1] == '\''))
                val = val[1..^1];
            result[m.Groups[1].Value] = val;
        }
        return result;
    }

    /// <summary>Значение ключа верхнего уровня из server.toml (до первой [секции]) или из секции.</summary>
    public static string? ReadToml(string path, string key, string? section = null)
    {
        if (!File.Exists(path)) return null;
        string? current = null;
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.StartsWith('#') || line.Length == 0) continue;
            var sec = Regex.Match(line, @"^\[([^\]]+)\]");
            if (sec.Success) { current = sec.Groups[1].Value.Trim(); continue; }
            if (!string.Equals(current, section, StringComparison.OrdinalIgnoreCase)) continue;
            var kv = Regex.Match(line, @"^([A-Za-z0-9_]+)\s*=\s*(.+?)\s*(#.*)?$");
            if (kv.Success && kv.Groups[1].Value == key)
                return kv.Groups[2].Value.Trim().Trim('"', '\'');
        }
        return null;
    }

    public static string SetEnvValue(string text, string key, string value)
    {
        var pattern = new Regex("(?m)^" + Regex.Escape(key) + "=.*$");
        if (pattern.IsMatch(text)) return pattern.Replace(text, key + "=" + value, 1);
        if (text.Length > 0 && !text.EndsWith('\n')) text += Environment.NewLine;
        return text + key + "=" + value + Environment.NewLine;
    }

    private static string NewSetupToken()
    {
        var hex = Convert.ToHexString(RandomNumberGenerator.GetBytes(8));
        return $"FLV-{hex[..4]}-{hex[4..8]}-{hex[8..12]}-{hex[12..16]}";
    }
}

public static class Workspace
{
    /// <summary>
    /// Папка gamemode — ваш сервер. Создаётся из sdk/template, если её нет;
    /// существующая не трогается никогда.
    /// </summary>
    public static bool CreateGamemodeIfMissing(string root)
    {
        var target = Path.Combine(root, "gamemode");
        var template = Path.Combine(root, "sdk", "template");
        if (Directory.Exists(target) || !Directory.Exists(template)) return false;
        CopyDirectory(template, target);
        return true;
    }

    /// <summary>
    /// Добавить ресурс в список resources в server.toml, если его там нет и он
    /// собран (есть resource.toml). Возвращает true, если файл изменён.
    /// </summary>
    public static bool EnsureResourceEnabled(string root, string name)
    {
        var serverToml = Path.Combine(root, "server", "server.toml");
        if (!File.Exists(serverToml)) return false;
        if (!File.Exists(Path.Combine(root, "server", "resources", name, "resource.toml"))) return false;

        var text = File.ReadAllText(serverToml);
        var match = System.Text.RegularExpressions.Regex.Match(text, @"(?ms)^resources\s*=\s*\[(.*?)^\s*\]");
        if (!match.Success) return false;
        if (match.Groups[1].Value.Contains("\"" + name + "\"")) return false;

        var body = match.Groups[1];
        var insertAt = body.Index + body.Length;
        var newline = text.Contains("\r\n") ? "\r\n" : "\n";
        text = text.Insert(insertAt, "    \"" + name + "\"," + newline);
        File.WriteAllText(serverToml, text, new System.Text.UTF8Encoding(false));
        return true;
    }

    /// <summary>Папка отложенной сборки: сюда собирает build.cmd, пока сервер запущен.</summary>
    public static string PendingDir(string root) => Path.Combine(root, "gamemode", ".pending");

    /// <summary>
    /// Подставить отложенную сборку своего сервера перед запуском. Пока сервер
    /// работает, его Gamemode.dll занята, и сборка кладёт результат в
    /// gamemode/.pending. При следующем запуске он переносится в
    /// server/resources/gamemode — владельцу не нужно ловить момент, когда
    /// сервер остановлен, чтобы собрать свой код.
    /// Возвращает true, если сборка была подставлена.
    /// </summary>
    public static bool ApplyPendingGamemode(string root)
    {
        var pending = PendingDir(root);
        if (!File.Exists(Path.Combine(pending, "Gamemode.dll"))) return false;

        var target = Path.Combine(root, "server", "resources", "gamemode");
        Directory.CreateDirectory(target);
        foreach (var dir in Directory.GetDirectories(pending, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(target, Path.GetRelativePath(pending, dir)));
        foreach (var file in Directory.GetFiles(pending, "*", SearchOption.AllDirectories))
            File.Copy(file, Path.Combine(target, Path.GetRelativePath(pending, file)), overwrite: true);

        Directory.Delete(pending, recursive: true);
        return true;
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, dir)));
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            File.Copy(file, Path.Combine(destination, Path.GetRelativePath(source, file)));
    }
}
