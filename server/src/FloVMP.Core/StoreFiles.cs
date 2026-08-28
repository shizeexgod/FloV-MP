namespace FloVMP.Core;

/// <summary>Утилиты файловых JSON-хранилищ.</summary>
public static class StoreFiles
{
    /// <summary>
    /// Отодвинуть битый файл в сторону: переименовать в
    /// <c>&lt;path&gt;.corrupt-&lt;utcTicks&gt;</c>. Так forensic-данные не
    /// теряются, а следующее сохранение пишет свежий файл, а не поверх мусора.
    /// Возвращает путь карантина или null, если перенос не удался.
    /// </summary>
    public static string? QuarantineCorrupt(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            var dest = $"{path}.corrupt-{DateTime.UtcNow.Ticks}";
            File.Move(path, dest);
            return dest;
        }
        catch
        {
            return null;
        }
    }
}
