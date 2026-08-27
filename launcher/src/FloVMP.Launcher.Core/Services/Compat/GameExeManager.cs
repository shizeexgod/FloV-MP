using System.IO;

namespace FloVMP.Launcher.Services.Compat;

public sealed record SwapResult(bool Ok, string Message);

/// <summary>
/// Вариант 2 (Fallback Mode): временная подмена GTA5.exe игрока кэшированным
/// стабильным билдом, с гарантированным откатом.
///
/// Инженерные меры безопасности (по требованию из HANDOFF):
///   1. Подмена — атомарный File.Replace/Move (MoveFileEx), не copy+delete.
///   2. Маркер PendingRestore пишется ДО подмены, снимается только ПОСЛЕ
///      подтверждённого отката. Проверяется первым делом при старте.
///   3. Откат делает отдельный watchdog-процесс (см. лаунчер, режим
///      --watchdog), даже если UI лаунчера упал.
///   4. Бэкап оригинала не удаляется, пока откат не подтверждён. Если откат
///      не удался — оригинал остаётся под именем бэкапа, игроку честное
///      сообщение «запусти лаунчер снова».
///
/// Класс не запускает игру и не следит за процессом — только файловые
/// операции + маркер. Оркестрация (watchdog, запуск) — в слое лаунчера.
/// </summary>
public sealed class GameExeManager
{
    private readonly string _markerPath;

    public GameExeManager(string? markerPath = null)
    {
        _markerPath = markerPath ?? PendingRestore.DefaultPath;
    }

    public string MarkerPath => _markerPath;
    public bool HasPendingRestore => File.Exists(_markerPath);

    /// <summary>
    /// Подменить GTA5.exe в папке игры на <paramref name="cachedExePath"/>.
    /// Оригинал уезжает в &lt;gtaFolder&gt;\GTA5.exe.flovmp-orig, пишется маркер.
    /// </summary>
    public async Task<SwapResult> SwapInAsync(
        string gtaFolder, string cachedExePath, long nowUnixMs, int gamePid = 0,
        CancellationToken ct = default)
    {
        var target = Path.Combine(gtaFolder, "GTA5.exe");
        var backup = target + ".flovmp-orig";

        if (!File.Exists(cachedExePath))
            return new SwapResult(false, $"кэшированный билд не найден: {cachedExePath}");
        if (!File.Exists(target))
            return new SwapResult(false, $"GTA5.exe не найден: {target}");

        if (File.Exists(_markerPath))
            return new SwapResult(false, "уже есть незакрытый маркер подмены — сначала восстановление");

        if (File.Exists(backup))
            return new SwapResult(false, $"бэкап уже существует ({backup}) — незавершённая прошлая подмена, нужен ручной разбор");

        var origHash = await Cdn.ContentHasher.Sha256FileAsync(target, ct);
        var newHash = await Cdn.ContentHasher.Sha256FileAsync(cachedExePath, ct);

        if (string.Equals(origHash, newHash, StringComparison.OrdinalIgnoreCase))
            return new SwapResult(true, "подмена не нужна: билд уже совпадает");

        // 1) маркер ДО любых файловых операций
        var marker = new PendingRestore
        {
            OriginalPath = target,
            BackupPath = backup,
            OriginalSha256 = origHash,
            SwappedInSha256 = newHash,
            CreatedUnixMs = nowUnixMs,
            GamePid = gamePid,
        };
        marker.Save(_markerPath);

        try
        {
            // 2) оригинал -> бэкап (атомарно, тот же том)
            File.Move(target, backup);

            // 3) кэшированный билд -> на место GTA5.exe
            //    copy (кэш может быть на другом томе) + проверка хэша
            File.Copy(cachedExePath, target, overwrite: false);
            var placed = await Cdn.ContentHasher.Sha256FileAsync(target, ct);
            if (!string.Equals(placed, newHash, StringComparison.OrdinalIgnoreCase))
            {
                // откат немедленно
                TryDelete(target);
                File.Move(backup, target);
                PendingRestore.Delete(_markerPath);
                return new SwapResult(false, "подменённый файл побился при копировании — откат выполнен");
            }

            return new SwapResult(true, "GTA5.exe подменён, маркер выставлен");
        }
        catch (Exception ex)
        {
            // попытка немедленного отката
            try
            {
                if (!File.Exists(target) && File.Exists(backup)) File.Move(backup, target);
                if (File.Exists(target) && File.Exists(backup))
                {
                    var t = await Cdn.ContentHasher.Sha256FileAsync(target, ct);
                    if (string.Equals(t, origHash, StringComparison.OrdinalIgnoreCase)) TryDelete(backup);
                }
                PendingRestore.Delete(_markerPath);
            }
            catch { /* маркер остаётся — починим при следующем старте */ }

            return new SwapResult(false, "ошибка подмены: " + ex.GetBaseException().Message);
        }
    }

    /// <summary>
    /// Восстановить оригинальный GTA5.exe по маркеру. Идемпотентно: если
    /// маркера нет — ничего не делает и рапортует Ok. Безопасно вызывать
    /// при каждом старте лаунчера и из watchdog.
    /// </summary>
    public async Task<SwapResult> RestoreAsync(CancellationToken ct = default)
    {
        var marker = PendingRestore.Load(_markerPath);
        if (marker is null)
            return new SwapResult(true, "маркера нет — восстанавливать нечего");

        var target = marker.OriginalPath;
        var backup = marker.BackupPath;

        if (!File.Exists(backup))
        {
            // бэкапа нет. Либо уже восстановили и не сняли маркер, либо беда.
            if (File.Exists(target))
            {
                var h = await Cdn.ContentHasher.Sha256FileAsync(target, ct);
                if (string.Equals(h, marker.OriginalSha256, StringComparison.OrdinalIgnoreCase))
                {
                    PendingRestore.Delete(_markerPath);
                    return new SwapResult(true, "оригинал уже на месте, маркер снят");
                }
            }
            return new SwapResult(false,
                $"бэкап оригинала не найден ({backup}), а на месте не оригинал. Нужен ручной разбор — маркер НЕ снят.");
        }

        try
        {
            // текущий (подменённый) убираем, оригинал возвращаем атомарно
            if (File.Exists(target))
            {
                if (File.Exists(target + ".flovmp-swapped")) TryDelete(target + ".flovmp-swapped");
                File.Replace(backup, target, target + ".flovmp-swapped");
                TryDelete(target + ".flovmp-swapped");
            }
            else
            {
                File.Move(backup, target);
            }

            var restored = await Cdn.ContentHasher.Sha256FileAsync(target, ct);
            if (!string.Equals(restored, marker.OriginalSha256, StringComparison.OrdinalIgnoreCase))
                return new SwapResult(false,
                    "после отката хэш не совпал с оригиналом — маркер НЕ снят, нужен разбор");

            PendingRestore.Delete(_markerPath);
            return new SwapResult(true, "оригинальный GTA5.exe восстановлен");
        }
        catch (Exception ex)
        {
            return new SwapResult(false,
                "ошибка восстановления: " + ex.GetBaseException().Message + " — маркер оставлен, повтор при следующем старте");
        }
    }

    private static void TryDelete(string p)
    {
        try { if (File.Exists(p)) File.Delete(p); } catch { }
    }
}
