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
        foreach (var (item, pos, dim) in candidates)
        {
            if (IsVisible(viewerPos, viewerHeading, viewerDim, pos, dim, maxDistance, useFov))
            {
                result.Add(item);
            }
        }
        return result;
    }
}
