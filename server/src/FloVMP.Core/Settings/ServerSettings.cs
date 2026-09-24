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
        new("world.mp_map", Kind.Bool, "off", true, "Мир",
            "карта сетевой игры: интерьеры квартир, офисов и дополнений (нужна большинству RP-интерьеров)"),
        new("world.ipls", Kind.Text, "", false, "Мир",
            "части карты (IPL), которые загрузить всем: имена через запятую, например hei_yacht_heist, TrevorsTrailerTidy"),
        new("world.ipls_remove", Kind.Text, "", false, "Мир",
            "части карты (IPL), которые убрать у всех: имена через запятую"),
        new("world.name", Kind.Text, "main", false, "Мир",
            "имя мира в базе (машины и игроки): несколько серверов на одной базе с разными именами не видят данных друг друга"),

        // --- транспорт ---
        new("vehicles.power", Kind.Float, "1", true, "Транспорт",
            "множитель мощности двигателя: 1 — как в игре, 2 — вдвое быстрее разгон и выше предел", 0.1f, 10f),
        new("vehicles.torque", Kind.Float, "1", true, "Транспорт",
            "множитель тяги (разгон с места, езда в гору)", 0.1f, 10f),
        new("vehicles.max_registered", Kind.Int, "1000", false, "Транспорт",
            "потолок машин в реестре сервера (свои, /car и трафик, в который сели игроки)", 1, 100000),
        new("vehicles.abandoned_ttl_sec", Kind.Int, "300", false, "Транспорт",
            "через сколько секунд убирать машину трафика, в которую садились: пустую и без игроков рядом; 0 — никогда. Машины /car и геймода сами не исчезают", 0, 86400),
        new("vehicles.register_traffic", Kind.Bool, "on", false, "Транспорт",
            "брать в реестр машину трафика, в которую сел игрок (off — ездить можно только на машинах сервера и геймода)"),
        new("vehicles.register_cooldown_sec", Kind.Float, "2", false, "Транспорт",
            "не чаще одной регистрации машины трафика на игрока за столько секунд", 0, 60),
        new("vehicles.enter_distance", Kind.Float, "10", false, "Транспорт",
            "дальше скольких метров от машины сервер не сажает в неё (защита от «телепорта в машину»)", 2, 50),
        new("vehicles.plate_format", Kind.Text, "99AAA999", false, "Транспорт",
            "шаблон случайного номера: 9 — цифра, A — буква, остальное как есть (латиница, цифры, пробел), до 8 символов"),
        new("vehicles.persistence", Kind.Bool, "on", false, "Транспорт",
            "сохранять машины, помеченные «сохраняемая», и возвращать их после перезапуска (off — сохраняет ваш геймод сам)"),
        new("vehicles.save_interval_sec", Kind.Int, "30", false, "Транспорт",
            "как часто записывать изменившиеся сохраняемые машины, секунд (при парковке — сразу)", 5, 3600),
        new("vehicles.restore_damage", Kind.Bool, "on", false, "Транспорт",
            "после перезапуска машина с теми же повреждениями (off — все встают целыми)"),

        // --- Моды (пункт 4) ---
        new("mods.serve", Kind.Bool, "on", false, "Моды",
            "раздавать игрокам моды из server/mods с этого сервера (порт mods.http_port)"),
        new("mods.http_port", Kind.Int, "0", false, "Моды",
            "порт раздачи модов, TCP; 0 — порт игры + 20 (7788 → 7808)", 0, 65535),
        new("mods.public_url", Kind.Text, "", false, "Моды",
            "свой CDN с копией server/mods (…/manifest.json и …/files/<путь>): для большой карты и сотен игроков. Пусто — раздаёт сам сервер"),
        new("mods.required", Kind.Bool, "off", false, "Моды",
            "on — без нужных модов на сервер не пускать (игрок видит, что скачать); off — пускать с предупреждением"),

        // --- Метрики и оповещения ---
        new("metrics.log_interval_sec", Kind.Int, "60", false, "Метрики",
            "как часто писать метрики в журнал и flovmp-data/metrics.json, секунд; 0 — не писать", 0, 3600),
        new("metrics.http_port", Kind.Int, "0", false, "Метрики",
            "порт страницы метрик (/metrics — JSON, /metrics.prom — Prometheus); 0 — выключена", 0, 65535),
        new("metrics.http_bind", Kind.Text, "127.0.0.1", false, "Метрики",
            "адрес страницы метрик: 127.0.0.1 — только с этой машины, 0.0.0.0 — снаружи (только с metrics.token)"),
        new("metrics.token", Kind.Text, "", false, "Метрики",
            "токен страницы метрик: ?token=… или заголовок Authorization: Bearer …"),
        new("alerts.webhook_url", Kind.Text, "", false, "Метрики",
            "куда слать оповещения: webhook Discord, Slack, Mattermost или свой; пусто — только журнал и администраторы"),
        new("alerts.admins_chat", Kind.Bool, "on", false, "Метрики", "дублировать оповещения администраторам в игре"),
        new("alerts.tick_ms", Kind.Float, "250", false, "Метрики",
            "оповестить, если тик длился дольше, мс (рывок у всех игроков); 0 — не проверять", 0, 60000),
        new("alerts.min_tick_rate", Kind.Float, "0", false, "Метрики",
            "оповестить, если при игроках тиков в секунду меньше; 0 — не проверять", 0, 10000),
        new("alerts.errors_per_window", Kind.Int, "10", false, "Метрики",
            "оповестить, если ошибок за окно метрик больше; 0 — не проверять", 0, 100000),
        new("alerts.memory_mb", Kind.Int, "0", false, "Метрики", "оповестить, если процесс занял больше, МБ; 0 — не проверять", 0, 1000000),
        new("alerts.hang_sec", Kind.Int, "30", false, "Метрики",
            "оповестить, если главный поток сервера не отвечает столько секунд (зависание); 0 — не проверять", 0, 3600),
        new("alerts.cooldown_min", Kind.Int, "10", false, "Метрики", "не повторять одно и то же оповещение чаще, минут", 1, 1440),

        // --- Сохранение игрока ---
        new("players.persistence", Kind.Bool, "on", false, "Игроки",
            "сохранять игроков и восстанавливать при входе (off — всё делает ваш геймод)"),
        new("players.save_interval_sec", Kind.Int, "60", false, "Игроки",
            "как часто сохранять игроков в игре, секунд (при выходе и остановке сервера — всегда)", 10, 3600),
        new("players.restore_position", Kind.Bool, "on", false, "Игроки",
            "появляться там, где вышел (off — точка появления; нужно, если у вас выбор персонажа)"),
        new("players.restore_health", Kind.Bool, "on", false, "Игроки", "восстанавливать здоровье и броню (вышедший мёртвым появляется целым)"),
        new("players.restore_model", Kind.Bool, "on", false, "Игроки", "восстанавливать модель персонажа"),
        new("players.restore_weapons", Kind.Bool, "off", false, "Игроки",
            "возвращать оружие, выданное сервером. Патроны — выдано минус попадания: промахи сервер не видит, поэтому перезаход возвращает отстрелянное мимо. Включайте, если патроны не ценность экономики"),
        new("players.restore_dimension", Kind.Bool, "off", false, "Игроки",
            "возвращать в то же измерение (обычно измерения временные — квартиры, миссии)"),
        new("players.data", Kind.Bool, "on", false, "Игроки",
            "хранилище «ключ → значение» для геймода (инвентарь, деньги) — flovmp:player:data:*"),

        // --- Античит (вторая линия) ---
        new("anticheat.notify_score", Kind.Float, "50", false, "Античит",
            "счёт подозрений, при котором администраторам в чат уходит предупреждение; 0 — не предупреждать", 0, 100000),
        new("anticheat.kick_score", Kind.Float, "0", false, "Античит",
            "счёт, при котором игрока отключает; 0 — никогда (по умолчанию решает администратор или ваш геймод)", 0, 100000),
        new("anticheat.decay_per_minute", Kind.Float, "5", false, "Античит",
            "сколько очков подозрений тает за минуту: у честного игрока с лагами счёт не копится", 0, 10000),
        new("anticheat.weight_movement", Kind.Float, "10", false, "Античит", "вес: телепорт, скорость, полёт", 0, 10000),
        new("anticheat.weight_hit", Kind.Float, "5", false, "Античит", "вес: отклонённое попадание (дальность, частота, чужое оружие)", 0, 10000),
        new("anticheat.weight_weapon", Kind.Float, "25", false, "Античит", "вес: запрещённое или невыданное оружие в руках", 0, 10000),
        new("anticheat.weight_ammo", Kind.Float, "10", false, "Античит", "вес: попаданий больше, чем выдано патронов", 0, 10000),
        new("anticheat.weight_timescale", Kind.Float, "30", false, "Античит", "вес: часы игры разогнаны (speedhack)", 0, 10000),
        new("anticheat.weight_vehicle", Kind.Float, "10", false, "Античит", "вес: машина (чужой VSYNC, физика, запрещённая модель)", 0, 10000),
        new("anticheat.weight_model", Kind.Float, "25", false, "Античит", "вес: модель персонажа не из разрешённых", 0, 10000),
        new("anticheat.weapon_blacklist", Kind.Text,
            "weapon_minigun, weapon_rpg, weapon_hominglauncher, weapon_grenadelauncher, weapon_railgun, weapon_rayminigun, weapon_raypistol, weapon_emplauncher",
            false, "Античит", "оружие, которого не должно быть ни у кого: имена через запятую (пусто — без списка)"),
        new("anticheat.issued_weapons_only", Kind.Bool, "off", false, "Античит",
            "оружие только от сервера: всё, что не выдано командой или геймодом, — подозрение"),
        new("anticheat.ammo_accounting", Kind.Bool, "on", false, "Античит",
            "сверять попадания с выданными сервером патронами (оружие, выданное с 0 патронов, не считается)"),
        new("anticheat.ped_whitelist", Kind.Text, "", false, "Античит",
            "модели персонажа, которые можно носить, через запятую; пусто — любые (spawn.model и выданные сервером можно всегда)"),
        new("anticheat.vehicle_blacklist", Kind.Text, "rhino, khanjali, lazer, hydra, oppressor, oppressor2, deluxo", false, "Античит",
            "машины, которые не берутся в реестр как «трафик» (такие не ездят по улицам — значит, созданы читом); сервер и геймод создавать их могут"),
        new("anticheat.timescale_ratio", Kind.Float, "1.25", false, "Античит",
            "во сколько раз часы игры могут обгонять сервер, прежде чем это подозрение (speedhack)", 1.05, 10),

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
        new("hud.pause_menu", Kind.Bool, "off", true, "Интерфейс", "устарело: штатная пауза GTA всегда отключена, на Esc открывается меню FloV:MP"),
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
        new("nametags.scale", Kind.Float, "1", true, "Ники", "масштаб ника и полосок", 0.5, 1.5),
        new("nametags.bar_width", Kind.Float, "74", true, "Ники", "ширина полосок здоровья и брони", 24, 240),
        new("nametags.bar_height", Kind.Float, "5", true, "Ники", "высота полосок здоровья и брони", 2, 16),
        new("nametags.health_color", Kind.Color, "#4ade80", true, "Ники", "цвет обычного здоровья"),
        new("nametags.health_low_color", Kind.Color, "#f87171", true, "Ники", "цвет здоровья ниже 30 процентов"),
        new("nametags.armor_color", Kind.Color, "#60a5fa", true, "Ники", "цвет брони"),

        // --- Чат -------------------------------------------------------------------------
        new("chat.enabled", Kind.Bool, "on", true, "Чат", "чат (T)"),
        new("chat.lines", Kind.Int, "10", true, "Чат", "сколько строк видно", 3, 30),
        new("chat.fade_seconds", Kind.Int, "15", true, "Чат", "через сколько секунд гаснут строки (0 — не гаснут)", 0, 600),
        new("chat.timestamps", Kind.Bool, "on", true, "Чат", "время у сообщений"),
        new("chat.width", Kind.Int, "540", true, "Чат", "ширина, пикселей при 1080p", 300, 1200),
        new("chat.max_length", Kind.Int, "256", true, "Чат", "максимум символов в сообщении", 16, 1024),
        new("chat.rp_commands", Kind.Bool, "on", false, "Чат", "команды /me /do /b /s /w"),
        new("chat.radius", Kind.Float, "0", false, "Чат",
            "радиус обычного чата в метрах: 0 — слышно всему серверу, 20 — только рядом (для RP)", 0, 1000),
        new("chat.welcome", Kind.Text, "{ff3d8a}[FloV:MP]{ffffff} Добро пожаловать на сервер!", false, "Чат",
            "приветствие при входе; {rrggbb} — цвет. Пусто — без приветствия"),

        // --- Консоль ------------------------------------------------------------------------
        new("console.enabled", Kind.Bool, "on", true, "Консоль", "консоль F8 (журнал, сеть и FPS; админам — инструменты)"),
        new("console.theme", Kind.Text, "obsidian", true, "Консоль", "оформление консоли: obsidian, slate или glass"),

        // --- Загрузка и окно игры ------------------------------------------------------------
        new("loading.enabled", Kind.Bool, "on", true, "Загрузка", "загрузочный экран от подключения до появления в мире"),
        new("loading.title", Kind.Text, "", true, "Загрузка", "заголовок загрузочного экрана. Пусто — имя сервера"),
        new("loading.accent", Kind.Color, "#fbbf24", true, "Загрузка", "цвет полосы загрузки и надписей"),
        new("loading.background_url", Kind.Text, "", true, "Загрузка",
            "ссылка на фон загрузочного экрана (http или https, JPEG или PNG до 16 МБ). " +
            "Клиент скачивает картинку один раз и держит в кэше; чтобы сменить её, нужен новый адрес"),
        new("loading.logo_url", Kind.Text, "", true, "Загрузка",
            "ссылка на логотип слева снизу (PNG с прозрачностью). Пусто — логотип FloV:MP"),
        new("loading.tips", Kind.Text,
            "{a1a1aa}Голос:{71717a} удерживайте N, чтобы говорить. Вас слышат те, кто рядом. | " +
            "{a1a1aa}Чат:{71717a} клавиша T. Команды начинаются с «/», список — /help. | " +
            "{a1a1aa}Консоль:{71717a} клавиша F8 — там видно, что происходит, если что-то пошло не так. | " +
            "{a1a1aa}Первый вход{71717a} дольше обычного: игра подгружает город.",
            true, "Загрузка", "не используется с 1.0.6: подсказки с загрузочного экрана убраны"),
        new("window.title", Kind.Text, "FloV Multiplayer — {server}", true, "Загрузка",
            "заголовок окна игры (панель задач, Alt+Tab, диспетчер задач); {server} — имя сервера. Только Source Kit"),
        new("branding.name", Kind.Text, "FloV:MP", true, "Загрузка",
            "название платформы на загрузочном экране и в консоли F8. Только Source Kit"),

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

    /// <summary>
    /// Поменять значение из кода (событие flovmp:settings:set от геймода) с той
    /// же проверкой, что строка файла: опечатка не должна поставить серверу
    /// бессмысленное значение. false — ключа нет или значение не подходит.
    /// </summary>
    public bool TrySet(string key, string value, out string error)
    {
        error = "";
        if (!ByKey.TryGetValue(key ?? "", out var def)) { error = $"неизвестный ключ «{key}»"; return false; }
        var normalized = Normalize(def, (value ?? "").Trim(), out var why);
        if (normalized is null) { error = why; return false; }
        _values[def.Key] = normalized;
        return true;
    }

    public static bool IsClientKey(string key) => ByKey.TryGetValue(key, out var d) && d.Client;

    /// <summary>Ключи оформления платформы: менять их можно только с Source Kit.</summary>
    public static readonly IReadOnlySet<string> BrandingKeys =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "window.title", "branding.name" };

    /// <summary>
    /// Настройки для клиента. Без права на свой бренд ключи оформления
    /// уходят со значениями по умолчанию, что бы ни стояло в файле.
    /// </summary>
    public IEnumerable<(string Key, string Value)> ClientValues(bool brandingAllowed = true) =>
        Schema.Where(d => d.Client).Select(d =>
            (d.Key, !brandingAllowed && BrandingKeys.Contains(d.Key) ? d.Default : Get(d.Key)));

    /// <summary>Ключи оформления, изменённые владельцем (для предупреждения без Source Kit).</summary>
    public IEnumerable<string> CustomizedBrandingKeys() =>
        Schema.Where(d => BrandingKeys.Contains(d.Key) && Get(d.Key) != d.Default).Select(d => d.Key);

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
