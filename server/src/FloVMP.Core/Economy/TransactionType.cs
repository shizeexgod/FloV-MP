namespace FloVMP.Core.Economy;

/// <summary>
/// Тип экономической транзакции в «Держава Онлайн».
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