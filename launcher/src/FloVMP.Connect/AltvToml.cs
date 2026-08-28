namespace FloVMP.Connect;

/// <summary>
/// Генерация <c>altv.toml</c> в папке клиента. gtapath — НАСТОЯЩАЯ папка
/// игры игрока (без подмены GTA5.exe). gtaPlatform определяем по пути.
/// </summary>
public static class AltvToml
{
    public static void Write(string clientDir, string gtaPath, bool debug)
    {
        var platform = DetectPlatform(gtaPath);
        var cache = Path.Combine(clientDir, "cache").Replace('\\', '/');

        var toml = $"""
            autoBackup = true
            branch = 'release'
            cachePath = '{cache}'
            crashReporterEnabled = false
            debug = {(debug ? "true" : "false")}
            discordRichPresence = false
            gtaPlatform = '{platform}'
            gtapath = '{gtaPath}'
            lang = 'ru'
            name = 'FloVMP'
            netgraph = false
            permissionsSet = true
            region = 'global'
            update = false
            useSharedTextures = true
            voiceActivationEnabled = false
            voiceEnabled = false

            """;

        File.WriteAllText(Path.Combine(clientDir, "altv.toml"), toml);
    }

    private static string DetectPlatform(string gtaPath)
    {
        var p = gtaPath.ToLowerInvariant();
        if (p.Contains("steamapps") || p.Contains("\\steam\\")) return "steam";
        if (p.Contains("epic") || p.Contains("program files\\") && Path.GetFileName(gtaPath).Length == 32) return "egs";
        if (p.Contains("rockstar")) return "rockstar";
        // папка Epic часто называется 32-символьным GUID
        return Path.GetFileName(gtaPath.TrimEnd('\\')).Length == 32 ? "egs" : "steam";
    }
}
