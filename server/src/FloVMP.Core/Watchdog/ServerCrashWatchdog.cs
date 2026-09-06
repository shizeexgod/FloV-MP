using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace FloVMP.Core.Watchdog
{
    public record CrashReport(
        string IncidentId,
        DateTime Timestamp,
        string Reason,
        string StackTrace,
        long MemoryAllocatedMb,
        int ActiveThreads,
        double UptimeSeconds,
        bool AutoRestartTriggered
    );

    /// <summary>
    /// Automatic Server Crash Watchdog & Health Supervisor.
    /// Detects main thread freezes, unhandled CoreCLR exceptions, generates crash reports,
    /// and triggers automatic self-healing restarts with rate-limiting.
    /// </summary>
    public class ServerCrashWatchdog
    {
        private readonly ConcurrentBag<CrashReport> _crashReports = new();
        private readonly List<DateTime> _restartHistory = new();
        private readonly object _lock = new();

        private readonly DateTime _startTime;
        private DateTime _lastHeartbeat;

        public int HeartbeatTimeoutSeconds { get; set; } = 15;
        public int MaxRestartsPerHour { get; set; } = 5;
        public bool AutoRestartEnabled { get; set; } = true;

        public event Action<CrashReport>? OnCrashDetected;
        public event Action<CrashReport>? OnRestartInitiated;

        public ServerCrashWatchdog(DateTime? startTime = null)
        {
            _startTime = startTime ?? DateTime.UtcNow;
            _lastHeartbeat = _startTime;
        }

        public void RecordHeartbeat(DateTime? time = null)
        {
            _lastHeartbeat = time ?? DateTime.UtcNow;
        }

        public bool CheckLiveness(DateTime? currentTime = null)
        {
            var now = currentTime ?? DateTime.UtcNow;
            var elapsed = (now - _lastHeartbeat).TotalSeconds;

            if (elapsed > HeartbeatTimeoutSeconds)
            {
                TriggerCrash("Watchdog: Server main thread frozen / hung (Heartbeat missed)", "Thread hang detected during frame tick");
                return false;
            }

            return true;
        }

        public CrashReport TriggerCrash(string reason, string? stackTrace = null)
        {
            DateTime now = DateTime.UtcNow;
            string incidentId = $"CRASH-{now:yyyyMMdd}-{Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper()}";

            long memoryMb = 0;
            int threads = 0;
            try
            {
                using var process = Process.GetCurrentProcess();
                memoryMb = process.WorkingSet64 / (1024 * 1024);
                threads = process.Threads.Count;
            }
            catch
            {
                memoryMb = GC.GetTotalMemory(false) / (1024 * 1024);
            }

            double uptime = (now - _startTime).TotalSeconds;
            bool shouldRestart = CanRestartNow(now);

            var report = new CrashReport(
                IncidentId: incidentId,
                Timestamp: now,
                Reason: reason,
                StackTrace: stackTrace ?? "No stack trace provided",
                MemoryAllocatedMb: memoryMb,
                ActiveThreads: threads,
                UptimeSeconds: uptime,
                AutoRestartTriggered: shouldRestart
            );

            _crashReports.Add(report);
            OnCrashDetected?.Invoke(report);

            if (shouldRestart)
            {
                lock (_lock)
                {
                    _restartHistory.Add(now);
                }
                OnRestartInitiated?.Invoke(report);
            }

            return report;
        }

        public CrashReport CaptureException(Exception ex, string context = "CoreCLR")
        {
            if (ex == null) throw new ArgumentNullException(nameof(ex));
            return TriggerCrash($"[{context}] {ex.GetType().Name}: {ex.Message}", ex.StackTrace);
        }

        private bool CanRestartNow(DateTime now)
        {
            if (!AutoRestartEnabled) return false;

            lock (_lock)
            {
                // Remove restarts older than 1 hour
                _restartHistory.RemoveAll(t => (now - t).TotalHours >= 1.0);
                return _restartHistory.Count < MaxRestartsPerHour;
            }
        }

        public IReadOnlyList<CrashReport> GetRecentCrashReports(int limit = 10)
        {
            return _crashReports
                .OrderByDescending(r => r.Timestamp)
                .Take(limit)
                .ToList();
        }
    }
}
