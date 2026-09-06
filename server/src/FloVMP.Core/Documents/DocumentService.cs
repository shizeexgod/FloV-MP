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
        DateTime birthDate,
        string gender,
        string residence,
        string issuedBy = "Отдел УФМС ГУ МВД по г. Москве")
    {
        lock (_lock)
        {
            var series = $"45 {_rand.Next(10, 25):D2}";
            var number = $"{_rand.Next(100000, 999999)}";
            var docNumber = $"{series} {number}";

            var doc = new PlayerDocument(accountId, DocumentType.Passport, docNumber, fullName, issuedBy)
            {
                ExpiresAtUtc = null // Паспорт бессрочный
            };

            doc.SetMeta("BirthDate", birthDate.ToString("yyyy-MM-dd"));
            doc.SetMeta("Gender", gender);
            doc.SetMeta("Residence", residence);

            GetOrCreateDocs(accountId)[DocumentType.Passport] = doc;
            return doc;
        }
    }

    public PlayerDocument IssueDriverLicense(
        int accountId,
        string fullName,
        IEnumerable<string> categories,
        int validityDays = 30,
        string issuedBy = "1-й ОСБ ДПС ГИБДД по г. Москве")
    {
        lock (_lock)
        {
            var series = $"77 {_rand.Next(10, 25):D2}";
            var number = $"{_rand.Next(100000, 999999)}";
            var docNumber = $"{series} {number}";

            var doc = new PlayerDocument(
                accountId,
                DocumentType.DriverLicense,
                docNumber,
                fullName,
                issuedBy,
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
        string issuedBy = "ЦЛРР Главного управления Росгвардии")
    {
        lock (_lock)
        {
            var series = $"РОХа {_rand.Next(10, 99):D2}";
            var number = $"{_rand.Next(100000, 999999)}";
            var docNumber = $"{series} {number}";

            var doc = new PlayerDocument(
                accountId,
                DocumentType.WeaponLicense,
                docNumber,
                fullName,
                issuedBy,
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
        string issuedBy = "ГКБ им. С.П. Боткина")
    {
        lock (_lock)
        {
            var docNumber = $"МК-{_rand.Next(10000, 99999)}";
            var doc = new PlayerDocument(
                accountId,
                DocumentType.MedicalCard,
                docNumber,
                fullName,
                issuedBy,
                DateTime.UtcNow.AddDays(validityDays));

            doc.SetMeta("PsychiatristStatus", isPsychHealthy ? "Годен" : "Не годен");
            doc.SetMeta("NarcologistStatus", isSubstanceFree ? "Чист" : "Обнаружены ПАВ");
            doc.SetMeta("OverallStatus", (isPsychHealthy && isSubstanceFree) ? "Годен к службе и ношению оружия" : "Не годен к службе");

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
