using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using FloVMP.Core.AntiCheat;

namespace FloVMP.Core.Spatial;

/// <summary>
/// Высокопроизводительная пространственная хэш-сетка (Spatial Hash Grid) с временной сложностью O(1).
/// Позволяет мгновенно находить сущности (игроки, транспорт, пропы) в радиусе без перебора миллионов пар.
/// Поддерживает виртуальные миры (Dimensions).
/// </summary>
/// <typeparam name="T">Тип регистрируемой сущности (например, ulong playerId или объект игрока/транспорта)</typeparam>
public sealed class SpatialHashGrid<T> where T : notnull
{
    private readonly float _cellSize;
    private readonly object _lock = new();

    // Запись о местоположении сущности
    private readonly Dictionary<T, EntityPositionRecord> _entityLocations = new();

    // Хэш-ячейки: Ключ = (cellX, cellY, dimension), Значение = Множество сущностей в ячейке
    private readonly Dictionary<(int cellX, int cellY, int dimension), HashSet<T>> _cells = new();

    public float CellSize => _cellSize;
    public int Count
    {
        get
        {
            lock (_lock) return _entityLocations.Count;
        }
    }

    public SpatialHashGrid(float cellSize = 64.0f)
    {
        if (cellSize <= 0) throw new ArgumentOutOfRangeException(nameof(cellSize), "Cell size must be greater than 0");
        _cellSize = cellSize;
    }

    /// <summary>
    /// Добавляет или обновляет положение сущности в пространственной сетке.
    /// Если сущность пересекла границу чанка — она автоматически мигрирует в новую ячейку за O(1).
    /// </summary>
    public void InsertOrUpdate(T entity, Vector3D position, int dimension = 0)
    {
        int cellX = GetCellCoordinate(position.X);
        int cellY = GetCellCoordinate(position.Y);
        var newCellKey = (cellX, cellY, dimension);

        lock (_lock)
        {
            if (_entityLocations.TryGetValue(entity, out var existing))
            {
                var oldCellKey = (existing.CellX, existing.CellY, existing.Dimension);
                if (oldCellKey != newCellKey)
                {
                    // Миграция из старой ячейки в новую
                    if (_cells.TryGetValue(oldCellKey, out var oldBucket))
                    {
                        oldBucket.Remove(entity);
                        if (oldBucket.Count == 0)
                        {
                            _cells.Remove(oldCellKey);
                        }
                    }

                    if (!_cells.TryGetValue(newCellKey, out var newBucket))
                    {
                        newBucket = new HashSet<T>();
                        _cells[newCellKey] = newBucket;
                    }
                    newBucket.Add(entity);
                }

                _entityLocations[entity] = new EntityPositionRecord(cellX, cellY, dimension, position);
            }
            else
            {
                // Новая сущность
                if (!_cells.TryGetValue(newCellKey, out var bucket))
                {
                    bucket = new HashSet<T>();
                    _cells[newCellKey] = bucket;
                }
                bucket.Add(entity);

                _entityLocations[entity] = new EntityPositionRecord(cellX, cellY, dimension, position);
            }
        }
    }

    /// <summary>
    /// Удаляет сущность из сетки (например, при отключении игрока или удалении транспорта).
    /// </summary>
    public bool Remove(T entity)
    {
        lock (_lock)
        {
            if (!_entityLocations.TryGetValue(entity, out var existing))
            {
                return false;
            }

            _entityLocations.Remove(entity);
            var cellKey = (existing.CellX, existing.CellY, existing.Dimension);

            if (_cells.TryGetValue(cellKey, out var bucket))
            {
                bucket.Remove(entity);
                if (bucket.Count == 0)
                {
                    _cells.Remove(cellKey);
                }
            }

            return true;
        }
    }

