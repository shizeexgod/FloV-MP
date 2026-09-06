using FloVMP.Core.Auth;
using FloVMP.Core.Logging;

namespace FloVMP.Core.Economy;

/// <summary>
/// Сервис финансовых операций (наличные, банк, переводы) в «Держава Онлайн».
/// Гарантирует атомарность операций и защиту от отрицательных балансов и race-conditions.
/// </summary>
public sealed class EconomyService
{
    private readonly object _txLock = new();
    private readonly Action<TransactionRecord>? _onTransaction;
    private long _nextTxId = 1;

    public EconomyService(Action<TransactionRecord>? onTransaction = null)
    {
        _onTransaction = onTransaction;
    }

    /// <summary>
    /// Выдать наличные игроку.
    /// </summary>
    public bool TryGiveCash(Account account, long amount, string reason, out string error)
    {
        if (amount <= 0)
        {
            error = "Сумма должна быть больше нуля";
            return false;
        }

        lock (_txLock)
        {
            account.Cash += amount;
            LogTx(null, account.Id, amount, TransactionType.AdminGrant, reason);
            error = string.Empty;
            return true;
        }
    }

    /// <summary>
    /// Списать наличные у игрока.
    /// </summary>
    public bool TryTakeCash(Account account, long amount, string reason, out string error)
    {
        if (amount <= 0)
        {
            error = "Сумма должна быть больше нуля";
            return false;
        }

        lock (_txLock)
        {
            if (account.Cash < amount)
            {
                error = "Недостаточно наличных средств";
                return false;
            }

            account.Cash -= amount;
            LogTx(account.Id, null, amount, TransactionType.Purchase, reason);
            error = string.Empty;
            return true;
        }
    }

    /// <summary>
    /// Внести наличные на банковский счёт (депозит через банкомат/банк).
    /// </summary>
    public bool TryDeposit(Account account, long amount, out string error)
    {
        if (amount <= 0)
        {
            error = "Сумма депозита должна быть положительной";
            return false;
        }

        lock (_txLock)
        {
            if (account.Cash < amount)
            {
                error = "Недостаточно наличных для внесения на счёт";
                return false;
            }

            account.Cash -= amount;
            account.Bank += amount;
            LogTx(account.Id, account.Id, amount, TransactionType.Deposit, "Внесение наличных на банковский счёт");
            error = string.Empty;
            return true;
        }
    }

    /// <summary>
    /// Снять наличные с банковского счёта (банкомат/банк).
    /// </summary>
    public bool TryWithdraw(Account account, long amount, out string error)
    {
        if (amount <= 0)
        {
            error = "Сумма снятия должна быть положительной";
            return false;
        }

        lock (_txLock)
        {
            if (account.Bank < amount)
            {
                error = "Недостаточно средств на банковском счёте";
                return false;
            }

            account.Bank -= amount;
            account.Cash += amount;
            LogTx(account.Id, account.Id, amount, TransactionType.Withdraw, "Снятие наличных с банковского счёта");
            error = string.Empty;
            return true;
        }
    }

    /// <summary>
    /// Передать наличные из рук в руки (команда /pay).
    /// </summary>
    public bool TryPayCash(Account from, Account to, long amount, out string error)
    {
        if (from == to || from.Id == to.Id)
        {
            error = "Нельзя передать деньги самому себе";
            return false;
        }

        if (amount <= 0)
        {
            error = "Сумма передачи должна быть положительной";
            return false;
        }

        lock (_txLock)
        {
            if (from.Cash < amount)
            {
                error = "У вас недостаточно наличных средств";
                return false;
            }

            from.Cash -= amount;
            to.Cash += amount;
            LogTx(from.Id, to.Id, amount, TransactionType.PayCash, $"Передача наличных от {from.Username} к {to.Username}");
            error = string.Empty;
            return true;
        }
    }

    /// <summary>
    /// Безналичный банковский перевод между счетами (приложение банка/банкомат).
    /// </summary>
    public bool TryTransferBank(Account from, Account to, long amount, string description, out string error)
    {
        if (from == to || from.Id == to.Id)
        {
            error = "Нельзя выполнить перевод на собственный счёт";
            return false;
        }

        if (amount <= 0)
        {
            error = "Сумма перевода должна быть положительной";
            return false;
        }

        lock (_txLock)
        {
            if (from.Bank < amount)
            {
                error = "Недостаточно средств на банковском счёте для перевода";
                return false;
            }

            from.Bank -= amount;
            to.Bank += amount;
            var desc = string.IsNullOrWhiteSpace(description) ? "Банковский перевод" : description;
            LogTx(from.Id, to.Id, amount, TransactionType.Transfer, $"{desc} (от {from.Username} к {to.Username})");
            error = string.Empty;
            return true;
        }
    }

    private void LogTx(int? sender, int? receiver, long amount, TransactionType type, string description)
    {
        var record = new TransactionRecord(
            Id: _nextTxId++,
            SenderAccountId: sender,
            ReceiverAccountId: receiver,
            Amount: amount,
            Type: type,
            Description: description,
            TimestampUtc: DateTime.UtcNow
        );

        GameLog.System("economy_tx",
            ("type", type.ToString()),
            ("amount", amount),
            ("sender", sender ?? -1),
            ("receiver", receiver ?? -1),
            ("desc", description));

        _onTransaction?.Invoke(record);
    }
}