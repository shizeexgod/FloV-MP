using FloVMP.Core.Diagnostics;
using Xunit;

namespace FloVMP.Core.Tests;

/// <summary>
/// Профилировщик мерит главный поток сервера, поэтому сам обязан быть почти
/// бесплатным. Выделение памяти на замер означало бы сборки мусора ровно в том
/// месте, которое мы пытаемся измерить, — и цифры показывали бы нагрузку от
/// самого измерения.
/// </summary>
public sealed class TickProfilerTests
{
    [Fact]
    public void Measuring_does_not_allocate()
    {
        var id = TickProfiler.Register("test-noalloc");
        var wasEnabled = TickProfiler.Enabled;
        TickProfiler.Enabled = true;
        try
        {
            // прогрев: первые вызовы тянут JIT, его память к замеру не относится
            for (var i = 0; i < 1000; i++)
                using (TickProfiler.Measure(id)) { }

            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 100_000; i++)
                using (TickProfiler.Measure(id)) { }
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.Equal(0, allocated);
        }
        finally
        {
            TickProfiler.Enabled = wasEnabled;
            TickProfiler.Reset();
        }
    }

    [Fact]
    public void Disabled_profiler_keeps_counters_empty()
    {
        var id = TickProfiler.Register("test-disabled");
        var wasEnabled = TickProfiler.Enabled;
        TickProfiler.Enabled = false;
        TickProfiler.Reset();
        try
        {
            for (var i = 0; i < 100; i++)
                using (TickProfiler.Measure(id)) { }

            var report = TickProfiler.Report();
            Assert.Contains(report, l => l.Contains("выключен"));
            Assert.DoesNotContain(report, l => l.StartsWith("test-disabled"));
        }
        finally
        {
            TickProfiler.Enabled = wasEnabled;
            TickProfiler.Reset();
        }
    }

    [Fact]
    public void Report_lists_measured_section_with_call_count()
    {
        var id = TickProfiler.Register("test-report");
        var wasEnabled = TickProfiler.Enabled;
        TickProfiler.Enabled = true;
        TickProfiler.Reset();
        try
        {
            for (var i = 0; i < 5; i++)
                using (TickProfiler.Measure(id)) { }

            var line = Assert.Single(TickProfiler.Report(), l => l.StartsWith("test-report"));
            Assert.Contains("5", line);
        }
        finally
        {
            TickProfiler.Enabled = wasEnabled;
            TickProfiler.Reset();
        }
    }

    [Fact]
    public void Register_returns_same_id_for_same_name()
    {
        Assert.Equal(TickProfiler.Register("test-same"), TickProfiler.Register("test-same"));
    }
}
