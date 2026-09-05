using AltV.Net.Data;

namespace FloVMP.Gamemode;

/// <summary>
/// Точки спавна. Для Фазы 1 нужна одна общая площадка, где два игрока
/// гарантированно видят друг друга и могут разойтись пешком.
/// Legion Square (центр Los Santos) — ровная площадь, удобно проверять
/// синхронизацию перемещения и анимаций.
/// </summary>
public static class SpawnPoints
{
    /// <summary>Москва: Красная площадь / Кремль (центр карты RMRP).</summary>
    public static readonly Position MoscowRedSquare = new(-262.0f, -955.0f, 31.5f);

    /// <summary>Москва: Деловой центр Москва-Сити.</summary>
    public static readonly Position MoscowCity = new(-1150.0f, -440.0f, 45.0f);

    /// <summary>Москва: Главное управление МВД / Полиция.</summary>
    public static readonly Position MoscowPolice = new(440.0f, -982.0f, 30.7f);

    /// <summary>Москва: Городская больница / Склиф.</summary>
    public static readonly Position MoscowHospital = new(310.0f, -590.0f, 43.3f);

    /// <summary>Основная точка спавна проекта Держава Онлайн.</summary>
    public static readonly Position DefaultSpawn = MoscowRedSquare;

    /// <summary>Обратная совместимость (Legion Square = Красная площадь в URF RMRP).</summary>
    public static readonly Position LegionSquare = MoscowRedSquare;

    /// <summary>
    /// Небольшой разброс вокруг точки, чтобы игроки не спавнились строго
    /// в одну координату друг в друге. index — порядковый номер игрока.
    /// </summary>
    public static Position Scattered(Position origin, int index)
    {
        // шаг 1.5 м по кругу из 8 позиций
        var angle = index % 8 * (System.MathF.PI / 4f);
        return new Position(
            origin.X + System.MathF.Cos(angle) * 1.5f,
            origin.Y + System.MathF.Sin(angle) * 1.5f,
            origin.Z);
    }
}
