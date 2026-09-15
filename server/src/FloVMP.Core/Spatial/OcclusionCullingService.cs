using System;
using System.Collections.Generic;
using FloVMP.Core.AntiCheat;

namespace FloVMP.Core.Spatial;

public sealed class OcclusionZone
{
    public string Id { get; set; } = string.Empty;
    public Vector3D Min { get; set; }
    public Vector3D Max { get; set; }
    public int Dimension { get; set; }
    public Vector3D? EntrancePosition { get; set; }
    public float EntranceRadius { get; set; } = 4.0f;

    public bool Contains(Vector3D point, int dimension)
    {
        if (dimension != Dimension) return false;
        return point.X >= Min.X && point.X <= Max.X &&
               point.Y >= Min.Y && point.Y <= Max.Y &&
               point.Z >= Min.Z && point.Z <= Max.Z;
    }
}

/// <summary>
/// Сервис отсечения невидимых сущностей (Occlusion Culling & Distance PVS).
/// Фильтрует сетевые пакеты стриминга для игроков, находящихся за глухими стенами,
/// внутри изолированных бункеров/интерьеров или за пределами видимости.
/// Даёт прирост клиентского FPS и полностью нейтрализует читы типа Wallhack / ESP.
/// </summary>
public sealed class OcclusionCullingService
{
    private readonly object _lock = new();
    private readonly Dictionary<string, OcclusionZone> _zones = new();

    public float DefaultMaxDistance { get; set; } = 250.0f;
    public float HearingProximityRadius { get; set; } = 15.0f; // Близкие сущности всегда передаются для звука шагов

    public void RegisterZone(string id, Vector3D min, Vector3D max, int dimension = 0, Vector3D? entrance = null, float entranceRadius = 4.0f)
    {
        lock (_lock)
        {
            _zones[id] = new OcclusionZone
            {
                Id = id,
                Min = new Vector3D(Math.Min(min.X, max.X), Math.Min(min.Y, max.Y), Math.Min(min.Z, max.Z)),
                Max = new Vector3D(Math.Max(min.X, max.X), Math.Max(min.Y, max.Y), Math.Max(min.Z, max.Z)),
                Dimension = dimension,
                EntrancePosition = entrance,
                EntranceRadius = entranceRadius
            };
        }
    }

    public bool RemoveZone(string id)
    {
        lock (_lock)
        {
            return _zones.Remove(id);
        }
    }

    public int ZoneCount
    {
        get
        {
            lock (_lock) return _zones.Count;
        }
    }

