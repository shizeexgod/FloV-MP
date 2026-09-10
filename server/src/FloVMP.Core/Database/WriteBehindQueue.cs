using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FloVMP.Core.Database;

/// <summary>
/// Запись о грязной сущности, требующей сохранения в базу данных.
/// </summary>
public sealed class DirtyEntityRecord<TKey, TEntity>
{
    public TKey Key { get; }
    public TEntity Entity { get; private set; }
    public HashSet<string> DirtyFields { get; }
    public DateTime FirstDirtiedUtc { get; }
    public DateTime LastDirtiedUtc { get; private set; }

    // Монотонная версия изменения. Растёт при каждом изменении записи. Используется
    // для точного детекта «перегрязнили во время сброса» — в отличие от LastDirtiedUtc
    // (DateTime.UtcNow ~15мс), не путается на суб-15мс гонках.
    public long Version { get; private set; }

    public DirtyEntityRecord(TKey key, TEntity entity, IEnumerable<string>? initialFields = null)
    {
        Key = key;
        Entity = entity;
        DirtyFields = initialFields != null ? new HashSet<string>(initialFields, StringComparer.OrdinalIgnoreCase) : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        FirstDirtiedUtc = DateTime.UtcNow;
        LastDirtiedUtc = DateTime.UtcNow;
        Version = 1;
    }

    public void UpdateEntity(TEntity entity)
    {
        Entity = entity;
        LastDirtiedUtc = DateTime.UtcNow;
        Version++;
    }

    public void Touch()
    {
        LastDirtiedUtc = DateTime.UtcNow;
        Version++;
    }

    public void MarkDirty(string field)
    {
        if (!string.IsNullOrWhiteSpace(field))
        {
            DirtyFields.Add(field);
        }
        LastDirtiedUtc = DateTime.UtcNow;
        Version++;
    }
}

/// <summary>
/// Асинхронный буфер отложенной записи (Write-Behind Cache) для защиты MariaDB от пиковых нагрузок и микрофризов.
/// Сохраняет изменения сущностей в оперативной памяти и периодически сбрасывает их в базу пачками (батчами).
/// Гарантирует целостность данных и отсутствие потери изменений при перезагрузке.
/// </summary>
public sealed class WriteBehindQueue<TKey, TEntity> : IDisposable where TKey : notnull
{
    private readonly object _lock = new();
    private readonly Dictionary<TKey, DirtyEntityRecord<TKey, TEntity>> _dirtyEntities = new();
    private readonly Func<IReadOnlyList<DirtyEntityRecord<TKey, TEntity>>, Task<bool>>? _persister;
    private readonly Timer? _flushTimer;
    private bool _isFlushing;
    private bool _isDisposed;

    // Метрики
    private long _totalPersisted;
    private long _failedFlushes;

    public int PendingCount
    {
        get
        {
            lock (_lock) return _dirtyEntities.Count;
        }
    }

    public long TotalPersisted => Interlocked.Read(ref _totalPersisted);
    public long FailedFlushes => Interlocked.Read(ref _failedFlushes);
    public TimeSpan FlushInterval { get; }
    public int MaxBatchSize { get; set; } = 250;

    public event Action<int>? OnFlushSuccess;
    public event Action<Exception>? OnFlushError;

    public WriteBehindQueue(
        Func<IReadOnlyList<DirtyEntityRecord<TKey, TEntity>>, Task<bool>>? persister = null,
        TimeSpan? flushInterval = null)
    {
        _persister = persister;
        FlushInterval = flushInterval ?? TimeSpan.FromSeconds(5);

        if (_persister != null && FlushInterval > TimeSpan.Zero)
        {
            _flushTimer = new Timer(async _ => await AutoFlushSafeAsync(), null, FlushInterval, FlushInterval);
        }
    }

    /// <summary>
    /// Помечает сущность как изменённую (грязную) и обновляет её состояние в кэше.
    /// </summary>
    public void MarkDirty(TKey key, TEntity entity, string? changedField = null)
    {
        lock (_lock)
        {
            if (_dirtyEntities.TryGetValue(key, out var record))
            {
                record.UpdateEntity(entity);
                if (changedField != null)
                {
                    record.MarkDirty(changedField);
                }
                else
                {
                    record.Touch();
                }
            }
            else
            {
                var newRecord = new DirtyEntityRecord<TKey, TEntity>(key, entity);
                if (changedField != null)
                {
                    newRecord.MarkDirty(changedField);
                }
                _dirtyEntities[key] = newRecord;
            }
        }
    }

