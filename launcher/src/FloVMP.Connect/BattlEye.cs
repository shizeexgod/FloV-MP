using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.Principal;

namespace FloVMP.Connect;

/// <summary>
/// GTA V Legacy (свежие билды) требует BattlEye обязательно — файлы BE
/// удалять/переименовывать НЕЛЬЗЯ (игра откажется: "отсутствуют
/// необходимые файлы BattlEye"). А активный BE-драйвер блокирует alt:V
/// от suspend/inject главного потока игры ("Main thread suspend count: -1"
/// → game launch timeout).
///
/// Что делаем (файлы НЕ трогаем):
///  - останавливаем и отключаем службу Windows `BEService` (нужен админ) —
///    драйвер BE не грузится, файлы на месте, игра стартует, alt:V патчит;
///  - после игры возвращаем службу в Manual.
///
/// Если этого мало — надёжнее всего снять галку BattlEye в Rockstar Games
/// Launcher: Настройки → Grand Theft Auto V → BattlEye (см. вывод ниже).
/// </summary>
public static class BattlEye
{
    private const string Service = "BEService";

    public static bool IsPresent(string gtaDir) =>
        File.Exists(Path.Combine(gtaDir, "GTA5_BE.exe"));

    public static bool IsAdmin()
    {
        if (!OperatingSystem.IsWindows()) return false;
        try
        {
            using var id = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch { return false; }
    }

    /// <summary>Остановить + отключить службу BE на время игры.</summary>
    [SupportedOSPlatform("windows")]
    public static void SuppressService()
    {
        Sc("stop", Service);
        Sc("config", $"{Service} start= disabled");
        Console.WriteLine("[be] служба BEService остановлена и отключена на время игры");
    }

    /// <summary>Вернуть службу BE в исходный режим (Manual).</summary>
    [SupportedOSPlatform("windows")]
    public static void RestoreService()
    {
        Sc("config", $"{Service} start= demand");
        Console.WriteLine("[be] служба BEService возвращена (Manual)");
    }

    public static void Advise(string gtaDir)
    {
        if (!IsPresent(gtaDir)) return;
        Console.WriteLine();
        Console.WriteLine("  ┌─ BattlEye обнаружен ────────────────────────────────────────────");
        Console.WriteLine("  │ alt:V не работает при активном BattlEye.");
        if (!IsAdmin())
            Console.WriteLine("  │ Запусти FloVMP.Connect.exe ОТ АДМИНИСТРАТОРА — тогда служба BE");
        Console.WriteLine("  │ будет отключена автоматически.");
        Console.WriteLine("  │ Если игра всё равно не заходит — сними галку BattlEye вручную:");
        Console.WriteLine("  │   Rockstar Games Launcher → Настройки → Grand Theft Auto V →");
        Console.WriteLine("  │   выключить \"BattlEye\", затем повторить.");
        Console.WriteLine("  └────────────────────────────────────────────────────────────────");
        Console.WriteLine();
    }

    private static void Sc(string verb, string args)
    {
        try
        {
            var psi = new ProcessStartInfo("sc.exe", $"{verb} {args}")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            using var p = Process.Start(psi);
            p?.WaitForExit(8000);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[be] sc {verb} {args}: {ex.Message}");
        }
    }
}
