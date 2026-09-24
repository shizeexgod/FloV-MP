using System.Diagnostics;
using System.Net;
using AltV.Net;
using FloVMP.Core.Diagnostics;
using FloVMP.Core.Watchdog;

namespace FloVMP.Starter;

/// <summary>
/// Метрики и оповещения (пункт 11 roadmap). Раз в окно — строка в журнал,
/// flovmp-data/metrics.json, событие flovmp:metrics ресурсам и проверка
/// порогов; по желанию владельца — страница /metrics для Grafana/Prometheus.
///
/// Оповещения уходят в журнал, администраторам в игре и на webhook
/// (alerts.webhook_url). Падение сервер сам сообщить не может: об этом
/// сообщает его следующий запуск (метка running.json осталась — значит, прошлый
/// запуск не остановился штатно). Зависание главного потока ловит сторож
/// (<see cref="ServerCrashWatchdog"/>) из своего потока.
/// </summary>
public partial class StarterResource
{
    private readonly ServerMetrics _metrics = new();
    private readonly AlertPolicy _alertPolicy = new();
    private WebhookNotifier? _webhook;
    private MetricsHttpServer? _metricsHttp;
    private ServerCrashWatchdog? _watchdog;
    private System.Threading.Timer? _watchdogTimer;
    private string? _runMarkerPath;
    private DateTime _runStartedUtc;
    private long _nextMetricsMs;
    private long _hangStartedTicks;   // из потока сторожа; 0 — не зависали

    private void StartMetrics(string dataDir)
    {
        _webhook = new WebhookNotifier(Alt.LogWarning);
        ApplyMetricsSettings();

        _runMarkerPath = Path.Combine(dataDir, "running.json");
        _runStartedUtc = DateTime.UtcNow;
        if (RunMarker.Begin(_runMarkerPath) is { } crashed)
        {
            var since = crashed.StartedUtc == DateTime.MinValue ? "" : $" (запущен {crashed.StartedUtc:yyyy-MM-dd HH:mm} UTC)";
            Notify("crash", $"прошлый запуск сервера завершился аварийно{since} и сервер перезапущен." +
                            (crashed.LastMetrics is { } m ? $" Последние метрики: {m}" : ""));
        }

        StartMetricsHttp();

        // Сторож зависаний: пульс ставит тик, проверяет отдельный поток.
        _watchdog = new ServerCrashWatchdog { AutoRestartEnabled = false };
        _watchdog.OnCrashDetected += report =>
        {
            Interlocked.CompareExchange(ref _hangStartedTicks, Environment.TickCount64, 0);
            var text = $"главный поток сервера не отвечает дольше {_watchdog.HeartbeatTimeoutSeconds} с — игроки стоят. " +
                       $"Память {report.MemoryAllocatedMb} МБ, потоков {report.ActiveThreads}.";
            // Из потока сторожа: только webhook и консоль (игровой API — главного потока).
            Console.WriteLine("[FloV:MP] [Оповещение] " + text);
            if (_alertPolicy.Allow("hang", Environment.TickCount64)) _webhook?.Send(text);
        };
        _watchdogTimer = new System.Threading.Timer(_ =>
        {
            try { if (_watchdog.HeartbeatTimeoutSeconds > 0) _watchdog.CheckLiveness(); } catch { }
        }, null, 5000, 5000);
    }

    private void StartMetricsHttp()
    {
        var port = _settings.Int("metrics.http_port");
        if (port <= 0) return;
        try
        {
            var bind = IPAddress.TryParse(_settings.Get("metrics.http_bind").Trim(), out var ip) ? ip : IPAddress.Loopback;
            _metricsHttp = new MetricsHttpServer(bind, port, _settings.Get("metrics.token").Trim(), () => _metrics.Last);
            _metricsHttp.Start();
            Alt.Log($"[FloV:MP] [Метрики] Страница метрик: http://{bind}:{port}/metrics (Prometheus — /metrics.prom).");
        }
        catch (Exception ex)
        {
            _metricsHttp = null;
            Alt.LogWarning($"[FloV:MP] [Метрики] Страница метрик не запущена: {ex.Message}");
        }
    }

    /// <summary>Пороги оповещений и адрес webhook — при загрузке и при смене настроек.</summary>
    private void ApplyMetricsSettings()
    {
        _alertPolicy.MaxTickMs = _settings.Float("alerts.tick_ms");
        _alertPolicy.MinTickRate = _settings.Float("alerts.min_tick_rate");
        _alertPolicy.MaxErrorsPerWindow = _settings.Int("alerts.errors_per_window");
        _alertPolicy.MaxMemoryMb = _settings.Int("alerts.memory_mb");
        _alertPolicy.CooldownMs = _settings.Int("alerts.cooldown_min") * 60_000L;
        if (_webhook is not null)
        {
            _webhook.Url = _settings.Get("alerts.webhook_url").Trim();
            _webhook.ServerName = _native?.ServerName ?? ReadServerTomlValue("name") ?? "FloV:MP";
        }
        if (_watchdog is not null) _watchdog.HeartbeatTimeoutSeconds = _settings.Int("alerts.hang_sec");
    }

