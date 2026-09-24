using System.Diagnostics;
using System.Net;
using FloVMP.Core.Native;
using FloVMP.Core.Vehicles;

// Стенд: dotnet run --project server/tools/FloVMP.VehicleHarness -- [порт] [ttl_сек]
// Дальше: python native/legacy-3889/client/tools/bot.py --port <порт> --vehicle-test
//
// Повторяет только то, что нужно реестру: вход (WELCOME + SPAWN), позиции из
// STATE, VREQ/VENTER/VLEAVE/VSYNC и рассылку раз в 50 мс, как StarterResource.
// Бан, лицензия, чат, урон здесь не проверяются — для них живой сервер.
var port = args.Length > 0 && int.TryParse(args[0], out var p) ? p : 7798;
var ttlSec = args.Length > 1 && int.TryParse(args[1], out var t) ? t : 300;

var server = new NativeServer(IPAddress.Loopback, port, msg => Console.WriteLine("[шлюз] " + msg));
var host = new HarnessHost(server);
var vehicles = new NativeVehicleService(host);
var clock = Stopwatch.StartNew();
server.Start();
Console.WriteLine($"[стенд] реестр транспорта слушает 127.0.0.1:{port}, брошенные машины — {ttlSec} с");

var recipients = new List<VehiclePlayer>();
while (true)
{
    var now = clock.ElapsedMilliseconds;
    while (server.Events.TryDequeue(out var ev))
    {
        switch (ev)
        {
            case NativeJoined j:
                var s = j.Session;
                host.Registry.Add(s.Id);
                if (NativeVehicleProtocol.SupportsRegistry(s.ClientVersion)) host.RegistryClients.Add(s.Id);
                s.Send("WELCOME", s.Id, s.Name, s.Identity.ToString(), "FloV:MP стенд", "", 0, 0f);
                // Все появляются рядом: сценарию нужно, чтобы игроки видели друг друга.
                s.Send("SPAWN", 200f + s.Id, -900f, 30f, 0f, 0u);
                Console.WriteLine($"[стенд] вошёл [{s.Id}] {s.Name}, клиент {s.ClientVersion}" +
                                  (host.RegistryClients.Contains(s.Id) ? " (реестр)" : " (старый путь)"));
                break;
            case NativeMessage m:
                switch (m.Parts[0])
                {
                    case "VREQ": vehicles.HandleRequest(m.Session.Id, m.Parts, now); break;
                    case "VENTER": vehicles.HandleEnter(m.Session.Id, m.Parts, now); break;
                    case "VLEAVE": vehicles.HandleLeave(m.Session.Id, m.Parts, now); break;
                    case "CHAT" when m.Parts.Length > 1 && m.Parts[1].StartsWith("/car"):
                        var st = m.Session.State;
                        var model = m.Parts[1].Length > 5 ? m.Parts[1][5..] : "adder";
                        vehicles.SpawnForPlayer(m.Session.Id, JoaatHash(model), st.Heading, now);
                        break;
                    case "CHAT" when m.Parts.Length > 1 && m.Parts[1] == "/fix":
                        var mine = vehicles.Registry.VehicleOf(m.Session.Id);
                        if (mine is not null) vehicles.Repair(mine.Id);
                        break;
                }
                break;
            case NativeLeft l:
                vehicles.PlayerLeft(l.Session.Id, now);
                host.Registry.Remove(l.Session.Id);
                host.RegistryClients.Remove(l.Session.Id);
                Console.WriteLine($"[стенд] вышел [{l.Session.Id}] {l.Session.Name}: {l.Reason}");
                break;
        }
    }

    var nowUtc = DateTime.UtcNow;
    recipients.Clear();
    foreach (var id in host.Registry)
    {
        var session = server.Get(id);
        if (session is null) continue;
        if (host.RegistryClients.Contains(id) && session.TryTakeVehicleSync(out var sync))
            vehicles.HandleSync(id, sync, nowUtc);
        if (host.TryGetPlayer(id, out var pl)) recipients.Add(pl);
    }
    vehicles.Replicate(now, recipients, radius: 400f, maxStreamed: 150, abandonedTtlMs: ttlSec * 1000L);
    await Task.Delay(50);
}

// Хэш модели как у игры (joaat): /car adder → 0xB779A091.
static uint JoaatHash(string text)
{
    uint h = 0;
    foreach (var ch in text.ToLowerInvariant())
    {
        h += ch;
        h += h << 10;
        h ^= h >> 6;
    }
    h += h << 3;
    h ^= h >> 11;
    h += h << 15;
    return h;
}

sealed class HarnessHost : IVehicleHost
{
    private readonly NativeServer _server;
    public readonly HashSet<uint> Registry = new();
    public readonly HashSet<uint> RegistryClients = new();

    public HarnessHost(NativeServer server) => _server = server;

    public bool TryGetPlayer(uint id, out VehiclePlayer player)
    {
        player = default;
        var s = _server.Get(id);
        if (s is null) return false;
        var st = s.State;
        player = new VehiclePlayer(id, st.X, st.Y, st.Z, s.Dimension, RegistryClients.Contains(id), s.HasState);
        return true;
    }

    public void Send(uint playerId, string line) => _server.Get(playerId)?.Send(line);
    public void Warn(string message) => Console.WriteLine("[журнал] " + message);
}
