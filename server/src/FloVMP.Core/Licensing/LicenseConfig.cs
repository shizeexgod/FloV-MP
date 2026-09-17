using System;

namespace FloVMP.Core.Licensing;

/// <summary>
/// Настройки телеметрии для портала (FLOVMP_TELEMETRY_URL и др.).
/// Сама лицензия проверяется по license.flv — см. LicenseFile.
/// </summary>
public class LicenseConfig
{
    public string LicenseKey { get; set; } = "";
    public string ServerIp { get; set; } = "127.0.0.1";
    public string ServerName { get; set; } = "FloV:MP Server";
    public string TelemetryUrl { get; set; } = "https://flovmp.ru/api/v1/telemetry/heartbeat";
    public int HeartbeatIntervalSec { get; set; } = 15;
    public int OfflineGraceHours { get; set; } = 24;

    public static LicenseConfig FromEnvironment()
    {
        return new LicenseConfig
        {
            LicenseKey = Environment.GetEnvironmentVariable("FLOVMP_LICENSE_KEY") ?? "",
            ServerIp = Environment.GetEnvironmentVariable("FLOVMP_SERVER_IP") ?? "127.0.0.1",
            ServerName = Environment.GetEnvironmentVariable("FLOVMP_SERVER_NAME") ?? "FloV:MP Server",
            TelemetryUrl = Environment.GetEnvironmentVariable("FLOVMP_TELEMETRY_URL") ?? "",
            HeartbeatIntervalSec = int.TryParse(Environment.GetEnvironmentVariable("FLOVMP_TELEMETRY_INTERVAL_SEC"), out var s) ? s : 15,
            OfflineGraceHours = int.TryParse(Environment.GetEnvironmentVariable("FLOVMP_OFFLINE_GRACE_HOURS"), out var g) ? g : 24
        };
    }
}