    /// <summary>
    /// Полностью сбрасывает все накопившиеся изменения во всех пакетах до нуля (для штатного завершения работы сервера).
    /// </summary>
    public async Task<int> FlushAllAsync(Func<IReadOnlyList<DirtyEntityRecord<TKey, TEntity>>, Task<bool>>? customPersister = null)
    {
        int totalFlushed = 0;
        while (PendingCount > 0)
        {
            int flushed = await FlushAsync(customPersister);
            if (flushed == 0) break;
            totalFlushed += flushed;
        }
        return totalFlushed;
    }

    /// <summary>
    /// Принудительно сбрасывает накопившиеся изменения (размером до MaxBatchSize) в хранилище.
    /// </summary>
    public async Task<int> FlushAsync(Func<IReadOnlyList<DirtyEntityRecord<TKey, TEntity>>, Task<bool>>? customPersister = null)
    {
        var persister = customPersister ?? _persister;
        if (persister == null) return 0;

        List<DirtyEntityRecord<TKey, TEntity>> batch;
        Dictionary<TKey, long> batchStamps;

        lock (_lock)
        {
            if (_isFlushing || _dirtyEntities.Count == 0)
            {
                return 0;
            }

            _isFlushing = true;
            batch = _dirtyEntities.Values.Take(MaxBatchSize).ToList();
            // Снимок ВЕРСИЙ на момент батча. Записи в _dirtyEntities — те же объекты,
            // что в batch; при повторном MarkDirty во время async-сброса их Version
            // растёт. Сравнивать надо с этим снимком, иначе current и record — один
            // объект, сравнение всегда истинно, и перегрязнённая запись удаляется ->
            // последнее изменение теряется (не доходит до БД).
            batchStamps = batch.ToDictionary(r => r.Key, r => r.Version);
        }

        try
        {
            bool success = await persister(batch);
            if (success)
            {
                lock (_lock)
                {
                    foreach (var record in batch)
                    {
                        // Удаляем только если сущность не была повторно модифицирована во
                        // время сброса — сравниваем текущую метку со СНИМКОМ на момент батча.
                        if (_dirtyEntities.TryGetValue(record.Key, out var current)
                            && batchStamps.TryGetValue(record.Key, out var stamp)
                            && current.Version <= stamp)
                        {
                            _dirtyEntities.Remove(record.Key);
                        }
                    }
                }

                Interlocked.Add(ref _totalPersisted, batch.Count);
                OnFlushSuccess?.Invoke(batch.Count);
                return batch.Count;
            }
            else
            {
                Interlocked.Increment(ref _failedFlushes);
                return 0;
            }
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _failedFlushes);
            OnFlushError?.Invoke(ex);
            return 0;
        }
        finally
        {
            lock (_lock)
            {
                _isFlushing = false;
            }
        }
    }

    /// <summary>
    /// Мгновенно сохраняет конкретную сущность (например, при выходе игрока с сервера).
    /// </summary>
    public async Task<bool> FlushEntityImmediatelyAsync(TKey key, Func<DirtyEntityRecord<TKey, TEntity>, Task<bool>> immediatePersister)
    {
        DirtyEntityRecord<TKey, TEntity>? target;
        long stamp;

        lock (_lock)
        {
            if (!_dirtyEntities.TryGetValue(key, out target))
            {
                return true; // Не было изменений, сохранять не нужно
            }
            // Снимок версии до persist (target — тот же объект, что в словаре, и мог бы
            // быть перегрязнён во время сохранения; сравнение с самим собой всегда истинно).
            stamp = target.Version;
        }

        try
        {
            bool success = await immediatePersister(target);
            if (success)
            {
                lock (_lock)
                {
                    if (_dirtyEntities.TryGetValue(key, out var current) && current.Version <= stamp)
                    {
                        _dirtyEntities.Remove(key);
                    }
                }
                Interlocked.Increment(ref _totalPersisted);
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _failedFlushes);
            OnFlushError?.Invoke(ex);
            return false;
        }
    }

    private async Task AutoFlushSafeAsync()
    {
        if (_isDisposed) return;
        try
        {
            await FlushAsync();
        }
        catch (Exception ex)
        {
            OnFlushError?.Invoke(ex);
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _flushTimer?.Dispose();
    }
}
