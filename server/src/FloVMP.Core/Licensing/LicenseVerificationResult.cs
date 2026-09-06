using System;
using System.Collections.Generic;

namespace FloVMP.Core.Licensing;

public class LicenseVerificationResult
{
    public bool IsValid { get; set; }
    public string LicenseKey { get; set; } = string.Empty;
    public string ServerName { get; set; } = string.Empty;
    public string Plan { get; set; } = "indie";
    public int MaxPlayers { get; set; } = 128;
    public DateTime ExpiresAt { get; set; } = DateTime.MinValue;
    public string BoundIp { get; set; } = "0.0.0.0";
    public Dictionary<string, bool> Features { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public bool SignatureValid { get; set; }
    public bool IsCachedOffline { get; set; }

    public bool IsExpired => ExpiresAt < DateTime.UtcNow;

    public static LicenseVerificationResult Failure(string error) => new()
    {
        IsValid = false,
        ErrorMessage = error
    };
}
