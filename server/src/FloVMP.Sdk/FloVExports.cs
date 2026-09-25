using AltV.Net;
using FloVMP.Core.Resources;

namespace FloVMP.Sdk;

/// <summary>Вызов экспорта другого ресурса не удался: понятная причина вместо падения сервера.</summary>
public sealed class FloVExportException : Exception
{
    public FloVExportException(string message, Exception? inner = null) : base(message, inner) { }
}

/// <summary>
/// Межресурсный слой (пункт 24 roadmap): функции одного ресурса, которые
/// другой вызывает и сразу получает результат.
///
/// <code>
/// // ресурс «bank»:
/// FloVExports.Provide&lt;int, long&gt;("balance", playerId =&gt; Balance(playerId));
/// FloVExports.Provide&lt;int, long, Task&lt;bool&gt;&gt;("withdrawAsync", WithdrawAsync);   // с базой
///
/// // ресурс «jobs»:
/// var money = FloVExports.Call&lt;int, long&gt;("bank", "balance", playerId);
/// var ok = await FloVExports.Call&lt;int, long, Task&lt;bool&gt;&gt;("bank", "withdrawAsync", playerId, 500);
/// </code>
///
/// Построено на штатных Alt.Export/Alt.Import, а не на Alt.Emit: живой
/// прогон двух ресурсов показал, что экспорты между C#-ресурсами синхронны
/// (результат — в том же вызове, в том же тике), а события доставляются
/// только после тика отправителя. Сверх штатного:
///   • подпись сверяется до вызова — Alt.Import молча приводит значения и
///     падает при вызове случайным FormatException;
///   • после перезапуска ресурса-экспортёра вызов идёт в его новый экземпляр —
///     штатный старый импорт продолжал звать код прежнего (проверено);
///   • функция экспортёра выполняется в его собственном контексте: её await
///     продолжается в очереди экспортёра, а не вызывающего — иначе остановка
///     вызывающего обрывала бы чужую работу;
///   • «нет ресурса», «нет функции», исключение внутри — одно
///     FloVExportException, а не InvalidImportException, который,
///     непойманный в OnStart, роняет весь сервер (проверено).
///
/// Типы: числа, bool, string, их одномерные массивы и Task&lt;…&gt; из них.
/// Звать — из главного потока сервера.
/// </summary>
public static class FloVExports
{
    private delegate bool Importer<TDel>(string resource, string key, out TDel? value);

    private static readonly Dictionary<(string Resource, string Name), Delegate> Cache = new();
    private static bool _subscribed;

    private static void Publish(string name, Type delegateType)
    {
        if (!ExportSignature.ValidName(name)) throw new ArgumentException("имя экспорта: латиница, цифры, _ . - (до 64): " + name);
        Alt.Export(ExportSignature.KeyFor(name), ExportSignature.Of(delegateType));
    }

    /// <summary>Выполнить функцию экспортёра в контексте его ресурса.</summary>
    private static T Own<T>(Func<T> body)
    {
        var queue = FloVAsync.Queue;
        if (!queue.Bound) return body();       // ресурс без FloVAsync.Attach — async в нём не разбирается
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(queue);
        try { return body(); }
        finally { SynchronizationContext.SetSynchronizationContext(previous); }
    }

    private static void Subscribe()
    {
        if (_subscribed) return;
        _subscribed = true;
        // Перезапуск экспортёра — новый экземпляр: старые делегаты не годятся.
        Alt.OnAnyResourceStop += r => Forget(r.Name);
        Alt.OnAnyResourceStart += r => Forget(r.Name);
    }

    private static void Forget(string resource)
    {
        foreach (var key in Cache.Keys.Where(k => k.Resource == resource).ToList()) Cache.Remove(key);
    }

    private static TDel Resolve<TDel>(string resource, string name, Importer<TDel> import) where TDel : Delegate
    {
        if (FloVAsync.Queue.Bound && !FloVAsync.IsMainThread)
            throw new FloVExportException($"{resource}.{name}: экспорты зовутся только из главного потока (после await без ConfigureAwait(false))");
        Subscribe();
        if (Cache.TryGetValue((resource, name), out var cached) && cached is TDel ready) return ready;
        var expected = ExportSignature.Of(typeof(TDel));
        string? actual = null;
        try { if (Alt.Import(resource, ExportSignature.KeyFor(name), out string sig)) actual = sig; }
        catch (Exception) { actual = null; }
        if (actual != expected) throw new FloVExportException(ExportSignature.Describe(resource, name, expected, actual));
        TDel? d;
        try { if (!import(resource, name, out d) || d is null) throw new FloVExportException($"{resource}.{name}: экспорт не найден"); }
        catch (FloVExportException) { throw; }
        catch (Exception ex) { throw new FloVExportException($"{resource}.{name}: не импортируется ({ex.Message})", ex); }
        Cache[(resource, name)] = d;
        return d;
    }

