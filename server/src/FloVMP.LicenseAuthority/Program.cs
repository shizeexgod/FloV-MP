using System.Globalization;
using System.Security.Cryptography;
using FloVMP.LicenseAuthority;

// flovmp-license — сервер лицензий FloV:MP и команды владельца платформы.
// Настройки: /etc/flovmp-license/license.env (или FLOVMP_LICENSE_CONFIG):
//   FLOVMP_LA_DB=Server=127.0.0.1;Database=flovmp_licensing;User ID=...;Password=...
//   FLOVMP_LA_KEY=/etc/flovmp-license/authority.pem   (закрытый ключ подписи)
//   FLOVMP_LA_DATA=/var/lib/flovmp-license            (релизы для раздачи)
//   FLOVMP_LA_LISTEN=http://127.0.0.1:7799/

Console.OutputEncoding = System.Text.Encoding.UTF8;
try { return Cli.Run(args); }
catch (CliError e) { Console.Error.WriteLine("ОШИБКА: " + e.Message); return 2; }
catch (Exception e) { Console.Error.WriteLine("ОШИБКА: " + e.Message); return 1; }

sealed class CliError(string message) : Exception(message);

static class Cli
{
    const string Help = """
        flovmp-license — сервер лицензий FloV:MP

          new --project "Держава RP" --owner "Иван" [--contact tg:@ivan] [--plan business]
              [--slots 1000] [--servers 1] [--days 365 | --lifetime] [--note "..."]
                                          выдать ключ (статус «выдан»)
          list [--status issued|active|suspended|revoked]
          show <ключ>                     ключ, серверы, последние события
          suspend <ключ> [--reason "..."] приостановить (серверы закроют вход при следующей проверке)
          resume <ключ>                   снять приостановку
          revoke <ключ> [--reason "..."]  отозвать навсегда
          extend <ключ> --days N | --until ГГГГ-ММ-ДД | --lifetime
          set <ключ> [--slots N] [--servers N] [--plan X] [--project X] [--owner X] [--contact X] [--note X]
          unbind <ключ> <ID сервера|all>  отвязать сервер (переезд клиента)
          events [<ключ>] [--limit 50]    журнал
          publish <папка релиза>          опубликовать подписанный релиз для раздачи
          init                            создать таблицы и ключ подписи (один раз)
          pubkey                          открытый ключ подписи (вшивается в сервер)
          serve                           запустить службу
        """;

