using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;
using AltV.Net;
using AltV.Net.Elements.Entities;
using FloVMP.Core.Native;

namespace FloVMP.Starter;

/// <summary>
/// Мир и интерфейс для клиентов GTA V Legacy 1.0.3889.0 без клиентских скриптов:
/// объекты карты, метки, маркеры, 3D-надписи, NPC, меню, уведомления, клавиши.
///
/// Владелец управляет ими из своего ресурса (C#, события flovmp:world:*,
/// flovmp:ui:*, flovmp:keys:*; ответы — flovmp:native:*) или файлами карт в server/config/maps
/// (*.json, *.xml расстановок Menyoo) — без программирования. Сервер хранит
/// всё в реестре и отдаёт каждому вошедшему; объекты клиент сам подгружает
/// вокруг игрока и выгружает вдали, поэтому большая карта не бьёт по FPS.
/// </summary>
public partial class StarterResource
{
    /// <summary>Элемент мира: вид (OBJ, BLIP, MARKER, LABEL, NPC), ID, измерение и поля протокола.</summary>
    private sealed record WorldItem(string Kind, string Id, int Dimension, object?[] Fields);

    private readonly Dictionary<string, WorldItem> _world = new(StringComparer.Ordinal);
    private readonly HashSet<string> _worldFromMaps = new(StringComparer.Ordinal);
    private readonly HashSet<string> _boundKeys = new(StringComparer.OrdinalIgnoreCase);
    // Открытое у игрока меню: выбор принимается только из него (подделать выбор чужого меню нельзя).
    private readonly Dictionary<uint, string> _openMenus = new();
    private readonly Dictionary<uint, (int Count, long WindowStart)> _uiRate = new();

    private const int MaxWorldItems = 60000;
    private const int MaxIdLength = 64;
    private const int DimensionAll = int.MinValue; // «во всех измерениях»

    private static readonly Dictionary<string, string> KindByName = new(StringComparer.OrdinalIgnoreCase)
    {
        ["object"] = "OBJ", ["blip"] = "BLIP", ["marker"] = "MARKER", ["label"] = "LABEL", ["npc"] = "NPC",
        ["ipl"] = "IPL", ["interiorSet"] = "ISET",
    };

    // IPL — часть карты игры по имени (интерьер больницы, яхта, квартиры),
    // набор интерьера — его оформление (мебель, вывески). Имена — как в игре.
    private static readonly System.Text.RegularExpressions.Regex GameName =
        new("^[A-Za-z0-9_-]{1,64}$", System.Text.RegularExpressions.RegexOptions.Compiled);

    // Какие IPL сейчас заданы файлом настроек: при reloadsettings убранные из
    // файла возвращаются игре, а заданные ресурсом не трогаются.
    private readonly HashSet<string> _cfgIpls = new(StringComparer.OrdinalIgnoreCase);

    private bool PutIpl(string? name, bool loaded)
    {
        var n = (name ?? "").Trim();
        if (!GameName.IsMatch(n)) { Alt.LogWarning($"[FloV:MP] мир: IPL «{name}» — латиница, цифры, _ и -, до 64 символов"); return false; }
        if (!PutWorld("IPL", n, DimensionAll, loaded ? 1 : 0)) return false;
        // Заданный файлом настроек или кодом ресурса IPL принадлежит им, а не
        // карте: reloadmaps его не заменит и не снимет.
        _worldFromMaps.Remove(Key("IPL", n));
        return true;
    }

