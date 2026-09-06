using System;
using System.Collections.Generic;
using System.Linq;

namespace FloVMP.Core.Documents;

/// <summary>
/// Сервис выпуска, проверки и аннулирования документов граждан («Держава Онлайн»).
/// Чистый .NET 8, без внешних зависимостей, потокобезопасен.
/// </summary>
public sealed class DocumentService
{
    private readonly object _lock = new();
    private readonly Dictionary<int, Dictionary<DocumentType, PlayerDocument>> _playerDocs = new();
    private readonly Random _rand = new();

    public PlayerDocument IssuePassport(
        int accountId,
        string fullName,
        string? docNumber = null,
        string? issuedBy = null,
        DateTime? birthDate = null,
        string? gender = null,
        string? residence = null)
    {
        lock (_lock)
        {
            var number = docNumber ?? $"{_rand.Next(1000, 9999)} {_rand.Next(100000, 999999)}";
            var issuer = issuedBy ?? "Department of Civil Registration";

            var doc = new PlayerDocument(accountId, DocumentType.Passport, number, fullName, issuer)
            {
                ExpiresAtUtc = null // Паспорт бессрочный
            };

            if (birthDate.HasValue) doc.SetMeta("BirthDate", birthDate.Value.ToString("yyyy-MM-dd"));
            if (!string.IsNullOrEmpty(gender)) doc.SetMeta("Gender", gender);
            if (!string.IsNullOrEmpty(residence)) doc.SetMeta("Residence", residence);

            GetOrCreateDocs(accountId)[DocumentType.Passport] = doc;
            return doc;
        }
    }

    public PlayerDocument IssueDriverLicense(
        int accountId,
        string fullName,
        IEnumerable<string> categories,
        int validityDays = 30,
        string? docNumber = null,
        string? issuedBy = null)
    {
        lock (_lock)
        {
            var number = docNumber ?? $"DL-{_rand.Next(100000, 999999)}";
            var issuer = issuedBy ?? "Department of Motor Vehicles";

            var doc = new PlayerDocument(
                accountId,
                DocumentType.DriverLicense,
                number,
                fullName,
                issuer,
                DateTime.UtcNow.AddDays(validityDays));

            var catList = categories.Select(c => c.Trim().ToUpperInvariant()).Distinct().ToList();
            doc.SetMeta("Categories", string.Join(",", catList));

            GetOrCreateDocs(accountId)[DocumentType.DriverLicense] = doc;
            return doc;
        }
    }

    public bool HasDriverCategory(int accountId, string category)
    {
        lock (_lock)
        {
            var doc = GetDocument(accountId, DocumentType.DriverLicense);
            if (doc == null || !doc.IsValid) return false;

            var cats = doc.GetMeta("Categories")?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
            return cats.Contains(category.Trim().ToUpperInvariant(), StringComparer.OrdinalIgnoreCase);
        }
    }

    public bool TryAddDriverCategory(int accountId, string category, out string error)
    {
        lock (_lock)
        {
            var doc = GetDocument(accountId, DocumentType.DriverLicense);
            if (doc == null)
            {
                error = "У игрока нет водительского удостоверения";
                return false;
            }

            if (!doc.IsValid)
            {
                error = "Водительское удостоверение недействительно или изъято";
                return false;
            }

            var cat = category.Trim().ToUpperInvariant();
            var cats = (doc.GetMeta("Categories")?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>()).ToList();

            if (cats.Contains(cat, StringComparer.OrdinalIgnoreCase))
            {
                error = $"Категория '{cat}' уже открыта";
                return false;
            }

            cats.Add(cat);
            doc.SetMeta("Categories", string.Join(",", cats));
            error = string.Empty;
            return true;
        }
    }

    public bool TryRevokeDriverCategory(int accountId, string category, out string error)
    {
        lock (_lock)
        {
            var doc = GetDocument(accountId, DocumentType.DriverLicense);
            if (doc == null || !doc.IsValid)
            {
                error = "Действующее водительское удостоверение не найдено";
                return false;
            }

            var cat = category.Trim().ToUpperInvariant();
            var cats = (doc.GetMeta("Categories")?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>()).ToList();

            if (!cats.Remove(cat))
            {
                error = $"Категория '{cat}' отсутствует в удостоверении";
                return false;
            }

            doc.SetMeta("Categories", string.Join(",", cats));
            error = string.Empty;
            return true;
        }
    }

    public PlayerDocument IssueWeaponLicense(
        int accountId,
        string fullName,
        int validityDays = 30,
        string? docNumber = null,
        string? issuedBy = null)
    {
        lock (_lock)
        {
            var number = docNumber ?? $"WPN-{_rand.Next(100000, 999999)}";
            var issuer = issuedBy ?? "Licensing Authority";

            var doc = new PlayerDocument(
                accountId,
                DocumentType.WeaponLicense,
                number,
                fullName,
                issuer,
                DateTime.UtcNow.AddDays(validityDays));

            GetOrCreateDocs(accountId)[DocumentType.WeaponLicense] = doc;
            return doc;
        }
    }

    public PlayerDocument IssueMedicalCard(
        int accountId,
        string fullName,
        bool isPsychHealthy,
        bool isSubstanceFree,
        int validityDays = 14,
        string? docNumber = null,
        string? issuedBy = null)
    {
        lock (_lock)
        {
            var number = docNumber ?? $"MED-{_rand.Next(10000, 99999)}";
            var issuer = issuedBy ?? "Medical Health Center";

            var doc = new PlayerDocument(
                accountId,
                DocumentType.MedicalCard,
                number,
                fullName,
                issuer,
                DateTime.UtcNow.AddDays(validityDays));

            doc.SetMeta("PsychiatristStatus", isPsychHealthy ? "Fit" : "Unfit");
            doc.SetMeta("NarcologistStatus", isSubstanceFree ? "Clean" : "SubstanceDetected");
            doc.SetMeta("OverallStatus", (isPsychHealthy && isSubstanceFree) ? "Approved" : "Rejected");

            GetOrCreateDocs(accountId)[DocumentType.MedicalCard] = doc;
            return doc;
        }
    }

    public PlayerDocument? GetDocument(int accountId, DocumentType type)
    {
        lock (_lock)
        {
            if (_playerDocs.TryGetValue(accountId, out var map) && map.TryGetValue(type, out var doc))
            {
                return doc;
            }
            return null;
        }
    }

    public IReadOnlyList<PlayerDocument> GetAllDocuments(int accountId)
    {
        lock (_lock)
        {
            if (_playerDocs.TryGetValue(accountId, out var map))
            {
                return map.Values.ToList();
            }
            return Array.Empty<PlayerDocument>();
        }
    }

    public bool TryRevokeDocument(int accountId, DocumentType type, string reason, out string error)
    {
        lock (_lock)
        {
            var doc = GetDocument(accountId, type);
            if (doc == null)
            {
                error = "У игрока нет такого документа";
                return false;
            }

            if (doc.IsRevoked)
            {
                error = "Документ уже изъят / аннулирован";
                return false;
            }

            doc.IsRevoked = true;
            doc.RevocationReason = reason;
            error = string.Empty;
            return true;
        }
    }

    private Dictionary<DocumentType, PlayerDocument> GetOrCreateDocs(int accountId)
    {
        if (!_playerDocs.TryGetValue(accountId, out var map))
        {
            map = new Dictionary<DocumentType, PlayerDocument>();
            _playerDocs[accountId] = map;
        }
        return map;
    }
}
