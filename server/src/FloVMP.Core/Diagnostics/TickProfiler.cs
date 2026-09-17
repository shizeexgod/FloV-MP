using System.Diagnostics;

namespace FloVMP.Core.Diagnostics;

/// <summary>
/// Замер времени внутри игрового тика.
///
/// Зачем: движок зовёт OnTick сотни раз в секунду, и всё, что там делается,
/// делается на главном потоке — то есть в бюджете кадра всех игроков сразу.
/// Без замеров разговор про «сервер тормозит» сводится к догадкам: видно, что
/// плохо, но не видно, чему именно принадлежат миллисекунды.
///
/// Требование к самому профилировщику: он не имеет права быть источником
/// нагрузки, которую измеряет. Поэтому здесь нет ни словарей по строковому
/// имени, ни выделения памяти на замер: секция регистрируется один раз и
/// дальше адресуется числом, счётчики лежат в массиве. Тест
/// <c>TickProfilerTests</c> проверяет ровно это — 100 000 замеров не должны
/// выделить ни байта.
///
/// Выключен по умолчанию: включается переменной FLOVMP_PERF=1 или командой
/// <c>perf on</c> в консоли сервера. В выключенном состоянии замер стоит одну
/// проверку логического поля.
/// </summary>
public static class TickProfiler
{
    public const int MaxSections = 64;

    private static readonly string[] Names = new string[MaxSections];
    private static readonly long[] Ticks = new long[MaxSections];
    private static readonly long[] Counts = new long[MaxSections];
    private static readonly long[] MaxTicks = new long[MaxSections];
    private static int _sectionCount;
    private static long _startedAtTicks = Stopwatch.GetTimestamp();

    /// <summary>Включён ли замер. Выключенный профилировщик не трогает счётчики.</summary>
    public static bool Enabled { get; set; } =
        Environment.GetEnvironmentVariable("FLOVMP_PERF") == "1";

    /// <summary>
    /// Зарегистрировать секцию один раз при старте и запомнить её номер.
    /// Повторный вызов с тем же именем возвращает прежний номер: ресурс может
    /// перезапускаться, а статические поля переживают перезапуск.
    /// </summary>
    public static int Register(string name)
    {
        lock (Names)
        {
            for (var i = 0; i < _sectionCount; i++)
                if (Names[i] == name) return i;

            if (_sectionCount >= MaxSections) return -1;
            var id = _sectionCount++;
            Names[id] = name;
            return id;
        }
    }

    /// <summary>Замер секции: <c>using var _ = TickProfiler.Measure(id);</c></summary>
    public static Scope Measure(int sectionId) => new(sectionId);

    public readonly ref struct Scope
    {
        private readonly int _id;
        private readonly long _start;

        internal Scope(int id)
        {
            if (!Enabled || id < 0)
            {
                _id = -1;
                _start = 0;
                return;
            }
            _id = id;
            _start = Stopwatch.GetTimestamp();
        }

        public void Dispose()
        {
            if (_id < 0) return;
            var elapsed = Stopwatch.GetTimestamp() - _start;
            Ticks[_id] += elapsed;
            Counts[_id]++;
            if (elapsed > MaxTicks[_id]) MaxTicks[_id] = elapsed;
        }
    }

    /// <summary>Сбросить накопленное и начать новое окно измерения.</summary>
    public static void Reset()
    {
        Array.Clear(Ticks);
        Array.Clear(Counts);
        Array.Clear(MaxTicks);
        _startedAtTicks = Stopwatch.GetTimestamp();
    }

    /// <summary>
    /// Отчёт по окну: сколько времени секция заняла всего, в среднем и в
    /// худшем случае, и какую долю окна она съела. Доля важнее среднего:
    /// секция с безобидным средним, но съедающая десятую часть времени
    /// сервера, — это и есть то, что ищут.
    /// </summary>
    public static IReadOnlyList<string> Report()
    {
        var windowTicks = Math.Max(1, Stopwatch.GetTimestamp() - _startedAtTicks);
        var windowMs = windowTicks * 1000.0 / Stopwatch.Frequency;
        var lines = new List<string>
        {
            $"окно {windowMs:F0} мс, секций {_sectionCount}, замер {(Enabled ? "включён" : "выключен")}",
            "секция              вызовов   всего мс   средн мс    макс мс   доля",
        };

        var order = Enumerable.Range(0, _sectionCount).OrderByDescending(i => Ticks[i]);
        foreach (var i in order)
        {
            if (Counts[i] == 0) continue;
            var totalMs = Ticks[i] * 1000.0 / Stopwatch.Frequency;
            var avgMs = totalMs / Counts[i];
            var maxMs = MaxTicks[i] * 1000.0 / Stopwatch.Frequency;
            var share = totalMs / windowMs * 100.0;
            lines.Add($"{Names[i],-18} {Counts[i],9} {totalMs,10:F1} {avgMs,10:F4} {maxMs,10:F2} {share,6:F2}%");
        }

        if (lines.Count == 2) lines.Add("(за окно не было ни одного замера)");
        return lines;
    }
}
