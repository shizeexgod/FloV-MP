using FloVMP.Core.Admin;
using FloVMP.Core.Auth;
using Xunit;

namespace FloVMP.Core.Tests;

[Collection("AdminEnvironment")]
public class AdminTests
{
    [Theory]
    [InlineData(0, "Игрок")]
    [InlineData(1, "Мл. Модератор")]
    [InlineData(2, "Модератор")]
    [InlineData(3, "Ст. Модератор")]
    [InlineData(4, "Администратор")]
    [InlineData(5, "Ст. Администратор")]
    [InlineData(6, "Куратор")]
    [InlineData(7, "Главный Администратор")]
    [InlineData(8, "Руководитель проекта")]
    public void AdminTitles_ReturnsCorrectTitle_ForEachLevel(int level, string expectedTitle)
    {
        Assert.Equal(expectedTitle, AdminTitles.GetTitle(level));
    }

    [Fact]
    public void ByDefault_OnlyFounderHasEveryCommand()
    {
        AdminCommandRegistry.ResetToDefaults();

        // На новом сервере админ один — создатель, и ему доступно всё.
        foreach (var def in AdminCommandRegistry.All)
        {
            Assert.Equal(8, def.MinLevel);
            Assert.True(AdminCommandRegistry.CanExecute(8, def.Name));
            Assert.False(AdminCommandRegistry.CanExecute(7, def.Name));
            Assert.False(AdminCommandRegistry.CanExecute(0, def.Name));
        }

        // Команды, которых в платформе нет, не разрешены никому.
        Assert.False(AdminCommandRegistry.CanExecute(8, "jail"));
        Assert.False(AdminCommandRegistry.CanExecute(8, "givemoney"));
    }

    [Fact]
    public void CommandLevels_OwnerFileOverridesDefaults()
    {
        try
        {
            var (levels, problems) = AdminCommandLevels.Parse(
                "# свой сервер\nkick 1\nban 1   # модератор банит сам\n\n");
            Assert.Empty(problems);
            Assert.Equal(2, levels.Count);

            Assert.Equal(2, AdminCommandLevels.Parse("kick 1\nban 1\n").Levels.Count);
            AdminCommandRegistry.ApplyOverrides(levels);

            Assert.True(AdminCommandRegistry.CanExecute(1, "kick"));
            Assert.True(AdminCommandRegistry.CanExecute(1, "ban"));
            // Команда, которой в файле нет, остаётся у создателя сервера.
            Assert.False(AdminCommandRegistry.CanExecute(7, "setadmin"));
            Assert.False(AdminCommandRegistry.CanExecute(7, "noclip"));
        }
        finally
        {
            AdminCommandRegistry.ResetToDefaults();
        }
    }

    [Fact]
    public void CommandLevels_BadLinesAreReportedAndSkipped()
    {
        // Опечатка в файле не должна ни ронять сервер, ни открывать команду всем.
        var (levels, problems) = AdminCommandLevels.Parse(
            "kikc 1\nkick\nkick 99\nkick abc\nkick 2\n");

        Assert.Equal(4, problems.Count);
        Assert.Single(levels);
        Assert.Equal(2, levels["kick"]);
    }

    [Fact]
    public void CommandLevels_DefaultFileCoversEveryCommand()
    {
        AdminCommandRegistry.ResetToDefaults();
        var (levels, problems) = AdminCommandLevels.Parse(AdminCommandLevels.DefaultFileContent());

        Assert.Empty(problems);
        Assert.Equal(AdminCommandRegistry.All.Count, levels.Count);
        foreach (var def in AdminCommandRegistry.All)
            Assert.Equal(def.MinLevel, levels[def.Name]);
    }

    [Fact]
    public void Account_BanChecks_WorkProperly()
    {
        var acc = new Account { Username = "BannedUser" };
        var now = DateTime.UtcNow;

        // Не забанен
        Assert.False(acc.IsBanActive(now));

        // Перманентный бан (IsBanned = true, BanUntilUtc пустой)
        acc.IsBanned = true;
        acc.BanUntilUtc = "";
        Assert.True(acc.IsBanActive(now));
        Assert.True(acc.IsBanActive(now.AddYears(5)));

        // Временный бан на 3 дня
        acc.BanUntilUtc = now.AddDays(3).ToString("O");
        Assert.True(acc.IsBanActive(now));
        Assert.True(acc.IsBanActive(now.AddDays(2)));
        Assert.False(acc.IsBanActive(now.AddDays(4))); // Истёк

        // Снятие бана
        acc.IsBanned = false;
        Assert.False(acc.IsBanActive(now));
    }

