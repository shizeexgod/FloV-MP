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
    /// <summary>Legion Square, тротуар у фонтана.</summary>
    public static readonly Position LegionSquare = new(-262.0f, -955.0f, 31.22f);

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