    private static T Guard<T>(string resource, string name, Func<T> call)
    {
        try { return call(); }
        catch (FloVExportException) { throw; }
        catch (Exception ex) { throw new FloVExportException($"ошибка внутри {resource}.{name}: {ex.Message}", ex); }
    }

    /// <summary>Есть ли у ресурса такой экспорт (и запущен ли он).</summary>
    public static bool Has(string resource, string name)
    {
        try { return Alt.Import(resource, ExportSignature.KeyFor(name), out string _); }
        catch { return false; }
    }

    /// <summary>Экспортировать функцию с результатом (0 арг.).</summary>
    public static void Provide<TResult>(string name, Func<TResult> func)
    {
        Publish(name, typeof(Func<TResult>));
        Alt.Export(name, (Func<TResult>)(() => Own(() => func())));
    }

    /// <summary>Экспортировать действие без результата (0 арг.).</summary>
    public static void ProvideAction(string name, Action action)
    {
        Publish(name, typeof(Action));
        Alt.Export(name, (Action)(() => Own(() => { action(); return true; })));
    }

    /// <summary>Экспортировать функцию с результатом (1 арг.).</summary>
    public static void Provide<T1, TResult>(string name, Func<T1, TResult> func)
    {
        Publish(name, typeof(Func<T1, TResult>));
        Alt.Export(name, (Func<T1, TResult>)((a1) => Own(() => func(a1))));
    }

    /// <summary>Экспортировать действие без результата (1 арг.).</summary>
    public static void ProvideAction<T1>(string name, Action<T1> action)
    {
        Publish(name, typeof(Action<T1>));
        Alt.Export(name, (Action<T1>)((a1) => Own(() => { action(a1); return true; })));
    }

    /// <summary>Экспортировать функцию с результатом (2 арг.).</summary>
    public static void Provide<T1, T2, TResult>(string name, Func<T1, T2, TResult> func)
    {
        Publish(name, typeof(Func<T1, T2, TResult>));
        Alt.Export(name, (Func<T1, T2, TResult>)((a1, a2) => Own(() => func(a1, a2))));
    }

    /// <summary>Экспортировать действие без результата (2 арг.).</summary>
    public static void ProvideAction<T1, T2>(string name, Action<T1, T2> action)
    {
        Publish(name, typeof(Action<T1, T2>));
        Alt.Export(name, (Action<T1, T2>)((a1, a2) => Own(() => { action(a1, a2); return true; })));
    }

    /// <summary>Экспортировать функцию с результатом (3 арг.).</summary>
    public static void Provide<T1, T2, T3, TResult>(string name, Func<T1, T2, T3, TResult> func)
    {
        Publish(name, typeof(Func<T1, T2, T3, TResult>));
        Alt.Export(name, (Func<T1, T2, T3, TResult>)((a1, a2, a3) => Own(() => func(a1, a2, a3))));
    }

    /// <summary>Экспортировать действие без результата (3 арг.).</summary>
    public static void ProvideAction<T1, T2, T3>(string name, Action<T1, T2, T3> action)
    {
        Publish(name, typeof(Action<T1, T2, T3>));
        Alt.Export(name, (Action<T1, T2, T3>)((a1, a2, a3) => Own(() => { action(a1, a2, a3); return true; })));
    }

    /// <summary>Экспортировать функцию с результатом (4 арг.).</summary>
    public static void Provide<T1, T2, T3, T4, TResult>(string name, Func<T1, T2, T3, T4, TResult> func)
    {
        Publish(name, typeof(Func<T1, T2, T3, T4, TResult>));
        Alt.Export(name, (Func<T1, T2, T3, T4, TResult>)((a1, a2, a3, a4) => Own(() => func(a1, a2, a3, a4))));
    }