    public static int Run(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help" or "help") { Console.WriteLine(Help); return 0; }
        var cfg = Config.Load();
        var cmd = args[0];
        var rest = args.Skip(1).ToArray();
        var opts = Options.Parse(rest);

        if (cmd == "init") return Init(cfg);
        if (cmd == "pubkey") { using var k = LoadKey(cfg); Console.WriteLine(k.ExportSubjectPublicKeyInfoPem()); return 0; }

        var store = new MySqlStore(cfg.Db);
        switch (cmd)
        {
            case "serve":
            {
                store.EnsureSchema();
                var service = new LicenseService(store, LoadKey(cfg));
                var api = new HttpApi(service, Path.Combine(cfg.Data, "releases"));
                using var stop = new CancellationTokenSource();
                Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Cancel(); };
                AppDomain.CurrentDomain.ProcessExit += (_, _) => stop.Cancel();
                api.RunAsync(cfg.Listen, stop.Token).GetAwaiter().GetResult();
                return 0;
            }
            case "new":
            {
                var l = new LicenseRecord
                {
                    Key = LicenseService.NewKey(),
                    Project = opts.Required("project"),
                    Owner = opts.Required("owner"),
                    Contact = opts.Get("contact") ?? "",
                    Plan = opts.Get("plan") ?? "business",
                    MaxPlayers = opts.Int("slots") ?? 1000,
                    MaxServers = opts.Int("servers") ?? 1,
                    Status = LicenseStatus.Issued,
                    CreatedAt = DateTime.UtcNow,
                    Note = opts.Get("note") ?? "",
                };
                l.ExpiresAt = Expiry(opts, DateTime.UtcNow) ?? throw new CliError("укажите --days N или --lifetime");
                if (l.ExpiresAt == DateTime.MaxValue) l.ExpiresAt = null;
                Validate(l);
                store.Insert(l);
                Event(store, l, "выдан", $"{l.Plan}, {l.MaxPlayers} слотов, серверов {l.MaxServers}, до {Until(l)}");
                Console.WriteLine();
                Console.WriteLine($"  Ключ: {l.Key}");
                Console.WriteLine();
                Console.WriteLine($"  {l.Project} — {l.Owner}; {l.Plan}, {l.MaxPlayers} слотов, серверов: {l.MaxServers}, срок: {Until(l)}");
                Console.WriteLine("  Отправьте ключ клиенту. Статус — «выдан», станет «активирован» при первом запуске его сервера.");
                return 0;
            }
            case "list":
            {
                var list = store.List(opts.Get("status"));
                if (list.Count == 0) { Console.WriteLine("ключей нет"); return 0; }
                foreach (var l in list)
                {
                    var servers = store.Activations(l.Id).Count;
                    var expired = l.Expired(DateTime.UtcNow) ? " (истёк)" : "";
                    Console.WriteLine($"{l.Key}  {LicenseStatus.Russian(l.Status),-22}{expired}  до {Until(l),-10}  серверов {servers}/{l.MaxServers}  {l.Project} — {l.Owner}");
                }
                return 0;
            }
            case "show":
            {
                var l = Find(store, rest);
                Console.WriteLine($"Ключ:       {l.Key}");
                Console.WriteLine($"Проект:     {l.Project} — {l.Owner} {l.Contact}");
                Console.WriteLine($"Статус:     {LicenseStatus.Russian(l.Status)}{(l.StatusReason.Length > 0 ? " (" + l.StatusReason + ")" : "")}{(l.Expired(DateTime.UtcNow) ? " — СРОК ИСТЁК" : "")}");
                Console.WriteLine($"Тариф:      {l.Plan}, {l.MaxPlayers} слотов, серверов до {l.MaxServers}");
                Console.WriteLine($"Выдан:      {l.CreatedAt:dd.MM.yyyy HH:mm} UTC; активирован: {(l.ActivatedAt is { } at ? at.ToString("dd.MM.yyyy HH:mm") + " UTC" : "нет")}");
                Console.WriteLine($"Срок:       {Until(l)}");
                if (l.Note.Length > 0) Console.WriteLine($"Заметка:    {l.Note}");
                var acts = store.Activations(l.Id);
                Console.WriteLine($"Серверы ({acts.Count}):");
                foreach (var a in acts)
                    Console.WriteLine($"  {a.ServerId}  IP {a.Ip}  версия {a.Version}  слотов {a.Slots}  на связи {a.LastSeen:dd.MM.yyyy HH:mm} UTC (впервые {a.FirstSeen:dd.MM.yyyy})");
                Console.WriteLine("Последние события:");
                foreach (var e in store.Events(l.Id, 15)) PrintEvent(e);
                return 0;
            }
            case "suspend":
            case "revoke":
            {
                var l = Find(store, rest);
                if (l.Status == LicenseStatus.Revoked) throw new CliError("ключ уже отозван");
                l.Status = cmd == "suspend" ? LicenseStatus.Suspended : LicenseStatus.Revoked;
                l.StatusReason = opts.Get("reason") ?? "";
                store.Update(l);
                Event(store, l, cmd == "suspend" ? "приостановлен" : "отозван", l.StatusReason);
                Console.WriteLine($"{l.Key}: {LicenseStatus.Russian(l.Status)}. Серверы закроют вход при следующей проверке (до 5 минут).");
                return 0;
            }
            case "resume":
            {
                var l = Find(store, rest);
                if (l.Status != LicenseStatus.Suspended) throw new CliError("ключ не приостановлен (отозванный вернуть нельзя)");
                l.Status = l.ActivatedAt is null ? LicenseStatus.Issued : LicenseStatus.Active;
                l.StatusReason = "";
                store.Update(l);
                Event(store, l, "возобновлён", "");
                Console.WriteLine($"{l.Key}: {LicenseStatus.Russian(l.Status)}.");
                return 0;
            }
            case "extend":
            {
                var l = Find(store, rest);
                var baseDate = l.ExpiresAt is { } e && e > DateTime.UtcNow ? e : DateTime.UtcNow;
                var until = Expiry(opts, baseDate) ?? throw new CliError("укажите --days N, --until ГГГГ-ММ-ДД или --lifetime");
                l.ExpiresAt = until == DateTime.MaxValue ? null : until;
                store.Update(l);
                Event(store, l, "продлён", "до " + Until(l));
                Console.WriteLine($"{l.Key}: срок {Until(l)}. Серверы получат новый файл лицензии при следующей проверке.");
                return 0;
            }
            case "set":
            {
                var l = Find(store, rest);
                l.MaxPlayers = opts.Int("slots") ?? l.MaxPlayers;
                l.MaxServers = opts.Int("servers") ?? l.MaxServers;
                l.Plan = opts.Get("plan") ?? l.Plan;
                l.Project = opts.Get("project") ?? l.Project;
                l.Owner = opts.Get("owner") ?? l.Owner;
                l.Contact = opts.Get("contact") ?? l.Contact;
                l.Note = opts.Get("note") ?? l.Note;
                Validate(l);
                store.Update(l);
                Event(store, l, "изменён", $"{l.Plan}, {l.MaxPlayers} слотов, серверов {l.MaxServers}");
                Console.WriteLine($"{l.Key}: сохранено.");
                return 0;
            }
            case "unbind":
            {
                var l = Find(store, rest);
                var target = rest.Length > 1 && !rest[1].StartsWith("--") ? rest[1] : throw new CliError("укажите ID сервера (из show) или all");
                var n = store.RemoveActivations(l.Id, target == "all" ? null : target.ToLowerInvariant());
                Event(store, l, "отвязан", target);
                Console.WriteLine(n == 0 ? "такого сервера у ключа нет" : $"отвязано серверов: {n}");
                return 0;
            }
            case "events":
            {
                long? id = null;
                if (rest.Length > 0 && !rest[0].StartsWith("--")) id = Find(store, rest).Id;
                foreach (var e in store.Events(id, opts.Int("limit") ?? 50)) PrintEvent(e);
                return 0;
            }
            case "publish":
                return Publish(cfg, rest.FirstOrDefault() ?? throw new CliError("укажите папку релиза"));
        }
        throw new CliError($"неизвестная команда «{cmd}». Список: flovmp-license help");
    }

    static int Init(Config cfg)
    {
        if (!File.Exists(cfg.KeyFile))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(cfg.KeyFile)!);
            using var rsa = RSA.Create(3072);
            File.WriteAllText(cfg.KeyFile, rsa.ExportPkcs8PrivateKeyPem());
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(cfg.KeyFile, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            Console.WriteLine($"создан ключ подписи: {cfg.KeyFile} — сделайте резервную копию");
        }
        new MySqlStore(cfg.Db).EnsureSchema();
        Directory.CreateDirectory(Path.Combine(cfg.Data, "releases"));
        Console.WriteLine("таблицы готовы: licenses, activations, license_events");
        using var k = LoadKey(cfg);
        Console.WriteLine("Открытый ключ (должен совпадать с вшитым в сервер FloV:MP):");
        Console.WriteLine(k.ExportSubjectPublicKeyInfoPem());
        return 0;
    }

    static int Publish(Config cfg, string folder)
    {
        folder = Path.GetFullPath(folder);
        var found = new List<(string Os, Dictionary<string, string> Info)>();
        foreach (var os in new[] { "linux", "windows" })
        {
            var txt = Path.Combine(folder, $"release-{os}.txt");
            if (!File.Exists(txt)) continue;
            if (!File.Exists(txt + ".sig")) throw new CliError($"нет подписи {txt}.sig");
            var info = File.ReadAllLines(txt).Where(l => l.Contains('=')).ToDictionary(l => l[..l.IndexOf('=')], l => l[(l.IndexOf('=') + 1)..].Trim());
            using var fs = File.OpenRead(Path.Combine(folder, info["file"]));
            var sha = Convert.ToHexString(SHA256.HashData(fs)).ToLowerInvariant();
            if (sha != info["sha256"]) throw new CliError($"SHA-256 {info["file"]} не совпадает с release-{os}.txt");
            found.Add((os, info));
        }
        if (found.Count == 0) throw new CliError("в папке нет release-linux.txt / release-windows.txt");
        var releases = Path.Combine(cfg.Data, "releases");
        var dest = Path.Combine(releases, found[0].Info["version"]);
        Directory.CreateDirectory(dest);
        foreach (var f in Directory.GetFiles(folder)) File.Copy(f, Path.Combine(dest, Path.GetFileName(f)), true);
        var current = Path.Combine(releases, "current");
        var tmp = current + ".new";
        if (Directory.Exists(tmp) || File.Exists(tmp)) Directory.Delete(tmp);
        Directory.CreateSymbolicLink(tmp, dest);
        if (Directory.Exists(current)) Directory.Delete(current);
        Directory.Move(tmp, current);
        foreach (var (os, info) in found) Console.WriteLine($"опубликован {os} {info["version"]} ({info["file"]})");
        return 0;
    }

    static DateTime? Expiry(Options o, DateTime from)
    {
        if (o.Flag("lifetime")) return DateTime.MaxValue;
        if (o.Int("days") is { } d && d > 0) return from.AddDays(d);
        if (o.Get("until") is { } u)
            return DateTime.SpecifyKind(DateTime.ParseExact(u, "yyyy-MM-dd", CultureInfo.InvariantCulture).AddDays(1).AddSeconds(-1), DateTimeKind.Utc);
        return null;
    }

    static string Until(LicenseRecord l) => l.ExpiresAt is { } e ? e.ToString("dd.MM.yyyy") : "бессрочно";

    static void Validate(LicenseRecord l)
    {
        if (l.MaxPlayers is < 1 or > 10000) throw new CliError("--slots: 1..10000");
        if (l.MaxServers is < 1 or > 100) throw new CliError("--servers: 1..100");
        if (l.Project.Length is 0 or > 128 || l.Owner.Length is 0 or > 128) throw new CliError("--project и --owner: 1..128 символов");
    }

    static LicenseRecord Find(ILicenseStore store, string[] rest)
    {
        var key = LicenseService.NormalizeKey(rest.FirstOrDefault()) ?? throw new CliError("укажите ключ FLV-...");
        return store.FindByKey(key) ?? throw new CliError("ключ не найден");
    }

    static void Event(ILicenseStore store, LicenseRecord l, string type, string detail) =>
        store.AddEvent(new EventRecord { At = DateTime.UtcNow, LicenseId = l.Id, Key = LicenseService.Mark(l.Key), Type = type, Ip = "консоль", Detail = detail });

    static void PrintEvent(EventRecord e) =>
        Console.WriteLine($"  {e.At:dd.MM HH:mm:ss}  {e.Type,-16} {e.Ip,-15} {(e.ServerId.Length > 0 ? "сервер " + e.ServerId + "  " : "")}{e.Detail}");

    static RSA LoadKey(Config cfg)
    {
        if (!File.Exists(cfg.KeyFile)) throw new CliError($"нет ключа подписи {cfg.KeyFile} — выполните flovmp-license init");
        var rsa = RSA.Create();
        rsa.ImportFromPem(File.ReadAllText(cfg.KeyFile));
        return rsa;
    }
}

