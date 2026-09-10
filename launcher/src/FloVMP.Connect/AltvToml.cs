namespace FloVMP.Connect;

/// <summary>
/// Генерация <c>altv.toml</c> в папке клиента.
/// 'steam' с переменной окружения SteamAppId=271590 используется для прямого
/// автономного запуска без обращения к Epic Games Launcher и Rockstar Games Launcher.
/// </summary>
public static class AltvToml
{
    public static void Write(string clientDir, string gtaPath, bool debug, string? platformOverride = null, string? nickname = null)
    {
        var cache = Path.Combine(clientDir, "cache").Replace('\\', '/');
        var platform = string.IsNullOrWhiteSpace(platformOverride)
            ? DetectPlatform(gtaPath)
            : platformOverride.Trim().ToLowerInvariant();
        // Ник идёт в TOML literal-строку name = '...'. В TOML одинарные кавычки
        // внутри такой строки НЕэкранируемы, а лаунчерный/session.json-ник (в
        // отличие от серверного) может содержать что угодно. Ник вроде O'Brien
        // сломал бы весь altv.toml -> клиент не стартует. Санитизируем: оставляем
        // буквы/цифры/пробел/._- , режем длину, пустой -> дефолт.
        var playerName = SanitizeName(nickname);

        // Ветка клиента. ВАЖНО: alt:V принимает только 'release' / 'rc' / 'dev'.
        // 'internal' — НЕвалидна: лаунчер падает с "Указана недопустимая ветка
        // (0x30,012)" и GTA не запускается вовсе. Поэтому дефолт — 'release'
        // (клиент запускается и открывает игру). WRONG_STABLE_BUILD на release
        // снимается отдельно (патч сервера/клиента силами RE), не веткой.
        // FLOVMP_BRANCH позволяет попробовать 'dev'/'rc' при необходимости.
        var branch = Environment.GetEnvironmentVariable("FLOVMP_BRANCH");
        if (string.IsNullOrWhiteSpace(branch)) branch = "release";
        branch = branch.Trim().ToLowerInvariant();
        if (branch != "release" && branch != "rc" && branch != "dev") branch = "release";

        var toml = $"""
            audioFrameLimit = false
            autoBackup = true
            autoFindMic = false
            branch = '{branch}'
            cachePath = '{cache}'
            cefAlwaysFullCopy = false
            cefUseHardwareAcceleration = true
            consoleHeight = 0.40000000596046448
            consoleWidth = 0.40000000596046448
            crashOnFatalError = false
            crashReporterEnabled = false
            debug = {(debug ? "true" : "false")}
            disableForcedRawInput = false
            disableRtl = false
            discordRichPresence = false
            displaySystemUpdateMessagesInLog = false
            earlyAuthTestURL = ''
            enableDiscordOverlay = false
            enableGuildedOverlay = false
            enableNvidiaShadowPlayOverlay = false
            enableOverwolfOverlay = false
            expandedConsole = false
            externalConsoleX = 0
            externalConsoleY = 0
            gtaPlatform = '{platform}'
            gtapath = '{gtaPath}'
            heapSize = 2048
            lang = 'ru'
            lastip = ''
            launcherSkin = 'default'
            launcherSkinsDisabled = []
            linuxCompatibility = false
            logTimeFormat = '%H:%M:%S'
            maxDownloadSpeed = 0
            name = '{playerName}'
            netgraph = false
            permissionsSet = true
            promotedOnTop = false
            region = 'global'
            streamerMode = false
            textureBudgetPatch = true
            uiVolume = 50
            update = false
            useExternalConsole = false
            useSharedTextures = true
            voiceActivationEnabled = false
            voiceEnabled = false

            """;

        File.WriteAllText(Path.Combine(clientDir, "flovmp.toml"), toml);
        File.WriteAllText(Path.Combine(clientDir, "altv.toml"), toml);
    }

    /// <summary>
    /// Приводит ник к безопасному для TOML literal-строки виду: только буквы,
    /// цифры, пробел и <c>. _ -</c>; управляющие символы и кавычки убираются;
    /// длина ≤ 32. Пустой результат -> дефолт 'shize5'.
    /// </summary>
    private static string SanitizeName(string? nickname)
    {
        if (string.IsNullOrWhiteSpace(nickname)) return "shize5";
        var sb = new System.Text.StringBuilder(nickname.Length);
        foreach (var ch in nickname.Trim())
        {
            if (sb.Length >= 32) break;
            if (char.IsLetterOrDigit(ch) || ch is ' ' or '.' or '_' or '-')
                sb.Append(ch);
        }
        var name = sb.ToString().Trim();
        return name.Length == 0 ? "shize5" : name;
    }

    public static string DetectPlatform(string gtaPath)
    {
        if (string.IsNullOrWhiteSpace(gtaPath) || !Directory.Exists(gtaPath))
            return "egs";

        bool Has(string file) => File.Exists(Path.Combine(gtaPath, file));
        bool HasDir(string dir) => Directory.Exists(Path.Combine(gtaPath, dir));

        if (Has("EOSSDK-Win64-Shipping.dll") || Has("EOSSDK-Win64-Shipping-1.17.1.3.dll") || Has("Rockstar-Games-Epic.exe") || HasDir(".egstore") || Has("title.rgl"))
            return "egs";
        if (Has("steam_api64.dll") || HasDir("steamapps"))
            return "steam";
        if (Has("PlayGTAV.exe") && Has("GTAVLauncher.exe") && !Has("steam_api64.dll"))
            return "rgl";

        var path = gtaPath.ToLowerInvariant();
        if (path.Contains("\\epic games\\") || path.Contains("epicgames") || Path.GetFileName(gtaPath.TrimEnd('\\')).Length == 32)
            return "egs";
        if (path.Contains("steamapps") || path.Contains("\\steam\\"))
            return "steam";

        return "egs";
    }
}
