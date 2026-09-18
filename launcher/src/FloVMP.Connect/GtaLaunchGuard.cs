using System.Diagnostics;

namespace FloVMP.Connect;

public sealed record GtaLaunchCheck(bool Allowed, string Code, string Message);

/// <summary>
/// Prevents the legacy alt:V 16.4.39 native client from launching a GTA
/// profile it cannot understand. This is a preflight guard, not a compatibility
/// implementation: actual support still requires a native adapter for the
/// selected GTA build.
/// </summary>
public static class GtaLaunchGuard
{
    private const string SupportedLegacyBuild = "1.0.3521.0";

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

        if (string.Equals(version, SupportedLegacyBuild, StringComparison.OrdinalIgnoreCase))
        {
            return new(true, "legacy-3521", $"Legacy {version} совместим с текущим клиентом.");
        }

        return new(false, "legacy-native-adapter-missing",
            $"Legacy {version} несовместим с native-клиентом FloV:MP 16.4.39: текущий клиент рассчитан на RPF {SupportedLegacyBuild[4..^2]}. Запуск остановлен до native-адаптера, чтобы не получить Rpf mismatch/FAILED_TO_VERIFY_GAME_LICENSE.");
    }
}
