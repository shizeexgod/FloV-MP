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
            ? "steam"
            : platformOverride.Trim().ToLowerInvariant();
        var playerName = string.IsNullOrWhiteSpace(nickname) ? "shize5" : nickname.Trim();

        var toml = $"""
            audioFrameLimit = false
            autoBackup = true
            autoFindMic = false
            branch = 'release'
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
            heapSize = 1024
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

        File.WriteAllText(Path.Combine(clientDir, "altv.toml"), toml);
    }

    public static string DetectPlatform(string gtaPath) => "steam";
}
