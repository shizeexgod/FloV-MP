namespace FloVMP.Launcher.Services.Cdn;

public sealed record SyncReport(
    bool Success,
    Manifest? Manifest,
    SyncPlan? Plan,
    DownloadOutcome? Download,
    string Message)
{
    public static SyncReport Fail(string msg) => new(false, null, null, null, msg);
}

/// <summary>
/// Полный цикл синхронизации управляемой папки с CDN:
/// манифест → сверка → докачка недостающего/битого → повторная сверка.
/// </summary>
public sealed class SyncService
{
    private readonly ManifestClient _manifests;
    private readonly FileDownloader _downloader;

    public SyncService(ManifestClient? manifests = null, FileDownloader? downloader = null)
    {
        _manifests = manifests ?? new ManifestClient();
        _downloader = downloader ?? new FileDownloader();
    }

    /// <summary>Только проверить, что папка соответствует манифесту (без скачивания).</summary>
    public async Task<SyncReport> CheckAsync(
        string manifestSource, string rootDir,
        IProgress<string>? log = null, CancellationToken ct = default)
    {
        Manifest manifest;
        try
        {
            manifest = await _manifests.FetchAsync(manifestSource, ct);
        }
        catch (Exception ex)
        {
            return SyncReport.Fail("манифест не загружен: " + ex.GetBaseException().Message);
        }

        var plan = await SyncPlanner.BuildAsync(rootDir, manifest, log, ct);
        var msg = plan.UpToDate
            ? $"актуально ({manifest.Files.Count} файлов, версия {manifest.Version})"
            : $"нужно обновить: {plan.ToDownload.Count} файл(ов), {Mb(plan.BytesToDownload)}";
        return new SyncReport(plan.UpToDate, manifest, plan, null, msg);
    }

    public async Task<SyncReport> SyncAsync(
        string manifestSource, string rootDir,
        IProgress<string>? log = null,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken ct = default)
    {
        var check = await CheckAsync(manifestSource, rootDir, log, ct);
        if (check.Manifest is null) return check;
        if (check.Plan!.UpToDate) return check with { Success = true };

        log?.Report($"скачивание: {check.Plan.ToDownload.Count} файл(ов), {Mb(check.Plan.BytesToDownload)}");
        var entries = check.Plan.ToDownload.Select(c => c.Entry).ToList();
        var outcome = await _downloader.DownloadAsync(
            rootDir, check.Manifest, entries, progress, ct);

        var after = await SyncPlanner.BuildAsync(rootDir, check.Manifest, log, ct);
        var ok = outcome.AllOk && after.UpToDate;
        var msg = ok
            ? $"обновлено ({check.Manifest.Files.Count} файлов)"
            : $"с ошибками: скачано {outcome.Ok}, не удалось {outcome.Failed}; ещё не хватает {after.ToDownload.Count}";

        return new SyncReport(ok, check.Manifest, after, outcome, msg);
    }

    private static string Mb(long bytes) => $"{bytes / 1024d / 1024d:0.0} МБ";
}
