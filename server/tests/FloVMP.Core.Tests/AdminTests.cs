using FloVMP.Core.Admin;
using FloVMP.Core.Auth;
using Xunit;

namespace FloVMP.Core.Tests;

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
    public void Helper_Level1_CanUseHelperCommands_CannotUseModeratorCommands()
    {
        Assert.True(AdminCommandRegistry.CanExecute(1, "a"));
        Assert.True(AdminCommandRegistry.CanExecute(1, "stats"));
        Assert.True(AdminCommandRegistry.CanExecute(1, "freeze"));
        Assert.True(AdminCommandRegistry.CanExecute(1, "unfreeze"));

        Assert.False(AdminCommandRegistry.CanExecute(1, "kick"));
        Assert.False(AdminCommandRegistry.CanExecute(1, "ban"));
        Assert.False(AdminCommandRegistry.CanExecute(1, "veh"));
    }

    [Fact]
    public void Moderator_Level2_CanKickAndMute_CannotBan()
    {
        Assert.True(AdminCommandRegistry.CanExecute(2, "kick"));
        Assert.True(AdminCommandRegistry.CanExecute(2, "mute"));
        Assert.True(AdminCommandRegistry.CanExecute(2, "goto"));
        Assert.True(AdminCommandRegistry.CanExecute(2, "gethere"));
        Assert.True(AdminCommandRegistry.CanExecute(2, "revive"));
        Assert.True(AdminCommandRegistry.CanExecute(2, "heal"));
        Assert.True(AdminCommandRegistry.CanExecute(2, "armor"));

        Assert.False(AdminCommandRegistry.CanExecute(2, "ban"));
        Assert.False(AdminCommandRegistry.CanExecute(2, "veh"));
        Assert.False(AdminCommandRegistry.CanExecute(2, "god"));
    }

    [Fact]
    public void SeniorMod_Level3_CanBan_CannotSpawnVehicles()
    {
        Assert.True(AdminCommandRegistry.CanExecute(3, "ban"));
        Assert.True(AdminCommandRegistry.CanExecute(3, "warn"));
        Assert.True(AdminCommandRegistry.CanExecute(3, "slap"));

        Assert.False(AdminCommandRegistry.CanExecute(3, "veh"));
        Assert.False(AdminCommandRegistry.CanExecute(3, "tp"));
    }

    [Fact]
    public void Admin_Level4_CanSpawnVehiclesAndSetHp()
    {
        Assert.True(AdminCommandRegistry.CanExecute(4, "veh"));
        Assert.True(AdminCommandRegistry.CanExecute(4, "car"));
        Assert.True(AdminCommandRegistry.CanExecute(4, "dv"));
        Assert.True(AdminCommandRegistry.CanExecute(4, "sethp"));
        Assert.True(AdminCommandRegistry.CanExecute(4, "repair"));
        Assert.True(AdminCommandRegistry.CanExecute(4, "fix"));
        Assert.True(AdminCommandRegistry.CanExecute(4, "god"));

        Assert.False(AdminCommandRegistry.CanExecute(4, "tp"));
        Assert.False(AdminCommandRegistry.CanExecute(4, "makeadmin"));
    }

    [Fact]
    public void SeniorAdmin_Level5_CanTeleportAndSetWeather()
    {
        Assert.True(AdminCommandRegistry.CanExecute(5, "tp"));
        Assert.True(AdminCommandRegistry.CanExecute(5, "tpm"));
        Assert.True(AdminCommandRegistry.CanExecute(5, "setweather"));
        Assert.True(AdminCommandRegistry.CanExecute(5, "settime"));

        Assert.False(AdminCommandRegistry.CanExecute(5, "makeadmin"));
    }

    [Fact]
    public void MainAdmin_Level7_CanMakeAdmin_CannotFullOverride()
    {
        Assert.True(AdminCommandRegistry.CanExecute(7, "makeadmin"));
        Assert.True(AdminCommandRegistry.CanExecute(7, "banip"));

        Assert.False(AdminCommandRegistry.CanExecute(7, "setadminlevel"));
    }

    [Fact]
    public void Owner_Level8_HasFullAccess()
    {
        Assert.True(AdminCommandRegistry.CanExecute(8, "setadminlevel"));
        Assert.True(AdminCommandRegistry.CanExecute(8, "srvrestart"));
        Assert.True(AdminCommandRegistry.CanExecute(8, "ban"));
        Assert.True(AdminCommandRegistry.CanExecute(8, "veh"));
        Assert.True(AdminCommandRegistry.CanExecute(8, "tpm"));
    }

    [Fact]
    public void NewAdminCommands_LevelsAreEnforced()
    {
        // Level 1: noclip, esp
        Assert.True(AdminCommandRegistry.CanExecute(1, "noclip"));
        Assert.True(AdminCommandRegistry.CanExecute(1, "esp"));
        Assert.False(AdminCommandRegistry.CanExecute(0, "noclip"));
        Assert.False(AdminCommandRegistry.CanExecute(0, "esp"));

        // Level 4: speed
        Assert.True(AdminCommandRegistry.CanExecute(4, "speed"));
        Assert.False(AdminCommandRegistry.CanExecute(3, "speed"));

        // Level 5: weather, time, skin
        Assert.True(AdminCommandRegistry.CanExecute(5, "weather"));
        Assert.True(AdminCommandRegistry.CanExecute(5, "time"));
        Assert.True(AdminCommandRegistry.CanExecute(5, "skin"));
        Assert.False(AdminCommandRegistry.CanExecute(4, "weather"));
        Assert.False(AdminCommandRegistry.CanExecute(4, "time"));
        Assert.False(AdminCommandRegistry.CanExecute(4, "skin"));

        // Level 7: promote
        Assert.True(AdminCommandRegistry.CanExecute(7, "promote"));
        Assert.False(AdminCommandRegistry.CanExecute(6, "promote"));

        // Level 8: setadmin
        Assert.True(AdminCommandRegistry.CanExecute(8, "setadmin"));
        Assert.False(AdminCommandRegistry.CanExecute(7, "setadmin"));
    }

    [Fact]
    public void Account_MuteChecks_WorkProperly()
    {
        var acc = new Account { Username = "TestUser" };
        var now = DateTime.UtcNow;

        Assert.False(acc.IsMuted(now));

        acc.MuteUntilUtc = now.AddMinutes(15).ToString("O");
        Assert.True(acc.IsMuted(now));
        Assert.False(acc.IsMuted(now.AddMinutes(16)));
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
    public void Account_JailChecks_WorkProperly()
    {
        var acc = new Account { Username = "JailedUser" };
        var now = DateTime.UtcNow;

        Assert.False(acc.IsJailed(now));

        acc.JailUntilUtc = now.AddMinutes(30).ToString("O");
        Assert.True(acc.IsJailed(now));
        Assert.True(acc.IsJailed(now.AddMinutes(15)));
        Assert.False(acc.IsJailed(now.AddMinutes(35)));
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

