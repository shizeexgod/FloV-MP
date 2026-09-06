using FloVMP.Core.Auth;
using FloVMP.Core.Economy;
using Xunit;

namespace FloVMP.Core.Tests;

public sealed class EconomyTests
{
    private readonly EconomyService _economy;
    private readonly List<TransactionRecord> _records = new();

    public EconomyTests()
    {
        _economy = new EconomyService(r => _records.Add(r));
    }

    [Fact]
    public void GiveCash_adds_money_and_logs_tx()
    {
        var acc = new Account { Id = 1, Username = "TestUser", Cash = 1000 };
        var ok = _economy.TryGiveCash(acc, 500, "Prize", out var err);

        Assert.True(ok);
        Assert.Empty(err);
        Assert.Equal(1500, acc.Cash);
        Assert.Single(_records);
        Assert.Equal(500, _records[0].Amount);
        Assert.Equal(TransactionType.AdminGrant, _records[0].Type);
    }

    [Fact]
    public void TakeCash_deducts_money_when_sufficient()
    {
        var acc = new Account { Id = 1, Username = "TestUser", Cash = 1000 };
        var ok = _economy.TryTakeCash(acc, 400, "Store", out var err);

        Assert.True(ok);
        Assert.Empty(err);
        Assert.Equal(600, acc.Cash);
    }

    [Fact]
    public void TakeCash_fails_when_insufficient()
    {
        var acc = new Account { Id = 1, Username = "TestUser", Cash = 300 };
        var ok = _economy.TryTakeCash(acc, 400, "Store", out var err);

        Assert.False(ok);
        Assert.Equal("Недостаточно наличных средств", err);
        Assert.Equal(300, acc.Cash);
    }

    [Fact]
    public void Deposit_moves_cash_to_bank()
    {
        var acc = new Account { Id = 1, Username = "TestUser", Cash = 1000, Bank = 5000 };
        var ok = _economy.TryDeposit(acc, 600, out var err);

        Assert.True(ok);
        Assert.Empty(err);
        Assert.Equal(400, acc.Cash);
        Assert.Equal(5600, acc.Bank);
    }

    [Fact]
    public void Withdraw_moves_bank_to_cash()
    {
        var acc = new Account { Id = 1, Username = "TestUser", Cash = 400, Bank = 5000 };
        var ok = _economy.TryWithdraw(acc, 1000, out var err);

        Assert.True(ok);
        Assert.Empty(err);
        Assert.Equal(1400, acc.Cash);
        Assert.Equal(4000, acc.Bank);
    }

    [Fact]
    public void PayCash_transfers_between_players()
    {
        var acc1 = new Account { Id = 1, Username = "Alice", Cash = 2000 };
        var acc2 = new Account { Id = 2, Username = "Bob", Cash = 500 };

        var ok = _economy.TryPayCash(acc1, acc2, 800, out var err);

        Assert.True(ok);
        Assert.Empty(err);
        Assert.Equal(1200, acc1.Cash);
        Assert.Equal(1300, acc2.Cash);
    }

    [Fact]
    public void TransferBank_transfers_between_accounts()
    {
        var acc1 = new Account { Id = 1, Username = "Alice", Bank = 10000 };
        var acc2 = new Account { Id = 2, Username = "Bob", Bank = 2000 };

        var ok = _economy.TryTransferBank(acc1, acc2, 3500, "Payment for car", out var err);

        Assert.True(ok);
        Assert.Empty(err);
        Assert.Equal(6500, acc1.Bank);
        Assert.Equal(5500, acc2.Bank);
    }

    [Fact]
    public async Task Concurrent_transfers_maintain_total_money_invariant()
    {
        var acc1 = new Account { Id = 1, Username = "Alice", Bank = 50_000 };
        var acc2 = new Account { Id = 2, Username = "Bob", Bank = 50_000 };
        const long initialTotal = 100_000;

        var tasks = new List<Task>();
        for (int i = 0; i < 50; i++)
        {
            tasks.Add(Task.Run(() => _economy.TryTransferBank(acc1, acc2, 200, "tx", out _)));
            tasks.Add(Task.Run(() => _economy.TryTransferBank(acc2, acc1, 150, "tx", out _)));
        }

        await Task.WhenAll(tasks);

        Assert.Equal(initialTotal, acc1.Bank + acc2.Bank);
    }
}