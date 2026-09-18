using System.Diagnostics;
using System.Security.Cryptography;

namespace FloVMP.Connect;

public sealed record GtaLaunchCheck(bool Allowed, string Code, string Message);

public sealed record GtaBuildIdentity(
    string FileVersion,
    string Sha256,
    long Size,
    string Executable,
    string Edition,
    string Platform);

/// <summary>
/// Prevents the legacy alt:V 16.4.39 native client from launching a GTA
/// profile it cannot understand. This is a preflight guard, not a compatibility
/// implementation: actual support still requires a native adapter for the
/// selected GTA build.
/// </summary>
public static class GtaLaunchGuard
{
    private const string NativeClientVersion = "16.4.39";
    private const string Legacy3521 = "1.0.3521.0";
    private const string Legacy3889 = "1.0.3889.0";

    // These identities are fingerprints of the user's installed game builds,
    // not a replacement for native compatibility. A build is allowed only
    // after its native adapter has passed the end-to-end gate.
    private static readonly IReadOnlyDictionary<string, (string Sha256, long Size, string Platform, bool Supported)> LegacyBuilds =
        new Dictionary<string, (string, long, string, bool)>(StringComparer.OrdinalIgnoreCase)
        {
            [Legacy3521] = ("", 0, "unknown", false),
            [Legacy3889] = ("677e4e355cfbdb13273b1d992407e3c261b3a108dc4dd5c8a0c4c1da651802e5", 47467128, "egs", false),
        };

    public static GtaLaunchCheck Evaluate(string gameExePath)
    {
        if (string.IsNullOrWhiteSpace(gameExePath) || !File.Exists(gameExePath))
            return new(false, "game-executable-missing", "GTA V executable не найден.");

        var fileName = Path.GetFileName(gameExePath);
        if (fileName.Equals("GTA5_Enhanced.exe", StringComparison.OrdinalIgnoreCase))
        {
            return new(false, "enhanced-native-client-missing",
                "GTA V Enhanced пока не поддерживается текущим native-клиентом FloV:MP 16.4.39.");
        }

        string version;
        try
        {
            version = FileVersionInfo.GetVersionInfo(gameExePath).FileVersion?.Trim() ?? "";
        }
        catch (Exception ex)
        {
            return new(false, "game-version-unreadable", $"Не удалось определить версию GTA5.exe: {ex.Message}");
        }

        if (string.Equals(version, Legacy3521, StringComparison.OrdinalIgnoreCase))
        {
            return new(false, "legacy-3521-native-validation-required",
                $"Legacy {version} распознан, но native-профиль FloV:MP 16.4.39 не прошёл полный E2E-тест. Запуск остановлен до подтверждения адаптера.");
        }

        if (string.Equals(version, Legacy3889, StringComparison.OrdinalIgnoreCase))
        {
            var fingerprint = GetSha256(gameExePath);
            var expected = LegacyBuilds[Legacy3889];
            if (!string.Equals(fingerprint, expected.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                return new(false, "legacy-3889-fingerprint-mismatch",
                    $"Legacy {version} найден, но SHA-256 GTA5.exe не совпал с зарегистрированным EGS-профилем b3889. Оригинальные файлы игры не изменялись.");
            }

            return new(false, "legacy-3889-native-adapter-missing",
                $"Legacy {version} EGS распознан точно, но native-клиент FloV:MP {NativeClientVersion} ещё не поддерживает RPF b3889. Нужен адаптер 3889; запуск со старым клиентом остановлен.");
        }

        return new(false, "legacy-native-adapter-missing",
            $"Legacy {version} не зарегистрирован в native-профилях FloV:MP {NativeClientVersion}. Запуск остановлен до добавления проверенного адаптера.");
    }

    public static GtaBuildIdentity Inspect(string gameExePath)
    {
        if (!File.Exists(gameExePath))
            throw new FileNotFoundException("GTA V executable не найден.", gameExePath);

        var info = FileVersionInfo.GetVersionInfo(gameExePath);
        var fileVersion = info.FileVersion?.Trim() ?? "";
        var fileName = Path.GetFileName(gameExePath);
        var isEnhanced = fileName.Equals("GTA5_Enhanced.exe", StringComparison.OrdinalIgnoreCase);
        var platform = DetectPlatform(Path.GetDirectoryName(gameExePath) ?? "");

        return new GtaBuildIdentity(
            fileVersion,
            GetSha256(gameExePath),
            new FileInfo(gameExePath).Length,
            fileName,
            isEnhanced ? "enhanced" : "legacy",
            platform);
    }

    private static string DetectPlatform(string gtaDir)
    {
        if (Directory.Exists(Path.Combine(gtaDir, ".egstore"))) return "egs";
        if (File.Exists(Path.Combine(gtaDir, "steam_api64.dll"))) return "steam";
        if (File.Exists(Path.Combine(gtaDir, "title.rgl"))) return "rgl";
        return "unknown";
    }

    private static string GetSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}
