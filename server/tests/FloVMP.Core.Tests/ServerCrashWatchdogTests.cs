using System;
using FloVMP.Core.Watchdog;
using Xunit;

namespace FloVMP.Core.Tests
{
    public class ServerCrashWatchdogTests
    {
        [Fact]
        public void CheckLiveness_WhenHeartbeatFresh_ReturnsTrue()
        {
            var start = new DateTime(2026, 9, 6, 12, 0, 0, DateTimeKind.Utc);
            var watchdog = new ServerCrashWatchdog(start);

            watchdog.RecordHeartbeat(start.AddSeconds(5));
            bool isAlive = watchdog.CheckLiveness(start.AddSeconds(10));

            Assert.True(isAlive);
        }

        [Fact]
        public void CheckLiveness_WhenHeartbeatExpired_TriggersCrashAndRestart()
        {
            var start = new DateTime(2026, 9, 6, 12, 0, 0, DateTimeKind.Utc);
            var watchdog = new ServerCrashWatchdog(start)
            {
                HeartbeatTimeoutSeconds = 10
            };

            CrashReport? receivedReport = null;
            watchdog.OnRestartInitiated += (report) => receivedReport = report;

            bool isAlive = watchdog.CheckLiveness(start.AddSeconds(25));

            Assert.False(isAlive);
            Assert.NotNull(receivedReport);
            Assert.Contains("frozen", receivedReport.Reason);
            Assert.True(receivedReport.AutoRestartTriggered);
        }

        [Fact]
        public void CaptureException_CreatesValidReportAndEnforcesRateLimit()
        {
            var start = new DateTime(2026, 9, 6, 12, 0, 0, DateTimeKind.Utc);
            var watchdog = new ServerCrashWatchdog(start)
            {
                MaxRestartsPerHour = 2
            };

            var ex = new InvalidOperationException("Database pool exhausted");
            var r1 = watchdog.CaptureException(ex, "Database");
            var r2 = watchdog.CaptureException(ex, "Database");
            var r3 = watchdog.CaptureException(ex, "Database");

            Assert.True(r1.AutoRestartTriggered);
            Assert.True(r2.AutoRestartTriggered);
            Assert.False(r3.AutoRestartTriggered); // Exceeded 2 per hour

            var reports = watchdog.GetRecentCrashReports();
            Assert.Equal(3, reports.Count);
        }
    }
}
