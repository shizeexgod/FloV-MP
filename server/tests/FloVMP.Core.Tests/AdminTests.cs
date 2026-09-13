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

        Assert.False(AdminCommandRegistry.CanExecute(2, "ban"));
        Assert.False(AdminCommandRegistry.CanExecute(2, "veh"));
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
        Assert.True(AdminCommandRegistry.CanExecute(4, "dv"));
        Assert.True(AdminCommandRegistry.CanExecute(4, "sethp"));
        Assert.True(AdminCommandRegistry.CanExecute(4, "repair"));

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
    public void AdminBootstrapManager_GeneratesSetupToken_WhenEmpty()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"flovmp_admins_test_{Guid.NewGuid():N}.json");
        try
        {
            var mgr = new AdminBootstrapManager(tempFile);
            Assert.False(string.IsNullOrWhiteSpace(mgr.CurrentSetupToken));
            Assert.StartsWith("FLV-", mgr.CurrentSetupToken);
            Assert.True(mgr.CanAutoClaim);
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
            Assert.Equal(8, mgr.GetAssignedRank(0, "RegularUser", "127.0.0.1"));
            Assert.Equal(8, mgr.GetAssignedRank(0, "RegularUser", "::1"));
            Assert.Equal(8, mgr.GetAssignedRank(0, "RegularUser", "localhost"));
            Assert.Equal(0, mgr.GetAssignedRank(0, "RegularUser", "192.168.1.100"));
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
            mgr.SetAdmin("ModeratorUser", 2);
            mgr.SetAdmin("CuratorUser", 6);
            mgr.SetAdmin("OverClamped", 15); // Должно обрезаться до 8

            Assert.Equal(2, mgr.GetAssignedRank(0, "ModeratorUser", "10.0.0.1"));
            Assert.Equal(6, mgr.GetAssignedRank(0, "CuratorUser", "10.0.0.1"));
            Assert.Equal(8, mgr.GetAssignedRank(0, "OverClamped", "10.0.0.1"));

            // Проверка снятия прав (уровень 0)
            mgr.SetAdmin("ModeratorUser", 0);
            Assert.Equal(0, mgr.GetAssignedRank(0, "ModeratorUser", "10.0.0.1"));

            // Проверка перезагрузки с диска
            var mgrReloaded = new AdminBootstrapManager(tempFile);
            Assert.Equal(6, mgrReloaded.GetAssignedRank(0, "CuratorUser", "10.0.0.1"));
            Assert.Equal(0, mgrReloaded.GetAssignedRank(0, "ModeratorUser", "10.0.0.1"));
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}

