using AltV.Net;
using FloVMP.Core.Async;

namespace FloVMP.Sdk;

/// <summary>
/// Асинхронный код в ресурсе геймода (пункт 23 roadmap).
///
/// <code>
/// public override void OnStart()
/// {
///     FloVAsync.Attach();                         // один раз, первой строкой
///     FloVAsync.OnServerAsync&lt;int, string&gt;("flovmp:native:command", async (id, cmd, args, ct) =&gt;
///     {
///         var money = await db.GetMoneyAsync(id, ct); // запрос в фоне — тик не стоит
///         Alt.Emit("flovmp:native:chat", id, $"Баланс: {money}"); // снова главный поток
///     });
/// }
/// public override void OnTick() =&gt; FloVAsync.Pump();
/// public override void OnStop() =&gt; FloVAsync.Stop();
/// </code>
///
/// После <c>await</c> код продолжается в главном потоке сервера в одном из
/// следующих тиков — API движка можно звать как обычно. Токен <c>ct</c>
/// отменяется при остановке ресурса: передавайте его в запросы к базе.
/// Ошибка в обработчике пишется в журнал и не роняет сервер.
///
/// Не используйте <c>ConfigureAwait(false)</c> перед вызовами API движка:
/// тогда продолжение пойдёт в фоновом потоке, а движок так нельзя.
/// </summary>
public static class FloVAsync
{
    /// <summary>Очередь главного потока этого ресурса (у каждого ресурса своя копия SDK).</summary>
    public static MainThreadQueue Queue { get; } = new(Alt.LogWarning);

    /// <summary>Отменяется при остановке ресурса.</summary>
    public static CancellationToken Stopping => Queue.Stopping;

    /// <summary>Из OnStart: запомнить главный поток.</summary>
    public static void Attach() => Queue.BindToCurrentThread();

    /// <summary>Из OnTick ресурса — обязательно, иначе продолжения после await не выполнятся.</summary>
    public static void Pump() => Queue.Pump();

    /// <summary>Из OnStop ресурса: отменить работу и выбросить неразобранное.</summary>
    public static void Stop() => Queue.Stop();

    /// <summary>Запустить асинхронную работу из главного потока (например, из OnStart).</summary>
    public static void Run(Func<CancellationToken, Task> work, string what = "фоновая работа") => Queue.Start(work, what);

    /// <summary>Из фонового потока: выполнить в главном и дождаться результата.</summary>
    public static Task<T> OnMainThread<T>(Func<T> func) => Queue.Invoke(func);

    /// <summary><c>await FloVAsync.NextTick()</c> — продолжить в следующем тике.</summary>
    public static Task NextTick() => Queue.NextTick();

    /// <summary>Сейчас главный поток ресурса: API движка звать можно.</summary>
    public static bool IsMainThread => Queue.IsMainThread;

    /// <summary>Асинхронный обработчик события сервера (0 арг.): после await — снова в главном потоке.</summary>
    public static void OnServerAsync(string eventName, Func<CancellationToken, Task> handler) =>
        Alt.OnServer(eventName, () => Queue.Start(ct => handler(ct), eventName));

    /// <summary>Асинхронный обработчик события сервера (1 арг.): после await — снова в главном потоке.</summary>
    public static void OnServerAsync<T1>(string eventName, Func<T1, CancellationToken, Task> handler) =>
        Alt.OnServer<T1>(eventName, a1 => Queue.Start(ct => handler(a1, ct), eventName));

    /// <summary>Асинхронный обработчик события сервера (2 арг.): после await — снова в главном потоке.</summary>
    public static void OnServerAsync<T1, T2>(string eventName, Func<T1, T2, CancellationToken, Task> handler) =>
        Alt.OnServer<T1, T2>(eventName, (a1, a2) => Queue.Start(ct => handler(a1, a2, ct), eventName));

    /// <summary>Асинхронный обработчик события сервера (3 арг.): после await — снова в главном потоке.</summary>
    public static void OnServerAsync<T1, T2, T3>(string eventName, Func<T1, T2, T3, CancellationToken, Task> handler) =>
        Alt.OnServer<T1, T2, T3>(eventName, (a1, a2, a3) => Queue.Start(ct => handler(a1, a2, a3, ct), eventName));

    /// <summary>Асинхронный обработчик события сервера (4 арг.): после await — снова в главном потоке.</summary>
    public static void OnServerAsync<T1, T2, T3, T4>(string eventName, Func<T1, T2, T3, T4, CancellationToken, Task> handler) =>
        Alt.OnServer<T1, T2, T3, T4>(eventName, (a1, a2, a3, a4) => Queue.Start(ct => handler(a1, a2, a3, a4, ct), eventName));

    /// <summary>Асинхронный обработчик события сервера (5 арг.): после await — снова в главном потоке.</summary>
    public static void OnServerAsync<T1, T2, T3, T4, T5>(string eventName, Func<T1, T2, T3, T4, T5, CancellationToken, Task> handler) =>
        Alt.OnServer<T1, T2, T3, T4, T5>(eventName, (a1, a2, a3, a4, a5) => Queue.Start(ct => handler(a1, a2, a3, a4, a5, ct), eventName));

    /// <summary>Асинхронный обработчик события сервера (6 арг.): после await — снова в главном потоке.</summary>
    public static void OnServerAsync<T1, T2, T3, T4, T5, T6>(string eventName, Func<T1, T2, T3, T4, T5, T6, CancellationToken, Task> handler) =>
        Alt.OnServer<T1, T2, T3, T4, T5, T6>(eventName, (a1, a2, a3, a4, a5, a6) => Queue.Start(ct => handler(a1, a2, a3, a4, a5, a6, ct), eventName));
}
