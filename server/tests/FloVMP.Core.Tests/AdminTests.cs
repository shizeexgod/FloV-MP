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
}
