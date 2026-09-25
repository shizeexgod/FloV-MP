using AltV.Net;
public sealed class ProbeB : Resource
{
    public static long Tick;
    public override void OnStart()
    {
        Alt.Export("add", (Func<int, int, int>)((x, y) => x + y));
        Alt.Export("echo", (Func<string, string>)(s => "B:" + s));
        Alt.Export("where", (Func<long, string>)(aTick => $"поток={Environment.CurrentManagedThreadId} тикB={Tick} тикA={aTick}"));
        Alt.Export("slow", (Func<int>)(() => { Thread.Sleep(50); return 7; }));
        Alt.Export("boom", (Func<int>)(() => throw new InvalidOperationException("исключение в B")));
        Alt.Export("dbl", (Func<double, double>)(d => d * 2));
        Alt.Export("flag", (Func<bool, bool>)(f => !f));
        Alt.OnServer<long>("probe:ping", aTick => Alt.Log($"[PROBE] B получил Emit: отправлен в тике A={aTick}, сейчас тик B={Tick}, поток={Environment.CurrentManagedThreadId}"));
        Alt.Log($"[PROBE] B запущен, поток={Environment.CurrentManagedThreadId}");
    }
    public override void OnTick() { Tick++; }
    public override void OnStop() { }
}
