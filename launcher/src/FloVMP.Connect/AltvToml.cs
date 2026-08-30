namespace FloVMP.Connect;

/// <summary>
/// Генерация <c>altv.toml</c> в папке клиента. gtapath — НАСТОЯЩАЯ папка
/// игры игрока (без подмены GTA5.exe). gtaPlatform определяем по пути.
/// </summary>
public static class AltvToml
{
    public static void Write(string clientDir, string gtaPath, bool debug, string? platformOverride = null)
    {
        var cache = Path.Combine(clientDir, "cache").Replace('\\', '/');
        var platform = string.IsNullOrWhiteSpace(platformOverride)
            ? DetectPlatform(gtaPath)
            : platformOverride.Trim().ToLowerInvariant();

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

        if (Has("EOSSDK-Win64-Shipping.dll") || Has("EOSSDK-Win64-Shipping-1.17.1.3.dll") || Has("Rockstar-Games-Epic.exe") || HasDir(".egstore") || Has("title.rgl"))
            return "rgl";
        if (Has("steam_api64.dll") || HasDir("steamapps")) return "steam";
        if (Has("PlayGTAV.exe") && Has("Launcher.exe") && !Has("steam_api64.dll")) return "rgl";

        // 2) по пути (fallback)
        var p = gtaPath.ToLowerInvariant();
        if (p.Contains("\\epic games\\") || p.Contains("epicgames") || p.Contains("9d2d0eb64d5c44529cece33fe2a46482")) return "rgl";
        if (p.Contains("steamapps") || p.Contains("\\steam\\")) return "steam";
        if (p.Contains("rockstar")) return "rgl";
        if (Path.GetFileName(gtaPath.TrimEnd('\\')).Length == 32) return "rgl"; // GUID-папка Epic

        // По умолчанию rgl для Epic/Rockstar
        return "rgl";
    }
}
