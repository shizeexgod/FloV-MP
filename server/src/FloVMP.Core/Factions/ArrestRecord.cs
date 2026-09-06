using System;

namespace FloVMP.Core.Factions;

/// <summary>
/// Запись об аресте / заключении под стражу в ИВС или тюрьму.
/// </summary>
public sealed class ArrestRecord
{
    public int AccountId { get; set; }
    public int OfficerAccountId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public int RemainingSeconds { get; set; }
    public DateTime ArrestedAtUtc { get; set; } = DateTime.UtcNow;

    public ArrestRecord() { }

    public ArrestRecord(int accountId, int officerAccountId, string reason, int seconds)
    {
        AccountId = accountId;
        OfficerAccountId = officerAccountId;
        Reason = reason;
        RemainingSeconds = seconds;
        ArrestedAtUtc = DateTime.UtcNow;
    }
}
