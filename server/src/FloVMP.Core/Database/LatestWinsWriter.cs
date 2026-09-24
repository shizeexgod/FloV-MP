namespace FloVMP.Core.Database;

/// <summary>
/// Запись в хранилище мимо игрового потока, «последнее побеждает» по ключу.
///
/// Сохранения идут из тика (машины при парковке, игроки при выходе), а
/// запрос к базе — десятки миллисекунд: на главном потоке это рывок у всех.
/// Здесь по каждому ключу в очереди лежит только последняя запись: если база
/// легла, в памяти остаётся свежее состояние и уходит, как только база
/// вернётся, — а не пачка устаревших. Порядок записей по разным ключам не
/// гарантируется и не нужен: каждая запись самодостаточна.
/// </summary>
public sealed class LatestWinsWriter<TKey> : IDisposable where TKey : notnull
{
    private readonly Action<string> _warn;
    private readonly string _what;
    private readonly object _lock = new();
    private readonly Dictionary<TKey, Action> _pending = new();
    private readonly AutoResetEvent _signal = new(false);
    private readonly Thread _worker;
    private volatile bool _stopping;
    private volatile bool _writing;
    private long _lastWarnMs;

    public LatestWinsWriter(string threadName, string what, Action<string> warn)
    {
        _what = what;
        _warn = warn;
        _worker = new Thread(Run) { IsBackground = true, Name = threadName };
        _worker.Start();
    }

    /// <summary>Сколько записей ещё не выполнено.</summary>
    public int Pending { get { lock (_lock) return _pending.Count; } }

    /// <summary>Поставить запись; прежняя невыполненная по этому ключу отменяется.</summary>
    public void Enqueue(TKey key, Action write)
    {
        lock (_lock) _pending[key] = write;
        _signal.Set();
    }

    /// <summary>Дождаться выполнения всего накопленного (остановка сервера).</summary>
    public bool FlushBlocking(TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            _signal.Set();
            if (Pending == 0 && !_writing) return true;
            Thread.Sleep(20);
        }
        return Pending == 0;
    }

    private void Run()
    {
        while (!_stopping)
        {
            _signal.WaitOne(1000);
            List<KeyValuePair<TKey, Action>> batch;
            lock (_lock)
            {
                if (_pending.Count == 0) continue;
                batch = _pending.ToList();
                _pending.Clear();
                _writing = true;
            }
            try
            {
                for (var i = 0; i < batch.Count; i++)
                {
                    try { batch[i].Value(); }
                    catch (Exception ex)
                    {
                        // Вернуть эту и все оставшиеся, если по ним не пришло более свежее.
                        lock (_lock)
                            for (var j = i; j < batch.Count; j++) _pending.TryAdd(batch[j].Key, batch[j].Value);
                        var now = Environment.TickCount64;
                        if (now - _lastWarnMs > 30_000)
                        {
                            _lastWarnMs = now;
                            _warn($"[FloV:MP] {_what} не сохранены ({ex.Message}) — повторю, когда хранилище ответит.");
                        }
                        Thread.Sleep(1000);
                        break;
                    }
                }
            }
            finally { _writing = false; }
        }
    }

    public void Dispose()
    {
        FlushBlocking(TimeSpan.FromSeconds(5));
        _stopping = true;
        _signal.Set();
        _worker.Join(2000);
    }
}
