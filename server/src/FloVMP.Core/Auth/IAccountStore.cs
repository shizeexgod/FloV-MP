namespace FloVMP.Core.Auth;

/// <summary>
/// Хранилище учёток. Скелет — JSON-файл (<see cref="JsonAccountStore"/>);
/// позже — БД (та же схема, отдельная от Florida V — см. CLAUDE.md).
/// Реализация обязана быть потокобезопасной: события alt:V приходят с
/// нескольких потоков синхронизации.
/// </summary>
public interface IAccountStore
{
    Account? FindByUsername(string username);
    bool Exists(string username);

    /// <summary>Создаёт учётку. Бросает, если имя занято.</summary>
    Account Create(string username, string passwordHash);

    void Update(Account account);
}
