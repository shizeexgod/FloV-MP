namespace FloVMP.Core.Security;

/// <summary>
/// Хранилище блокировок.
///
/// До его появления MultiTierBanService держал баны только в памяти: любой
/// рестарт сервера снимал все блокировки разом, а при нескольких инстансах
/// забаненный на одном спокойно заходил на другой. Для платформы, где бан по
/// HWID/MAC/IP заявлен как штатная возможность, это дыра, а не мелочь.
/// </summary>
public interface IBanStore
{
    /// <summary>Все записи, включая снятые — снятые нужны для истории наказаний.</summary>
    IReadOnlyList<BanRecord> LoadAll();

    /// <summary>Создать или обновить запись (снятие бана — тоже обновление).</summary>
    void Upsert(BanRecord record);

    /// <summary>Сохранить всё накопившееся немедленно.</summary>
    void Flush();
}
