namespace FloVMP.Connect;

/// <summary>
/// Читает только собственный launcher-лог native-клиента и отличает
/// «Rockstar/Epic ещё авторизует игру» от ситуации, когда bootstrap уже
/// закончен, но GTA5.exe так и не был создан.
/// </summary>
public static class NativeBootstrapDiagnostics
{
    private const string LauncherPatchCompleted = "Launcher patch completed";
    private const string InjectionFinished = "Injection finished";
    private static readonly TimeSpan StaleAfterInjection = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan StaleAfterPatch = TimeSpan.FromSeconds(90);

    /// <summary>
    /// Определяет действительно зависший native bootstrap.
    ///
    /// <para><c>Launcher patch completed</c> НЕ означает, что игра должна
    /// уже существовать: после этой строки launcher ещё внедряет client DLL и
    /// ждёт окно GTA. Ранняя версия коннектора приняла этот промежуточный этап
    /// за окончательный и сама обрывала b3889-запуск примерно через 20 секунд.</para>
    /// </summary>
    public static bool IsStalledWithoutGame(string clientDir, DateTime launchedAt, DateTime now)
    {
        try
        {
            var latest = FindCurrentLog(clientDir, launchedAt);
            if (latest is null) return false;

            using var stream = new FileStream(latest.FullName, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            var text = reader.ReadToEnd();
            var staleFor = now - latest.LastWriteTime;

            // "Injection finished" — финальная штатная фаза bootstrap. Если
            // после неё целую минуту нет ни GTA, ни новых строк лога, дальнейшее
            // ожидание не даст результата.
            if (text.Contains(InjectionFinished, StringComparison.Ordinal))
                return staleFor >= StaleAfterInjection;

            // Патчер может застрять до инъекции. Но даём ему заметно больше
            // времени, чем обычно занимает этот переход, особенно на первом
            // запуске Epic/Rockstar.
            return text.Contains(LauncherPatchCompleted, StringComparison.Ordinal) &&
                   staleFor >= StaleAfterPatch;
        }
        catch
        {
            // Диагностика не должна ломать игровой запуск, если лог занят.
            return false;
        }
    }

    private static FileInfo? FindCurrentLog(string clientDir, DateTime launchedAt)
    {
        var logDir = Path.Combine(clientDir, "logs");
        if (!Directory.Exists(logDir)) return null;

        return Directory.EnumerateFiles(logDir, "launcher_*.log")
            .Select(path => new FileInfo(path))
            .Where(file => file.LastWriteTime >= launchedAt.AddSeconds(-2))
            .OrderByDescending(file => file.LastWriteTime)
            .FirstOrDefault();
    }

    public static void PrintFailure()
    {
        Console.Error.WriteLine("[native] Native bootstrap остановился, но GTA5.exe не появился.");
        Console.Error.WriteLine("[native] Это не ошибка сервера, лицензии FloV:MP или CDN.");
        Console.Error.WriteLine("[native] Сохраните launcher_*.log: bootstrap не дошёл до создания процесса игры.");
        Console.Error.WriteLine("[native] Точный профиль GTA не менялся; дальнейший разбор идёт по этой попытке,");
        Console.Error.WriteLine("[native] а не через замену GTA5.exe или RPF-файлов.");
    }
}
