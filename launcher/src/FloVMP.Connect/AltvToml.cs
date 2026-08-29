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

    public static string DetectPlatform(string gtaPath)
    {
        // 1) по файлам в папке игры (надёжнее пути)
        bool Has(string f) => File.Exists(Path.Combine(gtaPath, f));
        bool HasDir(string d) => Directory.Exists(Path.Combine(gtaPath, d));

        if (Has("steam_api64.dll") || HasDir("steamapps")) return "steam";
        if (Has("EOSSDK-Win64-Shipping.dll") || Has("Rockstar-Games-Epic.exe") || HasDir(".egstore"))
            return "egs";
        if (Has("PlayGTAV.exe") && Has("Launcher.exe") && !Has("steam_api64.dll")) return "rockstar";

        // 2) по пути (fallback)
        var p = gtaPath.ToLowerInvariant();
        if (p.Contains("steamapps") || p.Contains("\\steam\\")) return "steam";
        if (p.Contains("\\epic games\\") || p.Contains("epicgames")) return "egs";
        if (p.Contains("rockstar")) return "rockstar";
        if (Path.GetFileName(gtaPath.TrimEnd('\\')).Length == 32) return "egs"; // GUID-папка Epic

        // По умолчанию egs, а НЕ steam: ошибочный 'steam' даёт "Не удалось
        // запустить Steam" на Epic-копии. alt:V принимает steam|egs|rockstar.
        return "egs";
    }
}
