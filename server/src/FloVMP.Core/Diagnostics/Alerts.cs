using System.Text.Json;
using System.Collections.Concurrent;

namespace FloVMP.Core.Diagnostics;

/// <summary>Оповещение владельцу: вид (для повторов) и текст.</summary>
public readonly record struct Alert(string Kind, string Text);

/// <summary>
/// Когда беспокоить владельца (пункт 11 roadmap). Пороги — alerts.* в
/// client.cfg, 0 — проверка выключена. Одно и то же оповещение не
/// повторяется чаще <see cref="CooldownMs"/>: сервер, тормозящий час, не
/// должен присылать сто сообщений.
/// </summary>
public sealed class AlertPolicy
{
    public double MaxTickMs { get; set; } = 250;
    public double MinTickRate { get; set; }
    public int MaxErrorsPerWindow { get; set; } = 10;
    public long MaxMemoryMb { get; set; }
    public long CooldownMs { get; set; } = 10 * 60_000;

    private readonly Dictionary<string, long> _lastSent = new();

    public List<Alert> Evaluate(MetricsSnapshot m, long nowMs)
    {
        var list = new List<Alert>();
        if (MaxTickMs > 0 && m.MaxTickMs > MaxTickMs)
            Add(list, "tick", $"тик длился {m.MaxTickMs:0} мс (порог {MaxTickMs:0}) — у игроков был рывок", nowMs);
        // Тикрейт судим только при игроках: пустой сервер вправе простаивать.
        if (MinTickRate > 0 && m.Online > 0 && m.TickRate < MinTickRate)
            Add(list, "tickrate", $"тиков {m.TickRate:0}/с при пороге {MinTickRate:0}/с — сервер не успевает", nowMs);
        if (MaxErrorsPerWindow > 0 && m.Errors > MaxErrorsPerWindow)
            Add(list, "errors", $"ошибок за окно метрик: {m.Errors} (порог {MaxErrorsPerWindow}) — смотрите журнал сервера", nowMs);
        if (MaxMemoryMb > 0 && m.MemoryMb > MaxMemoryMb)
            Add(list, "memory", $"память процесса {m.MemoryMb} МБ (порог {MaxMemoryMb} МБ)", nowMs);
        return list;
    }

    /// <summary>Разовое оповещение (падение, зависание, от геймода) — с тем же ограничением повторов.</summary>
    // Зовут и главный поток (пороги), и поток сторожа зависаний.
    public bool Allow(string kind, long nowMs)
    {
        lock (_lastSent)
        {
            if (_lastSent.TryGetValue(kind, out var at) && nowMs - at < CooldownMs) return false;
            _lastSent[kind] = nowMs;
            return true;
        }
    }

    private void Add(List<Alert> list, string kind, string text, long nowMs)
    {
        if (Allow(kind, nowMs)) list.Add(new Alert(kind, text));
    }
}

/// <summary>
/// Метка «сервер работает» (flovmp-data/running.json). Пишется при старте,
/// удаляется при штатной остановке. Упавший сервер сам сообщить о падении не
/// может — зато его следующий запуск (systemd или хост Windows перезапускают
/// его сами) находит оставшуюся метку и понимает: прошлый запуск упал.
/// </summary>
public static class RunMarker
{
    public sealed record Info(DateTime StartedUtc, int Pid, string? LastMetrics);

    /// <summary>Записать метку; вернуть сведения о прошлом запуске, если он упал.</summary>
    public static Info? Begin(string path)
    {
        Info? crashed = null;
        try
        {
            if (File.Exists(path)) crashed = JsonSerializer.Deserialize<Info>(File.ReadAllText(path));
        }
        catch (Exception) { crashed = new Info(DateTime.MinValue, 0, null); }
        Write(path, new Info(DateTime.UtcNow, Environment.ProcessId, null));
        return crashed;
    }

    /// <summary>Дописать последние метрики: после падения по ним видно, что было перед ним.</summary>
    public static void Update(string path, DateTime startedUtc, string lastMetrics) =>
        Write(path, new Info(startedUtc, Environment.ProcessId, lastMetrics));

    public static void End(string path)
    {
        try { File.Delete(path); } catch { }
    }

    private static void Write(string path, Info info)
    {
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(info));
            File.Move(tmp, path, overwrite: true);
        }
        catch (Exception ex) { CoreConsole.Warning($"[FloV:MP] метка запуска не записана: {ex.Message}"); }
    }
}

/// <summary>
/// Отправка оповещений на webhook (alerts.webhook_url): Discord, Slack,
/// Mattermost и свои сервисы принимают JSON с полем content или text — шлём
/// оба. Отправка в фоне: сеть не должна тормозить тик, а недоступный
/// webhook — ронять сервер.
/// </summary>
public sealed class WebhookNotifier : IDisposable
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };
    private readonly BlockingCollection<string> _queue = new(boundedCapacity: 100);
    private readonly Thread _worker;
    private readonly Action<string> _warn;
    private long _lastWarnMs;

    public WebhookNotifier(Action<string> warn)
    {
        _warn = warn;
        _worker = new Thread(Run) { IsBackground = true, Name = "flovmp-alerts" };
        _worker.Start();
    }

    /// <summary>Адрес webhook; пусто — оповещения только в журнал и администраторам.</summary>
    public string Url { get; set; } = "";
    public string ServerName { get; set; } = "FloV:MP";

    public void Send(string text)
    {
        if (string.IsNullOrWhiteSpace(Url)) return;
        var message = $"[{ServerName}] {text}";
        if (message.Length > 1900) message = message[..1900] + "…";
        _queue.TryAdd(message);   // очередь полна — старые важнее, новое теряем
    }

    private void Run()
    {
        foreach (var message in _queue.GetConsumingEnumerable())
        {
            var url = Url;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "https" && uri.Scheme != "http")) continue;
            try
            {
                // Обычное тело с длиной, а не chunked (как у PostAsJsonAsync): простые
                // самописные приёмники часто не понимают тело по частям.
                using var body = new StringContent(JsonSerializer.Serialize(new { content = message, text = message }),
                    System.Text.Encoding.UTF8, "application/json");
                using var resp = Http.PostAsync(uri, body).GetAwaiter().GetResult();
                if (!resp.IsSuccessStatusCode) Warn($"webhook ответил {(int)resp.StatusCode}");
            }
            catch (Exception ex) { Warn(ex.Message); }
        }
    }

    private void Warn(string why)
    {
        var now = Environment.TickCount64;
        if (now - _lastWarnMs < 60_000) return;
        _lastWarnMs = now;
        _warn($"[FloV:MP] [Оповещения] webhook не принял сообщение: {why}");
    }

    public void Dispose()
    {
        _queue.CompleteAdding();
        _worker.Join(3000);
    }
}
