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
    public void PatchCompletion_AloneIsNotATerminalBootstrapFailure()
    {
        var launched = DateTime.Now;
        File.WriteAllText(Path.Combine(_clientDir, "logs", "launcher_2026-09-18-12-00-00.log"),
            "[12:00:00] [info] Launcher patch completed");

        Assert.False(NativeBootstrapDiagnostics.IsStalledWithoutGame(_clientDir, launched, launched.AddSeconds(20)));
    }

    [Fact]
    public void InjectionFinishedAndStaleLog_IsTerminalBootstrapFailure()
    {
        var launched = DateTime.Now.AddMinutes(-2);
        var log = Path.Combine(_clientDir, "logs", "launcher_2026-09-18-12-00-00.log");
        File.WriteAllText(log, "[12:00:00] [info] Launcher patch completed\n[12:00:30] [info] Injection finished");
        File.SetLastWriteTime(log, launched.AddSeconds(30));

        Assert.True(NativeBootstrapDiagnostics.IsStalledWithoutGame(_clientDir, launched, launched.AddSeconds(91)));
    }

    [Fact]
    public void PreviousLaunchLog_IsIgnored()
    {
        var log = Path.Combine(_clientDir, "logs", "launcher_2026-09-18-11-00-00.log");
        File.WriteAllText(log, "[11:00:00] [info] Injection finished");
        File.SetLastWriteTime(log, DateTime.Now.AddMinutes(-5));

        Assert.False(NativeBootstrapDiagnostics.IsStalledWithoutGame(_clientDir, DateTime.Now, DateTime.Now.AddMinutes(2)));
    }

    public void Dispose()
    {
        try { Directory.Delete(_clientDir, recursive: true); } catch { }
    }
}
