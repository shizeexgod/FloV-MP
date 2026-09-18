using FloVMP.Connect;
using Xunit;

namespace FloVMP.Launcher.Tests;

public sealed class NativeBootstrapDiagnosticsTests : IDisposable
{
    private readonly string _clientDir = Path.Combine(Path.GetTempPath(), "flovmp-bootstrap", Guid.NewGuid().ToString("N"));

    public NativeBootstrapDiagnosticsTests()
    {
        Directory.CreateDirectory(Path.Combine(_clientDir, "logs"));
    }

    [Fact]
    public void CompletedWithoutGame_RecognizesCurrentLauncherCompletion()
    {
        var launched = DateTime.Now;
        File.WriteAllText(Path.Combine(_clientDir, "logs", "launcher_2026-09-18-12-00-00.log"),
            "[12:00:00] [info] Launcher patch completed");

        Assert.True(NativeBootstrapDiagnostics.CompletedWithoutGame(_clientDir, launched));
    }

    [Fact]
    public void CompletedWithoutGame_IgnoresLogsFromPreviousLaunch()
    {
        var log = Path.Combine(_clientDir, "logs", "launcher_2026-09-18-11-00-00.log");
        File.WriteAllText(log, "[11:00:00] [info] Launcher patch completed");
        File.SetLastWriteTime(log, DateTime.Now.AddMinutes(-5));

        Assert.False(NativeBootstrapDiagnostics.CompletedWithoutGame(_clientDir, DateTime.Now));
    }

    public void Dispose()
    {
        try { Directory.Delete(_clientDir, recursive: true); } catch { }
    }
}
