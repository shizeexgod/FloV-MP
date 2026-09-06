namespace FloVMP.Core.Economy;

/// <summary>
/// Запись о банковской или наличной транзакции для аудита и истории.
/// </summary>
public record TransactionRecord(
    long Id,
    int? SenderAccountId,
    int? ReceiverAccountId,
    long Amount,
    TransactionType Type,
    string Description,
    DateTime TimestampUtc
);