    /// <summary>
    /// Находит все сущности в заданном радиусе (в пределах указанного виртуального мира).
    /// Проверяются только ячейки сетки, пересекающие окружность, гарантируя O(1) при нормальной плотности.
    /// </summary>
    public IReadOnlyList<T> FindInRadius(Vector3D center, float radius, int dimension = 0, bool use3D = true)
    {
        if (float.IsNaN(radius) || float.IsInfinity(radius) || radius <= 0) return Array.Empty<T>();
        if (float.IsNaN(center.X) || float.IsNaN(center.Y) || float.IsNaN(center.Z)) return Array.Empty<T>();
        if (radius > 5000f) radius = 5000f; // Предотвращение исчерпания памяти при экстремальных радиусах

        int minCellX = GetCellCoordinate(center.X - radius);
        int maxCellX = GetCellCoordinate(center.X + radius);
        int minCellY = GetCellCoordinate(center.Y - radius);
        int maxCellY = GetCellCoordinate(center.Y + radius);

        float radiusSq = radius * radius;
        var results = new List<T>();

        lock (_lock)
        {
            for (int cx = minCellX; cx <= maxCellX; cx++)
            {
                for (int cy = minCellY; cy <= maxCellY; cy++)
                {
                    var cellKey = (cx, cy, dimension);
                    if (!_cells.TryGetValue(cellKey, out var bucket))
                    {
                        continue;
                    }

                    foreach (var entity in bucket)
                    {
                        if (_entityLocations.TryGetValue(entity, out var rec))
                        {
                            float dx = rec.Position.X - center.X;
                            float dy = rec.Position.Y - center.Y;
                            float distSq = dx * dx + dy * dy;

                            if (use3D)
                            {
                                float dz = rec.Position.Z - center.Z;
                                distSq += dz * dz;
                            }

                            if (distSq <= radiusSq)
                            {
                                results.Add(entity);
                            }
                        }
                    }
                }
            }
        }

        return results;
    }

    /// <summary>
    /// Находит все сущности в радиусе и возвращает их вместе с точной дистанцией (отсортировано от ближайших).
    /// Идеально для 3D голосового чата (рассчёт затухания) и выбора цели взаимодействия.
    /// </summary>
    public IReadOnlyList<(T Entity, float Distance)> FindInRadiusWithDistance(Vector3D center, float radius, int dimension = 0, bool use3D = true)
    {
        if (float.IsNaN(radius) || float.IsInfinity(radius) || radius <= 0) return Array.Empty<(T, float)>();
        if (float.IsNaN(center.X) || float.IsNaN(center.Y) || float.IsNaN(center.Z)) return Array.Empty<(T, float)>();
        if (radius > 5000f) radius = 5000f;

        int minCellX = GetCellCoordinate(center.X - radius);
        int maxCellX = GetCellCoordinate(center.X + radius);
        int minCellY = GetCellCoordinate(center.Y - radius);
        int maxCellY = GetCellCoordinate(center.Y + radius);

        float radiusSq = radius * radius;
        var results = new List<(T Entity, float Distance)>();

        lock (_lock)
        {
            for (int cx = minCellX; cx <= maxCellX; cx++)
            {
                for (int cy = minCellY; cy <= maxCellY; cy++)
                {
                    var cellKey = (cx, cy, dimension);
                    if (!_cells.TryGetValue(cellKey, out var bucket))
                    {
                        continue;
                    }

                    foreach (var entity in bucket)
                    {
                        if (_entityLocations.TryGetValue(entity, out var rec))
                        {
                            float dx = rec.Position.X - center.X;
                            float dy = rec.Position.Y - center.Y;
                            float distSq = dx * dx + dy * dy;

                            if (use3D)
                            {
                                float dz = rec.Position.Z - center.Z;
                                distSq += dz * dz;
                            }

                            if (distSq <= radiusSq)
                            {
                                results.Add((entity, (float)Math.Sqrt(distSq)));
                            }
                        }
                    }
                }
            }
        }

        results.Sort((a, b) => a.Distance.CompareTo(b.Distance));
        return results;
    }

    /// <summary>
    /// Возвращает текущее положение сущности, если она зарегистрирована в сетке.
    /// </summary>
    public bool TryGetPosition(T entity, out Vector3D position, out int dimension)
    {
        lock (_lock)
        {
            if (_entityLocations.TryGetValue(entity, out var rec))
            {
                position = rec.Position;
                dimension = rec.Dimension;
                return true;
            }
        }

        position = Vector3D.Zero;
        dimension = 0;
        return false;
    }

    /// <summary>
    /// Очищает всю сетку.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _entityLocations.Clear();
            _cells.Clear();
        }
    }

    private int GetCellCoordinate(float worldCoord)
    {
        return (int)Math.Floor(worldCoord / _cellSize);
    }

    private readonly struct EntityPositionRecord
    {
        public readonly int CellX;
        public readonly int CellY;
        public readonly int Dimension;
        public readonly Vector3D Position;

        public EntityPositionRecord(int cellX, int cellY, int dimension, Vector3D position)
        {
            CellX = cellX;
            CellY = cellY;
            Dimension = dimension;
            Position = position;
        }
    }
}
