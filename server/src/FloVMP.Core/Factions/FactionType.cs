namespace FloVMP.Core.Factions;

/// <summary>
/// Тип государственной, криминальной или общественной организации в «Держава Онлайн».
/// </summary>
public enum FactionType
{
    None = 0,

    /// <summary>Правительство / Мэрия Москвы</summary>
    Government = 1,

    /// <summary>Министерство Внутренних Дел (Полиция Москвы / ГИБДД)</summary>
    Police = 2,

    /// <summary>Федеральная Служба Безопасности (ФСБ РФ)</summary>
    SecurityService = 3,

    /// <summary>Вооружённые Силы РФ (Армия / Воинская часть)</summary>
    Army = 4,

    /// <summary>Городская клиническая больница (Скорая помощь / ЕМС)</summary>
    Hospital = 5,

    /// <summary>Средства массовой информации (Телеканал Москва 24 / Пресса)</summary>
    News = 6,

    /// <summary>Организованная Преступная Группировка (ОПГ / Синдикат)</summary>
    Mafia = 7,

    /// <summary>Уличная банда / Группировка</summary>
    Gang = 8
}
