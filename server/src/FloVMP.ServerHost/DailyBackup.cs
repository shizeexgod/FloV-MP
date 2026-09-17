using System.Diagnostics;

namespace FloVMP.ServerHost;

/// <summary>
/// Ежедневная копия базы, пока сервер запущен: раз в час проверяет возраст
/// последней копии в backups/ и, если она старше суток, в фоне запускает
/// scripts/backup-db.ps1. Системных задач не создаёт.
/// </summary>
public sealed class DailyBackup : IDisposable
{
    private static readonly TimeSpan CheckEvery = TimeSpan.FromHours(1);
    private static readonly TimeSpan MaxAge = TimeSpan.FromHours(24);

    private readonly string _root;
    private readonly Func<Dictionary<string, string>> _env;
    private readonly Action<string> _info;
    private readonly Timer _timer;
    private int _running;

    public DailyBackup(string root, Func<Dictionary<string, string>> env, Action<string> info)
    {
        _root = root;
        _env = env;
        _info = info;
        // Первая проверка — через 5 минут: не нагружать базу в момент старта сервера.
        _timer = new Timer(_ => Check(), null, TimeSpan.FromMinutes(5), CheckEvery);
    }

    /// <summary>Нужна ли копия: база настроена и последней копии нет или она старше суток.</summary>
    public static bool IsDue(string root, IReadOnlyDictionary<string, string> env, DateTime nowUtc)
    {
        if (string.IsNullOrEmpty(env.GetValueOrDefault("FLOVMP_DB_PASSWORD"))) return false;
        var db = env.GetValueOrDefault("FLOVMP_DB_NAME") is { Length: > 0 } name ? name : "flovmp_server";
        var dir = Path.Combine(root, "backups");
        if (!Directory.Exists(dir)) return true;
        var latest = Directory.GetFiles(dir, $"db_{db}_*.sql")
            .Select(File.GetLastWriteTimeUtc)
            .DefaultIfEmpty(DateTime.MinValue)
            .Max();
        return nowUtc - latest > MaxAge;
    }

    private void Check()
    {
        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0) return;
        try
        {
            if (!IsDue(_root, _env(), DateTime.UtcNow)) return;
            var script = Path.Combine(_root, "scripts", "backup-db.ps1");
            if (!File.Exists(script)) return;

            var psi = new ProcessStartInfo("powershell.exe")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            foreach (var arg in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", script })
                psi.ArgumentList.Add(arg);
            using var proc = Process.Start(psi)!;
            proc.StandardOutput.ReadToEnd();
            var err = proc.StandardError.ReadToEnd();
            proc.WaitForExit(10 * 60 * 1000);
            _info(proc.ExitCode == 0
                ? "Ежедневная копия базы сохранена в backups."
                : "Ежедневная копия базы не удалась: " + err.Trim().Split('\n').LastOrDefault());
        }
        catch (Exception ex)
        {
            _info("Ежедневная копия базы не удалась: " + ex.Message);
        }
        finally
        {
            Interlocked.Exchange(ref _running, 0);
        }
    }

    public void Dispose() => _timer.Dispose();
}
