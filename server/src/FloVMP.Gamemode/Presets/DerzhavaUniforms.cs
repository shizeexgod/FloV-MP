using System;
using FloVMP.Core.Characters;

namespace FloVMP.Gamemode.Presets;

/// <summary>
/// Пресеты служебной формы государственных ведомств г. Москвы для RP-проекта «Держава Онлайн».
/// </summary>
public static class DerzhavaUniforms
{
    public static void RegisterAll(FactionUniformService? service = null)
    {
        var target = service ?? FactionUniformService.Default;

        // 1. Мэрия Москвы (Деловой костюм)
        target.RegisterUniform(1, (appearance, rankLevel) =>
        {
            bool isMale = appearance.Gender == 0;
            if (isMale)
            {
                appearance.SetCloth(11, 4, 0);  // Пиджак
                appearance.SetCloth(3, 4, 0);   // Торс
                appearance.SetCloth(8, 10, 0);  // Рубашка с галстуком
                appearance.SetCloth(4, 10, 0);  // Брюки классика
                appearance.SetCloth(6, 10, 0);  // Туфли
            }
            else
            {
                appearance.SetCloth(11, 7, 0);
                appearance.SetCloth(3, 3, 0);
                appearance.SetCloth(8, 6, 0);
                appearance.SetCloth(4, 6, 0);
                appearance.SetCloth(6, 6, 0);
            }
        });

        // 2. ГУ МВД по г. Москве (Полиция)
        target.RegisterUniform(2, (appearance, rankLevel) =>
        {
            bool isMale = appearance.Gender == 0;
            if (rankLevel >= 6) // Старший комсостав (Майор+)
            {
                if (isMale)
                {
                    appearance.SetCloth(11, 26, 0); // Парадный китель
                    appearance.SetCloth(3, 0, 0);
                    appearance.SetCloth(8, 10, 0);
                    appearance.SetCloth(4, 25, 0);
                    appearance.SetCloth(6, 21, 0);
                    appearance.SetProp(0, 46, 0);   // Фуражка
                }
                else
                {
                    appearance.SetCloth(11, 27, 0);
                    appearance.SetCloth(3, 14, 0);
                    appearance.SetCloth(8, 35, 0);
                    appearance.SetCloth(4, 37, 0);
                    appearance.SetCloth(6, 13, 0);
                    appearance.SetProp(0, 45, 0);
                }
            }
            else // Рядовой, сержант, лейтенант (Патрульная служба ППСП)
            {
                if (isMale)
                {
                    appearance.SetCloth(11, 55, 0); // Полицейская куртка/рубашка
                    appearance.SetCloth(3, 0, 0);
                    appearance.SetCloth(8, 58, 0);  // Тактический жилет
                    appearance.SetCloth(9, 10, 0);  // Бронежилет МВД
                    appearance.SetCloth(4, 35, 0);  // Тактические штаны
                    appearance.SetCloth(6, 25, 0);  // Берцы
                    appearance.SetProp(0, 46, 0);   // Фуражка/кепка
                }
                else
                {
                    appearance.SetCloth(11, 48, 0);
                    appearance.SetCloth(3, 14, 0);
                    appearance.SetCloth(8, 35, 0);
                    appearance.SetCloth(9, 9, 0);
                    appearance.SetCloth(4, 34, 0);
                    appearance.SetCloth(6, 25, 0);
                    appearance.SetProp(0, 45, 0);
                }
            }
        });

        // 3. УФСБ России
        target.RegisterUniform(3, (appearance, rankLevel) =>
        {
            bool isMale = appearance.Gender == 0;
            if (isMale)
            {
                appearance.SetCloth(11, 53, 0); // Спецназ ФСБ (Тактический чёрный)
                appearance.SetCloth(3, 1, 0);
                appearance.SetCloth(8, 15, 0);
                appearance.SetCloth(9, 12, 1);  // Тяжёлый бронежилет ФСБ
                appearance.SetCloth(4, 31, 0);
                appearance.SetCloth(6, 25, 0);
                appearance.SetCloth(1, 52, 0);  // Тактическая балаклава
                appearance.SetProp(0, 39, 0);   // Шлем
            }
            else
            {
                appearance.SetCloth(11, 46, 0);
                appearance.SetCloth(3, 3, 0);
                appearance.SetCloth(8, 14, 0);
                appearance.SetCloth(9, 11, 1);
                appearance.SetCloth(4, 30, 0);
                appearance.SetCloth(6, 25, 0);
                appearance.SetCloth(1, 57, 0);
                appearance.SetProp(0, 38, 0);
            }
        });

        // 4. ГКБ им. Боткина (Медицинская служба)
        target.RegisterUniform(4, (appearance, rankLevel) =>
        {
            bool isMale = appearance.Gender == 0;
            if (isMale)
            {
                appearance.SetCloth(11, 249, 0); // Медицинский халат / форма скорой
                appearance.SetCloth(3, 85, 0);
                appearance.SetCloth(8, 15, 0);
                appearance.SetCloth(4, 96, 0);
                appearance.SetCloth(6, 51, 0);
                appearance.ClearProp(0);
            }
            else
            {
                appearance.SetCloth(11, 257, 0);
                appearance.SetCloth(3, 101, 0);
                appearance.SetCloth(8, 14, 0);
                appearance.SetCloth(4, 99, 0);
                appearance.SetCloth(6, 52, 0);
                appearance.ClearProp(0);
            }
        });

        // 5. Воинская часть ВС РФ (Армия)
        target.RegisterUniform(5, (appearance, rankLevel) =>
        {
            bool isMale = appearance.Gender == 0;
            if (isMale)
            {
                appearance.SetCloth(11, 221, 0); // Камуфляж ВС РФ
                appearance.SetCloth(3, 17, 0);
                appearance.SetCloth(8, 15, 0);
                appearance.SetCloth(9, 16, 0);   // Армейский бронежилет Ратник
                appearance.SetCloth(4, 87, 0);
                appearance.SetCloth(6, 62, 0);
                appearance.SetProp(0, 107, 0);   // Армейская каска
            }
            else
            {
                appearance.SetCloth(11, 231, 0);
                appearance.SetCloth(3, 18, 0);
                appearance.SetCloth(8, 14, 0);
                appearance.SetCloth(9, 15, 0);
                appearance.SetCloth(4, 90, 0);
                appearance.SetCloth(6, 65, 0);
                appearance.SetProp(0, 106, 0);
            }
        });
    }
}
