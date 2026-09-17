namespace FloVMP.Core.Logging;

/// <summary>
/// Единая точка логирования для всех систем. Вызовы
/// однотипны: <c>GameLog.Admin(...)</c>, <c>GameLog.System(...)</c> и т.д.
///
/// До <see cref="Configure"/> — тихий no-op (безопасно вызывать из тестов
/// и до инициализации).
/// </summary>
public static class GameLog
{
    private static ILogSink? _sink;

    public static void Configure(ILogSink sink) => _sink = sink;

    public static async Task ShutdownAsync()
    {
        var s = _sink;
        _sink = null;
        if (s is null) return;
        try { await s.FlushAsync().ConfigureAwait(false); } catch { }
        await s.DisposeAsync().ConfigureAwait(false);
    }

    public static Task FlushAsync() => _sink?.FlushAsync() ?? Task.CompletedTask;

    // --- общий примитив ------------------------------------------------

    public static void Write(
        string category, string action, LogActor actor,
        string target = "", IReadOnlyDictionary<string, object?>? details = null, string ip = "")
    {
        var sink = _sink;
        if (sink is null) return;

        sink.Write(new LogEntry
        {
            Category = category,
            Action = action,
            Actor = actor,
            Target = target,
            Details = details ?? new Dictionary<string, object?>(),
            Ip = ip,
        });
    }

    private static Dictionary<string, object?> D(params (string k, object? v)[] pairs)
    {
        var d = new Dictionary<string, object?>(pairs.Length);
        foreach (var (k, v) in pairs) d[k] = v;
        return d;
    }

    // --- категорийные помощники ---

    public static void Account(string action, LogActor actor, string ip = "", params (string, object?)[] details) =>
        Write(LogCategory.Account, action, actor, actor.AccountId.ToString(), D(details), ip);

    public static void Admin(string action, LogActor admin, string target, params (string, object?)[] details) =>
        Write(LogCategory.Admin, action, admin, target, D(details));

    public static void Punishment(string action, LogActor admin, string target, string reason, long? seconds = null) =>
        Write(LogCategory.Punishment, action, admin, target,
            D(("reason", reason), ("seconds", seconds)));

    public static void System(string action, params (string, object?)[] details) =>
        Write(LogCategory.System, action, LogActor.SystemActor, "", D(details));
}
