using System.Net;
using AltV.Net;
using FloVMP.Core.Mods;
using FloVMP.Core.Native;

namespace FloVMP.Starter;

/// <summary>
/// Раздача модов игрокам (пункт 4 roadmap), серверная часть.
///
/// Владелец кладёт моды в server/mods раскладкой папки GTA\mods. При запуске
/// (и по reloadmods) сервер в фоне считает SHA-256 файлов — с кэшем, карта
/// весит гигабайты — и:
///   • раздаёт их по HTTP (mods.http_port, по умолчанию порт игры + 20) с
///     докачкой по Range, либо указывает на внешний CDN (mods.public_url);
///   • сообщает клиенту 1.0.7+ строкой MODS, какие моды нужны:
///     MODS адрес отпечаток файлов байт, где адрес — URL CDN или «:порт»
///     (тот же хост, к которому клиент подключился: своего внешнего адреса
///     сервер не знает, а клиент знает).
/// Исполняемые файлы (.asi, .dll, …) не раздаются никогда.
/// </summary>
public partial class StarterResource
{
    private volatile ModManifest _mods = ModManifest.Empty;
    private ModFileServer? _modServer;
    private string _modsRoot = "";
    private string _modsCachePath = "";
    private readonly CancellationTokenSource _modsStop = new();
    private int _modsBuilding;
    private volatile bool _modsChanged;

    private void StartMods(string dataDir)
    {
        _modsRoot = Path.Combine(Directory.GetCurrentDirectory(), "mods");
        _modsCachePath = Path.Combine(dataDir, "mods-hashes.json");
        try { Directory.CreateDirectory(_modsRoot); } catch { }
        RebuildMods(startup: true);
    }

    /// <summary>Пересчитать список в фоне: игровой поток ждать чтения гигабайт не должен.</summary>
    private void RebuildMods(bool startup)
    {
        if (Interlocked.Exchange(ref _modsBuilding, 1) == 1)
        {
            Alt.Log("[FloV:MP] [Моды] список уже пересчитывается — дождитесь строки «Моды готовы».");
            return;
        }
        var stop = _modsStop.Token;
        _ = Task.Run(() =>
        {
            try
            {
                var started = DateTime.UtcNow;
                var cache = new ModHashCache(_modsCachePath);
                var m = ModManifest.Build(_modsRoot, cache, stop);
                cache.Save();
                // Готовый список для своего CDN (mods.public_url): на CDN кладутся
                // этот файл как manifest.json и копия server/mods как files/.
                if (m.Files.Count > 0)
                {
                    var cdn = Path.Combine(Path.GetDirectoryName(_modsCachePath)!, "mods-cdn-manifest.json");
                    File.WriteAllText(cdn + ".tmp", m.ToJson());
                    File.Move(cdn + ".tmp", cdn, overwrite: true);
                }
                var changed = m.Digest != _mods.Digest;
                _mods = m;
                foreach (var skipped in m.Skipped.Take(20)) BackgroundLog("[FloV:MP] [Моды] пропущен " + skipped);
                if (m.Skipped.Count > 20) BackgroundLog($"[FloV:MP] [Моды] … и ещё {m.Skipped.Count - 20} пропущенных");
                if (m.Files.Count > 0 || !startup)
                    BackgroundLog($"[FloV:MP] [Моды] Моды готовы: {m.Files.Count} файлов, {m.TotalSize / 1048576.0:0.#} МБ " +
                                      $"(отпечаток {m.Digest[..12]}, {(DateTime.UtcNow - started).TotalSeconds:0.#} с).");
                if (changed) _modsChanged = true;   // разошлём из игрового потока
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { BackgroundLog("[FloV:MP] [Моды] список не собран: " + ex.Message); }
            finally { Interlocked.Exchange(ref _modsBuilding, 0); }
        }, stop);
    }

    /// <summary>
    /// Сообщения из фоновой сборки списков модов и клиентских пакетов. Раньше
    /// они шли в Console.WriteLine из фонового потока и в server.log не
    /// попадали — владелец не видел «Моды готовы», «пропущен …» или «нет
    /// index.js». Печатаются в тике через Alt.Log.
    /// </summary>
    private readonly System.Collections.Concurrent.ConcurrentQueue<string> _backgroundLog = new();

    private void BackgroundLog(string line) => _backgroundLog.Enqueue(line);

    /// <summary>Из тика: после пересчёта — запустить раздачу и сообщить игрокам.</summary>
    private void PumpMods()
    {
        while (_backgroundLog.TryDequeue(out var line)) Alt.Log(line);
        if (!_modsChanged) return;
        _modsChanged = false;
        EnsureModServer();
        foreach (var (_, player) in _nativePlayers)
            SendModsInfo(((NativePlayerProxy)(object)player).Session);
    }

    private int ModsHttpPort()
    {
        var port = _settings.Int("mods.http_port");
        if (port > 0) return port;
        return int.TryParse(ReadServerTomlValue("port"), out var game) && game is > 0 and < 65516 ? game + 20 : 7808;
    }

    private void EnsureModServer()
    {
        // Один HTTP-сервер на два набора: моды игры и клиентские пакеты. Нужен,
        // если есть хоть что-то раздавать; моды — только при mods.serve.
        var serveMods = _settings.Bool("mods.serve") && _mods.Files.Count > 0;
        if (_modServer is not null || (!serveMods && _clientPkgs.Files.Count == 0)) return;
        var port = ModsHttpPort();
        try
        {
            _modServer = new ModFileServer(IPAddress.Any, port, new[]
            {
                new FileArea("mods", _modsRoot, () => _settings.Bool("mods.serve") ? _mods : ModManifest.Empty),
                new FileArea("client", _clientRoot, () => _clientPkgs),
            }, Alt.Log);
            _modServer.Start();
            Alt.Log($"[FloV:MP] [Моды] Раздача модов игрокам: TCP {port} (/mods/manifest.json). Для игроков из интернета откройте этот порт. " +
                    "Для большой карты и сотен игроков лучше CDN — mods.public_url.");
        }
        catch (Exception ex)
        {
            _modServer = null;
            Alt.LogWarning($"[FloV:MP] [Моды] Раздача не запущена (TCP {port}): {ex.Message}. Порт занят? Задайте mods.http_port.");
        }
    }

    /// <summary>Клиенту: какие моды нужны. Модов нет — строка не шлётся.</summary>
    private void SendModsInfo(NativeSession session)
    {
        var m = _mods;
        if (m.Files.Count == 0) return;
        var url = _settings.Get("mods.public_url").Trim().TrimEnd('/');
        var source = url.Length > 0 ? url : _modServer is not null ? ":" + _modServer.Port : "";
        if (source.Length == 0) return;   // раздача выключена и CDN не задан — брать неоткуда
        session.Send("MODS", source, m.Digest, m.Files.Count, m.TotalSize, _settings.Bool("mods.required") ? 1 : 0);
    }

    private void PrintModsStatus()
    {
        var m = _mods;
        Alt.Log($"[Console] Моды: {m.Files.Count} файлов, {m.TotalSize / 1048576.0:0.#} МБ, отпечаток {(m.Files.Count > 0 ? m.Digest[..12] : "—")}; " +
                $"папка {_modsRoot}; раздача {(_modServer is null ? "выключена" : $"TCP {_modServer.Port}, отдано {_modServer.BytesServed / 1048576.0:0.#} МБ")}" +
                (_settings.Get("mods.public_url").Trim() is { Length: > 0 } cdn ? $"; CDN {cdn}" : ""));
    }

    private void StopMods()
    {
        _modsStop.Cancel();
        _modServer?.Dispose();
    }
}
