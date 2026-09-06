namespace FloVMP.Core.Housing;

/// <summary>
/// Тип объекта недвижимости в «Держава Онлайн».
/// </summary>
public enum PropertyType
{
    None = 0,

    /// <summary>Квартира в жилом комплексе</summary>
    Apartment = 1,

    /// <summary>Частный дом в черте города или области</summary>
    House = 2,

    /// <summary>Элитный коттедж / особняк на Рублёвке</summary>
    Mansion = 3,

    /// <summary>Гаражный бокс для хранения авто и тюнинга</summary>
    Garage = 4
}
