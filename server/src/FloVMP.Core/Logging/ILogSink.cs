namespace FloVMP.Core.Logging;

/// <summary>
/// Приёмник логов. Реализация ОБЯЗАНА быть неблокирующей на <see cref="Write"/>
/// (гейм-тик не должен ждать диск/сеть) и потокобезопасной.
/// </summary>
public interface ILogSink : IAsyncDisposable
{
    /// <summary>Поставить запись в очередь. Не бросает, не блокирует.</summary>
    void Write(LogEntry entry);

    /// <summary>Дождаться, пока всё в очереди записано (автосейв/выключение).</summary>
    Task FlushAsync(CancellationToken ct = default);
}
