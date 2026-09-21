using System.Globalization;
using System.Text;

namespace FloVMP.Core.Settings;

/// <summary>
/// Настройки сервера владельцем без программирования: файл
/// <c>server/config/client.cfg</c>, строки «ключ = значение», # — комментарий.
///
/// Всё, что платформа показывает или делает «от себя» (HUD, ники, NPC, пауза,
/// маркеры, чат, голос, спавн, погода...), настраивается здесь: у каждого
/// проекта свой вкус, и ждать ради этого обновления платформы владелец не должен.
/// Ключи с пометкой «клиент» сервер рассылает игрокам при входе и после
/// <c>reloadsettings</c> — без перезапуска сервера и игры.
///
/// Разбор терпимый: опечатка — предупреждение в лог и значение по умолчанию.
/// </summary>
public sealed class ServerSettings
{
    public const string FileName = "client.cfg";

    public enum Kind { Bool, Int, Float, Text, Color, Key }

    public sealed record Def(string Key, Kind Type, string Default, bool Client, string Section, string Help,
                             double Min = double.MinValue, double Max = double.MaxValue);

    /// <summary>Схема: порядок = порядок в файле-образце.</summary>
    public static readonly IReadOnlyList<Def> Schema = new Def[]
    {
        // --- Мир ------------------------------------------------------------------
        new("world.peds", Kind.Bool, "off", true, "Мир", "прохожие (NPC) на улицах"),
        new("world.traffic", Kind.Bool, "off", true, "Мир", "машины NPC на дорогах"),
        new("world.parked_vehicles", Kind.Bool, "off", true, "Мир", "стоящие машины у обочин"),
        new("world.police", Kind.Bool, "off", true, "Мир", "полиция и розыск"),
        new("world.ambient_events", Kind.Bool, "off", true, "Мир", "поезда, лодки, самолёты, мусоровозы"),
        new("world.weather", Kind.Text, "", false, "Мир", "погода при старте: EXTRASUNNY, CLEAR, CLOUDS, RAIN... Пусто — у каждого своя"),
        new("world.time", Kind.Text, "", false, "Мир", "время при старте, ЧЧ:ММ. Пусто — у каждого своё"),
        new("world.freeze_time", Kind.Bool, "off", true, "Мир", "остановить часы (время не идёт)"),

        // --- Появление -------------------------------------------------------------
        new("spawn.points", Kind.Text, "198.8, -935.6, 30.7, 140", false, "Появление",
            "точки появления «x, y, z, курс» через «;» — выбирается случайная"),
        new("spawn.model", Kind.Text, "mp_m_freemode_01", false, "Появление", "модель персонажа при входе"),
        new("spawn.health", Kind.Int, "200", false, "Появление", "здоровье при появлении (100 — ноль, 200 — полное)", 101, 1000),
        new("spawn.armor", Kind.Int, "0", false, "Появление", "броня при появлении", 0, 100),
        new("spawn.respawn", Kind.Bool, "on", false, "Появление", "возрождать после смерти (off — решает ваш ресурс)"),
        new("spawn.respawn_delay", Kind.Int, "3000", false, "Появление", "задержка возрождения, мс", 0, 60000),

        // --- Интерфейс ----------------------------------------------------------------
        new("hud.minimap", Kind.Bool, "on", true, "Интерфейс", "мини-карта"),
        new("hud.ability_bar", Kind.Bool, "off", true, "Интерфейс", "полоска способности персонажа (оранжевая)"),
        new("hud.area_names", Kind.Bool, "off", true, "Интерфейс", "названия районов и улиц"),
        new("hud.vehicle_names", Kind.Bool, "off", true, "Интерфейс", "название машины при посадке"),
        new("hud.weapon_wheel", Kind.Bool, "off", true, "Интерфейс", "колесо оружия на Tab"),
        new("hud.pause_menu", Kind.Bool, "off", true, "Интерфейс", "меню паузы на Esc (в сюжетной GTA оно останавливает игру)"),
        new("hud.player_blips", Kind.Bool, "off", true, "Интерфейс", "маркеры игроков на карте и мини-карте"),
        new("hud.watermark", Kind.Bool, "off", true, "Интерфейс", "строка «сервер · ID · пинг» в углу экрана"),
        new("hud.accent", Kind.Color, "#ffffff", true, "Интерфейс", "акцентный цвет интерфейса (чат, консоль)"),

        // --- Ники -----------------------------------------------------------------------
        new("nametags.enabled", Kind.Bool, "on", true, "Ники", "ники над игроками"),
        new("nametags.distance", Kind.Float, "30", true, "Ники", "дальность, м", 3, 500),
        new("nametags.show_id", Kind.Bool, "on", true, "Ники", "ID в скобках: Nick Name (12)"),
        new("nametags.health", Kind.Bool, "on", true, "Ники", "полоска здоровья"),
        new("nametags.armor", Kind.Bool, "on", true, "Ники", "полоска брони (только если броня есть)"),
        new("nametags.voice_icon", Kind.Bool, "on", true, "Ники", "значок микрофона у говорящего"),
        new("nametags.admin_badge", Kind.Bool, "off", true, "Ники", "метка ADMIN у администраторов"),
        new("nametags.color", Kind.Color, "#ffffff", true, "Ники", "цвет ника"),
        new("nametags.underscore_to_space", Kind.Bool, "on", true, "Ники", "Nick_Name показывать как Nick Name"),

        // --- Чат -------------------------------------------------------------------------
        new("chat.enabled", Kind.Bool, "on", true, "Чат", "чат (T)"),
        new("chat.lines", Kind.Int, "10", true, "Чат", "сколько строк видно", 3, 30),
        new("chat.fade_seconds", Kind.Int, "15", true, "Чат", "через сколько секунд гаснут строки (0 — не гаснут)", 0, 600),
        new("chat.timestamps", Kind.Bool, "on", true, "Чат", "время у сообщений"),
        new("chat.width", Kind.Int, "540", true, "Чат", "ширина, пикселей при 1080p", 300, 1200),
        new("chat.max_length", Kind.Int, "256", true, "Чат", "максимум символов в сообщении", 16, 1024),
        new("chat.rp_commands", Kind.Bool, "on", false, "Чат", "команды /me /do /b /s /w"),
        new("chat.welcome", Kind.Text, "{ff3d8a}[FloV:MP]{ffffff} Добро пожаловать на сервер!", false, "Чат",
            "приветствие при входе; {rrggbb} — цвет. Пусто — без приветствия"),

        // --- Консоль ------------------------------------------------------------------------
        new("console.enabled", Kind.Bool, "on", true, "Консоль", "консоль F8 (журнал, сеть и FPS; админам — инструменты)"),
        new("console.theme", Kind.Text, "obsidian", true, "Консоль", "оформление консоли: obsidian, slate или glass"),

        // --- Загрузка и окно игры ------------------------------------------------------------
        new("loading.enabled", Kind.Bool, "on", true, "Загрузка", "загрузочный экран от подключения до появления в мире"),
        new("loading.title", Kind.Text, "", true, "Загрузка", "заголовок загрузочного экрана. Пусто — имя сервера"),
        new("loading.accent", Kind.Color, "#fbbf24", true, "Загрузка", "цвет полосы загрузки и логотипа"),
        new("loading.tips", Kind.Text,
            "{a1a1aa}Голос:{71717a} удерживайте N, чтобы говорить. Вас слышат те, кто рядом. | " +
            "{a1a1aa}Чат:{71717a} клавиша T. Команды начинаются с «/», список — /help. | " +
            "{a1a1aa}Консоль:{71717a} клавиша F8 — там видно, что происходит, если что-то пошло не так. | " +
            "{a1a1aa}Первый вход{71717a} дольше обычного: игра подгружает город.",
            true, "Загрузка", "подсказки на загрузочном экране через « | »; {rrggbb} — цвет"),
        new("window.title", Kind.Text, "FloV Multiplayer — {server}", true, "Загрузка",
            "заголовок окна игры (панель задач, Alt+Tab, диспетчер задач); {server} — имя сервера"),

        // --- Голос ----------------------------------------------------------------------------
        new("voice.enabled", Kind.Bool, "on", true, "Голос", "голосовой чат клиентов b3889"),
        new("voice.radius", Kind.Float, "25", true, "Голос", "радиус слышимости, м", 3, 500),
        new("voice.key", Kind.Key, "N", true, "Голос", "кнопка разговора (держать)"),

        // --- Клавиши администратора ------------------------------------------------------------
        new("keys.esp", Kind.Key, "F3", true, "Клавиши", "ESP (администраторам)"),
        new("keys.noclip", Kind.Key, "F4", true, "Клавиши", "NoClip (администраторам)"),
        new("keys.waypoint", Kind.Key, "F5", true, "Клавиши", "телепорт на метку (администраторам)"),
        new("keys.chat", Kind.Key, "T", true, "Клавиши", "открыть чат"),
        new("keys.console", Kind.Key, "F8", true, "Клавиши", "консоль"),

        // --- Производительность ------------------------------------------------------------------
        new("sync.stream_radius", Kind.Float, "400", false, "Производительность", "дальность, в которой видно других игроков, м", 50, 1500),
        new("sync.max_streamed", Kind.Int, "150", false, "Производительность",
            "сколько ближайших игроков максимум видит каждый (FPS и трафик при большом онлайне)", 10, 1000),
    };