    /// <summary>Конец каждого тика: длительность, пульс сторожу, раз в окно — снимок.</summary>
    private void TickMetrics(long tickStartTimestamp)
    {
        _metrics.RecordTick(Stopwatch.GetTimestamp() - tickStartTimestamp);
        _watchdog?.RecordHeartbeat();
        var hang = Interlocked.Exchange(ref _hangStartedTicks, 0);
        if (hang != 0)
            Notify("hang-recovered", $"сервер снова отвечает (не отвечал около {(Environment.TickCount64 - hang) / 1000 + _watchdog!.HeartbeatTimeoutSeconds} с).");

        var nowMs = _clock.ElapsedMilliseconds;
        var interval = _settings.Int("metrics.log_interval_sec");
        if (_nextMetricsMs == 0)
        {
            // Первое окно — полное, от запуска: иначе первая строка была бы о тиках за 0 секунд.
            _nextMetricsMs = nowMs + (interval > 0 ? interval : 60) * 1000L;
            return;
        }
        if (nowMs < _nextMetricsMs) return;
        // Окно собирается и при выключенном журнале: его ждут страница и пороги.
        _nextMetricsMs = nowMs + (interval > 0 ? interval : 60) * 1000L;
        var m = _metrics.Collect(AllPlayers().Count, _license.PlayerLimit);
        if (interval > 0)
        {
            Alt.Log("[FloV:MP] [Метрики] " + m.ToLogLine());
            WriteMetricsFile(m);
        }
        if (_runMarkerPath is not null) RunMarker.Update(_runMarkerPath, _runStartedUtc, m.ToLogLine());
        Alt.Emit("flovmp:metrics", m.Online, (float)m.TickRate, (float)m.AvgTickMs, (float)m.MaxTickMs, m.Errors, (int)m.MemoryMb);
        foreach (var alert in _alertPolicy.Evaluate(m, nowMs)) Notify(alert.Kind, alert.Text, cooled: true);
    }

    private void WriteMetricsFile(MetricsSnapshot m)
    {
        try
        {
            var path = Path.Combine(Directory.GetCurrentDirectory(), "flovmp-data", "metrics.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path + ".tmp", m.ToJson());
            File.Move(path + ".tmp", path, overwrite: true);
        }
        catch (Exception ex) { Alt.LogWarning($"[FloV:MP] [Метрики] metrics.json не записан: {ex.Message}"); }
    }

    /// <summary>Оповещение владельцу: журнал, администраторы в игре, webhook.</summary>
    private void Notify(string kind, string text, bool cooled = false)
    {
        if (!cooled && !_alertPolicy.Allow(kind, _clock.ElapsedMilliseconds)) return;
        Alt.LogWarning("[FloV:MP] [Оповещение] " + text);
        if (_settings.Bool("alerts.admins_chat"))
            foreach (var admin in AllPlayers())
                if (admin.Exists && IsAdmin(admin, 1)) SendChatMessage(admin, "{f59e0b}[Сервер] " + text);
        _webhook?.Send(text);
    }

    private void StopMetrics()
    {
        try { _watchdogTimer?.Dispose(); } catch { }
        _metricsHttp?.Dispose();
        _webhook?.Dispose();
        // Штатная остановка: метки нет — следующий запуск не решит, что мы упали.
        if (_runMarkerPath is not null) RunMarker.End(_runMarkerPath);
    }

    private void RegisterMetricsApi()
    {
        // Своя метрика ресурса: попадёт в metrics.json и /metrics.prom (flovmp_custom_<имя>).
        Alt.OnServer<string, float>("flovmp:metrics:set", (name, value) =>
        {
            if (!_metrics.SetCustom(name ?? "", value))
                Alt.LogWarning($"[FloV:MP] flovmp:metrics:set «{name}»: имя — a-z, 0-9, _ до 48 символов, до 50 метрик");
        });
        // Своё оповещение ресурса — тем же путём (журнал, администраторы, webhook).
        Alt.OnServer<string>("flovmp:alert", text =>
        {
            var clean = Clean(text, 500);
            if (clean.Length > 0) Notify("resource:" + clean, clean);
        });
    }
}
