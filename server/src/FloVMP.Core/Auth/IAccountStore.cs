namespace FloVMP.Core.Auth;

/// <summary>
/// Хранилище учётных записей: JSON-файл (<see cref="JsonAccountStore"/>)
/// или MariaDB (<see cref="FloVMP.Core.Database.MySqlAccountStore"/>).
/// Реализация обязана быть потокобезопасной: события alt:V приходят с
/// нескольких потоков синхронизации.
/// </summary>
public interface IAccountStore
{
    Account? FindByUsername(string username);
    Account? FindByBankAccount(string bankAccountNumber);
    bool Exists(string username);

    /// <summary>Создаёт учётку. Бросает, если имя занято.</summary>
    Account Create(string username, string passwordHash);

    void Update(Account account);
}
