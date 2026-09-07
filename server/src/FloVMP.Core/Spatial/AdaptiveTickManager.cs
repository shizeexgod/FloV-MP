using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using FloVMP.Core.AntiCheat;

namespace FloVMP.Core.Spatial;

public enum EntityActivityTier
{
    CombatOrHighSpeed = 0, // 60 Hz (каждый тик)
    StandardNearby = 1,    // 45-60 Hz (каждые 1-2 тика)
    PassiveOrDistant = 2,  // 20-30 Hz (каждые 2-3 тика)
    InteriorOrFar = 3      // 10-15 Hz (каждые 4-6 тиков)
}

public sealed class EntitySyncState
{
    public Vector3D Position { get; set; }
    public Vector3D Velocity { get; set; }
    public int Dimension { get; set; }
    public bool IsInCombat { get; set; }
    public DateTime LastActivityUtc { get; set; } = DateTime.UtcNow;
    public EntityActivityTier Tier { get; set; } = EntityActivityTier.StandardNearby;
}

/// <summary>
/// Сервис адаптивного масштабирования тикрейта (Adaptive Tick Scaling).
/// Динамически регулирует частоту отправки сетевых пакетов для каждой сущности
/// на основе её активности, скорости, боя и общей нагрузки на процессор сервера.
/// Позволяет удерживать 60 Hz в PvP и погонях при онлайне 1000+ игроков.
/// </summary>
public sealed class AdaptiveTickManager<TId> where TId : notnull
{
    private readonly object _lock = new();
    private readonly Dictionary<TId, EntitySyncState> _entities = new();

    // Параметры скорости и дистанций
    public float HighSpeedThreshold { get; set; } = 15.0f; // 15 м/с (~54 км/ч)
    public float FarDistanceThreshold { get; set; } = 50.0f;
    public float ExtremeDistanceThreshold { get; set; } = 120.0f;

    // Фактор нагрузки на сервер: 0.0 (пустой) до 1.0 (пик нагрузки)
    private float _serverLoadFactor = 0.0f;
    public float ServerLoadFactor
    {
        get
        {
            lock (_lock) return _serverLoadFactor;
        }
        set
        {
            lock (_lock) _serverLoadFactor = Math.Clamp(value, 0.0f, 1.0f);
        }
    }

    public int RegisteredCount
    {
        get
        {
            lock (_lock) return _entities.Count;
        }
    }

    public void RegisterEntity(TId id, Vector3D position, int dimension = 0)
    {
        lock (_lock)
        {
            _entities[id] = new EntitySyncState
            {
                Position = position,
                Dimension = dimension,
                Velocity = Vector3D.Zero,
                IsInCombat = false,
                Tier = EntityActivityTier.StandardNearby,
                LastActivityUtc = DateTime.UtcNow
            };
        }
    }

    public void UnregisterEntity(TId id)
    {
        lock (_lock)
        {
            _entities.Remove(id);
        }
    }

    public void UpdateEntityState(TId id, Vector3D position, Vector3D velocity, bool isInCombat, int dimension = 0)
    {
        lock (_lock)
        {
            if (!_entities.TryGetValue(id, out var state))
            {
                state = new EntitySyncState();
                _entities[id] = state;
            }

            state.Position = position;
            state.Velocity = velocity;
            state.Dimension = dimension;
            state.IsInCombat = isInCombat;
            state.LastActivityUtc = DateTime.UtcNow;

            // Расчет уровня активности сущности
            float speed = (float)Math.Sqrt(velocity.X * velocity.X + velocity.Y * velocity.Y + velocity.Z * velocity.Z);
            if (isInCombat || speed >= HighSpeedThreshold)
            {
                state.Tier = EntityActivityTier.CombatOrHighSpeed;
            }
            else if (speed > 1.0f)
            {
                state.Tier = EntityActivityTier.StandardNearby;
            }
            else
            {
                state.Tier = EntityActivityTier.PassiveOrDistant;
            }
        }
    }

    /// <summary>
    /// Определяет, должен ли конкретный наблюдатель (viewer) получить пакет синхронизации
    /// целевой сущности (target) на текущем тике сервера.
    /// </summary>
    public bool ShouldSyncThisTick(TId targetId, TId viewerId, long currentTickIndex)
    {
        EntitySyncState? target;
        EntitySyncState? viewer;

        lock (_lock)
        {
            if (!_entities.TryGetValue(targetId, out target)) return false;
            _entities.TryGetValue(viewerId, out viewer);
        }

        // Если разные виртуальные миры — не синхронизируем
        if (viewer != null && target.Dimension != viewer.Dimension)
        {
            return false;
        }

        float distance = viewer != null ? target.Position.DistanceTo(viewer.Position) : 0f;
        var effectiveTier = target.Tier;

        // Корректировка тира с учётом расстояния до наблюдателя
        if (effectiveTier != EntityActivityTier.CombatOrHighSpeed)
        {
            if (distance >= ExtremeDistanceThreshold)
            {
                effectiveTier = EntityActivityTier.InteriorOrFar;
            }
            else if (distance >= FarDistanceThreshold)
            {
                effectiveTier = EntityActivityTier.PassiveOrDistant;
            }
        }

        // Вычисляем интервал тиков для синхронизации
        int tickModulo = GetTickModuloForTier(effectiveTier, _serverLoadFactor);
        if (tickModulo <= 1) return true;

        // Хэширование для детерминированного размазывания синхронизации по тикам
        int hashOffset = Math.Abs(targetId.GetHashCode()) % tickModulo;
        return (currentTickIndex + hashOffset) % tickModulo == 0;
    }

    /// <summary>
    /// Возвращает эффективную частоту синхронизации в герцах для отображения в мониторинге.
    /// </summary>
    public int GetEffectiveTickRate(TId id, float distanceToViewer = 0f)
    {
        EntitySyncState? state;
        float load;

        lock (_lock)
        {
            if (!_entities.TryGetValue(id, out state)) return 0;
            load = _serverLoadFactor;
        }

        var tier = state.Tier;
        if (tier != EntityActivityTier.CombatOrHighSpeed)
        {
            if (distanceToViewer >= ExtremeDistanceThreshold) tier = EntityActivityTier.InteriorOrFar;
            else if (distanceToViewer >= FarDistanceThreshold) tier = EntityActivityTier.PassiveOrDistant;
        }

        int modulo = GetTickModuloForTier(tier, load);
        return (int)Math.Round(60.0 / modulo);
    }

    private static int GetTickModuloForTier(EntityActivityTier tier, float loadFactor)
    {
        // При базовом сервере 60 Hz:
        // Modulo 1 = 60 Hz
        // Modulo 2 = 30 Hz
        // Modulo 3 = 20 Hz
        // Modulo 4 = 15 Hz
        // Modulo 6 = 10 Hz
        return tier switch
        {
            EntityActivityTier.CombatOrHighSpeed => 1, // Всегда 60 Hz без компромиссов
            EntityActivityTier.StandardNearby => loadFactor >= 0.75f ? 2 : 1, // 30-60 Hz
            EntityActivityTier.PassiveOrDistant => loadFactor switch
            {
                >= 0.8f => 3, // 20 Hz
                >= 0.5f => 2, // 30 Hz
                _ => 2        // 30 Hz
            },
            EntityActivityTier.InteriorOrFar => loadFactor >= 0.7f ? 6 : 4, // 10-15 Hz
            _ => 1
        };
    }
}