    private static readonly Dictionary<string, Def> ByKey =
        Schema.ToDictionary(d => d.Key, StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);

    public ServerSettings()
    {
        foreach (var d in Schema) _values[d.Key] = d.Default;
    }

    public string Get(string key) => _values.TryGetValue(key, out var v) ? v : ByKey[key].Default;
    public bool Bool(string key) => Get(key) is "on" or "true" or "1" or "yes";
    public int Int(string key) => int.Parse(Get(key), CultureInfo.InvariantCulture);
    public float Float(string key) => float.Parse(Get(key), CultureInfo.InvariantCulture);

    /// <summary>Настройки для клиента: «ключ=значение» через табуляцию-разделитель протокола.</summary>
    public IEnumerable<(string Key, string Value)> ClientValues() =>
        Schema.Where(d => d.Client).Select(d => (d.Key, Get(d.Key)));

    /// <summary>Точки появления: x, y, z, курс. Пустые/битые пропускаются.</summary>
    public List<(float X, float Y, float Z, float Heading)> SpawnPoints()
    {
        var list = new List<(float, float, float, float)>();
        foreach (var chunk in Get("spawn.points").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var p = chunk.Split(',', StringSplitOptions.TrimEntries);
            if (p.Length < 3) continue;
            if (!TryF(p[0], out var x) || !TryF(p[1], out var y) || !TryF(p[2], out var z)) continue;
            var h = p.Length > 3 && TryF(p[3], out var hh) ? hh : 0f;
            if (Math.Abs(x) > 25000 || Math.Abs(y) > 25000 || z < -500 || z > 3000) continue;
            list.Add((x, y, z, h));
        }
        return list;
    }

