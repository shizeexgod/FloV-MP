using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace FloVMP.Core.Licensing;

/// <summary>
/// Настройки связи с сервером лицензий FloV:MP: online-проверка и телеметрия.
/// Локальная подпись license.flv остаётся обязательной — см. LicenseFile.
/// </summary>
public class LicenseConfig
{
    /// <summary>Сервер лицензий FloV:MP (VDS). Замена — FLOVMP_LICENSE_URL.</summary>
    public const string DefaultAuthorityUrl = "http://188.127.229.224";

    public string LicenseKey { get; set; } = "";
    public string ServerIp { get; set; } = "127.0.0.1";
    public string ServerName { get; set; } = "FloV:MP Server";
    public string AuthorityUrl { get; set; } = DefaultAuthorityUrl;
    public string TelemetryUrl { get; set; } = DefaultAuthorityUrl + "/api/v1/telemetry/heartbeat";
    public string LicenseVerifyUrl { get; set; } = DefaultAuthorityUrl + "/api/v1/license/verify";
    /// <summary>ID этой установки: по нему сервер лицензий считает серверы на ключ.</summary>
    public string ServerId { get; set; } = "";
    public int HeartbeatIntervalSec { get; set; } = 15;
    public int OfflineGraceHours { get; set; } = 24;

    public string LicenseDownloadUrl(string key) =>
        AuthorityUrl.TrimEnd('/') + "/api/v1/licenses/download-by-key?key=" + Uri.EscapeDataString(key.Trim()) +
        "&server=" + Uri.EscapeDataString(ServerId);

    /// <summary>
    /// Значение из окружения, кроме адресов старого портала flovmp.ru: они
    /// остались в config/flovmp.env прежних установок, а портал лицензии
    /// больше не выдаёт — иначе обновлённый сервер ходил бы не туда.
    /// </summary>
    private static string? Current(string name)
    {
        var v = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(v)) return null;
        return v.Contains("flovmp.ru", StringComparison.OrdinalIgnoreCase) ? null : v;
    }

    public static LicenseConfig FromEnvironment()
    {
        var baseUrl = (Current("FLOVMP_LICENSE_URL") ?? DefaultAuthorityUrl).Trim().TrimEnd('/');
        return new LicenseConfig
        {
            LicenseKey = Environment.GetEnvironmentVariable("FLOVMP_LICENSE_KEY") ?? "",
            ServerIp = Environment.GetEnvironmentVariable("FLOVMP_SERVER_IP") ?? "127.0.0.1",
            ServerName = Environment.GetEnvironmentVariable("FLOVMP_SERVER_NAME") ?? "FloV:MP Server",
            AuthorityUrl = baseUrl,
            TelemetryUrl = Current("FLOVMP_TELEMETRY_URL") ?? baseUrl + "/api/v1/telemetry/heartbeat",
            LicenseVerifyUrl = Current("FLOVMP_LICENSE_VERIFY_URL") ?? baseUrl + "/api/v1/license/verify",
            ServerId = ServerIdentity.Get(),
            HeartbeatIntervalSec = int.TryParse(Environment.GetEnvironmentVariable("FLOVMP_TELEMETRY_INTERVAL_SEC"), out var s) ? s : 15,
            OfflineGraceHours = int.TryParse(Environment.GetEnvironmentVariable("FLOVMP_OFFLINE_GRACE_HOURS"), out var g) ? g : 24
        };
    }
}

/// <summary>
/// Стабильный ID установки сервера: машина + папка установки. Копия папки на
/// другую машину — другой ID, то есть ещё один сервер в лимите ключа.
/// Повторный запуск и обновление платформы ID не меняют.
/// </summary>
public static class ServerIdentity
{
    private static string? _cached;

    public static string Get(string? installRoot = null)
    {
        if (installRoot is null && _cached is not null) return _cached;
        var root = installRoot ?? Directory.GetParent(Directory.GetCurrentDirectory())?.FullName ?? Directory.GetCurrentDirectory();
        var id = Compute(MachineId(), root);
        if (installRoot is null) _cached = id;
        return id;
    }

    public static string Compute(string machineId, string installRoot)
    {
        var normalized = Path.GetFullPath(installRoot).TrimEnd('\\', '/').ToLowerInvariant();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes("flovmp-server|" + machineId.Trim().ToLowerInvariant() + "|" + normalized));
        return Convert.ToHexString(hash)[..20].ToLowerInvariant();
    }

    private static string MachineId()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
                if (key?.GetValue("MachineGuid") is string guid && guid.Length > 0) return guid;
            }
            foreach (var path in new[] { "/etc/machine-id", "/var/lib/dbus/machine-id" })
                if (File.Exists(path)) return File.ReadAllText(path).Trim();
        }
        catch (Exception) { }
        return Environment.MachineName;
    }
}
