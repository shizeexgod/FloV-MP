using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FloVMP.Launcher.Services.Compat;

/// <summary>
/// Маркер «идёт подмена GTA5.exe, откат ещё не подтверждён».
///
/// Пишется НА ДИСК до подмены, удаляется только после подтверждённого
/// отката. Лаунчер и watchdog проверяют его первым делом при старте:
/// если маркер есть — предыдущий запуск не откатил подмену (краш / kill /
/// выключили свет), нужно чинить.
/// </summary>
public sealed class PendingRestore
{
    [JsonPropertyName("originalPath")]
    public string OriginalPath { get; set; } = "";

    /// <summary>Куда отложен оригинальный GTA5.exe игрока.</summary>
    [JsonPropertyName("backupPath")]
    public string BackupPath { get; set; } = "";

    /// <summary>SHA-256 оригинала — проверка, что откатываем именно его.</summary>
    [JsonPropertyName("originalSha256")]
    public string OriginalSha256 { get; set; } = "";

    /// <summary>Что подменили (кэшированный билд) — для логов/диагностики.</summary>
    [JsonPropertyName("swappedInSha256")]
    public string SwappedInSha256 { get; set; } = "";

    /// <summary>Unix-время создания маркера (мс). Ставится вызывающим кодом.</summary>
    [JsonPropertyName("createdUnixMs")]
    public long CreatedUnixMs { get; set; }

    /// <summary>PID игры, за которым следит watchdog (0 — не запущена).</summary>
    [JsonPropertyName("gamePid")]
    public int GamePid { get; set; }

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FloVMP", "pending-restore.json");

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        // durable write: во временный файл + атомарная замена
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(this, JsonOpts));
        if (File.Exists(path)) File.Replace(tmp, path, null);
        else File.Move(tmp, path);
    }

    public static PendingRestore? Load(string path)
    {
        try
        {
            return File.Exists(path)
                ? JsonSerializer.Deserialize<PendingRestore>(File.ReadAllText(path))
                : null;
        }
        catch
        {
            return null;
        }
    }

    public static void Delete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* повторим при следующем старте */ }
    }
}
