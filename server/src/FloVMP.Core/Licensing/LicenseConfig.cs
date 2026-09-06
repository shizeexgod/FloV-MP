using System;

namespace FloVMP.Core.Licensing;

/// <summary>
/// Конфигурация лицензирования сервера FloV:MP.
/// Загружается из server.toml или переменных окружения.
/// </summary>
public class LicenseConfig
{
    public string LicenseKey { get; set; } = "FLV-COMMERCIAL-DEFAULT";
    public string ServerIp { get; set; } = "127.0.0.1";
    public string ServerName { get; set; } = "Держава RP";
    public string VerifyUrl { get; set; } = "https://flovmp.ru/api/v1/license/verify";
    public string TelemetryUrl { get; set; } = "https://flovmp.ru/api/v1/telemetry/heartbeat";
    public int HeartbeatIntervalSec { get; set; } = 15;
    public int OfflineGraceHours { get; set; } = 24;
    public bool StrictMode { get; set; } = false;

    public static LicenseConfig FromEnvironment()
    {
        return new LicenseConfig
        {
            LicenseKey = Environment.GetEnvironmentVariable("FLOVMP_LICENSE_KEY") ?? "FLV-ENTERPRISE-2026-DERZHAVA",
            ServerIp = Environment.GetEnvironmentVariable("FLOVMP_SERVER_IP") ?? "188.127.229.224",
            ServerName = Environment.GetEnvironmentVariable("FLOVMP_SERVER_NAME") ?? "Держава Онлайн",
            VerifyUrl = Environment.GetEnvironmentVariable("FLOVMP_LICENSE_VERIFY_URL") ?? "http://localhost:3000/api/v1/license/verify",
            TelemetryUrl = Environment.GetEnvironmentVariable("FLOVMP_TELEMETRY_URL") ?? "http://localhost:3000/api/v1/telemetry/heartbeat",
            HeartbeatIntervalSec = int.TryParse(Environment.GetEnvironmentVariable("FLOVMP_TELEMETRY_INTERVAL_SEC"), out var s) ? s : 15,
            OfflineGraceHours = int.TryParse(Environment.GetEnvironmentVariable("FLOVMP_OFFLINE_GRACE_HOURS"), out var g) ? g : 24,
            StrictMode = Environment.GetEnvironmentVariable("FLOVMP_LICENSE_STRICT") == "true"
        };
    }
}
