using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace FloVMP.Core.World;

/// <summary>
/// Тип виртуального измерения
/// </summary>
public enum DimensionType
{
    Global = 0,
    Interior = 1,
    Housing = 2,
    Garage = 3,
    AdminJail = 4,
    EventZone = 5,
    PrivateInstance = 6
}

/// <summary>
/// Описание зарегистрированного виртуального мира
/// </summary>
public sealed record DimensionInfo(
    int Id,
    DimensionType Type,
    string Name,
    string? OwnerIdentifier = null,
    DateTime CreatedAtUtc = default);

/// <summary>
/// Менеджер пространственных измерений (Dimensions) FloV:MP.
/// Управляет пулом виртуальных миров, изоляцией интерьеров, квартир, гаражей и инстансов.
/// </summary>
public class DimensionManager
{
    public const int GlobalDimension = 0;
    public const int AdminJailDimension = 999;
    public const int EventDimension = 9999;

    public const int HousingMin = 10000;
    public const int HousingMax = 50000;

    public const int InstanceMin = 50001;
    public const int InstanceMax = 99999;

    private readonly ConcurrentDictionary<int, DimensionInfo> _dimensions = new();
    private readonly ConcurrentDictionary<int, HashSet<long>> _entitiesInDimension = new();
    private int _nextInstanceId = InstanceMin;

    public DimensionManager()
    {
        // Регистрируем базовые системные миры
        Register(new DimensionInfo(GlobalDimension, DimensionType.Global, "Общий мир (Москва / Overworld)", null, DateTime.UtcNow));
        Register(new DimensionInfo(AdminJailDimension, DimensionType.AdminJail, "Деморган (Админ-тюрьма)", null, DateTime.UtcNow));
        Register(new DimensionInfo(EventDimension, DimensionType.EventZone, "Зона игровых мероприятий", null, DateTime.UtcNow));
    }

    public bool Register(DimensionInfo info)
    {
        if (info == null) throw new ArgumentNullException(nameof(info));
        _entitiesInDimension.TryAdd(info.Id, new HashSet<long>());
        return _dimensions.TryAdd(info.Id, info);
    }

    public bool Unregister(int dimensionId)
    {
        if (dimensionId == GlobalDimension || dimensionId == AdminJailDimension)
            return false; // Защита системных измерений

        _entitiesInDimension.TryRemove(dimensionId, out _);
        return _dimensions.TryRemove(dimensionId, out _);
    }

    public DimensionInfo? Get(int dimensionId)
    {
        _dimensions.TryGetValue(dimensionId, out var info);
        return info;
    }

    /// <summary>
    /// Выделяет уникальное изолированное измерение под дом/квартиру
    /// </summary>
    public int AllocateHousingDimension(int propertyId, string propertyName, string ownerIdentifier)
    {
        var dimId = HousingMin + propertyId;
        var info = new DimensionInfo(dimId, DimensionType.Housing, $"Жилье: {propertyName}", ownerIdentifier, DateTime.UtcNow);
        _dimensions[dimId] = info;
        _entitiesInDimension.TryAdd(dimId, new HashSet<long>());
        return dimId;
    }

    /// <summary>
    /// Выделяет временное изолированное измерение для квеста, автошколы или создания персонажа
    /// </summary>
    public int AllocateTemporaryInstance(string name, string? ownerIdentifier = null)
    {
        lock (_dimensions)
        {
            var attempts = 0;
            while (attempts < (InstanceMax - InstanceMin))
            {
                var current = _nextInstanceId++;
                if (_nextInstanceId > InstanceMax) _nextInstanceId = InstanceMin;

                if (!_dimensions.ContainsKey(current))
                {
                    var info = new DimensionInfo(current, DimensionType.PrivateInstance, name, ownerIdentifier, DateTime.UtcNow);
                    _dimensions[current] = info;
                    _entitiesInDimension.TryAdd(current, new HashSet<long>());
                    return current;
                }
                attempts++;
            }
        }
        throw new InvalidOperationException("Пул виртуальных измерений переполнен");
    }

    /// <summary>
    /// Фиксирует перемещение сущности (игрока или ТС) между измерениями
    /// </summary>
    public void TrackEntityMove(long entityId, int oldDimension, int newDimension)
    {
        if (_entitiesInDimension.TryGetValue(oldDimension, out var oldSet))
        {
            lock (oldSet)
            {
                oldSet.Remove(entityId);
            }
        }

        var newSet = _entitiesInDimension.GetOrAdd(newDimension, _ => new HashSet<long>());
        lock (newSet)
        {
            newSet.Add(entityId);
        }
    }

    /// <summary>
    /// Возвращает количество сущностей в измерении
    /// </summary>
    public int GetEntityCountInDimension(int dimensionId)
    {
        if (_entitiesInDimension.TryGetValue(dimensionId, out var set))
        {
            lock (set)
            {
                return set.Count;
            }
        }
        return 0;
    }

    public IReadOnlyList<DimensionInfo> GetAllDimensions() => _dimensions.Values.ToList();
}