    /// <summary>Экспортировать действие без результата (4 арг.).</summary>
    public static void ProvideAction<T1, T2, T3, T4>(string name, Action<T1, T2, T3, T4> action)
    {
        Publish(name, typeof(Action<T1, T2, T3, T4>));
        Alt.Export(name, (Action<T1, T2, T3, T4>)((a1, a2, a3, a4) => Own(() => { action(a1, a2, a3, a4); return true; })));
    }

    /// <summary>Экспортировать функцию с результатом (5 арг.).</summary>
    public static void Provide<T1, T2, T3, T4, T5, TResult>(string name, Func<T1, T2, T3, T4, T5, TResult> func)
    {
        Publish(name, typeof(Func<T1, T2, T3, T4, T5, TResult>));
        Alt.Export(name, (Func<T1, T2, T3, T4, T5, TResult>)((a1, a2, a3, a4, a5) => Own(() => func(a1, a2, a3, a4, a5))));
    }

    /// <summary>Экспортировать действие без результата (5 арг.).</summary>
    public static void ProvideAction<T1, T2, T3, T4, T5>(string name, Action<T1, T2, T3, T4, T5> action)
    {
        Publish(name, typeof(Action<T1, T2, T3, T4, T5>));
        Alt.Export(name, (Action<T1, T2, T3, T4, T5>)((a1, a2, a3, a4, a5) => Own(() => { action(a1, a2, a3, a4, a5); return true; })));
    }

    /// <summary>Экспортировать функцию с результатом (6 арг.).</summary>
    public static void Provide<T1, T2, T3, T4, T5, T6, TResult>(string name, Func<T1, T2, T3, T4, T5, T6, TResult> func)
    {
        Publish(name, typeof(Func<T1, T2, T3, T4, T5, T6, TResult>));
        Alt.Export(name, (Func<T1, T2, T3, T4, T5, T6, TResult>)((a1, a2, a3, a4, a5, a6) => Own(() => func(a1, a2, a3, a4, a5, a6))));
    }

    /// <summary>Экспортировать действие без результата (6 арг.).</summary>
    public static void ProvideAction<T1, T2, T3, T4, T5, T6>(string name, Action<T1, T2, T3, T4, T5, T6> action)
    {
        Publish(name, typeof(Action<T1, T2, T3, T4, T5, T6>));
        Alt.Export(name, (Action<T1, T2, T3, T4, T5, T6>)((a1, a2, a3, a4, a5, a6) => Own(() => { action(a1, a2, a3, a4, a5, a6); return true; })));
    }

    /// <summary>Вызвать функцию другого ресурса (0 арг.) и получить результат в том же вызове.</summary>
    public static TResult Call<TResult>(string resource, string name) =>
        Guard(resource, name, () => Resolve<Func<TResult>>(resource, name, (string r, string k, out Func<TResult>? d) => Alt.Import(r, k, out d))());

    /// <summary>Вызвать действие другого ресурса (0 арг.).</summary>
    public static void Invoke(string resource, string name) =>
        Guard(resource, name, () => { Resolve<Action>(resource, name, (string r, string k, out Action? d) => Alt.Import(r, k, out d))(); return true; });

    /// <summary>Вызвать функцию другого ресурса (1 арг.) и получить результат в том же вызове.</summary>
    public static TResult Call<T1, TResult>(string resource, string name, T1 a1) =>
        Guard(resource, name, () => Resolve<Func<T1, TResult>>(resource, name, (string r, string k, out Func<T1, TResult>? d) => Alt.Import(r, k, out d))(a1));

    /// <summary>Вызвать действие другого ресурса (1 арг.).</summary>
    public static void Invoke<T1>(string resource, string name, T1 a1) =>
        Guard(resource, name, () => { Resolve<Action<T1>>(resource, name, (string r, string k, out Action<T1>? d) => Alt.Import(r, k, out d))(a1); return true; });

    /// <summary>Вызвать функцию другого ресурса (2 арг.) и получить результат в том же вызове.</summary>
    public static TResult Call<T1, T2, TResult>(string resource, string name, T1 a1, T2 a2) =>
        Guard(resource, name, () => Resolve<Func<T1, T2, TResult>>(resource, name, (string r, string k, out Func<T1, T2, TResult>? d) => Alt.Import(r, k, out d))(a1, a2));

