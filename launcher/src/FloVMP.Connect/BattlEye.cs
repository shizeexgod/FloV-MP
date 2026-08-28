using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.Principal;

namespace FloVMP.Connect;

/// <summary>
/// BattlEye + alt:V. GTA V Legacy (свежие билды) требует BE обязательно.
/// Мы НИЧЕГО не трогаем в файлах BE и НЕ глушим службу (проверено — не
/// помогает: защита в kernel-драйвере, а поломка службы ломает запуск).
///
/// Единственный надёжный способ: отключить BattlEye в настройках
/// Rockstar Games Launcher (Настройки → Grand Theft Auto V → BattlEye).
/// Тогда GTA5.exe не перезапускает себя под BE и alt:V успевает пропатчить.
///
/// Здесь — только совет пользователю + разовое восстановление службы
/// BEService, если её сломал прошлый (ошибочный) заход коннектора.
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

    /// <summary>Вернуть службу BEService в Manual, если она осталась Disabled.</summary>
    [SupportedOSPlatform("windows")]
    public static void RepairServiceIfBroken()
    {
        try
        {
            var q = new ProcessStartInfo("sc.exe", $"qc {Service}")
            {
                UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true,
            };
            using var p = Process.Start(q);
            var outp = p?.StandardOutput.ReadToEnd() ?? "";
            p?.WaitForExit(5000);
            if (outp.Contains("DISABLED", StringComparison.OrdinalIgnoreCase))
            {
                Run("sc.exe", $"config {Service} start= demand");
                Console.WriteLine("[be] служба BEService восстановлена (Manual)");
            }
        }
        catch { /* не критично */ }
    }

    public static void Advise(string gtaDir)
    {
        if (!IsPresent(gtaDir)) return;
        Console.WriteLine();
        Console.WriteLine("  == BattlEye обнаружен ==========================================");
        Console.WriteLine("  alt:V не запустится, пока GTA V стартует под BattlEye.");
        Console.WriteLine("  ОТКЛЮЧИ BattlEye в Rockstar Games Launcher:");
        Console.WriteLine("    Launcher -> Настройки -> Grand Theft Auto V -> выключить BattlEye");
        Console.WriteLine("  затем повтори запуск коннектора.");
        Console.WriteLine("  ===============================================================");
        Console.WriteLine();
    }

    private static void Run(string exe, string args)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo(exe, args)
            {
                UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true,
            });
            p?.WaitForExit(8000);
        }
        catch { }
    }
}
