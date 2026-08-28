using System.Text.Json;

namespace FloVMP.Connect;

/// <summary>
/// alt:V не работает с BattlEye: при прямом запуске GTA5.exe (Epic/legacy)
/// игра перезапускает себя через GTA5_BE.exe, alt:V теряет процесс и не
/// может пропатчить память ("Main thread suspend count: -1" → game launch
/// timeout).
///
/// Обходим: переименовываем GTA5_BE.exe → GTA5_BE.exe.flovmp-off перед
/// запуском, возвращаем после выхода из игры. Маркер в
/// %LOCALAPPDATA%\FloVMP\be-restore.json — чтобы восстановить, даже если
/// коннектор упал.
/// </summary>
public static class BattlEye
{
    private const string Off = ".flovmp-off";

    private static string MarkerPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FloVMP", "be-restore.json");

    private sealed record Marker(string BeExe, string OffExe);

    /// <summary>Отключить BattlEye в папке игры. Идемпотентно.</summary>
    public static void Disable(string gtaDir)
    {
        var be = Path.Combine(gtaDir, "GTA5_BE.exe");
        var off = be + Off;

        if (!File.Exists(be))
        {
            // уже отключён (или его нет) — ничего не делаем
            if (File.Exists(off) && !File.Exists(MarkerPath))
                SaveMarker(be, off);
            return;
        }

        try
        {
            if (File.Exists(off)) File.Delete(off);
            File.Move(be, off);
            SaveMarker(be, off);
            Console.WriteLine("[be] GTA5_BE.exe отключён на время игры");
        }
        catch (UnauthorizedAccessException)
        {
            Console.Error.WriteLine(
                "[be] НЕТ ПРАВ переименовать GTA5_BE.exe в Program Files.\n" +
                "     alt:V не заведётся с BattlEye. Запусти FloVMP.Connect.exe ОТ ИМЕНИ АДМИНИСТРАТОРА,\n" +
                "     либо переименуй вручную:\n" +
                $"       ren \"{be}\" GTA5_BE.exe.flovmp-off");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[be] не удалось отключить BattlEye: {ex.Message}");
        }
    }

    /// <summary>Вернуть BattlEye. Идемпотентно.</summary>
    public static void Restore(string? gtaDir = null)
    {
        var marker = LoadMarker();
        var be = marker?.BeExe ?? (gtaDir is null ? null : Path.Combine(gtaDir, "GTA5_BE.exe"));
        var off = marker?.OffExe ?? (be is null ? null : be + Off);
        if (be is null || off is null) return;

        try
        {
            if (File.Exists(off) && !File.Exists(be)) File.Move(off, be);
            else if (File.Exists(off) && File.Exists(be)) File.Delete(off); // be уже на месте
            DeleteMarker();
            Console.WriteLine("[be] GTA5_BE.exe возвращён");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[be] не удалось вернуть GTA5_BE.exe: {ex.Message} (маркер оставлен)");
        }
    }

    /// <summary>Проверить незакрытый маркер при старте коннектора.</summary>
    public static void RestorePendingOnStartup()
    {
        if (File.Exists(MarkerPath)) Restore();
    }

    private static void SaveMarker(string be, string off)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(MarkerPath)!);
        File.WriteAllText(MarkerPath, JsonSerializer.Serialize(new Marker(be, off)));
    }

    private static Marker? LoadMarker()
    {
        try
        {
            return File.Exists(MarkerPath)
                ? JsonSerializer.Deserialize<Marker>(File.ReadAllText(MarkerPath))
                : null;
        }
        catch { return null; }
    }

    private static void DeleteMarker()
    {
        try { if (File.Exists(MarkerPath)) File.Delete(MarkerPath); } catch { }
    }
}
