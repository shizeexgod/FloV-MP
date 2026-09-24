using System.Globalization;

namespace FloVMP.Core.AntiCheat;

/// <summary>Хэш имени модели или оружия, как у игры (joaat): «weapon_pistol» → 0x1B06D571.</summary>
public static class GameHash
{
    public static uint Joaat(string text)
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

    /// <summary>
    /// Список «имя или число» через запятую — в набор хэшей. Так владелец
    /// пишет в client.cfg понятные имена (weapon_rpg, rhino), а не числа.
    /// </summary>
    public static HashSet<uint> ParseList(string? list)
    {
        var set = new HashSet<uint>();
        if (string.IsNullOrWhiteSpace(list)) return set;
        foreach (var raw in list.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
                uint.TryParse(raw[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hex)) set.Add(hex);
            else if (uint.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var num)) set.Add(num);
            else set.Add(Joaat(raw));
        }
        return set;
    }
}

/// <summary>
/// Учёт оружия и патронов, выданных сервером (вторая линия античита).
///
/// Сервер знает, что он выдал: команда /weapon, flovmp:native:weapon геймода.
/// Точного числа выстрелов клиент не сообщает, но каждое засчитанное
/// попадание — это минимум один выстрел. Попаданий больше, чем было патронов,
/// не бывает: это стрельба без патронов (бесконечные патроны у чита). Оружие,
/// которого сервер не выдавал, в руках у игрока — выдано самим игроком.
///
/// Холодное оружие и кулаки патронов не тратят: оружие, выданное с нулём
/// патронов, не считается. Только главный поток.
/// </summary>
public sealed class WeaponLedger
{
    public const uint Unarmed = 0xA2719263;

    private readonly Dictionary<uint, Dictionary<uint, int>> _issued = new();   // игрок → оружие → патроны (−1 — без учёта)

    /// <summary>Сервер выдал оружие: патроны добавляются к уже выданным.</summary>
    public void Issue(uint playerId, uint weapon, int ammo)
    {
        if (weapon == 0 || weapon == Unarmed) return;
        var map = MapOf(playerId);
        if (ammo <= 0) { map[weapon] = -1; return; }
        map[weapon] = map.TryGetValue(weapon, out var left) && left >= 0 ? left + ammo : ammo;
    }

    /// <summary>Сервер забрал всё оружие (разоружение, смерть по правилам геймода).</summary>
    public void Clear(uint playerId) => _issued.Remove(playerId);

    public void Remove(uint playerId) => _issued.Remove(playerId);

    public bool WasIssued(uint playerId, uint weapon) =>
        weapon == 0 || weapon == Unarmed || (_issued.TryGetValue(playerId, out var map) && map.ContainsKey(weapon));

    public int AmmoLeft(uint playerId, uint weapon) =>
        _issued.TryGetValue(playerId, out var map) && map.TryGetValue(weapon, out var left) ? left : -1;

    /// <summary>
    /// Засчитанное попадание оружием. false — патронов по учёту уже нет
    /// (попаданий больше, чем выдано патронов). Невыданное оружие здесь не
    /// проверяется — это <see cref="WasIssued"/>.
    /// </summary>
    public bool ConsumeHit(uint playerId, uint weapon)
    {
        if (!_issued.TryGetValue(playerId, out var map) || !map.TryGetValue(weapon, out var left) || left < 0) return true;
        if (left == 0) return false;
        map[weapon] = left - 1;
        return true;
    }

    private Dictionary<uint, int> MapOf(uint playerId)
    {
        if (!_issued.TryGetValue(playerId, out var map)) _issued[playerId] = map = new();
        return map;
    }
}

/// <summary>
/// Ускорение времени (speedhack): чит разгоняет часы игры, и клиент живёт
/// быстрее сервера — бегает, стреляет и перезаряжается чаще. Клиент каждые
/// ~2 с шлёт PING со своими часами; за окно в десятки секунд их ход обязан
/// совпадать с часами сервера. Отношение выше порога — часы разогнаны.
/// Пауза игры (меню) на отношение не влияет: во время неё не идут ни те, ни
/// другие отметки.
/// </summary>
public sealed class ClockDriftDetector
{
    private readonly Dictionary<uint, (long Client, long Server)> _anchor = new();

    /// <summary>Сколько секунд сервера копить, прежде чем судить (anticheat.timescale_window_sec).</summary>
    public long WindowMs { get; set; } = 20_000;
    /// <summary>Во сколько раз часы клиента могут идти быстрее (anticheat.timescale_ratio).</summary>
    public float MaxRatio { get; set; } = 1.25f;

    /// <summary>
    /// Отметка PING. Возвращает отношение хода часов клиента к серверу, если
    /// окно накоплено и отношение выше порога, иначе null. После каждого
    /// суждения окно начинается заново — одна проверка на окно.
    /// </summary>
    public float? Sample(uint playerId, long clientMs, long serverMs)
    {
        if (!_anchor.TryGetValue(playerId, out var a) || clientMs < a.Client || serverMs < a.Server)
        {
            _anchor[playerId] = (clientMs, serverMs);   // первая отметка или часы клиента сброшены
            return null;
        }
        var serverSpan = serverMs - a.Server;
        if (serverSpan < WindowMs) return null;
        var ratio = (float)(clientMs - a.Client) / serverSpan;
        _anchor[playerId] = (clientMs, serverMs);
        return ratio > MaxRatio ? ratio : null;
    }

    public void Remove(uint playerId) => _anchor.Remove(playerId);
}
