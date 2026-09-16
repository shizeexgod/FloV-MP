namespace FloVMP.Core.Admin;

/// <summary>Запись о правах администратора в хранилище.</summary>
public sealed record AdminRecord(string SocialClub, int Level, bool IsFounder);

/// <summary>
/// Хранилище прав администраторов.
///
/// Нужно для двух вещей. Первая — несколько инстансов на одной базе видят общих
/// администраторов. Вторая — сценарий установки «выдать себе права через базу»:
/// владелец правит таблицу admins, и сервер подхватывает это без перезапуска.
///
/// Права ключуются только по SocialClubId: ник в alt:V задаёт клиент, а
/// аккаунта в базовой платформе нет.
/// </summary>
public interface IAdminStore
{
    /// <summary>Все записи с уровнем больше нуля.</summary>
    IReadOnlyList<AdminRecord> LoadAll();

    /// <summary>Выдать или изменить права. Уровень 0 снимает права.</summary>
    void Upsert(string socialClub, int level, bool isFounder, string? grantedBy);
}
