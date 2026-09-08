using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using FloVMP.Core.AntiCheat;

namespace FloVMP.Core.Sync;

/// <summary>
/// Снимок физического состояния сущности в определенный момент времени.
/// </summary>
public readonly struct EntitySnapshot
{
    public long TimestampMs { get; }
    public Vector3D Position { get; }
    public Vector3D Velocity { get; }
    public Vector3D Rotation { get; }

    public EntitySnapshot(long timestampMs, Vector3D position, Vector3D velocity, Vector3D rotation)
    {
        TimestampMs = timestampMs;
        Position = position;
        Velocity = velocity;
        Rotation = rotation;
    }
}

/// <summary>
/// Кольцевой буфер истории состояний для сущности (для плавного Dead-Reckoning и Lag Compensation).
/// </summary>
public sealed class EntityHistoryBuffer
{
    private readonly EntitySnapshot[] _buffer;
    private int _head;
    private int _count;
    private readonly object _lock = new();

    public int Capacity { get; }

    public EntityHistoryBuffer(int capacity = 64)
    {
        if (capacity < 4) capacity = 4;
        Capacity = capacity;
        _buffer = new EntitySnapshot[capacity];
    }

    public void Add(EntitySnapshot snapshot)
    {
        lock (_lock)
        {
            _buffer[_head] = snapshot;
            _head = (_head + 1) % Capacity;
            if (_count < Capacity) _count++;
        }
    }

    public int Count
    {
        get { lock (_lock) return _count; }
    }

    public EntitySnapshot? GetLatest()
    {
        lock (_lock)
        {
            if (_count == 0) return null;
            int idx = (_head - 1 + Capacity) % Capacity;
            return _buffer[idx];
        }
    }

    /// <summary>
    /// Копирует отсортированные по возрастанию времени снимки.
    /// </summary>
    public List<EntitySnapshot> GetOrderedSnapshots()
    {
        lock (_lock)
        {
            var list = new List<EntitySnapshot>(_count);
            if (_count == 0) return list;

            int startIdx = (_head - _count + Capacity) % Capacity;
            for (int i = 0; i < _count; i++)
            {
                int idx = (startIdx + i) % Capacity;
                list.Add(_buffer[idx]);
            }
            return list;
        }
    }
}

/// <summary>
/// Результат валидации попадания с компенсацией задержки (Lag Compensation Hit Validation).
/// </summary>
public sealed class LagCompensationHitResult
{
    public bool IsHit { get; }
    public Vector3D RewoundPosition { get; }
    public float DistanceToRay { get; }
    public string? Reason { get; }

    public LagCompensationHitResult(bool isHit, Vector3D rewoundPosition, float distanceToRay, string? reason = null)
    {
        IsHit = isHit;
        RewoundPosition = rewoundPosition;
        DistanceToRay = distanceToRay;
        Reason = reason;
    }
}

/// <summary>
/// Движок предиктивной интерполяции Dead-Reckoning и компенсации сетевой задержки (Lag Compensation).
/// Устраняет микро-телепортации и рывки транспорта при 1500–3000 онлайн,
/// а также обеспечивает идеальную регистрацию попаданий при стрельбе с учётом пинга стрелка.
/// </summary>
public sealed class DeadReckoningInterpolator
{
    private readonly ConcurrentDictionary<ulong, EntityHistoryBuffer> _entityHistories = new();

    /// <summary>
    /// Запись нового пакета синхронизации для сущности.
    /// </summary>
    public void RecordSnapshot(ulong entityId, long timestampMs, Vector3D position, Vector3D velocity, Vector3D rotation)
    {
        if (float.IsNaN(position.X) || float.IsNaN(position.Y) || float.IsNaN(position.Z) ||
            float.IsInfinity(position.X) || float.IsInfinity(position.Y) || float.IsInfinity(position.Z) ||
            float.IsNaN(velocity.X) || float.IsNaN(velocity.Y) || float.IsNaN(velocity.Z) ||
            float.IsInfinity(velocity.X) || float.IsInfinity(velocity.Y) || float.IsInfinity(velocity.Z))
        {
            return;
        }

        var history = _entityHistories.GetOrAdd(entityId, _ => new EntityHistoryBuffer(capacity: 64));
        history.Add(new EntitySnapshot(timestampMs, position, velocity, rotation));
    }