    [Fact]
    public void AdminBootstrapManager_GeneratesSetupToken_WhenEmpty()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"flovmp_admins_test_{Guid.NewGuid():N}.json");
        try
        {
            var mgr = new AdminBootstrapManager(tempFile);
            Assert.False(string.IsNullOrWhiteSpace(mgr.CurrentSetupToken));
            Assert.StartsWith("FLV-", mgr.CurrentSetupToken);
            // Безопасный дефолт: авто-захват прав первым игроком ВЫКЛЮЧЕН.
            // Первичная настройка только через токен из консоли сервера.
            Assert.False(mgr.CanAutoClaim);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void AdminBootstrapManager_LocalHost_ReturnsLevel8()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"flovmp_admins_test_{Guid.NewGuid():N}.json");
        try
        {
            var mgr = new AdminBootstrapManager(tempFile);
            // Безопасный дефолт: localhost НЕ даёт прав. За nginx/прокси IP
            // схлопывается в 127.0.0.1 — иначе Основателя получили бы все.
            Environment.SetEnvironmentVariable("FLOVMP_ALLOW_LOCAL_OWNER", null);
            Assert.Equal(0, mgr.GetAssignedRank(0, "RegularUser", "127.0.0.1"));
            Assert.Equal(0, mgr.GetAssignedRank(0, "RegularUser", "::1"));
            Assert.Equal(0, mgr.GetAssignedRank(0, "RegularUser", "localhost"));
            Assert.Equal(0, mgr.GetAssignedRank(0, "RegularUser", "192.168.1.100"));

            // Осознанное включение для локальной отладки — работает.
            try
            {
                Environment.SetEnvironmentVariable("FLOVMP_ALLOW_LOCAL_OWNER", "1");
                Assert.Equal(8, mgr.GetAssignedRank(0, "RegularUser", "127.0.0.1"));
                Assert.Equal(0, mgr.GetAssignedRank(0, "RegularUser", "192.168.1.100"));
            }
            finally
            {
                Environment.SetEnvironmentVariable("FLOVMP_ALLOW_LOCAL_OWNER", null);
            }
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void AdminBootstrapManager_TryClaimOwner_SuccessAndInvalidatesToken()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"flovmp_admins_test_{Guid.NewGuid():N}.json");
        try
        {
            var mgr = new AdminBootstrapManager(tempFile);
            var token = mgr.CurrentSetupToken;

            // Неверный токен отклоняется
            Assert.False(mgr.TryClaimOwner("INVALID-TOKEN", "OwnerPlayer", 12345678, out var errMsg));
            Assert.Contains("Неверный токен", errMsg);
            Assert.Equal(0, mgr.GetAssignedRank(12345678, "OwnerPlayer", "192.168.1.50"));

            // Верный токен одобряется
            Assert.True(mgr.TryClaimOwner(token, "OwnerPlayer", 12345678, out var okMsg));
            Assert.Contains("успешно подтверждено", okMsg);

            // Права установлены и токен инвалидирован
            Assert.Equal(8, mgr.GetAssignedRank(12345678, "OwnerPlayer", "192.168.1.50"));
            Assert.True(mgr.IsFounder(12345678, "OwnerPlayer"));
            Assert.Empty(mgr.CurrentSetupToken);
            Assert.False(mgr.CanAutoClaim);

            // Повторная попытка с тем же токеном отклоняется
            Assert.False(mgr.TryClaimOwner(token, "Attacker", 99999999, out _));
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void AdminBootstrapManager_SetAdmin_PersistsAndClampsLevel()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"flovmp_admins_test_{Guid.NewGuid():N}.json");
        try
        {
            var mgr = new AdminBootstrapManager(tempFile);
            // Привязка по SocialClubId — единственная безопасная (ник задаётся
            // клиентом и подделывается).
            mgr.SetAdmin("1001", 2);
            mgr.SetAdmin("1002", 6);
            mgr.SetAdmin("1003", 15); // Должно обрезаться до 8

            Assert.Equal(2, mgr.GetAssignedRank(1001, "ЛюбойНик", "10.0.0.1"));
            Assert.Equal(6, mgr.GetAssignedRank(1002, "ЛюбойНик", "10.0.0.1"));
            Assert.Equal(8, mgr.GetAssignedRank(1003, "ЛюбойНик", "10.0.0.1"));

            // Права по НИКУ не действуют по умолчанию: чужой ник прав не даёт.
            mgr.SetAdmin("ModeratorUser", 5);
            Assert.Equal(0, mgr.GetAssignedRank(0, "ModeratorUser", "10.0.0.1"));

            // Проверка снятия прав (уровень 0)
            mgr.SetAdmin("1001", 0);
            Assert.Equal(0, mgr.GetAssignedRank(1001, "ЛюбойНик", "10.0.0.1"));

            // Проверка перезагрузки с диска (привязка по SocialClubId переживает рестарт)
            var mgrReloaded = new AdminBootstrapManager(tempFile);
            Assert.Equal(6, mgrReloaded.GetAssignedRank(1002, "ЛюбойНик", "10.0.0.1"));
            Assert.Equal(0, mgrReloaded.GetAssignedRank(1001, "ЛюбойНик", "10.0.0.1"));
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}

