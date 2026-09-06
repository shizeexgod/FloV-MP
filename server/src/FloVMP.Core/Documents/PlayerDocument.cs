using System;
using System.Collections.Generic;

namespace FloVMP.Core.Documents;

/// <summary>
/// Представление официального документа гражданина в «Держава Онлайн».
/// </summary>
public sealed class PlayerDocument
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public int OwnerAccountId { get; set; }
    public DocumentType Type { get; set; }
    public string DocumentNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public DateTime IssuedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAtUtc { get; set; }
    public string IssuedBy { get; set; } = string.Empty;
    public bool IsRevoked { get; set; }
    public string RevocationReason { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = new();

    public bool IsValid => !IsRevoked && (ExpiresAtUtc == null || ExpiresAtUtc.Value > DateTime.UtcNow);

    public PlayerDocument() { }

    public PlayerDocument(
        int ownerAccountId,
        DocumentType type,
        string documentNumber,
        string fullName,
        string issuedBy,
        DateTime? expiresAtUtc = null)
    {
        OwnerAccountId = ownerAccountId;
        Type = type;
        DocumentNumber = documentNumber;
        FullName = fullName;
        IssuedBy = issuedBy;
        IssuedAtUtc = DateTime.UtcNow;
        ExpiresAtUtc = expiresAtUtc;
    }

    public string? GetMeta(string key) =>
        Metadata.TryGetValue(key, out var val) ? val : null;

    public void SetMeta(string key, string value) =>
        Metadata[key] = value;
}
