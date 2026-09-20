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
/// Prevents the bundled native client from launching a GTA profile it cannot
/// understand. This is a preflight guard, not a compatibility implementation:
/// actual support still requires a native adapter for the selected GTA build.
/// </summary>
public static class GtaLaunchGuard
{
    private const string NativeClientVersion = "16.4.39";
    private const string Legacy3521 = "1.0.3521.0";
    private const string Legacy3889 = "1.0.3889.0";
    private const string Enhanced1158 = "1.0.1158.13";
    private const string Legacy3889ExeSha256 = "677e4e355cfbdb13273b1d992407e3c261b3a108dc4dd5c8a0c4c1da651802e5";
    private const string Legacy3889UpdateRpfSha256 = "913a335314b4c3c616397782e4175d6a3cf217219750cb9b457b4a781a73ddc0";
    private const string Legacy3889Update2RpfSha256 = "8e2022693d3be6bf3961bf596045ef2762b34f6aa49d5afb8083659296ca1393";

    // These identities are fingerprints of the user's installed game builds,
    // not a replacement for native compatibility. A build is allowed only
    // after its native adapter has passed the end-to-end gate.
    private static readonly IReadOnlyDictionary<string, (string Sha256, long Size, string Platform, bool Supported)> LegacyBuilds =
        new Dictionary<string, (string, long, string, bool)>(StringComparer.OrdinalIgnoreCase)
        {
            [Legacy3521] = ("", 0, "unknown", false),
            [Legacy3889] = (Legacy3889ExeSha256, 47467128, "egs", false),
        };

    public static GtaLaunchCheck Evaluate(string gameExePath, string? nativeAdapterPath = null)
    {
        if (string.IsNullOrWhiteSpace(gameExePath) || !File.Exists(gameExePath))
            return new(false, "game-executable-missing", "GTA V executable не найден.");

        var fileName = Path.GetFileName(gameExePath);
        if (fileName.Equals("GTA5_Enhanced.exe", StringComparison.OrdinalIgnoreCase))
        {
            string enhancedVersion;
            try
            {
                enhancedVersion = FileVersionInfo.GetVersionInfo(gameExePath).FileVersion?.Trim() ?? "";
            }
            catch (Exception ex)
            {
                return new(false, "enhanced-version-unreadable",
                    $"Не удалось определить версию GTA V Enhanced: {ex.Message}");
            }

            if (string.Equals(enhancedVersion, Enhanced1158, StringComparison.OrdinalIgnoreCase))
            {
                return new(false, "enhanced-1158-native-adapter-missing",
                    $"GTA V Enhanced {enhancedVersion} распознан, но native-клиент FloV:MP {NativeClientVersion} не имеет проверенного адаптера Enhanced. Нужен адаптер flovmp-enhanced-native-1158; запуск остановлен.");
            }

            return new(false, "enhanced-native-client-missing",
                string.IsNullOrWhiteSpace(enhancedVersion)
                    ? "GTA V Enhanced распознан, но версия файла не читается; native-адаптер не подтверждён."
                    : $"GTA V Enhanced {enhancedVersion} не зарегистрирован в native-профилях FloV:MP {NativeClientVersion}; запуск остановлен до добавления проверенного адаптера.");
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

        if (string.IsNullOrWhiteSpace(version))
            return new(false, "game-version-unreadable", "У GTA5.exe отсутствует читаемая версия файла.");

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

            var gameDir = Path.GetDirectoryName(gameExePath) ?? "";
            var updateRpf = Path.Combine(gameDir, "update", "update.rpf");
            var update2Rpf = Path.Combine(gameDir, "update", "update2.rpf");
            if (!MatchesHash(updateRpf, Legacy3889UpdateRpfSha256))
            {
                return new(false, "legacy-3889-update-rpf-mismatch",
                    "Legacy 3889 найден, но update\\update.rpf отсутствует или не совпадает с EGS-профилем. Запуск остановлен без изменения файлов.");
            }

            if (!MatchesHash(update2Rpf, Legacy3889Update2RpfSha256))
            {
                return new(false, "legacy-3889-update2-rpf-mismatch",
                    "Legacy 3889 найден, но update\\update2.rpf отсутствует или не совпадает с EGS-профилем. Запуск остановлен без изменения файлов.");
            }

            var adapter = NativeAdapterProbe.Probe(nativeAdapterPath, gameDir);
            if (!adapter.Found)
            {
                return new(false, "legacy-3889-native-adapter-missing",
                    $"Legacy {version} EGS распознан точно, но native-клиент FloV:MP {NativeClientVersion} ещё не поддерживает RPF b3889. Нужен адаптер 3889; запуск со старым клиентом остановлен.");
            }

            if (!adapter.FingerprintValid || !adapter.RuntimeBound)
            {
                return new(false, $"legacy-3889-{adapter.Code}", adapter.Message);
            }

            return new(false, "legacy-3889-native-e2e-required",
                "Native adapter b3889 привязан, но двухклиентский E2E ещё не подтверждён; запуск остановлен до проверки синхронизации.");
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

    private static bool MatchesHash(string path, string expected)
    {
        return File.Exists(path) && string.Equals(GetSha256(path), expected, StringComparison.OrdinalIgnoreCase);
    }
}
