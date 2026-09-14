namespace FloVMP.LoadTest;

/// <summary>
/// Сбор замеров с перцентилями.
///
/// Среднее по тикам здесь почти бесполезно: игрок замечает не средний тик, а
/// худшие — именно они дают рывки и «резину». Поэтому отчёт строится на
/// p50/p95/p99/max, а вердикт — по p99.
/// </summary>
public sealed class Samples
{
    private readonly List<double> _values;
    public string Name { get; }

    public Samples(string name, int capacity = 1024)
    {
        Name = name;
        _values = new List<double>(capacity);
    }

    public void Add(double ms) => _values.Add(ms);
    public int Count => _values.Count;

    public double Min => _values.Count == 0 ? 0 : _values.Min();
    public double Max => _values.Count == 0 ? 0 : _values.Max();
    public double Mean => _values.Count == 0 ? 0 : _values.Average();
    public double P50 => Percentile(50);
    public double P95 => Percentile(95);
    public double P99 => Percentile(99);

    public double Percentile(double p)
    {
        if (_values.Count == 0) return 0;
        var sorted = _values.OrderBy(v => v).ToArray();
        // Ближайший ранг: для 100 замеров p99 — 99-й по возрастанию.
        var rank = (int)Math.Ceiling(p / 100.0 * sorted.Length) - 1;
        return sorted[Math.Clamp(rank, 0, sorted.Length - 1)];
    }
}

public static class Report
{
    public static void Header(string title)
    {
        Console.WriteLine();
        Console.WriteLine(new string('=', 78));
        Console.WriteLine("  " + title);
        Console.WriteLine(new string('=', 78));
    }

    public static void TableHeader()
    {
        Console.WriteLine($"{"сценарий",-34}{"p50",8} {"p95",8} {"p99",8} {"max",8}   (мс)");
        Console.WriteLine(new string('-', 78));
    }

    public static void Line(Samples s)
    {
        Console.WriteLine($"{s.Name,-34}{s.P50,8:F2} {s.P95,8:F2} {s.P99,8:F2} {s.Max,8:F2}");
    }

    public static void Verdict(string what, double valueMs, double budgetMs)
    {
        var ok = valueMs <= budgetMs;
        var prev = Console.ForegroundColor;
        Console.ForegroundColor = ok ? ConsoleColor.Green : ConsoleColor.Red;
        Console.WriteLine($"  [{(ok ? "OK  " : "ПЛОХО")}] {what}: {valueMs:F2} мс при бюджете {budgetMs:F2} мс");
        Console.ForegroundColor = prev;
    }

    public static void Note(string text) => Console.WriteLine("  " + text);
}
