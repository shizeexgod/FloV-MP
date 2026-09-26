using AltV.Net;
using FloVMP.Core.Mods;
using FloVMP.Core.Native;

namespace FloVMP.Starter;

/// <summary>
/// Клиентский код игроков GTA V Legacy 1.0.3889.0 (пункт 26 roadmap).
///
/// Владелец сервера кладёт скрипты и интерфейсы в server/client_packages —
/// так же, как в RAGE:MP. Точка входа — index.js. Сервер считает список с
/// SHA-256 и раздаёт файлы по HTTP с того же порта, что и моды
/// (/client/manifest.txt, /client/files/…). Клиент скачивает только новое и
/// изменённое во время загрузки, сверяет каждый файл и запускает index.js в
/// своём движке JS.
///
/// События между геймодом и клиентским кодом:
///   геймод → игрок:  Alt.Emit("flovmp:client:call", id, "имя", "[аргументы JSON]")
///                    Alt.Emit("flovmp:client:callAll", "имя", "[аргументы JSON]")
///   игрок → геймод:  событие "flovmp:client:event" (id, "имя", "[аргументы JSON]")
/// На клиенте это mp.events.add / mp.events.callRemote, как в RAGE:MP.
/// </summary>
public partial class StarterResource
{
    private volatile ModManifest _clientPkgs = ModManifest.Empty;
    private string _clientRoot = "";
    private string _clientCachePath = "";
    private int _clientBuilding;
    private volatile bool _clientChanged;

    /// <summary>
    /// Предел аргументов события. Строка протокола не длиннее 4096 байт; на
    /// имя и служебные поля остаётся запас.
    /// </summary>
    public const int MaxClientEventJson = NativeClientEventPolicy.MaxJsonChars;

    /// <summary>Сколько событий игрок может прислать в секунду.</summary>
    private const int MaxClientEventsPerSecond = 120;

    private readonly Dictionary<uint, (int Count, long WindowStart)> _clientEventRate = new();
    private readonly Dictionary<uint, long> _clientEventWarnedAt = new();

    private void StartClientPackages(string dataDir)
    {
        _clientRoot = Path.Combine(Directory.GetCurrentDirectory(), "client_packages");
        _clientCachePath = Path.Combine(dataDir, "client-packages-hashes.json");
        try { Directory.CreateDirectory(_clientRoot); } catch { }
        RebuildClientPackages(startup: true);
    }

    private void RebuildClientPackages(bool startup)
    {
        if (Interlocked.Exchange(ref _clientBuilding, 1) == 1) return;
        var stop = _modsStop.Token;
        _ = Task.Run(() =>
        {
            try
            {
                var cache = new ModHashCache(_clientCachePath);
                var m = ModManifest.Build(_clientRoot, ModManifest.ClientPackageExtensions, cache, stop);
                cache.Save();
                var changed = m.Digest != _clientPkgs.Digest;
                _clientPkgs = m;
                foreach (var skipped in m.Skipped.Take(20)) BackgroundLog("[FloV:MP] [Клиент] пропущен " + skipped);
                if (m.Files.Count > 0 && !m.TryGet("index.js", out _))
                    BackgroundLog("[FloV:MP] [Клиент] в client_packages нет index.js — клиентский код не запустится.");
                if (m.Files.Count > 0 || !startup)
                    BackgroundLog($"[FloV:MP] [Клиент] Клиентские пакеты: {m.Files.Count} файлов, " +
                                      $"{m.TotalSize / 1024.0:0.#} КБ (отпечаток {m.Digest[..12]}).");
                if (changed) _clientChanged = true;
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { BackgroundLog("[FloV:MP] [Клиент] список не собран: " + ex.Message); }
            finally { Interlocked.Exchange(ref _clientBuilding, 0); }
        }, stop);
    }

    /// <summary>Из тика: после пересчёта — поднять раздачу и сообщить игрокам.</summary>
    private void PumpClientPackages()
    {
        if (!_clientChanged) return;
        _clientChanged = false;
        EnsureModServer();
        foreach (var (_, player) in _nativePlayers)
            SendClientPackagesInfo(((NativePlayerProxy)(object)player).Session);
    }

    /// <summary>
    /// Клиенту: откуда и что скачать. CPKG «:порт» отпечаток файлов байт.
    /// Пакетов нет — строка не шлётся, и клиентский код не запускается.
    /// </summary>
    private void SendClientPackagesInfo(NativeSession session)
    {
        var m = _clientPkgs;
        if (m.Files.Count == 0 || _modServer is null) return;
        session.Send("CPKG", ":" + _modServer.Port, m.Digest, m.Files.Count, m.TotalSize);
    }

    private void RegisterClientEventApi()
    {
        Alt.OnServer<int, string, string>("flovmp:client:call", (id, name, argsJson) =>
        {
            if (!ValidClientEvent(name, argsJson, "flovmp:client:call")) return;
            var player = NativeById(id);
            if (player is null) return;
            ((NativePlayerProxy)(object)player).Session.Send("CEV", name, argsJson ?? "[]");
        });
        Alt.OnServer<string, string>("flovmp:client:callAll", (name, argsJson) =>
        {
            if (!ValidClientEvent(name, argsJson, "flovmp:client:callAll")) return;
            foreach (var (_, player) in _nativePlayers)
                ((NativePlayerProxy)(object)player).Session.Send("CEV", name, argsJson ?? "[]");
        });
    }

    private static bool ValidClientEvent(string name, string? argsJson, string api)
    {
        if (!NativeClientEventPolicy.IsValidName(name))
        {
            Alt.LogWarning($"[FloV:MP] {api}: недопустимое имя события «{name}» (буквы, цифры, _:.- , до 64)");
            return false;
        }
        if (!NativeClientEventPolicy.IsValid(name, argsJson, fromClient: false))
        {
            Alt.LogWarning($"[FloV:MP] {api} «{name}»: нужен JSON-массив до {MaxClientEventJson} символов и {NativeProtocol.MaxLineBytes} байт на проводе");
            return false;
        }
        return true;
    }

    /// <summary>Событие от клиентского кода игрока: CEVS имя аргументы.</summary>
    private void OnClientScriptEvent(NativeSession session, string[] p)
    {
        if (p.Length < 3) return;
        var name = p[1];
        var args = p[2];
        var now = _clock.ElapsedMilliseconds;
        var rate = _clientEventRate.TryGetValue(session.Id, out var r) && now - r.WindowStart < 1000
            ? (Count: r.Count + 1, r.WindowStart)
            : (Count: 1, WindowStart: now);
        _clientEventRate[session.Id] = rate;
        if (rate.Count > MaxClientEventsPerSecond || !NativeClientEventPolicy.IsValid(name, args, fromClient: true))
        {
            if (!_clientEventWarnedAt.TryGetValue(session.Id, out var warned) || now - warned > 10_000)
            {
                _clientEventWarnedAt[session.Id] = now;
                Alt.LogWarning($"[FloV:MP] [Клиент] [{session.Id}] {session.Name}: событие отброшено " +
                               $"({(rate.Count > MaxClientEventsPerSecond ? "слишком часто" : "неверное имя или размер")}).");
            }
            return;
        }
        Alt.Emit("flovmp:client:event", (int)session.Id, name, args);
    }

    private void ForgetClientEventState(uint id)
    {
        _clientEventRate.Remove(id);
        _clientEventWarnedAt.Remove(id);
    }
}
