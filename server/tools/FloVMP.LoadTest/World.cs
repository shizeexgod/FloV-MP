using FloVMP.Core.AntiCheat;

namespace FloVMP.LoadTest;

/// <summary>
/// Синтетический игрок стенда.
/// </summary>
public sealed class SimPlayer
{
    public ulong Id;
    public Vector3D Position;
    public Vector3D Velocity;
    public Vector3D Heading = new(1, 0, 0);
    public int Dimension;
    public bool InVehicle;
    public bool InCombat;
}

/// <summary>
/// Расселение синтетических игроков по карте.
///
/// Равномерная раскидка по всей карте — самый лёгкий и самый бесполезный
/// вариант: в ней у каждого игрока почти нет соседей, а именно число соседей
/// определяет стоимость стриминга, голоса и синхронизации. Реальный RP-сервер
/// наоборот кучкуется: банк, мэрия, автосалон, спавн. Поэтому по умолчанию
/// большая часть онлайна сидит в горячих точках — это и есть тот режим, в
/// котором сервер падает на проде.
/// </summary>
public static class World
{
    // Габариты игрового мира GTA V примерно 8000x8000 юнитов.
    private const float MapMin = -3800f;
    private const float MapMax = 4200f;

    /// <summary>Типовые точки скопления игроков на RP-сервере.</summary>
    private static readonly Vector3D[] Hotspots =
    {
        new(-1037f, -2738f, 20f),  // аэропорт
        new(215f, -810f, 31f),     // центр / банк
        new(-1108f, -2886f, 14f),  // ангар
        new(1276f, -1720f, 54f),   // спавн у пирса
        new(-347f, -134f, 39f),    // автосалон
        new(441f, -982f, 30f),     // полицейский участок
        new(-1339f, -1100f, 5f),   // пляж
        new(24f, -1347f, 29f),     // заправка
    };

    public static SimPlayer[] Create(int count, double hotspotShare, int seed)
    {
        var rng = new Random(seed);
        var players = new SimPlayer[count];

        for (var i = 0; i < count; i++)
        {
            var p = new SimPlayer { Id = (ulong)(i + 1) };

            if (rng.NextDouble() < hotspotShare)
            {
                // Кучкование: радиус 120 м вокруг точки интереса. Это даёт
                // десятки соседей в зоне стриминга — рабочая нагрузка.
                var h = Hotspots[rng.Next(Hotspots.Length)];
                var angle = rng.NextDouble() * Math.PI * 2;
                var r = Math.Sqrt(rng.NextDouble()) * 120.0;
                p.Position = new Vector3D(
                    h.X + (float)(Math.Cos(angle) * r),
                    h.Y + (float)(Math.Sin(angle) * r),
                    h.Z);
            }
            else
            {
                p.Position = new Vector3D(
                    (float)(MapMin + rng.NextDouble() * (MapMax - MapMin)),
                    (float)(MapMin + rng.NextDouble() * (MapMax - MapMin)),
                    (float)(rng.NextDouble() * 80.0));
            }

            // Треть игроков за рулём — они двигаются быстро и чаще
            // перескакивают между ячейками сетки, что дороже для стриминга.
            p.InVehicle = rng.NextDouble() < 0.33;
            var speed = p.InVehicle ? 8f + (float)rng.NextDouble() * 22f : (float)rng.NextDouble() * 2.5f;
            var dir = rng.NextDouble() * Math.PI * 2;
            p.Velocity = new Vector3D((float)(Math.Cos(dir) * speed), (float)(Math.Sin(dir) * speed), 0f);
            p.Heading = Normalize(p.Velocity);
            p.InCombat = rng.NextDouble() < 0.04;

            // Интерьеры/квартиры — отдельные измерения. Игроки в своём
            // измерении не видят друг друга, и это должно удешевлять выборку.
            p.Dimension = rng.NextDouble() < 0.08 ? rng.Next(1, 40) : 0;

            players[i] = p;
        }

        return players;
    }

    /// <summary>Шаг симуляции движения: позиция += скорость * dt.</summary>
    public static void Step(SimPlayer[] players, float dt, Random rng)
    {
        foreach (var p in players)
        {
            p.Position = new Vector3D(
                Clamp(p.Position.X + p.Velocity.X * dt),
                Clamp(p.Position.Y + p.Velocity.Y * dt),
                p.Position.Z);

            // Редкая смена направления — иначе все разъедутся по краям карты
            // и кучкование, ради которого стенд и затевался, исчезнет.
            if (rng.NextDouble() < 0.02)
            {
                var speed = MathF.Sqrt(p.Velocity.X * p.Velocity.X + p.Velocity.Y * p.Velocity.Y);
                var dir = rng.NextDouble() * Math.PI * 2;
                p.Velocity = new Vector3D((float)(Math.Cos(dir) * speed), (float)(Math.Sin(dir) * speed), 0f);
                p.Heading = Normalize(p.Velocity);
            }
        }
    }

    private static float Clamp(float v) => v < MapMin ? MapMin : (v > MapMax ? MapMax : v);

    private static Vector3D Normalize(Vector3D v)
    {
        var len = MathF.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);
        return len < 0.0001f ? new Vector3D(1, 0, 0) : new Vector3D(v.X / len, v.Y / len, v.Z / len);
    }
}
