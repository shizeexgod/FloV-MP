using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using FloVMP.Core.AntiCheat;

namespace FloVMP.Core.Items;

/// <summary>
/// Статус предмета, лежащего на земле.
/// 0 = Свободен для подбора (Available)
/// 1 = Захвачен в процессе транзакции (Claimed)
/// 2 = Успешно подобран и уничтожен (Consumed)
/// </summary>
public sealed class GroundItemDrop
{
    public string DropId { get; }
    public ItemStack Item { get; }
    public Vector3D Position { get; }
    public int Dimension { get; }
    public DateTime DroppedAtUtc { get; }
    public ulong DroppedByPlayerId { get; }

    // Атомарное состояние: 0 = Available, 1 = Claimed, 2 = Consumed
    private int _state;

    public GroundItemDrop(string dropId, ItemStack item, Vector3D position, int dimension, ulong droppedByPlayerId)
    {
        DropId = dropId;
        Item = item;
        Position = position;
        Dimension = dimension;
        DroppedByPlayerId = droppedByPlayerId;
        DroppedAtUtc = DateTime.UtcNow;
        _state = 0;
    }

    /// <summary>
    /// Атомарный захват предмета с использованием CAS (Compare-And-Swap).
    /// Гарантирует, что только ОДИН поток/игрок сможет подобрать предмет при одновременном клике.
    /// </summary>
    public bool TryClaim()
    {
        return Interlocked.CompareExchange(ref _state, 1, 0) == 0;
    }

    /// <summary>
    /// Освобождение захвата при неудаче (например, если у игрока перегруз инвентаря).
    /// </summary>
    public void ReleaseClaim()
    {
        Interlocked.CompareExchange(ref _state, 0, 1);
    }

    /// <summary>
    /// Фиксация успешного подбора предмета.
    /// </summary>
    public void MarkConsumed()
    {
        Interlocked.Exchange(ref _state, 2);
    }

    public bool IsConsumed => Volatile.Read(ref _state) == 2;
}

/// <summary>
/// Сервис атомарных транзакций инвентаря (Anti-Dupe & Two-Phase Commit Trade Engine).
/// Исключает дублирование предметов (дюпы) при одновременном подборе с пола и обмене между игроками.
/// </summary>
public sealed class AtomicInventoryTransactionService
{
    private readonly ConcurrentDictionary<string, GroundItemDrop> _groundDrops = new();
    private readonly object _globalTradeLock = new();

    public int ActiveDropsCount => _groundDrops.Count;

    /// <summary>
    /// Выбрасывает предмет из инвентаря игрока на землю.
    /// </summary>
    public GroundItemDrop? DropItem(Inventory sourceInv, string itemId, int quantity, Vector3D pos, int dimension, ulong playerId)
    {
        if (quantity <= 0) return null;

        lock (sourceInv)
        {
            var removeResult = sourceInv.Remove(itemId, quantity);
            if (!removeResult.Ok) return null;
        }

        var dropId = "drop_" + Guid.NewGuid().ToString("N")[..12];
        var drop = new GroundItemDrop(dropId, new ItemStack { ItemId = itemId, Quantity = quantity }, pos, dimension, playerId);
        _groundDrops[dropId] = drop;
        return drop;
    }

    /// <summary>
    /// Атомарный подбор предмета с пола с защитой от одновременного клика (Race-Condition Dupe Prevention).
    /// </summary>
    public bool TryPickupGroundItem(ulong playerId, string dropId, Inventory playerInv, Vector3D playerPos, float maxDistance, out ItemStack? pickedItem)
    {
        pickedItem = null;

        if (!_groundDrops.TryGetValue(dropId, out var drop))
        {
            return false; // Предмета уже не существует
        }

        // Проверка дистанции
        if (drop.Position.DistanceTo(playerPos) > maxDistance)
        {
            return false;
        }

        // Атомарная CAS-попытка захватить предмет
        if (!drop.TryClaim())
        {
            // Другой игрок уже забирает этот предмет в этот же миллисекундный квант!
            return false;
        }

        lock (playerInv)
        {
            var addResult = playerInv.Add(drop.Item.ItemId, drop.Item.Quantity);
            if (!addResult.Ok)
            {
                // У игрока перегруз или нет места — освобождаем предмет обратно на землю
                drop.ReleaseClaim();
                return false;
            }
        }

        // Успешно перемещён в инвентарь — окончательно удаляем с земли
        drop.MarkConsumed();
        _groundDrops.TryRemove(dropId, out _);
        pickedItem = drop.Item;
        return true;
    }

    /// <summary>
    /// Двухфазный атомарный обмен между двумя игроками (Two-Phase Commit Trade).
    /// Исключает рассинхрон или дюп при отмене трейда во время подтверждения.
    /// </summary>
    public bool ExecuteAtomicTrade(
        ulong playerAId,
        Inventory invA,
        IReadOnlyList<ItemStack> offerA,
        ulong playerBId,
        Inventory invB,
        IReadOnlyList<ItemStack> offerB)
    {
        // Упорядочивание блокировок по ID игроков для исключения Deadlock
        object firstLock = playerAId < playerBId ? invA : invB;
        object secondLock = playerAId < playerBId ? invB : invA;

        lock (firstLock)
        {
            lock (secondLock)
            {
                // Фаза 1: Валидация наличия всех предметов
                foreach (var item in offerA)
                {
                    if (invA.CountOf(item.ItemId) < item.Quantity) return false;
                }

                foreach (var item in offerB)
                {
                    if (invB.CountOf(item.ItemId) < item.Quantity) return false;
                }

                // Фаза 2: Проверка веса и вместимости принимающей стороны
                double weightDeltaA = 0;
                foreach (var item in offerB)
                {
                    var def = ItemCatalog.Get(item.ItemId);
                    if (def != null) weightDeltaA += def.Weight * item.Quantity;
                }
                foreach (var item in offerA)
                {
                    var def = ItemCatalog.Get(item.ItemId);
                    if (def != null) weightDeltaA -= def.Weight * item.Quantity;
                }

                if (invA.TotalWeight() + weightDeltaA > invA.MaxWeight + 1e-9) return false;

                double weightDeltaB = 0;
                foreach (var item in offerA)
                {
                    var def = ItemCatalog.Get(item.ItemId);
                    if (def != null) weightDeltaB += def.Weight * item.Quantity;
                }
                foreach (var item in offerB)
                {
                    var def = ItemCatalog.Get(item.ItemId);
                    if (def != null) weightDeltaB -= def.Weight * item.Quantity;
                }

                if (invB.TotalWeight() + weightDeltaB > invB.MaxWeight + 1e-9) return false;

                // Фаза 3: Атомарный перенос (Commit)
                foreach (var item in offerA)
                {
                    invA.Remove(item.ItemId, item.Quantity);
                    invB.Add(item.ItemId, item.Quantity);
                }

                foreach (var item in offerB)
                {
                    invB.Remove(item.ItemId, item.Quantity);
                    invA.Add(item.ItemId, item.Quantity);
                }

                return true;
            }
        }
    }
}
