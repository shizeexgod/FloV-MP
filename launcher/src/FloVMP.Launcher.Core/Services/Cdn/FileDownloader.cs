using System.IO;
using System.Net;
using System.Net.Http;

namespace FloVMP.Launcher.Services.Cdn;

public sealed record DownloadProgress(
    long DoneBytes,
    long TotalBytes,
    double BytesPerSecond,
    int FilesDone,
    int FilesTotal,
    string CurrentFile)
{
    public double Fraction => TotalBytes > 0 ? (double)DoneBytes / TotalBytes : 0;
}

public sealed record DownloadOutcome(int Ok, int Failed, IReadOnlyList<string> Errors)
{
    public bool AllOk => Failed == 0;
}

public sealed class DownloaderOptions
{
    public int Concurrency { get; init; } = 4;
    public int MaxRetries { get; init; } = 3;
    public TimeSpan PerRequestTimeout { get; init; } = TimeSpan.FromMinutes(5);
}

/// <summary>
/// Многопоточная загрузка файлов манифеста с докачкой (HTTP Range), ретраями,
/// атомарной заменой и проверкой SHA-256 после скачивания.
///
/// Поддерживает и абсолютные http(s)-URL, и локальные пути / file:// —
/// последнее для офлайн-тестов раздачи без реального CDN.
/// </summary>
public sealed class FileDownloader
{
    private readonly HttpClient _http;
    private readonly DownloaderOptions _opt;

    public FileDownloader(HttpClient? http = null, DownloaderOptions? options = null)
    {
        _opt = options ?? new DownloaderOptions();
        _http = http ?? new HttpClient();
        _http.Timeout = _opt.PerRequestTimeout;
    }

    public async Task<DownloadOutcome> DownloadAsync(
        string rootDir,
        Manifest manifest,
        IReadOnlyList<ManifestEntry> entries,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken ct = default)
    {
        long total = entries.Sum(e => e.Size);
        long done = 0;
        int filesDone = 0;
        int failed = 0;
        var errors = new List<string>();
        var startedAt = DateTime.UtcNow;
        var gate = new SemaphoreSlim(_opt.Concurrency);
        var sync = new object();

        void Report(string current)
        {
            var elapsed = (DateTime.UtcNow - startedAt).TotalSeconds;
            var bps = elapsed > 0.1 ? done / elapsed : 0;
            progress?.Report(new DownloadProgress(
                Interlocked.Read(ref done), total, bps,
                Volatile.Read(ref filesDone), entries.Count, current));
        }

        var tasks = entries.Select(async entry =>
        {
            await gate.WaitAsync(ct);
            try
            {
                Report(entry.Path);
                await DownloadOneAsync(rootDir, manifest, entry,
                    delta => { Interlocked.Add(ref done, delta); Report(entry.Path); }, ct);
                Interlocked.Increment(ref filesDone);
                Report(entry.Path);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lock (sync) { failed++; errors.Add($"{entry.Path}: {ex.GetBaseException().Message}"); }
            }
            finally
            {
                gate.Release();
            }
        });

        await Task.WhenAll(tasks);
        return new DownloadOutcome(entries.Count - failed, failed, errors);
    }

    private async Task DownloadOneAsync(
        string rootDir, Manifest manifest, ManifestEntry entry,
        Action<long> onBytes, CancellationToken ct)
    {
        var dest = Path.Combine(rootDir, entry.Path.Replace('/', Path.DirectorySeparatorChar));

        // Защита от path-traversal: entry.Path приходит из сетевого манифеста.
        // Если в нём '..' или абсолютный путь, Path.Combine мог бы вывести dest
        // за пределы rootDir (произвольная запись, напр. в Автозагрузку). Режем.
        var rootFull = Path.GetFullPath(rootDir);
        var destFull = Path.GetFullPath(dest);
        var rootPrefix = rootFull.EndsWith(Path.DirectorySeparatorChar)
            ? rootFull : rootFull + Path.DirectorySeparatorChar;
        if (!destFull.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"манифест: путь выходит за пределы папки движка: {entry.Path}");
        dest = destFull;

        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        var part = dest + ".part";
        var uri = ResolveUri(manifest, entry);

        Exception? last = null;
        for (var attempt = 1; attempt <= _opt.MaxRetries; attempt++)
        {
            try
            {
                if (uri.IsFile)
                {
                    File.Copy(uri.LocalPath, part, overwrite: true);
                    onBytes(new FileInfo(part).Length);
                }
                else
                {
                    await HttpFetchAsync(uri, part, onBytes, ct);
                }

                var hash = await ContentHasher.Sha256FileAsync(part, ct);
                if (!string.Equals(hash, entry.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException($"SHA-256 mismatch after download (got {hash})");

                if (File.Exists(dest)) File.Delete(dest);
                File.Move(part, dest);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                last = ex;
                TryDelete(part);
                if (attempt < _opt.MaxRetries)
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct);
            }
        }

        throw last ?? new IOException("download failed");
    }

    private async Task HttpFetchAsync(Uri uri, string part, Action<long> onBytes, CancellationToken ct)
    {
        long existing = File.Exists(part) ? new FileInfo(part).Length : 0;

        using var req = new HttpRequestMessage(HttpMethod.Get, uri);
        if (existing > 0) req.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(existing, null);

        using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);

        var append = resp.StatusCode == HttpStatusCode.PartialContent;
        if (!append && existing > 0)
        {
            // сервер не понял Range — качаем заново с нуля
            existing = 0;
            TryDelete(part);
        }
        resp.EnsureSuccessStatusCode();

        await using var netStream = await resp.Content.ReadAsStreamAsync(ct);
        await using var fileStream = new FileStream(
            part, append ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None,
            1 << 20, useAsync: true);

        var buffer = new byte[1 << 20];
        int read;
        while ((read = await netStream.ReadAsync(buffer, ct)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, read), ct);
            onBytes(read);
        }
    }

    private static Uri ResolveUri(Manifest manifest, ManifestEntry entry)
    {
        var raw = !string.IsNullOrWhiteSpace(entry.Url) ? entry.Url : entry.Path;

        // Абсолютная ссылка у самой записи.
        if (Uri.TryCreate(raw, UriKind.Absolute, out var abs) && abs.Scheme is "http" or "https" or "file")
            return abs;

        var b = manifest.BaseUrl;
        if (string.IsNullOrWhiteSpace(b))
            throw new InvalidOperationException($"нет baseUrl для относительной ссылки '{raw}'");

        // http(s)-база: комбинируем, гарантируя завершающий '/' у каталога.
        if (Uri.TryCreate(b, UriKind.Absolute, out var baseAbs) && baseAbs.Scheme is "http" or "https")
        {
            var withSlash = b.EndsWith("/") ? b : b + "/";
            return new Uri(new Uri(withSlash), raw);
        }

        // Локальный каталог раздачи (офлайн-тесты): file:// или обычный путь.
        var localBase = baseAbs is { IsFile: true } ? baseAbs.LocalPath : b;
        var rel = raw.Replace('/', Path.DirectorySeparatorChar);
        return new Uri(Path.GetFullPath(Path.Combine(localBase, rel)));
    }

    private static void TryDelete(string p) { try { if (File.Exists(p)) File.Delete(p); } catch { } }
}
