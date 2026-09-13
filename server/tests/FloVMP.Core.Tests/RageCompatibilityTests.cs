using System;
using FloVMP.Core.Bridge;
using Xunit;

namespace FloVMP.Core.Tests;

public class RageCompatibilityTests
{
    private class SampleRedAgeCommands
    {
        public bool CarSpawned { get; private set; }
        public string LastModel { get; private set; } = string.Empty;

        [Command("car", MinAdminLevel = 1)]
        public string CMD_Car(RageCallContext ctx, string model = "adder")
        {
            CarSpawned = true;
            LastModel = model;
            return $"Spawned {model} for {ctx.PlayerName}";
        }

        [Command("me")]
        public string CMD_Me(RageCallContext ctx, string action)
        {
            return $"{ctx.PlayerName} {action}";
        }

        [RemoteEvent("client:buyVehicle")]
        public bool OnBuyVehicle(RageCallContext ctx, string model, int price)
        {
            return price > 0 && !string.IsNullOrEmpty(model);
        }
    }

    [Fact]
    public void RegisterHandlers_RegistersCommandsAndRemoteEvents()
    {
        var sample = new SampleRedAgeCommands();
        var dispatcher = new RageCommandDispatcher();
        int count = dispatcher.RegisterHandlers(sample);

        Assert.Equal(3, count);
        Assert.True(dispatcher.HasCommand("car"));
        Assert.True(dispatcher.HasCommand("me"));
        Assert.True(dispatcher.HasRemoteEvent("client:buyVehicle"));
    }

    [Fact]
    public void ExecuteCommand_AdminCheck_BlocksRegularPlayer()
    {
        var sample = new SampleRedAgeCommands();
        var dispatcher = new RageCommandDispatcher();
        dispatcher.RegisterHandlers(sample);

        var playerContext = new RageCallContext(1, "Test_Player", AdminLevel: 0, 0, 0, 0, 0);
        bool executed = dispatcher.ExecuteCommand(playerContext, "/car sultanrs", out string feedback);

        Assert.True(executed);
        Assert.Contains("Доступ запрещен", feedback);
        Assert.False(sample.CarSpawned);
    }

    [Fact]
    public void ExecuteCommand_AdminCheck_ExecutesForAdmin()
    {
        var sample = new SampleRedAgeCommands();
        var dispatcher = new RageCommandDispatcher();
        dispatcher.RegisterHandlers(sample);

        var adminContext = new RageCallContext(1, "Admin_User", AdminLevel: 2, 0, 0, 0, 0);
        bool executed = dispatcher.ExecuteCommand(adminContext, "/car sultanrs", out string feedback);

        Assert.True(executed);
        Assert.Equal("Spawned sultanrs for Admin_User", feedback);
        Assert.True(sample.CarSpawned);
        Assert.Equal("sultanrs", sample.LastModel);
    }

    [Fact]
    public void TriggerRemoteEvent_ExecutesCorrectly()
    {
        var sample = new SampleRedAgeCommands();
        var dispatcher = new RageCommandDispatcher();
        dispatcher.RegisterHandlers(sample);

        var ctx = new RageCallContext(1, "Test_User", AdminLevel: 0, 0, 0, 0, 0);
        bool success = dispatcher.TriggerRemoteEvent(ctx, "client:buyVehicle", new object[] { "adder", 1500000 }, out var result);

        Assert.True(success);
        Assert.Equal(true, result);
    }

    [Fact]
    public void RedAgeCharacterAdapter_ToFlovmpAccount_MapsCorrectly()
    {
        var adapter = new RedAgeCharacterAdapter
        {
            Uuid = 142,
            FirstName = "Ivan",
            LastName = "Petrov",
            AdminLevel = 8,
            Money = 50000,
            Bank = 1200000
        };

        var account = adapter.ToFlovmpAccount();

        Assert.Equal(142, account.Id);
        Assert.Equal("Ivan_Petrov", account.Username);
        Assert.Equal(8, account.AdminLevel);
        Assert.Equal(50000, account.Cash);
        Assert.Equal(1200000, account.Bank);
    }

    [Fact]
    public void RedAgeCharacterAdapter_ParsePosition_HandlesJsonAndCsv()
    {
        var jsonPos = RedAgeCharacterAdapter.ParsePosition("{\"x\": 150.5, \"y\": -850.2, \"z\": 42.1}");
        Assert.Equal(150.5f, jsonPos.x, 1);
        Assert.Equal(-850.2f, jsonPos.y, 1);
        Assert.Equal(42.1f, jsonPos.z, 1);

        var csvPos = RedAgeCharacterAdapter.ParsePosition("100.2, 200.5, 30.0");
        Assert.Equal(100.2f, csvPos.x, 1);
        Assert.Equal(200.5f, csvPos.y, 1);
        Assert.Equal(30.0f, csvPos.z, 1);
    }
}
