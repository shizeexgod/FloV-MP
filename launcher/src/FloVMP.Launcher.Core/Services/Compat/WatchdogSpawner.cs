using System.Diagnostics;

namespace FloVMP.Launcher.Services.Compat;

/// <summary>
/// Запускает отдельный watchdog-процесс, который переживёт падение UI
/// лаунчера и откатит подмену GTA5.exe после выхода из игры.
///
/// Watchdog — тот же исполняемый файл лаунчера в режиме
/// <c>--watchdog &lt;gamePid&gt; &lt;markerPath&gt;</c> (см. App.OnStartup).
/// </summary>
public static class WatchdogSpawner
{
    public static Process Spawn(string launcherExePath, int gamePid, string markerPath)
    {
        var psi = new ProcessStartInfo(launcherExePath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = System.IO.Path.GetDirectoryName(launcherExePath) ?? Environment.CurrentDirectory,
        };
        psi.ArgumentList.Add("--watchdog");
        psi.ArgumentList.Add(gamePid.ToString());
        psi.ArgumentList.Add(markerPath);

        return Process.Start(psi)
               ?? throw new InvalidOperationException("не удалось запустить watchdog");
    }
}
