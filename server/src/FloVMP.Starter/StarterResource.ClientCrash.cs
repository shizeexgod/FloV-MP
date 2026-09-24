using AltV.Net;
using FloVMP.Core.Diagnostics;
using FloVMP.Core.Native;

namespace FloVMP.Starter;

/// <summary>
/// Отчёты о падениях клиента (пункт 12 roadmap). Клиент 1.0.6+ при падении
/// игры пишет у себя мини-дамп, а при следующем входе присылает строку
/// CRASH: где упало. Здесь она попадает в журнал, в
/// flovmp-data/client-crashes.log (история между перезапусками), в счёт
/// «где чаще всего падают» (консоль: crashes) и событием геймоду:
/// <c>flovmp:client:crash (playerId, code, where, clientVersion, ageSec)</c>.
/// </summary>
public partial class StarterResource
{
    private readonly ClientCrashStats _clientCrashes = new();
    // Один отчёт за вход: клиент шлёт только последний неотправленный, но
    // чужой клиент может слать строку CRASH сколько угодно.
    private readonly HashSet<uint> _crashReported = new();

    /// <summary>Столько одинаковых падений — повод сообщить владельцу.</summary>
    private const int CrashAlertCount = 5;

    private void OnClientCrashReport(NativeSession session, string[] p)
    {
        if (!_crashReported.Add(session.Id)) return;
        if (!ClientCrashReport.TryParse(p, out var report))
        {
            Alt.LogWarning($"[FloV:MP] [Падения] клиент [{session.Id}] прислал неразборчивый отчёт — пропущен.");
            return;
        }
        var line = report.Describe($"{FloVMP.Core.Chat.PlayerNamePolicy.ForLog(session.Name)} [{session.Id}]");
        Alt.LogWarning("[FloV:MP] [Падения] " + line);
        AppendCrashLog(line);
        var count = _clientCrashes.Add(report, _clock.ElapsedMilliseconds);
        if (count == CrashAlertCount)
            Notify("client-crash", $"клиенты {count} раз упали в одном месте: 0x{report.Code:X8} ({report.CodeName}) в {report.Where}, " +
                                   $"клиент {report.ClientVersion}. Список — команда crashes в консоли сервера.");
        Alt.Emit("flovmp:client:crash", (int)session.Id, $"0x{report.Code:X8}", report.Where, report.ClientVersion, (int)Math.Min(report.AgeSec, int.MaxValue));
    }

    private void ForgetCrashReport(uint playerId) => _crashReported.Remove(playerId);

    private static void AppendCrashLog(string line)
    {
        try
        {
            var path = Path.Combine(Directory.GetCurrentDirectory(), "flovmp-data", "client-crashes.log");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            // Файл растёт только от падений; без предела он всё же мог бы
            // разрастись от подставных отчётов — держим последние ~2 МБ.
            var info = new FileInfo(path);
            if (info.Exists && info.Length > 2 * 1024 * 1024) File.Move(path, path + ".old", overwrite: true);
            File.AppendAllText(path, $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}Z {line}{Environment.NewLine}");
        }
        catch (Exception ex) { Alt.LogWarning($"[FloV:MP] [Падения] client-crashes.log не записан: {ex.Message}"); }
    }

    /// <summary>Консоль: где клиенты падали чаще всего с запуска сервера.</summary>
    private void PrintClientCrashes()
    {
        var top = _clientCrashes.Top(10);
        if (top.Count == 0)
        {
            Alt.Log("[Console] С запуска сервера отчётов о падениях клиентов не было. История — flovmp-data/client-crashes.log.");
            return;
        }
        Alt.Log($"[Console] Падения клиентов с запуска: {_clientCrashes.Total}. Чаще всего:");
        foreach (var (place, count) in top) Alt.Log($"[Console]   {count,4} × {place}");
    }
}
