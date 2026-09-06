using FloVMP.Core.AntiCheat;
using FloVMP.Core.Housing;

namespace FloVMP.Gamemode.Presets;

/// <summary>
/// Пресеты недвижимости г. Москвы для RP-проекта «Держава Онлайн».
/// Содержит координаты и интерьеры квартир, особняков и гаражей на карте Москвы.
/// </summary>
public static class DerzhavaHousing
{
    public static void RegisterAll(HousingService service)
    {
        // 1. ЖК «Москва-Сити» (Апартаменты Башня Федерация)
        service.RegisterProperty(new Property(
            1,
            "Пресненская наб., 12, Башня Федерация, Ап. 42",
            PropertyType.Apartment,
            price: 25_000_000,
            entrance: new Vector3D(-268.3f, -956.8f, 31.2f),
            interior: new Vector3D(-786.8f, 315.7f, 217.6f),
            dimension: 1001));

        // 2. ЖК «Тверской Бульвар» (Квартира в центре)
        service.RegisterProperty(new Property(
            2,
            "Тверской бульвар, д. 15, кв. 8",
            PropertyType.Apartment,
            price: 8_500_000,
            entrance: new Vector3D(112.5f, -820.4f, 31.1f),
            interior: new Vector3D(340.9f, 437.1f, 149.3f),
            dimension: 1002));

        // 3. Посёлок «Барвиха Luxury» (Коттедж на Рублёвке)
        service.RegisterProperty(new Property(
            3,
            "Рублёво-Успенское ш., пос. Барвиха, Владение 7",
            PropertyType.Mansion,
            price: 85_000_000,
            entrance: new Vector3D(-1288.6f, 440.3f, 97.5f),
            interior: new Vector3D(1397.3f, 1141.2f, 114.3f),
            dimension: 1003));

        // 4. Гаражный кооператив «Москвич» (Гараж №24)
        service.RegisterProperty(new Property(
            4,
            "Южнопортовая ул., ГСК Москвич, Бокс 24",
            PropertyType.Garage,
            price: 1_200_000,
            entrance: new Vector3D(740.1f, -1002.5f, 22.8f),
            interior: new Vector3D(178.6f, -1005.8f, -98.9f),
            dimension: 1004));
    }
}
