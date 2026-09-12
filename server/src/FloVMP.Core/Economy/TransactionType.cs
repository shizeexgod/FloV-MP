namespace FloVMP.Core.Economy;

/// <summary>
/// Тип экономической транзакции в FloV:MP.
/// </summary>
public enum TransactionType
{
    Deposit,
    Withdraw,
    Transfer,
    PayCash,
    Salary,
    AdminGrant,
    AdminDeduct,
    Purchase,
    Fine
}