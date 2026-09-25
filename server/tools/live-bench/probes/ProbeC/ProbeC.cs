using AltV.Net;
using FloVMP.Sdk;
public sealed class ProbeC : Resource
{
    public static long Tick;
    public override void OnStart()
    {
        FloVAsync.Attach();
        FloVAsync.OnServerAsync<long>("probe:ask", async (aTick, ct) =>
        {
            Alt.Log($"[PROBE] C async: вызван из тика A={aTick}, тик C={Tick}, поток={Environment.CurrentManagedThreadId}");
            await Task.Run(() => Thread.Sleep(300), ct);                 // «запрос в базу» 300 мс
            Alt.Log($"[PROBE] C async: после await тик C={Tick}, поток={Environment.CurrentManagedThreadId}, главный={FloVAsync.IsMainThread}");
            Alt.Emit("probe:answer", Tick);                             // API движка после await
        });
        Alt.OnServer<long>("probe:askSync", aTick =>
        {
            Alt.Log($"[PROBE] C sync: блокирую главный поток на 300 мс (тик C={Tick})");
            Thread.Sleep(300);
        });
        FloVAsync.OnServerAsync<long>("probe:long", async (aTick, ct) =>
        {
            Alt.Log("[PROBE] C long: жду 5 с, токен остановки передан");
            try { await Task.Delay(5000, ct); Alt.Log("[PROBE] C long: ДОЖДАЛСЯ — ресурс не остановился?"); }
            finally { Alt.Log($"[PROBE] C long: finally, токен отменён={ct.IsCancellationRequested}"); }
        });
        FloVAsync.OnServerAsync("flovmp:platform:ready", async ct =>
        {
            await Task.Delay(50, ct);
            Alt.Log($"[PROBE] C: событие платформы flovmp:platform:ready обработано асинхронно, поток={Environment.CurrentManagedThreadId}");
        });
        var instance = Guid.NewGuid().ToString("N")[..8];
        Alt.Export("version", (Func<string>)(() => instance));
        Alt.Export("sum", (Func<int[], int>)(a => a.Sum()));
        Alt.Export("names", (Func<string[]>)(() => new[] { "один", "два" }));
        Alt.Export("bankAsync", (Func<int, Task<int>>)(async x =>
        {
            Alt.Log($"[PROBE] C bankAsync: начало, поток={Environment.CurrentManagedThreadId}, главный C={FloVAsync.IsMainThread}");
            await Task.Run(() => Thread.Sleep(200));
            Alt.Log($"[PROBE] C bankAsync: после await, поток={Environment.CurrentManagedThreadId}, главный C={FloVAsync.IsMainThread}");
            return x * 10;
        }));
        FloVExports.Provide<string>("x.version", () => instance);
        FloVExports.Provide<int, long>("x.balance", id => id * 100L);
        FloVExports.Provide<int, int>("x.boom", _ => throw new InvalidOperationException("касса пуста"));
        FloVExports.Provide<int, long, Task<bool>>("x.withdrawAsync", async (id, sum) =>
        {
            var own = SynchronizationContext.Current == FloVAsync.Queue;
            await Task.Run(() => Thread.Sleep(150));
            Alt.Log($"[PROBE] C withdrawAsync: до await в своей очереди={own}, после — своя очередь={SynchronizationContext.Current == FloVAsync.Queue}, главный={FloVAsync.IsMainThread}");
            return sum <= id * 100L;
        });
        Alt.Log($"[PROBE] C запущен, экземпляр {instance}, поток={Environment.CurrentManagedThreadId}");
    }
    public override void OnTick() { Tick++; FloVAsync.Pump(); }
    public override void OnStop() { var before = FloVAsync.Queue.InFlight; var sw = System.Diagnostics.Stopwatch.StartNew(); FloVAsync.Stop(); Alt.Log($"[PROBE] C InFlight до={before} после={FloVAsync.Queue.InFlight} очередь={FloVAsync.Queue.Pending}"); Alt.Log($"[PROBE] C OnStop: FloVAsync.Stop() занял {sw.ElapsedMilliseconds} мс"); }
}