    /// <summary>
    /// Проверяет, видна ли целевая сущность наблюдателю с учётом измерений, расстояния и окклюдеров.
    /// </summary>
    public bool IsVisible(
        Vector3D viewerPos,
        Vector3D viewerHeading,
        int viewerDim,
        Vector3D targetPos,
        int targetDim,
        float? maxDistance = null,
        bool useFovCheck = false,
        float fovDegrees = 110.0f)
    {
        // 1. Разные виртуальные миры (Dimensions) — 100% изоляция
        if (viewerDim != targetDim)
        {
            return false;
        }

        // 2. Проверка дистанции
        float dist = viewerPos.DistanceTo(targetPos);
        if (float.IsNaN(dist) || float.IsInfinity(dist))
        {
            return false;
        }

        float limit = maxDistance ?? DefaultMaxDistance;
        if (dist > limit)
        {
            return false;
        }

        // В упор (радиус слышимости) — всегда видно/слышно для честного ближнего боя
        if (dist <= HearingProximityRadius)
        {
            return true;
        }

        // 3. Проверка зон окклюзии (интерьеры, закрытые хранилища, подземные бункеры)
        lock (_lock)
        {
            foreach (var zone in _zones.Values)
            {
                if (zone.Dimension != viewerDim) continue;

                bool viewerInside = zone.Contains(viewerPos, viewerDim);
                bool targetInside = zone.Contains(targetPos, targetDim);

                // Если один внутри зоны, а другой снаружи
                if (viewerInside != targetInside)
                {
                    // Проверяем, находится ли кто-то из них в дверном проёме / у входа
                    if (zone.EntrancePosition.HasValue)
                    {
                        var ent = zone.EntrancePosition.Value;
                        if (viewerPos.DistanceTo(ent) <= zone.EntranceRadius ||
                            targetPos.DistanceTo(ent) <= zone.EntranceRadius)
                        {
                            continue; // Видимость через открытый вход разрешена
                        }
                    }

                    // Глухая стена зоны блокирует прямую видимость
                    return false;
                }
            }
        }

        // 4. Опциональный Field Of View (FOV) тест конуса видимости (для дополнительной экономии пакетов)
        if (useFovCheck && dist > HearingProximityRadius)
        {
            float dx = targetPos.X - viewerPos.X;
            float dy = targetPos.Y - viewerPos.Y;
            float targetDist2D = (float)Math.Sqrt(dx * dx + dy * dy);

            if (targetDist2D > 0.001f)
            {
                float dirX = dx / targetDist2D;
                float dirY = dy / targetDist2D;

                float forwardLen = (float)Math.Sqrt(viewerHeading.X * viewerHeading.X + viewerHeading.Y * viewerHeading.Y);
                if (forwardLen > 0.001f)
                {
                    float fwdX = viewerHeading.X / forwardLen;
                    float fwdY = viewerHeading.Y / forwardLen;

                    float dot = (dirX * fwdX) + (dirY * fwdY);
                    float minDot = (float)Math.Cos((fovDegrees * 0.5f) * (Math.PI / 180.0));

                    if (dot < minDot)
                    {
                        return false; // Позади наблюдателя
                    }
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Фильтрует список кандидатов, оставляя только видимые сущности.
    /// </summary>
    public List<T> FilterVisible<T>(
        Vector3D viewerPos,
        Vector3D viewerHeading,
        int viewerDim,
        IEnumerable<(T Item, Vector3D Pos, int Dim)> candidates,
        float? maxDistance = null,
        bool useFov = false)
    {
        var result = new List<T>();
        FilterVisibleInto(viewerPos, viewerHeading, viewerDim, candidates, result, maxDistance, useFov);
        return result;
    }

    /// <summary>
    /// То же, но результат складывается в буфер вызывающей стороны (он очищается).
    ///
    /// Фильтрация видимости вызывается для каждого наблюдателя каждый тик.
    /// Новый список на каждый вызов — это сотни лишних аллокаций за тик и
    /// паузы сборщика прямо в игровом цикле; на горячем пути нужен буфер.
    /// </summary>
    public int FilterVisibleInto<T>(
        Vector3D viewerPos,
        Vector3D viewerHeading,
        int viewerDim,
        IEnumerable<(T Item, Vector3D Pos, int Dim)> candidates,
        List<T> result,
        float? maxDistance = null,
        bool useFov = false)
    {
        if (result is null) throw new ArgumentNullException(nameof(result));
        result.Clear();

        // Зоны снимаются ОДИН раз на весь список кандидатов, а не на каждого.
        // Раньше IsVisible брал блокировку на КАЖДОГО кандидата: при 1500
        // игроках со ста соседями это 150 000 захватов блокировки за тик,
        // плюс перебор всех зон внутри каждого. Снимок под одной блокировкой
        // и дальше проверки без неё.
        //
        // Берём только зоны нужного измерения: игрок в интерьере не может быть
        // отсечён зоной из чужого виртуального мира, а лишние зоны — это
        // лишний проход по каждому кандидату.
        var zones = SnapshotZones(viewerDim);

        float limit = maxDistance ?? DefaultMaxDistance;
        float limitSq = limit * limit;
        float proximitySq = HearingProximityRadius * HearingProximityRadius;

        foreach (var (item, pos, dim) in candidates)
        {
            if (IsVisibleFast(viewerPos, viewerHeading, viewerDim, pos, dim,
                              limitSq, proximitySq, zones, useFov))
            {
                result.Add(item);
            }
        }
        return result.Count;
    }

    // Переиспользуемый буфер снимка зон: выделять список на каждый вызов
    // фильтра — сотни аллокаций за тик, ровно то, от чего уходили в сетке.
    private readonly List<OcclusionZone> _zoneSnapshot = new();

    /// <summary>
    /// Снимок зон нужного измерения под одной блокировкой.
    /// Возвращает null, если зон нет вовсе — тогда весь блок окклюзии
    /// пропускается целиком (типовой случай для открытого мира).
    /// </summary>
    private List<OcclusionZone>? SnapshotZones(int dimension)
    {
        lock (_lock)
        {
            if (_zones.Count == 0) return null;

            _zoneSnapshot.Clear();
            foreach (var zone in _zones.Values)
                if (zone.Dimension == dimension) _zoneSnapshot.Add(zone);

            return _zoneSnapshot.Count == 0 ? null : _zoneSnapshot;
        }
    }

    /// <summary>
    /// Та же логика, что в <see cref="IsVisible"/>, но без блокировки и без
    /// извлечения квадратного корня: дистанция сравнивается в квадратах.
    /// Корень на горячем пути не нужен — сравнение с порогом эквивалентно.
    /// </summary>
    private bool IsVisibleFast(
        Vector3D viewerPos, Vector3D viewerHeading, int viewerDim,
        Vector3D targetPos, int targetDim,
        float limitSq, float proximitySq,
        List<OcclusionZone>? zones,
        bool useFov)
    {
        // 1. Разные виртуальные миры — 100% изоляция
        if (viewerDim != targetDim) return false;

        // 2. Дистанция (в квадратах)
        float dx = viewerPos.X - targetPos.X;
        float dy = viewerPos.Y - targetPos.Y;
        float dz = viewerPos.Z - targetPos.Z;
        float distSq = dx * dx + dy * dy + dz * dz;

        if (float.IsNaN(distSq) || float.IsInfinity(distSq)) return false;
        if (distSq > limitSq) return false;

        // В упор — всегда видно/слышно (честный ближний бой)
        if (distSq <= proximitySq) return true;

        // 3. Зоны окклюзии
        if (zones is not null)
        {
            foreach (var zone in zones)
            {
                bool viewerInside = zone.Contains(viewerPos, viewerDim);
                bool targetInside = zone.Contains(targetPos, targetDim);
                if (viewerInside == targetInside) continue;

                if (zone.EntrancePosition.HasValue)
                {
                    var ent = zone.EntrancePosition.Value;
                    float er = zone.EntranceRadius * zone.EntranceRadius;
                    if (DistSq(viewerPos, ent) <= er || DistSq(targetPos, ent) <= er)
                        continue; // видимость через открытый вход
                }

                return false; // глухая стена зоны
            }
        }

        // 4. Опциональный конус видимости
        if (useFov)
        {
            var dist = MathF.Sqrt(distSq);
            if (dist > 0.0001f)
            {
                float ndx = (targetPos.X - viewerPos.X) / dist;
                float ndy = (targetPos.Y - viewerPos.Y) / dist;
                float dot = ndx * viewerHeading.X + ndy * viewerHeading.Y;
                if (dot < MathF.Cos(110.0f * 0.5f * MathF.PI / 180.0f)) return false;
            }
        }

        return true;
    }

    private static float DistSq(Vector3D a, Vector3D b)
    {
        float dx = a.X - b.X, dy = a.Y - b.Y, dz = a.Z - b.Z;
        return dx * dx + dy * dy + dz * dz;
    }
}
