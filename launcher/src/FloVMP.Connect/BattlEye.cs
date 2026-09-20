using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.Principal;

namespace FloVMP.Connect;

/// <summary>
/// Диагностика BattlEye для запуска FloV:MP.
///
/// ПОЧЕМУ ЗДЕСЬ НЕТ АВТОМАТИЧЕСКОГО ОТКЛЮЧЕНИЯ. BattlEye — античит, и
/// программное его выключение за игрока лаунчер делать не будет: это обход
/// защиты, а не удобство. Плюс технически это всё равно тупик — защита живёт
/// в kernel-драйвере, а поломка службы ломает и сам запуск игры (проверено).
///
/// ЧТО ЗДЕСЬ ЕСТЬ. Быстрый и точный диагноз вместо четырёхминутного молчания.
/// Раньше при включённом BattlEye коннектор просто ждал 240 секунд и писал
/// «время ожидания истекло» — игрок не знал, что делать, а владелец сервера
/// получал обращение в поддержку. Теперь состояние BattlEye проверяется ДО
/// запуска и ещё раз в момент, когда игра не поднялась, а игроку выдаётся
/// конкретная инструкция с точным путём в настройках Rockstar Games Launcher.
///
/// Отключает BattlEye игрок сам, в своём лаунчере, осознанно. Это его выбор и
/// его машина.
/// </summary>
public static class BattlEye
{
    private const string Service = "BEService";

    /// <summary>Итог проверки состояния BattlEye.</summary>
    public sealed record Status(
        bool InstalledInGame,
        bool ServiceRunning,
        bool ProcessRunning,
        bool LikelyBlocksLaunch)
    {
        public string Summary => !InstalledInGame
            ? "BattlEye в установке игры не найден — мешать запуску нечему"
            : LikelyBlocksLaunch
                ? "BattlEye активен и, скорее всего, не даст FloV:MP запуститься"
                : "BattlEye установлен, но сейчас не активен";
    }

    /// <summary>Есть ли BattlEye в самой установке игры.</summary>
    public static bool IsPresent(string gtaDir) =>
        File.Exists(Path.Combine(gtaDir, "GTA5_BE.exe")) ||
        Directory.Exists(Path.Combine(gtaDir, "BattlEye"));

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

    /// <summary>
    /// Текущее состояние BattlEye: установлен ли, запущена ли служба, крутится
    /// ли процесс. Ничего не меняет — только смотрит.
    /// </summary>
    public static Status Check(string gtaDir)
    {
        var installed = IsPresent(gtaDir);
        var serviceRunning = IsServiceRunning();
        var processRunning = IsProcessRunning();

        // Признак реальной помехи: BattlEye не просто установлен, а уже поднят.
        // Тогда GTA5.exe перезапустит себя под BE, и наш патч не переживёт
        // перезапуск процесса.
        return new Status(installed, serviceRunning, processRunning,
                          installed && (serviceRunning || processRunning));
    }

    /// <summary>Запущен ли процесс BattlEye-обёртки игры.</summary>
    public static bool IsProcessRunning()
    {
        foreach (var name in new[] { "GTA5_BE", "BEService", "BEDaisy" })
        {
            try
            {
                if (Process.GetProcessesByName(name).Length > 0) return true;
            }
            catch { /* нет прав посмотреть — считаем, что не запущен */ }
        }
        return false;
    }

