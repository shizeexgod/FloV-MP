using System.Diagnostics;
using AltV.Net;
using FloVMP.Sdk;
public sealed class ProbeA : Resource
{
    long _tick;
    Func<string>? _oldVersion;
    public override void OnStart()
    {
        FloVAsync.Attach();
        Alt.OnAnyResourceStop += r => Alt.Log($"[PROBE] A: OnAnyResourceStop {r.Name}");
        Alt.OnAnyResourceStart += r => Alt.Log($"[PROBE] A: OnAnyResourceStart {r.Name}");
        try { var ok = Alt.Import("probe-b", "add", out Func<int, int, int>? add); Alt.Log($"[PROBE] A OnStart: Import до загрузки B вернул {ok}"); }
        catch (Exception ex) { Alt.Log($"[PROBE] A OnStart: Import до загрузки B бросил {ex.GetType().Name}"); }
    }
    System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();
    double _lastMs, _maxGap; string _phase = "старт"; int _step;
    void Phase(string name) { Alt.Log($"[PROBE] A: окно «{_phase}» — самый длинный промежуток между тиками {_maxGap:0} мс"); _phase = name; _maxGap = 0; }
    public override void OnTick()
    {
        _tick++;
        FloVAsync.Pump();
        var now = _clock.Elapsed.TotalMilliseconds;
        if (_lastMs > 0) _maxGap = Math.Max(_maxGap, now - _lastMs);
        _lastMs = now;
        if (_step == 0 && now > 3000) { _step = 1; Phase("async-вопрос"); Alt.OnServer<long>("probe:answer", t => Alt.Log($"[PROBE] A получил ответ C (тик C={t}) в тике A={_tick}")); Alt.Emit("probe:ask", _tick); }
        if (_step == 1 && now > 4500) { _step = 2; Phase("sync-вопрос"); Alt.Emit("probe:askSync", _tick); }
        if (_step == 2 && now > 6000) { _step = 3; Phase("долгий + остановка C"); Alt.Emit("probe:long", _tick); }
        if (_step == 3 && now > 7000) { _step = 4; Exports24(); }
        if (_step == 4 && now > 11500) { _step = 5; AfterRestart(); }
        if (_tick != 100) return;
        var main = Environment.CurrentManagedThreadId;
        Alt.Log($"[PROBE] A тик {_tick}, поток={main}");
        if (!Alt.Import("probe-b", "add", out Func<int, int, int>? add) || add is null) { Alt.Log("[PROBE] Import add НЕ удался"); return; }
        Alt.Log($"[PROBE] add(2,3) = {add(2, 3)} — результат в том же вызове");
        Alt.Import("probe-b", "where", out Func<long, string>? where);
        Alt.Log($"[PROBE] where: {where!(_tick)} | A поток={main}");
        Alt.Import("probe-b", "echo", out Func<string, string>? echo);
        Alt.Log($"[PROBE] echo = {echo!("привет ✓")}");
        Alt.Import("probe-b", "dbl", out Func<double, double>? dbl);
        Alt.Import("probe-b", "flag", out Func<bool, bool>? flag);
        Alt.Log($"[PROBE] dbl(1.25)={dbl!(1.25)} flag(true)={flag!(true)}");
        Alt.Import("probe-b", "slow", out Func<int>? slow);
        var sw = Stopwatch.StartNew(); var v = slow!(); sw.Stop();
        Alt.Log($"[PROBE] slow() = {v} за {sw.ElapsedMilliseconds} мс (≥50 — вызов синхронный, A ждал B)");
        Alt.Import("probe-b", "boom", out Func<int>? boom);
        try { var r = boom!(); Alt.Log($"[PROBE] boom() вернул {r} без исключения"); }
        catch (Exception ex) { Alt.Log($"[PROBE] boom(): исключение дошло до A: {ex.GetType().Name}: {ex.Message}"); }
        sw.Restart(); long sum = 0; for (var i = 0; i < 10000; i++) sum += add(i, 1); sw.Stop();
        Alt.Log($"[PROBE] 10000 вызовов add: {sw.Elapsed.TotalMilliseconds:0.0} мс ({sw.Elapsed.TotalMilliseconds * 1000 / 10000:0.00} мкс на вызов), сумма {sum}");
        Alt.Emit("probe:ping", _tick);
        Alt.Log($"[PROBE] A отправил Emit в тике {_tick} и продолжает тот же тик");
        try { var missing = Alt.Import("probe-b", "nope", out Func<int>? _); Alt.Log($"[PROBE] Import несуществующего вернул {missing}"); }
        catch (Exception ex) { Alt.Log($"[PROBE] Import несуществующего бросил {ex.GetType().Name}"); }
    }
    void Exports24()
    {
        Try("Call balance(7)", () => FloVExports.Call<int, long>("probe-c", "x.balance", 7).ToString());
        Try("Call version", () => FloVExports.Call<string>("probe-c", "x.version"));
        Try("неверная подпись Call<int>(version)", () => FloVExports.Call<int>("probe-c", "x.version").ToString());
        Try("нет ресурса", () => FloVExports.Call<int, long>("nobody", "x.balance", 1).ToString());
        Try("нет функции", () => FloVExports.Call<int, long>("probe-c", "x.nope", 1).ToString());
        Try("исключение внутри", () => FloVExports.Call<int, int>("probe-c", "x.boom", 1).ToString());
        Alt.Log($"[PROBE] A: Has(x.balance)={FloVExports.Has("probe-c", "x.balance")} Has(nobody)={FloVExports.Has("nobody", "x")}");
        FloVAsync.Run(async ct =>
        {
            var ok = await FloVExports.Call<int, long, Task<bool>>("probe-c", "x.withdrawAsync", 7, 500);
            Alt.Log($"[PROBE] A: withdrawAsync(7,500)={ok}, A в своей очереди={SynchronizationContext.Current == FloVAsync.Queue}, главный={FloVAsync.IsMainThread}");
            await Task.Run(() => { try { FloVExports.Call<string>("probe-c", "x.version"); Alt.Log("[PROBE] A: вызов из фона ПРОШЁЛ"); } catch (FloVExportException ex) { Console.WriteLine("[PROBE] A: вызов из фона — " + ex.Message); } });
        }, "exports");
        Alt.Import("probe-c", "version", out _oldVersion);
        Alt.Log($"[PROBE] A: C.version = {_oldVersion!()}");
        Alt.Import("probe-c", "sum", out Func<int[], int>? sum);
        Alt.Log($"[PROBE] A: C.sum([1,2,3]) = {sum!(new[] { 1, 2, 3 })}");
        Alt.Import("probe-c", "names", out Func<string[]>? names);
        Alt.Log($"[PROBE] A: C.names() = {string.Join(",", names!())}");
        try { Alt.Import("probe-c", "version", out Func<int>? wrong); Alt.Log($"[PROBE] A: неверная сигнатура — Import прошёл, вызов: {wrong!()}"); }
        catch (Exception ex) { Alt.Log($"[PROBE] A: неверная сигнатура — {ex.GetType().Name}: {ex.Message}"); }
        Alt.Import("probe-c", "bankAsync", out Func<int, Task<int>>? bank);
        FloVAsync.Run(async ct =>
        {
            Alt.Log($"[PROBE] A: зову C.bankAsync(4), поток={Environment.CurrentManagedThreadId}");
            var r = await bank!(4);
            Alt.Log($"[PROBE] A: C.bankAsync(4) = {r}, поток={Environment.CurrentManagedThreadId}, главный A={FloVAsync.IsMainThread}");
        }, "bank");
    }
    static void Try(string what, Func<string> f)
    {
        try { Alt.Log($"[PROBE] A: {what} = {f()}"); }
        catch (FloVExportException ex) { Alt.Log($"[PROBE] A: {what} — FloVExportException: {ex.Message}"); }
        catch (Exception ex) { Alt.Log($"[PROBE] A: {what} — НЕОЖИДАННО {ex.GetType().Name}: {ex.Message}"); }
    }
    void AfterRestart()
    {
        Try("FloVExports version после перезапуска C", () => FloVExports.Call<string>("probe-c", "x.version"));
        try { Alt.Log($"[PROBE] A: старый импорт после перезапуска C вернул {_oldVersion!()}"); }
        catch (Exception ex) { Alt.Log($"[PROBE] A: старый импорт после перезапуска C — {ex.GetType().Name}: {ex.Message}"); }
        try { Alt.Import("probe-c", "version", out Func<string>? fresh); Alt.Log($"[PROBE] A: новый импорт — {fresh!()}"); }
        catch (Exception ex) { Alt.Log($"[PROBE] A: новый импорт — {ex.GetType().Name}"); }
    }
    public override void OnStop() { FloVAsync.Stop(); }
}