    /// <summary>
    /// Удаление сущности при отключении/уничтожении.
    /// </summary>
    public void RemoveEntity(ulong entityId)
    {
        _entityHistories.TryRemove(entityId, out _);
    }

    /// <summary>
    /// Предиктивная экстраполяция позиции на текущий момент (Dead-Reckoning)
    /// с расчетом скорости и плавным затуханием при задержке пакетов.
    /// </summary>
    public Vector3D ExtrapolatePosition(ulong entityId, long currentTimestampMs, float maxExtrapolationSeconds = 0.5f)
    {
        if (!_entityHistories.TryGetValue(entityId, out var history))
        {
            return Vector3D.Zero;
        }

        var latest = history.GetLatest();
        if (latest == null) return Vector3D.Zero;

        var snap = latest.Value;
        long deltaMs = currentTimestampMs - snap.TimestampMs;

        // Если пакет из будущего или время совпадает — возвращаем текущую позицию
        if (deltaMs <= 0) return snap.Position;

        float deltaSeconds = deltaMs / 1000.0f;
        // Ограничиваем экстраполяцию максимальным временем (чтобы брошенные машины не летели в бесконечность)
        if (deltaSeconds > maxExtrapolationSeconds)
        {
            deltaSeconds = maxExtrapolationSeconds;
        }

        // Dead-Reckoning формула: P(t) = P0 + V * dt
        float estX = snap.Position.X + snap.Velocity.X * deltaSeconds;
        float estY = snap.Position.Y + snap.Velocity.Y * deltaSeconds;
        float estZ = snap.Position.Z + snap.Velocity.Z * deltaSeconds;

        return new Vector3D(estX, estY, estZ);
    }

    /// <summary>
    /// Кубическая эрмитова интерполяция (Cubic Hermite Spline) между двумя ключевыми кадрами.
    /// Гарантирует гладкую траекторию C^1 без угловых изломов и рывков скорости.
    /// </summary>
    public static Vector3D HermiteInterpolate(Vector3D p0, Vector3D v0, Vector3D p1, Vector3D v1, float t)
    {
        t = Math.Clamp(t, 0.0f, 1.0f);
        float t2 = t * t;
        float t3 = t2 * t;

        // Базисные функции Эрмита
        float h00 = 2.0f * t3 - 3.0f * t2 + 1.0f;
        float h10 = t3 - 2.0f * t2 + t;
        float h01 = -2.0f * t3 + 3.0f * t2;
        float h11 = t3 - t2;

        float x = h00 * p0.X + h10 * v0.X + h01 * p1.X + h11 * v1.X;
        float y = h00 * p0.Y + h10 * v0.Y + h01 * p1.Y + h11 * v1.Y;
        float z = h00 * p0.Z + h10 * v0.Z + h01 * p1.Z + h11 * v1.Z;

        return new Vector3D(x, y, z);
    }

    /// <summary>
    /// Получение позиции сущности в прошлом с интерполяцией (Lag Compensation Time Rewind).
    /// </summary>
    public Vector3D? GetRewoundPosition(ulong entityId, long targetTimestampMs, long currentServerTimestampMs, long maxRewindMs = 500)
    {
        if (!_entityHistories.TryGetValue(entityId, out var history))
        {
            return null;
        }

        // Защита от эксплойтов: нельзя отмотать время дальше maxRewindMs или дальше текущего времени + 150мс
        long minAllowedTimestamp = currentServerTimestampMs - maxRewindMs;
        if (targetTimestampMs < minAllowedTimestamp)
        {
            targetTimestampMs = minAllowedTimestamp;
        }
        else if (targetTimestampMs > currentServerTimestampMs + 150)
        {
            targetTimestampMs = currentServerTimestampMs + 150;
        }

        var snapshots = history.GetOrderedSnapshots();
        if (snapshots.Count == 0) return null;
        if (snapshots.Count == 1) return snapshots[0].Position;

        // Если запрошенное время позже самого свежего снимка — экстраполируем
        var newest = snapshots[^1];
        if (targetTimestampMs >= newest.TimestampMs)
        {
            return ExtrapolatePosition(entityId, targetTimestampMs);
        }

        // Если запрошенное время раньше самого старого в буфере — возвращаем самый старый
        var oldest = snapshots[0];
        if (targetTimestampMs <= oldest.TimestampMs)
        {
            return oldest.Position;
        }

        // Находим два граничных снимка [t0, t1], между которыми находится targetTimestampMs
        for (int i = 0; i < snapshots.Count - 1; i++)
        {
            var s0 = snapshots[i];
            var s1 = snapshots[i + 1];

            if (targetTimestampMs >= s0.TimestampMs && targetTimestampMs <= s1.TimestampMs)
            {
                long duration = s1.TimestampMs - s0.TimestampMs;
                if (duration <= 0) return s0.Position;

                float t = (float)(targetTimestampMs - s0.TimestampMs) / duration;
                return HermiteInterpolate(s0.Position, s0.Velocity, s1.Position, s1.Velocity, t);
            }
        }

        return newest.Position;
    }

