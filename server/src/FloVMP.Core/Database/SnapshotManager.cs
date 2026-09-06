using System.Collections.Concurrent;
using System.Text.Json;

namespace FloVMP.Core.Database;

/// <summary>
/// Сервис моментальных снимков состояния (Time-Machine Rollback Engine) для «Держава Онлайн».
/// Позволяет сохранять срезы состояния игроков (деньги, координаты, инвентарь, дименшн)
/// и мгновенно откатывать отдельного игрока или группу без рестарта сервера и без вайпа базы данных.
/// </summary>
public sealed class SnapshotManager
{
    private readonly ConcurrentDictionary<ulong, List<PlayerStateSnapshot>> _snapshots = new();
    private readonly object _lock = new();

    /// <summary>
    /// Максимальное число хранимых снимков на одного игрока (кольцевой буфер).
    /// </summary>
    public int MaxSnapshotsPerPlayer { get; set; } = 30;

    /// <summary>
    /// Событие создания снимка.
    /// </summary>
    public event Action<PlayerStateSnapshot>? OnSnapshotCreated;

    /// <summary>
    /// Событие успешного отката персонажа к снимку.
    /// </summary>
    public event Action<ulong, PlayerStateSnapshot>? OnSnapshotRestored;

    /// <summary>
    /// Создаёт и сохраняет новый снимок состояния игрока.
    /// </summary>
    public PlayerStateSnapshot CaptureSnapshot(
        ulong playerId,
        string characterName,
        long bank,
        long cash,
        long dirtyCash,
        int dimension,
        float x,
        float y,
        float z,
        float heading = 0f,
        int health = 100,
        int armor = 0,
        string inventoryJson = "[]",
        IReadOnlyList<string>? licenses = null,
        IReadOnlyList<string>? ownedVehicles = null,
        string reason = "Periodic")
    {
        var snapshot = new PlayerStateSnapshot
        {
            PlayerId = playerId,
            CharacterName = characterName,
            CreatedAtUtc = DateTime.UtcNow,
            Reason = reason,
            BankBalance = bank,
            CashBalance = cash,
            DirtyCashBalance = dirtyCash,
            Dimension = dimension,
            PositionX = x,
            PositionY = y,
            PositionZ = z,
            Heading = heading,
            Health = health,
            Armor = armor,
            InventoryJson = inventoryJson,
            Licenses = licenses ?? Array.Empty<string>(),
            OwnedVehicles = ownedVehicles ?? Array.Empty<string>()
        };

        lock (_lock)
        {
            var list = _snapshots.GetOrAdd(playerId, _ => new List<PlayerStateSnapshot>());
            list.Add(snapshot);

            // Кольцевое отсечение старых снимков
            if (list.Count > MaxSnapshotsPerPlayer)
            {
                list.RemoveAt(0);
            }
        }

        OnSnapshotCreated?.Invoke(snapshot);
        return snapshot;
    }

    /// <summary>
    /// Возвращает список всех сохранённых снимков игрока от новых к старым.
    /// </summary>
    public IReadOnlyList<PlayerStateSnapshot> GetSnapshots(ulong playerId)
    {
        lock (_lock)
        {
            if (_snapshots.TryGetValue(playerId, out var list))
            {
                return list.OrderByDescending(s => s.CreatedAtUtc).ToList();
            }
            return Array.Empty<PlayerStateSnapshot>();
        }
    }

    /// <summary>
    /// Возвращает конкретный снимок по его ID.
    /// </summary>
    public PlayerStateSnapshot? GetSnapshot(ulong playerId, string snapshotId)
    {
        lock (_lock)
        {
            if (_snapshots.TryGetValue(playerId, out var list))
            {
                return list.FirstOrDefault(s => string.Equals(s.Id, snapshotId, StringComparison.OrdinalIgnoreCase));
            }
            return null;
        }
    }

    /// <summary>
    /// Откатывает игрока к выбранному снимку.
    /// Автоматически создаёт снимок текущего состояния ("PreRollbackBackup") перед применением отката.
    /// </summary>
    public PlayerStateSnapshot? RollbackToSnapshot(
        ulong playerId,
        string snapshotId,
        Func<ulong, PlayerStateSnapshot>? currentStateProvider = null)
    {
        PlayerStateSnapshot? target;

        lock (_lock)
        {
            if (!_snapshots.TryGetValue(playerId, out var list))
            {
                return null;
            }

            target = list.FirstOrDefault(s => string.Equals(s.Id, snapshotId, StringComparison.OrdinalIgnoreCase));
            if (target == null) return null;

            // Если предоставлен делегат текущего состояния — делаем контрольную копию перед откатом
            if (currentStateProvider != null)
            {
                var current = currentStateProvider(playerId);
                var backup = new PlayerStateSnapshot
                {
                    PlayerId = playerId,
                    CharacterName = current.CharacterName,
                    CreatedAtUtc = DateTime.UtcNow,
                    Reason = $"PreRollbackBackup(Target={snapshotId})",
                    BankBalance = current.BankBalance,
                    CashBalance = current.CashBalance,
                    DirtyCashBalance = current.DirtyCashBalance,
                    Dimension = current.Dimension,
                    PositionX = current.PositionX,
                    PositionY = current.PositionY,
                    PositionZ = current.PositionZ,
                    Heading = current.Heading,
                    Health = current.Health,
                    Armor = current.Armor,
                    InventoryJson = current.InventoryJson,
                    Licenses = current.Licenses,
                    OwnedVehicles = current.OwnedVehicles
                };
                list.Add(backup);
                if (list.Count > MaxSnapshotsPerPlayer)
                {
                    list.RemoveAt(0);
                }
            }
        }

        OnSnapshotRestored?.Invoke(playerId, target);
        return target;
    }

    /// <summary>
    /// Удаляет устаревшие снимки старше указанного возраста.
    /// </summary>
    public int PruneOlderThan(TimeSpan maxAge)
    {
        var cutoff = DateTime.UtcNow - maxAge;
        int prunedCount = 0;

        lock (_lock)
        {
            var emptyKeys = new List<ulong>();
            foreach (var kvp in _snapshots)
            {
                int removed = kvp.Value.RemoveAll(s => s.CreatedAtUtc < cutoff);
                prunedCount += removed;
                if (kvp.Value.Count == 0)
                {
                    emptyKeys.Add(kvp.Key);
                }
            }
            foreach (var key in emptyKeys)
            {
                _snapshots.TryRemove(key, out _);
            }
        }

        return prunedCount;
    }

    /// <summary>
    /// Экспорт снимков игрока в JSON для долговременного хранения / аудита.
    /// </summary>
    public string ExportSnapshotsJson(ulong playerId)
    {
        var snapshots = GetSnapshots(playerId);
        return JsonSerializer.Serialize(snapshots, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Очистить всю историю снимков (например, при полном удалении персонажа).
    /// </summary>
    public void Clear(ulong playerId)
    {
        lock (_lock)
        {
            _snapshots.TryRemove(playerId, out _);
        }
    }
}
