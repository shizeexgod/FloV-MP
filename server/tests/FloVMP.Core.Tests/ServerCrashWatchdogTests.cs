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

        [Fact]
        public void CheckLiveness_RepeatedCallsDuringSingleFreeze_TriggersRestartOnlyOnce()
        {
            var start = new DateTime(2026, 9, 6, 12, 0, 0, DateTimeKind.Utc);
            var watchdog = new ServerCrashWatchdog(start)
            {
                HeartbeatTimeoutSeconds = 10,
                MaxRestartsPerHour = 5
            };

            int restartCount = 0;
            watchdog.OnRestartInitiated += _ => restartCount++;

            // Heartbeat at T+0, polling at T+15, T+16, T+17 during same freeze
            bool r1 = watchdog.CheckLiveness(start.AddSeconds(15));
            bool r2 = watchdog.CheckLiveness(start.AddSeconds(16));
            bool r3 = watchdog.CheckLiveness(start.AddSeconds(17));

            Assert.False(r1);
            Assert.False(r2);
            Assert.False(r3);
            Assert.Equal(1, restartCount); // Debounced! Only 1 restart initiated

            // Server heartbeat recovers at T+20
            watchdog.RecordHeartbeat(start.AddSeconds(20));
            bool r4 = watchdog.CheckLiveness(start.AddSeconds(21));
            Assert.True(r4);

            // New separate freeze occurs at T+35
            bool r5 = watchdog.CheckLiveness(start.AddSeconds(35));
            Assert.False(r5);
            Assert.Equal(2, restartCount); // Now triggers second restart
        }
    }
}
