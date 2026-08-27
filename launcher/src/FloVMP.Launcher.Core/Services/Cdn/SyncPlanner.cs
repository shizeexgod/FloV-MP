using System.IO;

namespace FloVMP.Launcher.Services.Cdn;

public enum FileStatus { Ok, Missing, WrongSize, WrongHash }

public sealed record FileCheck(ManifestEntry Entry, FileStatus Status)
{
    public bool NeedsDownload => Status != FileStatus.Ok;
}

public sealed record SyncPlan(IReadOnlyList<FileCheck> Checks)
{
    public IReadOnlyList<FileCheck> ToDownload => Checks.Where(c => c.NeedsDownload).ToList();
    public long BytesToDownload => ToDownload.Sum(c => c.Entry.Size);
    public bool UpToDate => ToDownload.Count == 0;

    /// <summary>Лишние файлы в папке, которых нет в манифесте (для отчёта, не удаляем).</summary>
    public IReadOnlyList<string> Extra { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Сравнивает содержимое локальной папки с манифестом: чего нет, что бито
/// по размеру или по SHA-256.
/// </summary>
public static class SyncPlanner
{
    public static async Task<SyncPlan> BuildAsync(
        string rootDir,
        Manifest manifest,
        IProgress<string>? log = null,
        CancellationToken ct = default)
    {
        var checks = new List<FileCheck>(manifest.Files.Count);

        foreach (var entry in manifest.Files)
        {
            ct.ThrowIfCancellationRequested();
            var full = Path.Combine(rootDir, entry.Path.Replace('/', Path.DirectorySeparatorChar));

            if (!File.Exists(full))
            {
                checks.Add(new FileCheck(entry, FileStatus.Missing));
                continue;
            }

            var len = new FileInfo(full).Length;
            if (len != entry.Size)
            {
                checks.Add(new FileCheck(entry, FileStatus.WrongSize));
                continue;
            }

            var hash = await ContentHasher.Sha256FileAsync(full, ct);
            if (!string.Equals(hash, entry.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                log?.Report($"хэш не сошёлся: {entry.Path}");
                checks.Add(new FileCheck(entry, FileStatus.WrongHash));
                continue;
            }

            checks.Add(new FileCheck(entry, FileStatus.Ok));
        }

        var known = manifest.Files
            .Select(f => Path.GetFullPath(Path.Combine(rootDir, f.Path.Replace('/', Path.DirectorySeparatorChar))))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var extra = new List<string>();
        if (Directory.Exists(rootDir))
        {
            foreach (var f in Directory.EnumerateFiles(rootDir, "*", SearchOption.AllDirectories))
            {
                if (!known.Contains(Path.GetFullPath(f)))
                    extra.Add(Path.GetRelativePath(rootDir, f).Replace('\\', '/'));
            }
        }

        return new SyncPlan(checks) { Extra = extra };
    }
}
