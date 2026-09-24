using System.Globalization;
using System.Text.RegularExpressions;

namespace FloVMP.Core.Diagnostics;

/// <summary>
/// Отчёт клиента о падении игры (пункт 12 roadmap).
///
/// Клиент при падении пишет у себя мини-дамп и сводку, а при следующем входе
/// на сервер присылает одну строку:
/// <c>CRASH код модуль смещение версия_клиента сколько_секунд_назад версия_игры</c>.
/// Сам дамп остаётся у игрока — в нём память процесса, её на сервер не
/// шлём. Серверу хватает «где упало»: модуль и смещение одинаковы у всех,
/// кто падает на одном и том же месте, и по ним видно, наш это баг, мод
/// игрока или сама игра.
///
/// Всё поле за полем проверяется: строка приходит от клиента, её нельзя
/// пускать в журнал и в файл как есть.
/// </summary>
public sealed record ClientCrashReport(uint Code, string Module, ulong Offset, string ClientVersion, long AgeSec, string GameVersion)
{
    private static readonly Regex ModuleName = new(@"^[A-Za-z0-9_.\-]{1,64}$", RegexOptions.Compiled);
    private static readonly Regex VersionText = new(@"^[0-9A-Za-z.+\-]{1,32}$", RegexOptions.Compiled);

    /// <summary>Отчёты старше этого не принимаем: к нынешней версии клиента они уже не относятся.</summary>
    public const long MaxAgeSec = 30L * 24 * 3600;

    /// <summary>Место падения — ключ для счёта одинаковых падений.</summary>
    public string Where => $"{Module}+0x{Offset:X}";

    /// <summary>Имя кода исключения Windows, если он из частых.</summary>
    public string CodeName => Code switch
    {
        0xC0000005 => "нарушение доступа к памяти",
        0xC00000FD => "переполнение стека",
        0xC0000409 => "повреждение стека / быстрый выход",
        0xC000001D => "недопустимая инструкция",
        0xC0000094 => "деление на ноль",
        0xE06D7363 => "необработанное исключение C++",
        0x80000003 => "точка останова",
        _ => "код Windows",
    };

    public static bool TryParse(string[] p, out ClientCrashReport report)
    {
        report = null!;
        if (p.Length < 6 || p[0] != "CRASH") return false;
        if (!TryHex(p[1], out var code) || code > uint.MaxValue) return false;
        if (!ModuleName.IsMatch(p[2])) return false;
        if (!TryHex(p[3], out var offset)) return false;
        if (!VersionText.IsMatch(p[4])) return false;
        if (!long.TryParse(p[5], NumberStyles.None, CultureInfo.InvariantCulture, out var age) || age > MaxAgeSec) return false;
        var game = p.Length > 6 && VersionText.IsMatch(p[6]) ? p[6] : "?";
        report = new ClientCrashReport((uint)code, p[2], offset, p[4], age, game);
        return true;
    }

    private static bool TryHex(string s, out ulong value)
    {
        value = 0;
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) s = s[2..];
        return s.Length is > 0 and <= 16 &&
               ulong.TryParse(s, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>Строка для журнала сервера и файла client-crashes.log.</summary>
    public string Describe(string player) =>
        $"{player}: клиент {ClientVersion} (игра {GameVersion}) упал {Ago(AgeSec)} — 0x{Code:X8} ({CodeName}) в {Where}";

    private static string Ago(long sec) =>
        sec < 90 ? "только что" : sec < 5400 ? $"{sec / 60} мин назад" : sec < 172800 ? $"{sec / 3600} ч назад" : $"{sec / 86400} дн назад";
}

/// <summary>
/// Сколько раз клиенты падали в каждом месте с запуска сервера — чтобы
/// владелец видел не поток отдельных строк, а «вот это место роняет 40
/// игроков». Мест не больше <see cref="MaxPlaces"/>: иначе мусорные отчёты
/// раздули бы память.
/// </summary>
public sealed class ClientCrashStats
{
    public const int MaxPlaces = 500;
    private readonly object _lock = new();
    private readonly Dictionary<string, (int Count, string Sample, long LastMs)> _places = new(StringComparer.Ordinal);

    public int Total { get; private set; }

    /// <summary>Учесть отчёт. Возвращает, сколько раз клиенты уже падали в этом месте.</summary>
    public int Add(ClientCrashReport r, long nowMs)
    {
        var key = r.Where + " " + r.ClientVersion;
        lock (_lock)
        {
            Total++;
            if (_places.TryGetValue(key, out var e))
            {
                _places[key] = (e.Count + 1, e.Sample, nowMs);
                return e.Count + 1;
            }
            if (_places.Count < MaxPlaces) _places[key] = (1, $"0x{r.Code:X8} ({r.CodeName}) в {r.Where}, клиент {r.ClientVersion}", nowMs);
            return 1;
        }
    }

    /// <summary>Самые частые места падений.</summary>
    public IReadOnlyList<(string Place, int Count)> Top(int n)
    {
        lock (_lock)
            return _places.Values.OrderByDescending(v => v.Count).ThenByDescending(v => v.LastMs)
                .Take(n).Select(v => (v.Sample, v.Count)).ToList();
    }
}