    /// <summary>Вызвать действие другого ресурса (2 арг.).</summary>
    public static void Invoke<T1, T2>(string resource, string name, T1 a1, T2 a2) =>
        Guard(resource, name, () => { Resolve<Action<T1, T2>>(resource, name, (string r, string k, out Action<T1, T2>? d) => Alt.Import(r, k, out d))(a1, a2); return true; });

    /// <summary>Вызвать функцию другого ресурса (3 арг.) и получить результат в том же вызове.</summary>
    public static TResult Call<T1, T2, T3, TResult>(string resource, string name, T1 a1, T2 a2, T3 a3) =>
        Guard(resource, name, () => Resolve<Func<T1, T2, T3, TResult>>(resource, name, (string r, string k, out Func<T1, T2, T3, TResult>? d) => Alt.Import(r, k, out d))(a1, a2, a3));

    /// <summary>Вызвать действие другого ресурса (3 арг.).</summary>
    public static void Invoke<T1, T2, T3>(string resource, string name, T1 a1, T2 a2, T3 a3) =>
        Guard(resource, name, () => { Resolve<Action<T1, T2, T3>>(resource, name, (string r, string k, out Action<T1, T2, T3>? d) => Alt.Import(r, k, out d))(a1, a2, a3); return true; });

    /// <summary>Вызвать функцию другого ресурса (4 арг.) и получить результат в том же вызове.</summary>
    public static TResult Call<T1, T2, T3, T4, TResult>(string resource, string name, T1 a1, T2 a2, T3 a3, T4 a4) =>
        Guard(resource, name, () => Resolve<Func<T1, T2, T3, T4, TResult>>(resource, name, (string r, string k, out Func<T1, T2, T3, T4, TResult>? d) => Alt.Import(r, k, out d))(a1, a2, a3, a4));

    /// <summary>Вызвать действие другого ресурса (4 арг.).</summary>
    public static void Invoke<T1, T2, T3, T4>(string resource, string name, T1 a1, T2 a2, T3 a3, T4 a4) =>
        Guard(resource, name, () => { Resolve<Action<T1, T2, T3, T4>>(resource, name, (string r, string k, out Action<T1, T2, T3, T4>? d) => Alt.Import(r, k, out d))(a1, a2, a3, a4); return true; });

    /// <summary>Вызвать функцию другого ресурса (5 арг.) и получить результат в том же вызове.</summary>
    public static TResult Call<T1, T2, T3, T4, T5, TResult>(string resource, string name, T1 a1, T2 a2, T3 a3, T4 a4, T5 a5) =>
        Guard(resource, name, () => Resolve<Func<T1, T2, T3, T4, T5, TResult>>(resource, name, (string r, string k, out Func<T1, T2, T3, T4, T5, TResult>? d) => Alt.Import(r, k, out d))(a1, a2, a3, a4, a5));

    /// <summary>Вызвать действие другого ресурса (5 арг.).</summary>
    public static void Invoke<T1, T2, T3, T4, T5>(string resource, string name, T1 a1, T2 a2, T3 a3, T4 a4, T5 a5) =>
        Guard(resource, name, () => { Resolve<Action<T1, T2, T3, T4, T5>>(resource, name, (string r, string k, out Action<T1, T2, T3, T4, T5>? d) => Alt.Import(r, k, out d))(a1, a2, a3, a4, a5); return true; });

    /// <summary>Вызвать функцию другого ресурса (6 арг.) и получить результат в том же вызове.</summary>
    public static TResult Call<T1, T2, T3, T4, T5, T6, TResult>(string resource, string name, T1 a1, T2 a2, T3 a3, T4 a4, T5 a5, T6 a6) =>
        Guard(resource, name, () => Resolve<Func<T1, T2, T3, T4, T5, T6, TResult>>(resource, name, (string r, string k, out Func<T1, T2, T3, T4, T5, T6, TResult>? d) => Alt.Import(r, k, out d))(a1, a2, a3, a4, a5, a6));

    /// <summary>Вызвать действие другого ресурса (6 арг.).</summary>
    public static void Invoke<T1, T2, T3, T4, T5, T6>(string resource, string name, T1 a1, T2 a2, T3 a3, T4 a4, T5 a5, T6 a6) =>
        Guard(resource, name, () => { Resolve<Action<T1, T2, T3, T4, T5, T6>>(resource, name, (string r, string k, out Action<T1, T2, T3, T4, T5, T6>? d) => Alt.Import(r, k, out d))(a1, a2, a3, a4, a5, a6); return true; });
}