sealed class Config
{
    public string Db = "";
    public string KeyFile = "/etc/flovmp-license/authority.pem";
    public string Data = "/var/lib/flovmp-license";
    public string Listen = "http://127.0.0.1:7799/";

    public static Config Load()
    {
        var path = Environment.GetEnvironmentVariable("FLOVMP_LICENSE_CONFIG") ?? "/etc/flovmp-license/license.env";
        var values = new Dictionary<string, string>();
        if (File.Exists(path))
            foreach (var line in File.ReadAllLines(path))
            {
                var t = line.Trim();
                var i = t.IndexOf('=');
                if (t.StartsWith('#') || i <= 0) continue;
                values[t[..i].Trim()] = t[(i + 1)..].Trim().Trim('"');
            }
        string Get(string k, string def) => Environment.GetEnvironmentVariable(k) ?? values.GetValueOrDefault(k) ?? def;
        var c = new Config();
        c.Db = Get("FLOVMP_LA_DB", "");
        c.KeyFile = Get("FLOVMP_LA_KEY", c.KeyFile);
        c.Data = Get("FLOVMP_LA_DATA", c.Data);
        c.Listen = Get("FLOVMP_LA_LISTEN", c.Listen);
        if (c.Db.Length == 0) throw new CliError($"нет FLOVMP_LA_DB (строка подключения к MariaDB) в {path}");
        return c;
    }
}

sealed class Options
{
    private readonly Dictionary<string, string?> _v = new();

    public static Options Parse(string[] args)
    {
        var o = new Options();
        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--")) continue;
            var name = args[i][2..];
            var hasValue = i + 1 < args.Length && !args[i + 1].StartsWith("--");
            o._v[name] = hasValue ? args[++i] : null;
        }
        return o;
    }

    public string? Get(string n) => _v.TryGetValue(n, out var v) ? v : null;
    public bool Flag(string n) => _v.ContainsKey(n);
    public int? Int(string n) => int.TryParse(Get(n), out var i) ? i : null;
    public string Required(string n) => Get(n) is { Length: > 0 } v ? v : throw new CliError($"нужен --{n}");
}
