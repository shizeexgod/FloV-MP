using System;
using System.Collections.Generic;
using FloVMP.Core.Documents;

namespace FloVMP.Gamemode.Presets;

/// <summary>
/// Генератор и регистратор базовых документов для RP-гейммода FloV:MP.
/// </summary>
public static class DefaultDocuments
{
    private static readonly Random Rand = new();

    public static PlayerDocument IssuePassport(
        DocumentService service,
        int accountId,
        string fullName,
        DateTime birthDate,
        string gender,
        string residence,
        string issuedBy = "Паспортный стол центрального района")
    {
        var series = $"45 {Rand.Next(10, 25):D2}";
        var number = $"{Rand.Next(100000, 999999)}";
        var docNumber = $"{series} {number}";

        return service.IssuePassport(
            accountId,
            fullName,
            docNumber: docNumber,
            issuedBy: issuedBy,
            birthDate: birthDate,
            gender: gender,
            residence: residence);
    }

    public static PlayerDocument IssueDriverLicense(
        DocumentService service,
        int accountId,
        string fullName,
        IEnumerable<string> categories,
        int validityDays = 30,
        string issuedBy = "Отдел дорожной инспекции")
    {
        var series = $"77 {Rand.Next(10, 25):D2}";
        var number = $"{Rand.Next(100000, 999999)}";
        var docNumber = $"{series} {number}";

        return service.IssueDriverLicense(
            accountId,
            fullName,
            categories,
            validityDays: validityDays,
            docNumber: docNumber,
            issuedBy: issuedBy);
    }

    public static PlayerDocument IssueWeaponLicense(
        DocumentService service,
        int accountId,
        string fullName,
        int validityDays = 30,
        string issuedBy = "Лицензионно-разрешительный отдел")
    {
        var series = $"WL-{Rand.Next(10, 99):D2}";
        var number = $"{Rand.Next(100000, 999999)}";
        var docNumber = $"{series} {number}";

        return service.IssueWeaponLicense(
            accountId,
            fullName,
            validityDays: validityDays,
            docNumber: docNumber,
            issuedBy: issuedBy);
    }

    public static PlayerDocument IssueMedicalCard(
        DocumentService service,
        int accountId,
        string fullName,
        bool isPsychHealthy,
        bool isSubstanceFree,
        int validityDays = 14,
        string issuedBy = "Центральная городская больница")
    {
        var docNumber = $"МК-{Rand.Next(10000, 99999)}";
        var doc = service.IssueMedicalCard(
            accountId,
            fullName,
            isPsychHealthy,
            isSubstanceFree,
            validityDays: validityDays,
            docNumber: docNumber,
            issuedBy: issuedBy);

        doc.SetMeta("PsychiatristStatus", isPsychHealthy ? "Годен" : "Не годен");
        doc.SetMeta("NarcologistStatus", isSubstanceFree ? "Чист" : "Обнаружены ПАВ");
        doc.SetMeta("OverallStatus", (isPsychHealthy && isSubstanceFree) ? "Годен к службе и ношению оружия" : "Не годен к службе");
        return doc;
    }
}