    private static bool IsServiceRunning()
    {
        if (!OperatingSystem.IsWindows()) return false;
        try
        {
            // Не используем System.ServiceProcess.ServiceController: в
            // framework-dependent архиве он добавляет отдельную runtime DLL и
            // одна пропущенная зависимость превращает обычную диагностику в
            // падение коннектора. sc.exe есть в каждой поддерживаемой Windows
            // и уже применяется ниже для восстановления службы.
            using var process = Process.Start(new ProcessStartInfo("sc.exe", $"query {Service}")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            });
            if (process is null) return false;

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);
            return output.Contains("RUNNING", StringComparison.OrdinalIgnoreCase) ||
                   output.Contains("START_PENDING", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            // Службы нет или нет прав — это не ошибка, просто неизвестно.
            return false;
        }
    }

    /// <summary>
    /// Вернуть службу BEService в Manual, если она осталась Disabled.
    ///
    /// Это восстановление, а не отключение: сломанная в Disabled служба мешает
    /// запускать GTA Online самому игроку. Ранние версии коннектора её глушили —
    /// чиним за собой.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static void RepairServiceIfBroken()
    {
        if (!OperatingSystem.IsWindows()) return;
        try
        {
            var q = new ProcessStartInfo("sc.exe", $"qc {Service}")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            };
            using var p = Process.Start(q);
            var outp = p?.StandardOutput.ReadToEnd() ?? "";
            p?.WaitForExit(5000);
            if (outp.Contains("DISABLED", StringComparison.OrdinalIgnoreCase))
            {
                Run("sc.exe", $"config {Service} start= demand");
                Console.WriteLine("[be] служба BEService возвращена в Manual (её отключил старый коннектор)");
            }
        }
        catch { /* не критично: это уборка за собой, а не условие запуска */ }
    }

    /// <summary>
    /// Предупреждение перед запуском. Печатается один раз и не блокирует
    /// попытку: бывают сборки, где BattlEye активен, а запуск всё равно проходит.
    /// </summary>
    public static void Preflight(string gtaDir)
    {
        var status = Check(gtaDir);
        Console.WriteLine($"[be] {status.Summary}");
        if (!status.LikelyBlocksLaunch) return;

        Console.WriteLine("[be] BattlEye сейчас активен — если игра не запустится, причина почти наверняка в нём.");
        PrintInstructions();
    }

    /// <summary>
    /// Диагноз, когда игра так и не поднялась. Вызывается вместо глухого
    /// «время ожидания истекло»: игроку нужен не таймаут, а причина.
    /// </summary>
    public static void ExplainLaunchFailure(string gtaDir, TimeSpan waited)
    {
        var status = Check(gtaDir);

        Console.WriteLine();
        Console.WriteLine($"[be] Игра не запустилась за {waited.TotalSeconds:F0} с. Диагностика:");
        Console.WriteLine($"[be]   BattlEye в папке игры : {(status.InstalledInGame ? "есть" : "нет")}");
        Console.WriteLine($"[be]   служба BEService      : {(status.ServiceRunning ? "ЗАПУЩЕНА" : "не запущена")}");
        Console.WriteLine($"[be]   процесс BattlEye      : {(status.ProcessRunning ? "ЗАПУЩЕН" : "не запущен")}");

        if (status.LikelyBlocksLaunch)
        {
            Console.WriteLine("[be] Причина: BattlEye перезапускает GTA5.exe под собой, и патч FloV:MP");
            Console.WriteLine("[be] не переживает этот перезапуск.");
            PrintInstructions();
        }
        else if (status.InstalledInGame)
        {
            Console.WriteLine("[be] BattlEye сейчас не активен — причина, скорее всего, в другом:");
            Console.WriteLine("[be]   * не завершилась авторизация Rockstar Games Launcher / Epic Games;");
            Console.WriteLine("[be]   * первый запуск компилирует шейдеры (это бывает дольше 4 минут);");
            Console.WriteLine("[be]   * антивирус заблокировал патч процесса игры.");
        }
        else
        {
            Console.WriteLine("[be] BattlEye в установке игры не найден — ищите причину вне его:");
            Console.WriteLine("[be]   авторизация лаунчера, шейдеры, антивирус.");
        }

        Console.WriteLine("[be] Полный лог запуска: %LOCALAPPDATA%\\FloVMP\\connect.log");
    }

    /// <summary>
    /// Инструкция игроку. Ровно один путь — тот, что действительно работает.
    /// Никаких «попробуйте ещё вот так»: в поддержке это только запутывает.
    /// </summary>
    public static void PrintInstructions()
    {
        Console.WriteLine("[be] ------------------------------------------------------------");
        Console.WriteLine("[be] Как отключить BattlEye (делается один раз, вручную):");
        Console.WriteLine("[be]   1. Откройте Rockstar Games Launcher.");
        Console.WriteLine("[be]   2. Настройки -> Grand Theft Auto V.");
        Console.WriteLine("[be]   3. Снимите галочку BattlEye (Включить BattlEye).");
        Console.WriteLine("[be]   4. Закройте лаунчер полностью и запустите FloV:MP заново.");
        Console.WriteLine("[be] Это настройка вашей копии игры: FloV:MP её не меняет и менять не будет.");
        Console.WriteLine("[be] Для GTA Online галочку нужно вернуть обратно тем же путём.");
        Console.WriteLine("[be] ------------------------------------------------------------");
    }

    /// <summary>Устаревшее имя. Оставлено, чтобы не ломать внешние вызовы.</summary>
    public static void Advise(string gtaDir) => Preflight(gtaDir);

    private static void Run(string exe, string args)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo(exe, args)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            });
            p?.WaitForExit(8000);
        }
        catch { /* диагностика, не условие запуска */ }
    }
}
