using System;
using System.Collections.Generic;
using FloVMP.Core.Documents;

namespace FloVMP.Gamemode.Presets;

/// <summary>
/// Генератор и регистратор документов российского образца для RP-проекта «Держава Онлайн».
/// Содержит специфичные для Москвы серии, органы выдачи и форматы справок.
/// </summary>
public static class DerzhavaDocuments
{
    private static readonly Random Rand = new();

    public static PlayerDocument IssueRussianPassport(
        DocumentService service,
        int accountId,
        string fullName,
        DateTime birthDate,
        string gender,
        string residence,
        string issuedBy = "Отдел УФМС ГУ МВД по г. Москве")
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

    public static PlayerDocument IssueRussianDriverLicense(
        DocumentService service,
        int accountId,
        string fullName,
        IEnumerable<string> categories,
        int validityDays = 30,
        string issuedBy = "1-й ОСБ ДПС ГИБДД по г. Москве")
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

    public static PlayerDocument IssueRussianWeaponLicense(
        DocumentService service,
        int accountId,
        string fullName,
        int validityDays = 30,
        string issuedBy = "ЦЛРР Главного управления Росгвардии")
    {
        var series = $"РОХа {Rand.Next(10, 99):D2}";
        var number = $"{Rand.Next(100000, 999999)}";
        var docNumber = $"{series} {number}";

        return service.IssueWeaponLicense(
            accountId,
            fullName,
            validityDays: validityDays,
            docNumber: docNumber,
            issuedBy: issuedBy);
    }

    public static PlayerDocument IssueRussianMedicalCard(
        DocumentService service,
        int accountId,
        string fullName,
        bool isPsychHealthy,
        bool isSubstanceFree,
        int validityDays = 14,
        string issuedBy = "ГКБ им. С.П. Боткина")
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