    /// <summary>
    /// Валидация попадания пули с учетом отмотки времени (Lag Compensation Hitscan Verification).
    /// Проверяет пересечение луча выстрела со сферическим/цилиндрическим хитбоксом цели в момент выстрела.
    /// </summary>
    public LagCompensationHitResult ValidateHit(
        ulong shooterId,
        ulong targetId,
        long clientShotTimestampMs,
        Vector3D rayOrigin,
        Vector3D rayDirection,
        float maxRange,
        float hitboxRadius,
        long currentServerTimestampMs,
        long maxRewindMs = 500)
    {
        if (clientShotTimestampMs > currentServerTimestampMs + 250)
        {
            return new LagCompensationHitResult(false, Vector3D.Zero, float.MaxValue, "Shot timestamp is in the future");
        }

        // Отмотка позиции цели на момент выстрела
        var rewoundPos = GetRewoundPosition(targetId, clientShotTimestampMs, currentServerTimestampMs, maxRewindMs);
        if (rewoundPos == null)
        {
            return new LagCompensationHitResult(false, Vector3D.Zero, float.MaxValue, "Target snapshot history unavailable");
        }

        var targetPos = rewoundPos.Value;

        // Расстояние от стрелка до цели
        float distToTarget = rayOrigin.DistanceTo(targetPos);
        if (distToTarget > maxRange + hitboxRadius)
        {
            return new LagCompensationHitResult(false, targetPos, distToTarget, "Target out of weapon max range");
        }

        // Нормализация вектора направления луча
        float dirLen = MathF.Sqrt(rayDirection.X * rayDirection.X + rayDirection.Y * rayDirection.Y + rayDirection.Z * rayDirection.Z);
        if (dirLen <= 1e-6f)
        {
            return new LagCompensationHitResult(false, targetPos, float.MaxValue, "Invalid bullet ray direction");
        }

        var dir = new Vector3D(rayDirection.X / dirLen, rayDirection.Y / dirLen, rayDirection.Z / dirLen);

        // Вектор от начала луча до центра хитбокса
        var toTarget = new Vector3D(targetPos.X - rayOrigin.X, targetPos.Y - rayOrigin.Y, targetPos.Z - rayOrigin.Z);

        // Проекция вектора цели на луч (скалярное произведение)
        float projection = toTarget.X * dir.X + toTarget.Y * dir.Y + toTarget.Z * dir.Z;

        // Если цель находится сзади стрелка
        if (projection < -hitboxRadius)
        {
            return new LagCompensationHitResult(false, targetPos, float.MaxValue, "Target is behind shooter");
        }

        // Ближайшая точка на луче к центру цели
        var closestPoint = new Vector3D(
            rayOrigin.X + dir.X * projection,
            rayOrigin.Y + dir.Y * projection,
            rayOrigin.Z + dir.Z * projection
        );

        // Расстояние от луча до центра цели
        float distanceToRay = closestPoint.DistanceTo(targetPos);

        bool isHit = distanceToRay <= hitboxRadius;
        return new LagCompensationHitResult(isHit, targetPos, distanceToRay, isHit ? null : "Bullet missed hitbox");
    }
}
