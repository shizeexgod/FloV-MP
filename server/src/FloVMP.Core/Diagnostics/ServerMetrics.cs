using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace FloVMP.Core.Diagnostics;

/// <summary>Сетевые счётчики шлюза 3889: пишутся из сетевых потоков, читаются метриками.</summary>
public static class NetCounters
{
    private static long _bytesIn, _bytesOut;
    public static void AddIn(long bytes) => Interlocked.Add(ref _bytesIn, bytes);
    public static void AddOut(long bytes) => Interlocked.Add(ref _bytesOut, bytes);
    public static long BytesIn => Interlocked.Read(ref _bytesIn);
    public static long BytesOut => Interlocked.Read(ref _bytesOut);
}

/// <summary>Метрики сервера за одно окно (пункт 11 roadmap).</summary>
public sealed record MetricsSnapshot(
    DateTime AtUtc, double UptimeSec, int Online, int MaxPlayers,
    double TickRate, double AvgTickMs, double MaxTickMs,
    double NetInKBps, double NetOutKBps, int Errors, long MemoryMb,
    IReadOnlyDictionary<string, double> Custom)
{
    public string ToLogLine() => string.Create(CultureInfo.InvariantCulture,
        $"онлайн {Online}/{MaxPlayers}, тик {TickRate:0}/с (ср. {AvgTickMs:0.00} мс, макс. {MaxTickMs:0.0} мс), " +
        $"сеть ↓{NetInKBps:0.0} ↑{NetOutKBps:0.0} КБ/с, ошибок {Errors}, память {MemoryMb} МБ, работает {TimeSpan.FromSeconds(UptimeSec):d\\.hh\\:mm\\:ss}");

    public string ToJson() => JsonSerializer.Serialize(new
    {
        at = AtUtc.ToString("O", CultureInfo.InvariantCulture),
        uptimeSec = Math.Round(UptimeSec),
        online = Online, maxPlayers = MaxPlayers,
        tickRate = Math.Round(TickRate, 1), avgTickMs = Math.Round(AvgTickMs, 3), maxTickMs = Math.Round(MaxTickMs, 2),
        netInKBps = Math.Round(NetInKBps, 1), netOutKBps = Math.Round(NetOutKBps, 1),
        errors = Errors, memoryMb = MemoryMb, custom = Custom,
    });

    /// <summary>Текст для Prometheus/Grafana: имя, значение — по строке.</summary>
    public string ToPrometheus()
    {
        var sb = new StringBuilder();
        void Gauge(string name, double v, string help)
        {
            sb.Append("# HELP ").Append(name).Append(' ').Append(help).Append('\n');
            sb.Append("# TYPE ").Append(name).Append(" gauge\n");
            sb.Append(name).Append(' ').Append(v.ToString("0.###", CultureInfo.InvariantCulture)).Append('\n');
        }
        Gauge("flovmp_online", Online, "игроков на сервере");
        Gauge("flovmp_max_players", MaxPlayers, "слотов по лицензии");
        Gauge("flovmp_tick_rate", TickRate, "тиков в секунду");
        Gauge("flovmp_tick_avg_ms", AvgTickMs, "средняя длительность тика, мс");
        Gauge("flovmp_tick_max_ms", MaxTickMs, "самый долгий тик за окно, мс");
        Gauge("flovmp_net_in_kbps", NetInKBps, "входящий трафик шлюза 3889, КБ/с");
        Gauge("flovmp_net_out_kbps", NetOutKBps, "исходящий трафик шлюза 3889, КБ/с");
        Gauge("flovmp_errors", Errors, "ошибок за окно");
        Gauge("flovmp_memory_mb", MemoryMb, "память процесса, МБ");
        Gauge("flovmp_uptime_seconds", UptimeSec, "время работы, с");
        foreach (var (k, v) in Custom) Gauge("flovmp_custom_" + k, v, "метрика ресурса");
        return sb.ToString();
    }
}

/// <summary>
/// Сборщик метрик. Тик — две отметки времени и сложение (никаких выделений
/// памяти: сборщик не должен быть той нагрузкой, которую меряет). Раз в окно
/// <see cref="Collect"/> отдаёт снимок и начинает окно заново.
/// Тик и Collect — главный поток; <see cref="RecordError"/> — из любого.
/// </summary>
public sealed class ServerMetrics
{
    private readonly long _startTs = Stopwatch.GetTimestamp();
    private long _windowTs = Stopwatch.GetTimestamp();
    private long _ticks, _tickSum, _tickMax;
    private int _errors;
    private long _netIn = NetCounters.BytesIn, _netOut = NetCounters.BytesOut;
    private readonly Dictionary<string, double> _custom = new();

    public MetricsSnapshot? Last { get; private set; }

    /// <summary>Длительность тика в тиках Stopwatch.</summary>
    public void RecordTick(long elapsedStopwatchTicks)
    {
        _ticks++;
        _tickSum += elapsedStopwatchTicks;
        if (elapsedStopwatchTicks > _tickMax) _tickMax = elapsedStopwatchTicks;
    }

    public void RecordError() => Interlocked.Increment(ref _errors);

    /// <summary>Метрика ресурса (геймода): имя — латиница, цифры и _, до 48 символов.</summary>
    public bool SetCustom(string name, double value)
    {
        if (string.IsNullOrEmpty(name) || name.Length > 48 || !name.All(ch => ch is >= 'a' and <= 'z' or >= '0' and <= '9' or '_')) return false;
        if (!double.IsFinite(value)) return false;
        if (!_custom.ContainsKey(name) && _custom.Count >= 50) return false;
        _custom[name] = value;
        return true;
    }

    public MetricsSnapshot Collect(int online, int maxPlayers, long? memoryBytes = null)
    {
        var now = Stopwatch.GetTimestamp();
        var windowSec = Math.Max(0.001, (now - _windowTs) / (double)Stopwatch.Frequency);
        var inNow = NetCounters.BytesIn;
        var outNow = NetCounters.BytesOut;
        var msPerTick = 1000.0 / Stopwatch.Frequency;
        var snapshot = new MetricsSnapshot(
            DateTime.UtcNow, (now - _startTs) / (double)Stopwatch.Frequency, online, maxPlayers,
            _ticks / windowSec,
            _ticks == 0 ? 0 : _tickSum * msPerTick / _ticks,
            _tickMax * msPerTick,
            (inNow - _netIn) / 1024.0 / windowSec, (outNow - _netOut) / 1024.0 / windowSec,
            Interlocked.Exchange(ref _errors, 0),
            (memoryBytes ?? Environment.WorkingSet) / (1024 * 1024),
            new Dictionary<string, double>(_custom));
        _windowTs = now;
        _ticks = _tickSum = _tickMax = 0;
        _netIn = inNow;
        _netOut = outNow;
        Last = snapshot;
        return snapshot;
    }
}
