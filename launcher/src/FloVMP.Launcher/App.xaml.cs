using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using FloVMP.Launcher.Services.Compat;

namespace FloVMP.Launcher;

public partial class App : Application
{
    /// <summary>
    /// Результат проверки маркера подмены при старте — MainViewModel покажет
    /// это в логе.
    /// </summary>
    public static string? StartupRestoreNote { get; private set; }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Режим watchdog: FloVMP.Launcher.exe --watchdog <gamePid> <markerPath>
        // Ждём завершения игры и откатываем подмену GTA5.exe. Без UI.
        if (e.Args.Length >= 1 && e.Args[0] == "--watchdog")
        {
            var code = await RunWatchdogAsync(e.Args);
            Shutdown(code);
            return;
        }

        DispatcherUnhandledException += OnUnhandledException;

        // Обычный старт: первым делом проверяем незакрытый маркер подмены.
        try
        {
            var mgr = new GameExeManager();
            if (mgr.HasPendingRestore)
            {
                var r = await mgr.RestoreAsync();
                StartupRestoreNote = "Найден маркер подмены GTA5.exe. Восстановление: " + r.Message;
            }
        }
        catch (Exception ex)
        {
            StartupRestoreNote = "Проверка маркера подмены упала: " + ex.Message;
        }

        new MainWindow().Show();
    }

    private static async Task<int> RunWatchdogAsync(string[] args)
    {
        try
        {
            var pid = args.Length > 1 && int.TryParse(args[1], out var p) ? p : 0;
            var markerPath = args.Length > 2 ? args[2] : PendingRestore.DefaultPath;
            var mgr = new GameExeManager(markerPath);

            if (pid > 0)
            {
                try
                {
                    var proc = Process.GetProcessById(pid);
                    await proc.WaitForExitAsync();
                }
                catch (ArgumentException)
                {
                    // процесс уже не существует — сразу к откату
                }
            }

            // небольшой запас, чтобы игра точно отпустила файл
            await Task.Delay(TimeSpan.FromSeconds(2));
            var r = await mgr.RestoreAsync();
            return r.Ok ? 0 : 1;
        }
        catch
        {
            return 2;
        }
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            e.Exception.ToString(),
            "FloV:MP Launcher — необработанная ошибка",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }
}