    private static bool TryF(string s, out float v) =>
        float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v) && float.IsFinite(v);

    /// <summary>Прочитать текст файла. Возвращает настройки и замечания (строка N: ...).</summary>
    public static (ServerSettings Settings, List<string> Problems, HashSet<string> Present) Parse(string text)
    {
        var s = new ServerSettings();
        var problems = new List<string>();
        var present = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var lineNo = 0;
        foreach (var raw in text.Split('\n'))
        {
            lineNo++;
            var line = StripComment(raw).Trim();
            if (line.Length == 0) continue;
            var eq = line.IndexOf('=');
            if (eq <= 0) { problems.Add($"строка {lineNo}: ожидалось «ключ = значение»"); continue; }
            var key = line[..eq].Trim();
            var value = line[(eq + 1)..].Trim();
            if (value.Length >= 2 && value[0] == '"' && value[^1] == '"') value = value[1..^1];
            if (!ByKey.TryGetValue(key, out var def)) { problems.Add($"строка {lineNo}: неизвестный ключ «{key}»"); continue; }
            present.Add(def.Key);
            var normalized = Normalize(def, value, out var why);
            if (normalized is null) { problems.Add($"строка {lineNo}: {def.Key} = «{value}» — {why}; оставлено {def.Default}"); continue; }
            s._values[def.Key] = normalized;
        }
        return (s, problems, present);
    }

    // Цвет #ff00ff в значении — не комментарий: комментарий — строка с # в начале
    // или « # » (решётка с пробелами) после значения.
    private static string StripComment(string raw)
    {
        var t = raw.TrimStart();
        if (t.StartsWith('#')) return "";
        var idx = raw.IndexOf(" # ", StringComparison.Ordinal);
        return idx >= 0 ? raw[..idx] : raw;
    }

    private static string? Normalize(Def d, string v, out string why)
    {
        why = "";
        switch (d.Type)
        {
            case Kind.Bool:
                var b = v.ToLowerInvariant();
                if (b is "on" or "true" or "1" or "yes" or "да" or "вкл") return "on";
                if (b is "off" or "false" or "0" or "no" or "нет" or "выкл") return "off";
                why = "нужно on или off"; return null;
            case Kind.Int:
                if (!int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i)) { why = "нужно целое число"; return null; }
                if (i < d.Min || i > d.Max) { why = $"допустимо {d.Min}..{d.Max}"; return null; }
                return i.ToString(CultureInfo.InvariantCulture);
            case Kind.Float:
                if (!TryF(v.Replace(',', '.'), out var f)) { why = "нужно число"; return null; }
                if (f < d.Min || f > d.Max) { why = $"допустимо {d.Min}..{d.Max}"; return null; }
                return f.ToString("0.###", CultureInfo.InvariantCulture);
            case Kind.Color:
                var c = v.TrimStart('#');
                if (c.Length != 6 || !c.All(Uri.IsHexDigit)) { why = "нужен цвет #rrggbb"; return null; }
                return "#" + c.ToLowerInvariant();
            case Kind.Key:
                var k = v.ToUpperInvariant();
                if (KeyCode(k) == 0) { why = "клавиша: A..Z, 0..9, F1..F12"; return null; }
                return k;
            default:
                if (v.Length > 512) { why = "слишком длинно (до 512 символов)"; return null; }
                if (v.Any(ch => ch == '\t' || char.IsControl(ch))) { why = "управляющие символы недопустимы"; return null; }
                return v;
        }
    }

    /// <summary>Виртуальный код клавиши Windows: A..Z, 0..9, F1..F12. 0 — неизвестная.</summary>
    public static int KeyCode(string k)
    {
        if (k.Length == 1 && k[0] is >= 'A' and <= 'Z' or >= '0' and <= '9') return k[0];
        if (k.Length is 2 or 3 && k[0] == 'F' && int.TryParse(k[1..], out var n) && n is >= 1 and <= 12) return 0x70 + n - 1;
        return 0;
    }

    public static string DefaultFileContent()
    {
        var sb = new StringBuilder();
        sb.Append("# FloV:MP — настройки сервера и того, что видят игроки.\n");
        sb.Append("#\n");
        sb.Append("# Формат: ключ = значение. on/off — включить/выключить.\n");
        sb.Append("# После правки: команда reloadsettings в консоли сервера — игроки получат\n");
        sb.Append("# изменения сразу, без перезахода. Ошибка в строке не ломает сервер: в лог\n");
        sb.Append("# пишется замечание, берётся значение по умолчанию.\n");
        string? section = null;
        foreach (var d in Schema)
        {
            if (d.Section != section)
            {
                section = d.Section;
                sb.Append("\n# --- ").Append(section).Append(' ').Append(new string('-', Math.Max(3, 60 - section.Length))).Append('\n');
            }
            sb.Append("# ").Append(d.Help).Append('\n');
            sb.Append(d.Key).Append(" = ").Append(d.Default).Append('\n');
        }
        return sb.ToString();
    }

    /// <summary>
    /// Загрузить из папки config (создать образец, если файла нет). Ключи,
    /// появившиеся в новой версии платформы, дописываются в конец файла
    /// владельца со значениями по умолчанию — его правки не трогаются.
    /// </summary>
    public static ServerSettings LoadOrCreate(string configDir, Action<string> warn, out string summary)
    {
        var path = Path.Combine(configDir, FileName);
        try
        {
            Directory.CreateDirectory(configDir);
            if (!File.Exists(path))
            {
                File.WriteAllText(path, DefaultFileContent(), new UTF8Encoding(false));
                summary = $"создан {path} — настройки по умолчанию";
                return new ServerSettings();
            }
            var text = File.ReadAllText(path, Encoding.UTF8);
            var (settings, problems, present) = Parse(text);
            foreach (var p in problems) warn($"[FloV:MP] {FileName}: {p}");
            var missing = Schema.Where(d => !present.Contains(d.Key)).ToList();
            if (missing.Count > 0)
            {
                var sb = new StringBuilder();
                if (!text.EndsWith('\n')) sb.Append('\n');
                sb.Append($"\n# --- добавлено обновлением платформы {DateTime.UtcNow:yyyy-MM-dd} ---\n");
                foreach (var d in missing) sb.Append("# ").Append(d.Help).Append('\n').Append(d.Key).Append(" = ").Append(d.Default).Append('\n');
                File.AppendAllText(path, sb.ToString(), new UTF8Encoding(false));
            }
            summary = $"{FileName}: {present.Count} ключей" + (missing.Count > 0 ? $", дописано новых: {missing.Count}" : "") +
                      (problems.Count > 0 ? $", замечаний: {problems.Count}" : "");
            return settings;
        }
        catch (Exception ex)
        {
            warn($"[FloV:MP] {FileName} не прочитан ({ex.Message}) — настройки по умолчанию");
            summary = $"{FileName}: по умолчанию";
            return new ServerSettings();
        }
    }
}