    /// <summary>world.ipls и world.ipls_remove из client.cfg — при старте и reloadsettings.</summary>
    private void ApplyIplSettings()
    {
        var wanted = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var n in SplitList(_settings.Get("world.ipls"))) wanted[n] = true;
        foreach (var n in SplitList(_settings.Get("world.ipls_remove"))) wanted[n] = false;
        foreach (var gone in _cfgIpls.Where(n => !wanted.ContainsKey(n)).ToList())
        {
            RemoveWorld("IPL", gone);
            _cfgIpls.Remove(gone);
        }
        foreach (var (n, loaded) in wanted)
            if (PutIpl(n, loaded)) _cfgIpls.Add(n);
    }

    private static IEnumerable<string> SplitList(string? list) =>
        (list ?? "").Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private void RegisterWorldApi()
    {
        // Объект: ID, модель (имя или 0x-хэш), позиция, поворот (градусы), измерение, заморожен, коллизия.
        Alt.OnServer<string, string, float, float, float, float, float, float, int, bool, bool>("flovmp:world:object",
            (id, model, x, y, z, rx, ry, rz, dim, frozen, collision) =>
                PutWorld("OBJ", id, dim, ModelHash(model), x, y, z, rx, ry, rz, (frozen ? 1 : 0) | (collision ? 0 : 2)));
        // Метка на карте: ID, позиция, значок (sprite), цвет, масштаб, подпись, измерение.
        Alt.OnServer<string, float, float, float, int, int, float, string, int>("flovmp:world:blip",
            (id, x, y, z, sprite, color, scale, name, dim) =>
                PutWorld("BLIP", id, dim, x, y, z, Math.Clamp(sprite, 0, 1000), Math.Clamp(color, 0, 100),
                    Math.Clamp(scale, 0.1f, 5f), 1, Clean(name, 64)));
        // Маркер: ID, тип (0..43), позиция, размер, цвет #rrggbbaa, измерение.
        Alt.OnServer<string, int, float, float, float, float, string, int>("flovmp:world:marker",
            (id, type, x, y, z, scale, rgba, dim) =>
            {
                var c = ParseRgba(rgba);
                PutWorld("MARKER", id, dim, Math.Clamp(type, 0, 43), x, y, z, Math.Clamp(scale, 0.1f, 50f),
                    c.R, c.G, c.B, c.A, 60f);
            });
        // 3D-надпись: ID, позиция, текст ({rrggbb} — цвет), дальность, измерение.
        Alt.OnServer<string, float, float, float, string, float, int>("flovmp:world:label",
            (id, x, y, z, text, distance, dim) =>
                PutWorld("LABEL", id, dim, x, y, z, Math.Clamp(distance, 1f, 200f), Clean(text, 200)));
        // NPC: ID, модель, позиция, поворот, сценарий (WORLD_HUMAN_... или пусто), измерение.
        Alt.OnServer<string, string, float, float, float, float, string, int>("flovmp:world:npc",
            (id, model, x, y, z, heading, scenario, dim) =>
                PutWorld("NPC", id, dim, ModelHash(model), x, y, z, heading, Clean(scenario, 64)));
        // Убрать элемент: вид (object, blip, marker, label, npc) и ID.
        // Часть карты: имя IPL, загрузить (true) или убрать (false). Во всех измерениях.
        Alt.OnServer<string, bool>("flovmp:world:ipl", (name, loaded) => PutIpl(name, loaded));
        // Набор интерьера: ID, точка внутри интерьера, имя набора, включить.
        Alt.OnServer<string, float, float, float, string, bool>("flovmp:world:interiorSet", (id, x, y, z, set, on) =>
        {
            var name = (set ?? "").Trim();
            if (!GameName.IsMatch(name)) { Alt.LogWarning($"[FloV:MP] мир: набор интерьера «{set}» — латиница, цифры, _ и -, до 64 символов"); return; }
            if (!Finite(x) || !Finite(y) || !Finite(z)) { Alt.LogWarning("[FloV:MP] мир: набор интерьера — недопустимые координаты"); return; }
            PutWorld("ISET", id, DimensionAll, x, y, z, name, on);
        });

        Alt.OnServer<string, string>("flovmp:world:remove", (kind, id) =>
        {
            if (KindByName.TryGetValue(kind ?? "", out var k)) RemoveWorld(k, id);
        });
        Alt.OnServer("flovmp:maps:reload", () => LoadMaps(broadcast: true));

        // Чат игроку по ID и всем: у игрока 3889 нет сущности движка, поэтому
        // ресурсы владельца работают с его номером (как в flovmp:native:*).
        Alt.OnServer<int, string>("flovmp:chat:to", (id, text) =>
        {
            var player = PlayerById((uint)Math.Max(0, id));
            if (player is null || !player.Exists) { Alt.LogWarning($"[FloV:MP] чат: игрока с ID {id} нет на сервере"); return; }
            SendChatMessage(player, Clean(text, 900));
        });
        Alt.OnServer<string>("flovmp:chat:all", text =>
        {
            var line = Clean(text, 900);
            foreach (var p in AllPlayers()) if (p.Exists) SendChatMessage(p, line);
        });

        // Интерфейс конкретного игрока 3889 — по его ID (у такого игрока нет
        // сущности движка, поэтому ресурсы получают и передают номер).
        Alt.OnServer<int, string, int>("flovmp:ui:notify", (id, text, ms) =>
            NativeById(id)?.Emit("flovmp:ui:notify", Clean(text, 300), Math.Clamp(ms, 1000, 20000).ToString(CultureInfo.InvariantCulture)));
        Alt.OnServer<int, string, string, string>("flovmp:ui:menu", (id, menu, title, itemsJson) =>
            NativeById(id)?.Emit("flovmp:ui:menu", Clean(menu, 64), Clean(title, 60), itemsJson ?? "[]"));
        Alt.OnServer<int>("flovmp:ui:closeMenu", id => NativeById(id)?.Emit("flovmp:ui:closeMenu"));
        // Клавиша, о нажатии которой клиент сообщает серверу: A..Z, 0..9, F1..F12.
        Alt.OnServer<string>("flovmp:keys:bind", key =>
        {
            var k = (key ?? "").Trim().ToUpperInvariant();
            if (FloVMP.Core.Settings.ServerSettings.KeyCode(k) == 0) { Alt.LogWarning($"[FloV:MP] flovmp:keys:bind: неизвестная клавиша «{key}»"); return; }
            if (_boundKeys.Add(k)) foreach (var p in _nativePlayers.Values) SendKeys(((NativePlayerProxy)(object)p).Session);
        });
    }

    private IPlayer? NativeById(int id)
    {
        if (id > 0 && _nativePlayers.TryGetValue((uint)id, out var p)) return p;
        Alt.LogWarning($"[FloV:MP] интерфейс: игрока 3889 с ID {id} нет на сервере");
        return null;
    }

    private static string Clean(string? text, int max)
    {
        var t = (text ?? "").Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ');
        return t.Length > max ? t[..max] : t;
    }

    private static uint ModelHash(string? model)
    {
        var m = (model ?? "").Trim();
        if (m.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
            uint.TryParse(m[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hex)) return hex;
        if (uint.TryParse(m, NumberStyles.Integer, CultureInfo.InvariantCulture, out var num)) return num;
        if (int.TryParse(m, NumberStyles.Integer, CultureInfo.InvariantCulture, out var neg)) return unchecked((uint)neg);
        return m.Length == 0 ? 0 : Alt.Hash(m.ToLowerInvariant());
    }

    private static (int R, int G, int B, int A) ParseRgba(string? s)
    {
        var t = (s ?? "").Trim().TrimStart('#');
        if (t.Length is 6 or 8 && uint.TryParse(t, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v))
        {
            if (t.Length == 6) v = (v << 8) | 0xB4;
            return ((int)(v >> 24), (int)((v >> 16) & 0xFF), (int)((v >> 8) & 0xFF), (int)(v & 0xFF));
        }
        return (255, 61, 138, 180);
    }

    private static string Key(string kind, string id) => kind + ":" + id;

    private bool PutWorld(string kind, string? id, int dimension, params object?[] fields)
    {
        id = (id ?? "").Trim();
        if (id.Length == 0 || id.Length > MaxIdLength || id.Any(char.IsControl))
        {
            Alt.LogWarning($"[FloV:MP] мир: неверный ID «{id}» ({kind}) — 1..{MaxIdLength} символов без управляющих");
            return false;
        }
        var key = Key(kind, id);
        if (!_world.ContainsKey(key) && _world.Count >= MaxWorldItems)
        {
            Alt.LogWarning($"[FloV:MP] мир: больше {MaxWorldItems} элементов — «{id}» не добавлен");
            return false;
        }
        var item = new WorldItem(kind, id, dimension, fields);
        _world[key] = item;
        var line = FormatWorld(item);
        foreach (var p in _nativePlayers.Values)
        {
            var np = (NativePlayerProxy)(object)p;
            if (_nativeReady.Contains(np.Session.Id) && Visible(item, np.DimensionValue)) SendWorldLine(np.Session, line);
        }
        return true;
    }

    private void RemoveWorld(string kind, string? id)
    {
        if (id is null || !_world.Remove(Key(kind, id))) return;
        foreach (var p in _nativePlayers.Values)
            SendWorldLine(((NativePlayerProxy)(object)p).Session, NativeProtocol.Format("WDEL", kind, id));
    }

    private static bool Visible(WorldItem item, int dimension) => item.Dimension == DimensionAll || item.Dimension == dimension;

    private static string FormatWorld(WorldItem item)
    {
        var all = new object?[item.Fields.Length + 1];
        all[0] = item.Id;
        Array.Copy(item.Fields, 0, all, 1, item.Fields.Length);
        return NativeProtocol.Format("W" + item.Kind, all);
    }

    /// <summary>
    /// Весь мир — вошедшему игроку или после смены измерения.
    ///
    /// Карта бывает в десятки тысяч объектов, а очередь отправки сессии
    /// ограничена: сваленный в неё целиком снимок переполнял её, и игрока
    /// выкидывало с «клиент не успевает принимать данные» прямо на входе.
    /// Поэтому снимок уходит порциями по мере того, как очередь пустеет.
    /// </summary>
    private void SendWorldSnapshot(NativeSession session, int dimension)
    {
        var queue = new Queue<string>();
        queue.Enqueue("WCLEAR");
        foreach (var item in _world.Values)
            if (Visible(item, dimension)) queue.Enqueue(FormatWorld(item));
        queue.Enqueue(NativeProtocol.Format("KEYS", string.Join(",", _boundKeys)));
        _worldPending[session.Id] = queue;
        PumpWorldSnapshots();
    }

    private readonly Dictionary<uint, Queue<string>> _worldPending = new();

    /// <summary>Сколько строк снимка держим в очереди сессии: остальное место
    /// нужно синхронизации игроков и чату.</summary>
    private const int WorldQueueHeadroom = 2048;

    /// <summary>Досылает снимки мира тем, кому они ещё не дошли. Зовётся каждый тик.</summary>
    private void PumpWorldSnapshots()
    {
        if (_worldPending.Count == 0) return;
        foreach (var id in _worldPending.Keys.ToList())
        {
            var queue = _worldPending[id];
            if (!_nativePlayers.TryGetValue(id, out var player))
            {
                _worldPending.Remove(id);
                continue;
            }
            var session = ((NativePlayerProxy)(object)player).Session;
            while (queue.Count > 0 && session.Queued < WorldQueueHeadroom)
            {
                if (!session.Send(queue.Peek())) { queue.Clear(); break; }
                queue.Dequeue();
            }
            if (queue.Count == 0) _worldPending.Remove(id);
        }
    }

    /// <summary>
    /// Строка мира игроку. Если очередь отправки подходит к концу (геймод
    /// строит карту кодом — тысячи объектов за тик), строка уходит в ту же
    /// порционную очередь, что и снимок: порядок сохраняется, игрока не
    /// выбрасывает за «не успевает принимать данные».
    /// </summary>
    private void SendWorldLine(NativeSession session, string line)
    {
        if (_worldPending.TryGetValue(session.Id, out var pending)) { pending.Enqueue(line); return; }
        if (session.Queued >= WorldQueueHeadroom)
        {
            var queue = new Queue<string>();
            queue.Enqueue(line);
            _worldPending[session.Id] = queue;
            return;
        }
        session.Send(line);
    }

    private void SendKeys(NativeSession session) => session.Send("KEYS", string.Join(",", _boundKeys));

    // --- интерфейс: меню и клавиши от клиента -------------------------------------------

    private bool UiRateOk(uint id)
    {
        var now = _clock.ElapsedMilliseconds;
        var r = _uiRate.TryGetValue(id, out var v) && now - v.WindowStart < 1000 ? (v.Count + 1, v.WindowStart) : (1, now);
        _uiRate[id] = r;
        return r.Item1 <= 20;
    }

    /// <summary>MENUSEL / MENUCLOSED / KEY от клиента b3889.</summary>
    private bool HandleNativeUi(IPlayer player, NativeSession session, string[] p)
    {
        switch (p[0])
        {
            case "MENUSEL":
            case "MENUCLOSED":
                {
                    if (!UiRateOk(session.Id)) return true;
                    var menuId = p.Length > 1 ? p[1] : "";
                    if (!_openMenus.TryGetValue(session.Id, out var open) || open != menuId) return true;
                    if (p[0] == "MENUCLOSED")
                    {
                        _openMenus.Remove(session.Id);
                        Alt.Emit("flovmp:native:menuClose", (int)session.Id, menuId);
                    }
                    else
                    {
                        var index = NativeProtocol.IntOr(p, 2, -1);
                        if (index < 0 || index > 500) return true;
                        Alt.Emit("flovmp:native:menuSelect", (int)session.Id, menuId, index);
                    }
                    return true;
                }
            case "KEY":
                {
                    if (!UiRateOk(session.Id)) return true;
                    var key = p.Length > 1 ? p[1].ToUpperInvariant() : "";
                    if (_boundKeys.Contains(key)) Alt.Emit("flovmp:native:key", (int)session.Id, key);
                    return true;
                }
        }
        return false;
    }

    /// <summary>Меню открыто сервером — запомнить (proxy вызывает при flovmp:ui:menu).</summary>
    internal void NoteMenuOpened(uint playerId, string? menuId)
    {
        if (menuId is null) _openMenus.Remove(playerId);
        else _openMenus[playerId] = menuId;
    }

    private void ForgetNativeUi(uint id)
    {
        _openMenus.Remove(id);
        _uiRate.Remove(id);
        // Недосланный снимок мира ушедшего игрока освобождаем сразу: на карте
        // в десятки тысяч объектов эта очередь заметного размера.
        _worldPending.Remove(id);
    }

    // --- файлы карт ------------------------------------------------------------------------

    /// <summary>
    /// server/config/maps: *.json (формат FloV:MP) и *.xml (расстановки Menyoo /
    /// Map Editor). Перечитываются командой reloadmaps. Элементы из карт
    /// заменяются целиком; элементы, созданные ресурсами, не трогаются.
    /// </summary>
    private void LoadMaps(bool broadcast)
    {
        var dir = Path.Combine(Directory.GetCurrentDirectory(), "config", "maps");
        // Интерьеры из прежних карт: клиент держит их и при WCLEAR (смена
        // измерения не должна перезагружать карту), поэтому убранные из файла
        // нужно снять явным WDEL — иначе они остались бы у игроков до выхода.
        var oldInteriors = _worldFromMaps.Where(k => k.StartsWith("IPL:", StringComparison.Ordinal) ||
                                                     k.StartsWith("ISET:", StringComparison.Ordinal)).ToList();
        foreach (var key in _worldFromMaps) _world.Remove(key);
        _worldFromMaps.Clear();
        if (!Directory.Exists(dir))
        {
            try
            {
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "README.txt"), MapsReadme, new System.Text.UTF8Encoding(false));
            }
            catch (Exception) { }
        }
        var files = Directory.Exists(dir)
            ? Directory.GetFiles(dir, "*.*").Where(f => f.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
                                                        f.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                       .OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList()
            : new List<string>();
        var total = 0;
        foreach (var file in files)
        {
            var before = _world.Count;
            var name = Path.GetFileNameWithoutExtension(file);
            try
            {
                if (new FileInfo(file).Length > 64L * 1024 * 1024) throw new InvalidDataException("файл больше 64 МБ");
                if (file.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)) LoadMenyooXml(file, name);
                else LoadMapJson(file, name);
                var added = _world.Count - before;
                total += added;
                Alt.Log($"[FloV:MP] карта {Path.GetFileName(file)}: {added} элементов");
            }
            catch (Exception ex)
            {
                Alt.LogWarning($"[FloV:MP] карта {Path.GetFileName(file)} не загружена: {ex.Message}");
            }
        }
        if (files.Count > 0) Alt.Log($"[FloV:MP] карты: {files.Count} файлов, {total} элементов");
        if (!broadcast) return;
        foreach (var p in _nativePlayers.Values)
        {
            var np = (NativePlayerProxy)(object)p;
            if (!_nativeReady.Contains(np.Session.Id)) continue;
            SendWorldSnapshot(np.Session, np.DimensionValue);
            foreach (var key in oldInteriors)
                if (!_world.ContainsKey(key))
                {
                    var colon = key.IndexOf(':');
                    SendWorldLine(np.Session, NativeProtocol.Format("WDEL", key[..colon], key[(colon + 1)..]));
                }
        }
    }

    private void MapPut(string kind, string id, int dim, params object?[] fields)
    {
        if (PutWorldQuiet(kind, id, dim, fields)) _worldFromMaps.Add(Key(kind, id));
    }

    // При загрузке карт рассылка идёт одним снимком в конце, а не строкой на элемент.
    private bool PutWorldQuiet(string kind, string id, int dim, object?[] fields)
    {
        if (_world.Count >= MaxWorldItems) return false;
        _world[Key(kind, id)] = new WorldItem(kind, id, dim, fields);
        return true;
    }

    private void LoadMapJson(string file, string map)
    {
        using var doc = JsonDocument.Parse(File.ReadAllBytes(file), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        var root = doc.RootElement;
        float F(JsonElement e, string n, float d = 0) => e.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetSingle(out var f) && float.IsFinite(f) ? f : d;
        int I(JsonElement e, string n, int d = 0) => e.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i) ? i : d;
        string S(JsonElement e, string n, string d = "") => e.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? d : d;
        bool B(JsonElement e, string n, bool d) => e.TryGetProperty(n, out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False ? v.GetBoolean() : d;
        int Dim(JsonElement e) => e.TryGetProperty("dimension", out var v) && v.ValueKind == JsonValueKind.String && v.GetString() == "all" ? DimensionAll : I(e, "dimension");
        IEnumerable<JsonElement> Arr(string n) => root.TryGetProperty(n, out var a) && a.ValueKind == JsonValueKind.Array ? a.EnumerateArray() : Enumerable.Empty<JsonElement>();

        var n = 0;
        foreach (var o in Arr("objects"))
            MapPut("OBJ", $"{map}#o{n++}", Dim(o), ModelHash(S(o, "model")), F(o, "x"), F(o, "y"), F(o, "z"),
                F(o, "rx"), F(o, "ry"), F(o, "rz"), (B(o, "frozen", true) ? 1 : 0) | (B(o, "collision", true) ? 0 : 2));
        n = 0;
        foreach (var b in Arr("blips"))
            MapPut("BLIP", $"{map}#b{n++}", Dim(b), F(b, "x"), F(b, "y"), F(b, "z"), Math.Clamp(I(b, "sprite", 1), 0, 1000),
                Math.Clamp(I(b, "color", 0), 0, 100), Math.Clamp(F(b, "scale", 0.8f), 0.1f, 5f), B(b, "shortRange", true) ? 1 : 0, Clean(S(b, "name"), 64));
        n = 0;
        foreach (var m in Arr("markers"))
        {
            var c = ParseRgba(S(m, "color", "#ff3d8ab4"));
            MapPut("MARKER", $"{map}#m{n++}", Dim(m), Math.Clamp(I(m, "type", 1), 0, 43), F(m, "x"), F(m, "y"), F(m, "z"),
                Math.Clamp(F(m, "scale", 1f), 0.1f, 50f), c.R, c.G, c.B, c.A, Math.Clamp(F(m, "distance", 60f), 5f, 500f));
        }
        n = 0;
        foreach (var l in Arr("labels"))
            MapPut("LABEL", $"{map}#l{n++}", Dim(l), F(l, "x"), F(l, "y"), F(l, "z"), Math.Clamp(F(l, "distance", 20f), 1f, 200f), Clean(S(l, "text"), 200));
        n = 0;
        foreach (var p in Arr("npcs"))
            MapPut("NPC", $"{map}#n{n++}", Dim(p), ModelHash(S(p, "model")), F(p, "x"), F(p, "y"), F(p, "z"), F(p, "heading"), Clean(S(p, "scenario"), 64));
        // Интерьерные моды обычно идут со списком IPL: карта включает их сама.
        // Строка — загрузить; объект {"name", "loaded": false} — убрать.
        foreach (var i in Arr("ipls"))
        {
            var name = i.ValueKind == JsonValueKind.String ? i.GetString() ?? "" : S(i, "name");
            var loaded = i.ValueKind != JsonValueKind.Object || B(i, "loaded", true);
            // IPL — один на имя: заданный в client.cfg или кодом ресурса важнее карты.
            if (GameName.IsMatch(name) && _world.ContainsKey(Key("IPL", name))) continue;
            if (GameName.IsMatch(name)) MapPut("IPL", name, DimensionAll, loaded ? 1 : 0);
            else Alt.LogWarning($"[FloV:MP] карта {map}: IPL «{name}» — латиница, цифры, _ и -");
        }
        n = 0;
        foreach (var s in Arr("interiorSets"))
        {
            var set = S(s, "set");
            if (GameName.IsMatch(set))
                MapPut("ISET", $"{map}#i{n++}", DimensionAll, F(s, "x"), F(s, "y"), F(s, "z"), set, B(s, "enabled", true));
            else Alt.LogWarning($"[FloV:MP] карта {map}: набор интерьера «{set}» — латиница, цифры, _ и -");
        }
    }

    /// <summary>Расстановка Menyoo (SpoonerPlacements): объекты и NPC.</summary>
    private void LoadMenyooXml(string file, string map)
    {
        var settings = new System.Xml.XmlReaderSettings { DtdProcessing = System.Xml.DtdProcessing.Prohibit, XmlResolver = null };
        using var reader = System.Xml.XmlReader.Create(file, settings);
        var doc = XDocument.Load(reader);
        float Num(XElement? e) => e is not null && float.TryParse(e.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) && float.IsFinite(f) ? f : 0f;
        var n = 0;
        var skipped = 0;
        foreach (var pl in doc.Descendants("Placement"))
        {
            var type = (string?)pl.Element("Type") ?? "3";
            var model = ModelHash((string?)pl.Element("ModelHash") ?? (string?)pl.Element("HashName"));
            var pr = pl.Element("PositionRotation");
            if (model == 0 || pr is null) { skipped++; continue; }
            float x = Num(pr.Element("X")), y = Num(pr.Element("Y")), z = Num(pr.Element("Z"));
            float pitch = Num(pr.Element("Pitch")), roll = Num(pr.Element("Roll")), yaw = Num(pr.Element("Yaw"));
            var frozen = !string.Equals((string?)pl.Element("FrozenPos"), "false", StringComparison.OrdinalIgnoreCase);
            var collision = !string.Equals((string?)pl.Element("IsCollisionProof"), "true", StringComparison.OrdinalIgnoreCase) &&
                            !string.Equals((string?)pl.Element("HasCollision") ?? "true", "false", StringComparison.OrdinalIgnoreCase);
            switch (type)
            {
                case "3":
                    MapPut("OBJ", $"{map}#x{n++}", 0, model, x, y, z, pitch, roll, yaw, (frozen ? 1 : 0) | (collision ? 0 : 2));
                    break;
                case "1":
                    var scenario = (string?)pl.Element("PedProperties")?.Element("ScenarioName") ?? "";
                    MapPut("NPC", $"{map}#p{n++}", 0, model, x, y, z, yaw, Clean(scenario, 64));
                    break;
                default:
                    skipped++; // транспорт из расстановок не ставится: у игроков он свой, через /car
                    break;
            }
        }
        if (skipped > 0) Alt.Log($"[FloV:MP] карта {Path.GetFileName(file)}: пропущено {skipped} (транспорт или без модели)");
    }

    private const string MapsReadme = """
        Карты и объекты FloV:MP для клиентов GTA V Legacy 1.0.3889.0
        ================================================================

        Положите сюда файлы карт — сервер загрузит их при старте, а команда
        reloadmaps в консоли сервера перечитает их без перезапуска (игроки
        получат изменения сразу).

        1) *.xml — расстановки Menyoo (Object Spooner → Save file) и Map Editor
           в формате Menyoo: объекты и NPC ставятся как в редакторе.

        2) *.json — свой формат:
        {
          "objects": [ { "model": "prop_bench_01a", "x": 200.0, "y": -930.0, "z": 29.7,
                         "rx": 0, "ry": 0, "rz": 90, "frozen": true, "collision": true, "dimension": 0 } ],
          "blips":   [ { "x": 200, "y": -930, "z": 30, "sprite": 1, "color": 2, "scale": 0.8, "name": "Мэрия" } ],
          "markers": [ { "type": 1, "x": 200, "y": -930, "z": 29, "scale": 1.5, "color": "#ff3d8ab4", "distance": 60 } ],
          "labels":  [ { "x": 200, "y": -930, "z": 31, "text": "{ff3d8a}Мэрия{ffffff} — вход", "distance": 20 } ],
          "npcs":    [ { "model": "a_m_y_business_01", "x": 201, "y": -931, "z": 29.7, "heading": 180,
                         "scenario": "WORLD_HUMAN_STAND_MOBILE" } ],
          "ipls":    [ "hei_yacht_heist", { "name": "fakeint", "loaded": false } ],
          "interiorSets": [ { "x": -1152.0, "y": -1520.0, "z": 10.6, "set": "office_chairs", "enabled": true } ]
        }
        ipls — части карты игры (интерьеры, яхта, квартиры): загрузить или убрать.
        interiorSets — оформление интерьера по точке внутри него (мебель, вывески).
        Большинству интерьеров квартир и офисов нужна world.mp_map = on в client.cfg.
        "dimension": "all" — видно во всех измерениях.

        Модели — любые, что есть в игре у игроков (имя или хэш 0x...). Свои
        модели (не из GTA) должны быть установлены у игроков как дополнение
        к игре — сам сервер их не передаёт.

        Из своего ресурса (C#) то же самое делается событиями:
          Alt.Emit("flovmp:world:object", "id", "prop_bench_01a", x, y, z, rx, ry, rz, dimension, frozen, collision);
          Alt.Emit("flovmp:world:blip", "id", x, y, z, sprite, color, scale, "Подпись", dimension);
          Alt.Emit("flovmp:world:marker", "id", type, x, y, z, scale, "#ff3d8ab4", dimension);
          Alt.Emit("flovmp:world:label", "id", x, y, z, "Текст", distance, dimension);
          Alt.Emit("flovmp:world:npc", "id", "a_m_y_business_01", x, y, z, heading, "WORLD_HUMAN_SMOKING", dimension);
          Alt.Emit("flovmp:world:remove", "object", "id");
        """;
}
