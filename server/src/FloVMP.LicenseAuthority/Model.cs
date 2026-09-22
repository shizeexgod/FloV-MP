namespace FloVMP.LicenseAuthority;

/// <summary>Статус ключа. Отзыв окончательный, приостановку можно снять.</summary>
public static class LicenseStatus
{
    public const string Issued = "issued";        // выдан, ни один сервер ещё не активировал
    public const string Active = "active";        // активирован
    public const string Suspended = "suspended";  // приостановлен владельцем платформы
    public const string Revoked = "revoked";      // отозван навсегда

    public static string Russian(string status) => status switch
    {
        Issued => "выдан, не активирован",
        Active => "активирован",
        Suspended => "приостановлен",
        Revoked => "отозван",
        _ => status,
    };
}

public sealed class LicenseRecord
{
    public long Id { get; set; }
    public string Key { get; set; } = "";
    public string Project { get; set; } = "";
    public string Owner { get; set; } = "";
    public string Contact { get; set; } = "";
    public string Plan { get; set; } = "business";
    public int MaxPlayers { get; set; } = 1000;
    public int MaxServers { get; set; } = 1;
    public string Status { get; set; } = LicenseStatus.Issued;
    public string StatusReason { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? ActivatedAt { get; set; }
    /// <summary>null — бессрочно.</summary>
    public DateTime? ExpiresAt { get; set; }
    public string Note { get; set; } = "";

    public bool Expired(DateTime nowUtc) => ExpiresAt is { } e && nowUtc > e;
}

public sealed class ActivationRecord
{
    public long Id { get; set; }
    public long LicenseId { get; set; }
    public string ServerId { get; set; } = "";
    public string Ip { get; set; } = "";
    public string Version { get; set; } = "";
    public int Slots { get; set; }
    public DateTime FirstSeen { get; set; }
    public DateTime LastSeen { get; set; }
}

public sealed class EventRecord
{
    public DateTime At { get; set; }
    public long? LicenseId { get; set; }
    public string Key { get; set; } = "";
    public string Type { get; set; } = "";
    public string Ip { get; set; } = "";
    public string ServerId { get; set; } = "";
    public string Detail { get; set; } = "";
}

/// <summary>Хранилище: MariaDB на VDS (MySqlStore), в тестах — в памяти.</summary>
public interface ILicenseStore
{
    void EnsureSchema();
    LicenseRecord? FindByKey(string key);
    List<LicenseRecord> List(string? status);
    void Insert(LicenseRecord license);
    void Update(LicenseRecord license);
    List<ActivationRecord> Activations(long licenseId);
    ActivationRecord? FindActivation(long licenseId, string serverId);
    void InsertActivation(ActivationRecord activation);
    void TouchActivation(long id, string ip, string version, int slots, DateTime at);
    int RemoveActivations(long licenseId, string? serverId);
    void AddEvent(EventRecord e);
    List<EventRecord> Events(long? licenseId, int limit);
}
