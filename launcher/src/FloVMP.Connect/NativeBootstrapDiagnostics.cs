namespace FloVMP.Connect;

/// <summary>
/// Читает только собственный launcher-лог native-клиента и отличает
/// «Rockstar/Epic ещё авторизует игру» от ситуации, когда bootstrap уже
/// закончен, но GTA5.exe так и не был создан.
/// </summary>
public static class NativeBootstrapDiagnostics
{
    private const string LauncherPatchCompleted = "Launcher patch completed";

    public static bool CompletedWithoutGame(string clientDir, DateTime launchedAt)
    {
        try
        {
            var logDir = Path.Combine(clientDir, "logs");
            if (!Directory.Exists(logDir)) return false;

            var latest = Directory.EnumerateFiles(logDir, "launcher_*.log")
                .Select(path => new FileInfo(path))
                .Where(file => file.LastWriteTime >= launchedAt.AddSeconds(-2))
                .OrderByDescending(file => file.LastWriteTime)
                .FirstOrDefault();
            if (latest is null) return false;

            using var stream = new FileStream(latest.FullName, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd().Contains(LauncherPatchCompleted, StringComparison.Ordinal);
        }
        catch
        {
            // Диагностика не должна ломать игровой запуск, если лог занят.
            return false;
        }
    }

    public static void PrintFailure()
    {
        Console.Error.WriteLine("[native] Bootstrap-клиент завершил внедрение, но GTA5.exe не появился.");
        Console.Error.WriteLine("[native] Это не ошибка сервера, лицензии FloV:MP или CDN.");
        Console.Error.WriteLine("[native] Установленная GTA Legacy 1.0.3889 требует отдельного native-адаптера;");
        Console.Error.WriteLine("[native] старый bootstrap 16.4.39 нельзя безопасно выдать за поддержку этой версии.");
    }
}
