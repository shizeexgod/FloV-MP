using System.Diagnostics;
using System.IO;

namespace FloVMP.Launcher.Services.Compat;

/// <summary>
/// Идентификатор установленной версии GTA V. Комбинация метаданных PE
/// (FileVersion) + размер файла. Полный SHA-256 считается по запросу
/// (файл ~70 МБ — не на каждый чих).
/// </summary>
public sealed record GameVersion(string FileVersion, long Size, string ExePath)
{
    /// <summary>Короткий ключ для сопоставления с манифестом совместимости.</summary>
    public string Key => $"{FileVersion}|{Size}";

    public static GameVersion? Detect(string gtaFolder)
    {
        if (string.IsNullOrWhiteSpace(gtaFolder)) return null;
        var exe = Path.Combine(gtaFolder, "GTA5.exe");
        if (!File.Exists(exe)) return null;

        string fv;
        try
        {
            fv = FileVersionInfo.GetVersionInfo(exe).FileVersion?.Trim() ?? "";
        }
        catch
        {
            fv = "";
        }

        return new GameVersion(fv, new FileInfo(exe).Length, exe);
    }

    public async Task<string> Sha256Async(CancellationToken ct = default) =>
        await Cdn.ContentHasher.Sha256FileAsync(ExePath, ct);
}
