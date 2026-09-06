using System;
using System.Collections.Generic;
using FloVMP.Core.Documents;
using Xunit;

namespace FloVMP.Core.Tests;

public class DocumentTests
{
    [Fact]
    public void Passport_IssuanceAndValidity()
    {
        var service = new DocumentService();
        const int accountId = 42;

        var pass = service.IssuePassport(
            accountId,
            "Иванов Иван Иванович",
            new DateTime(1995, 5, 12),
            "Мужской",
            "г. Москва, ул. Тверская, д. 7, кв. 14");

        Assert.NotNull(pass);
        Assert.Equal(DocumentType.Passport, pass.Type);
        Assert.Equal("Иванов Иван Иванович", pass.FullName);
        Assert.True(pass.IsValid);
        Assert.Null(pass.ExpiresAtUtc); // Паспорт бессрочный
        Assert.Equal("1995-05-12", pass.GetMeta("BirthDate"));
        Assert.Equal("Мужской", pass.GetMeta("Gender"));
        Assert.Contains("Москва", pass.GetMeta("Residence")!);

        var retrieved = service.GetDocument(accountId, DocumentType.Passport);
        Assert.NotNull(retrieved);
        Assert.Equal(pass.DocumentNumber, retrieved.DocumentNumber);
    }

    [Fact]
    public void DriverLicense_CategoriesAndManagement()
    {
        var service = new DocumentService();
        const int accountId = 42;

        var lic = service.IssueDriverLicense(
            accountId,
            "Иванов Иван Иванович",
            new[] { "B" },
            validityDays: 30);

        Assert.NotNull(lic);
        Assert.True(service.HasDriverCategory(accountId, "B"));
        Assert.False(service.HasDriverCategory(accountId, "A"));
        Assert.False(service.HasDriverCategory(accountId, "C"));

        // Add category A (мотоцикл)
        Assert.True(service.TryAddDriverCategory(accountId, "A", out var errAdd));
        Assert.Empty(errAdd);
        Assert.True(service.HasDriverCategory(accountId, "A"));

        // Cannot add duplicate category
        Assert.False(service.TryAddDriverCategory(accountId, "A", out var errDup));
        Assert.Equal("Категория 'A' уже открыта", errDup);

        // Revoke category B
        Assert.True(service.TryRevokeDriverCategory(accountId, "B", out var errRevoke));
        Assert.Empty(errRevoke);
        Assert.False(service.HasDriverCategory(accountId, "B"));
        Assert.True(service.HasDriverCategory(accountId, "A"));

        // Cannot revoke category not held
        Assert.False(service.TryRevokeDriverCategory(accountId, "C", out var errNoCat));
        Assert.Equal("Категория 'C' отсутствует в удостоверении", errNoCat);
    }

    [Fact]
    public void WeaponLicenseAndMedicalCard_Flow()
    {
        var service = new DocumentService();
        const int accountId = 10;

        var med = service.IssueMedicalCard(
            accountId,
            "Петров Петр",
            isPsychHealthy: true,
            isSubstanceFree: true,
            validityDays: 14);

        Assert.NotNull(med);
        Assert.True(med.IsValid);
        Assert.Equal("Годен к службе и ношению оружия", med.GetMeta("OverallStatus"));

        var wep = service.IssueWeaponLicense(
            accountId,
            "Петров Петр",
            validityDays: 60);

        Assert.NotNull(wep);
        Assert.True(wep.IsValid);
        Assert.StartsWith("РОХа", wep.DocumentNumber);

        var allDocs = service.GetAllDocuments(accountId);
        Assert.Equal(2, allDocs.Count);
    }

    [Fact]
    public void RevokeDocument_Invalidates()
    {
        var service = new DocumentService();
        const int accountId = 99;

        service.IssueWeaponLicense(accountId, "Сидоров Алексей");
        var doc = service.GetDocument(accountId, DocumentType.WeaponLicense);
        Assert.NotNull(doc);
        Assert.True(doc.IsValid);

        Assert.True(service.TryRevokeDocument(accountId, DocumentType.WeaponLicense, "Нарушение правил обращения с оружием", out var err));
        Assert.Empty(err);

        Assert.True(doc.IsRevoked);
        Assert.False(doc.IsValid);
        Assert.Equal("Нарушение правил обращения с оружием", doc.RevocationReason);

        // Cannot revoke already revoked document
        Assert.False(service.TryRevokeDocument(accountId, DocumentType.WeaponLicense, "Повторно", out var errAlready));
        Assert.Equal("Документ уже изъят / аннулирован", errAlready);
    }
}
