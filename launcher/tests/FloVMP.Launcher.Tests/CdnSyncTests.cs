using System.Text;
using FloVMP.Launcher.Services.Cdn;
using Xunit;

namespace FloVMP.Launcher.Tests;

/// <summary>
/// Тесты цикла CDN-синхронизации на локальной «раздаче» (folder-as-CDN),
/// без реального сервера.
/// </summary>
public sealed class CdnSyncTests : IDisposable
{
    private readonly string _cdn;   // «сервер раздачи»
    private readonly string _local; // папка игрока

    public CdnSyncTests()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), "flovmp-cdn-tests", Guid.NewGuid().ToString("N"));
        _cdn = Path.Combine(baseDir, "cdn");
        _local = Path.Combine(baseDir, "local");
        Directory.CreateDirectory(_cdn);
        Directory.CreateDirectory(_local);
    }

    public void Dispose()
    {
        try { Directory.Delete(Path.GetDirectoryName(_cdn)!, recursive: true); } catch { }
    }

    private void WriteCdn(string rel, string content)
    {
        var p = Path.Combine(_cdn, rel.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(p)!);
        File.WriteAllText(p, content, new UTF8Encoding(false));
    }

    private void WriteLocal(string rel, string content)
    {
        var p = Path.Combine(_local, rel.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(p)!);
        File.WriteAllText(p, content, new UTF8Encoding(false));
    }

    private async Task<string> BuildManifestAsync(params string[] rels)
    {
        var m = new Manifest { Version = "test-1", Branch = "release", BaseUrl = _cdn };
        foreach (var rel in rels)
        {
            var p = Path.Combine(_cdn, rel.Replace('/', Path.DirectorySeparatorChar));
            m.Files.Add(new ManifestEntry
            {
                Path = rel,
                Size = new FileInfo(p).Length,
                Sha256 = await ContentHasher.Sha256FileAsync(p),
            });
        }
        var mp = Path.Combine(_cdn, "manifest.json");
        await File.WriteAllTextAsync(mp,
            System.Text.Json.JsonSerializer.Serialize(m));
        return mp;
    }

    [Fact]
    public async Task Check_reports_up_to_date_when_local_matches()
    {
        WriteCdn("a.txt", "alpha");
        WriteCdn("sub/b.txt", "bravo");
        var manifest = await BuildManifestAsync("a.txt", "sub/b.txt");
        WriteLocal("a.txt", "alpha");
        WriteLocal("sub/b.txt", "bravo");

        var report = await new SyncService().CheckAsync(manifest, _local);

        Assert.True(report.Success);
        Assert.True(report.Plan!.UpToDate);
    }

    [Fact]
    public async Task Check_classifies_missing_wrongsize_wronghash()
    {
        WriteCdn("missing.txt", "need this");
        WriteCdn("size.txt", "correct length here");
        WriteCdn("hash.txt", "canonical");
        var manifest = await BuildManifestAsync("missing.txt", "size.txt", "hash.txt");

        // missing.txt — не создаём
        WriteLocal("size.txt", "different length entirely!!");
        WriteLocal("hash.txt", "malformed"); // тот же размер (9) что и "canonical"

        var plan = await SyncPlanner.BuildAsync(_local,
            await new ManifestClient().FetchAsync(manifest));

        FileStatus S(string p) => plan.Checks.Single(c => c.Entry.Path == p).Status;
        Assert.Equal(FileStatus.Missing, S("missing.txt"));
        Assert.Equal(FileStatus.WrongSize, S("size.txt"));
        Assert.Equal(FileStatus.WrongHash, S("hash.txt"));
    }

    [Fact]
    public async Task Sync_downloads_missing_and_repairs_corrupt()
    {
        WriteCdn("bin/core.dll", "PRETEND BINARY CONTENT v1");
        WriteCdn("cfg/app.json", "{\"k\":1}");
        WriteCdn("readme.txt", "hello");
        var manifest = await BuildManifestAsync("bin/core.dll", "cfg/app.json", "readme.txt");

        // локально: readme ок, core.dll битый, app.json отсутствует
        WriteLocal("readme.txt", "hello");
        WriteLocal("bin/core.dll", "corrupted junk");

        var report = await new SyncService().SyncAsync(manifest, _local);

        Assert.True(report.Success, report.Message);
        Assert.Equal("PRETEND BINARY CONTENT v1", File.ReadAllText(Path.Combine(_local, "bin", "core.dll")));
        Assert.Equal("{\"k\":1}", File.ReadAllText(Path.Combine(_local, "cfg", "app.json")));
        Assert.True(report.Plan!.UpToDate);
    }

    [Fact]
    public async Task Sync_reports_progress_reaching_full()
    {
        for (var i = 0; i < 6; i++) WriteCdn($"f{i}.dat", new string('x', 500 + i * 100));
        var manifest = await BuildManifestAsync("f0.dat", "f1.dat", "f2.dat", "f3.dat", "f4.dat", "f5.dat");

        double lastFraction = 0;
        var progress = new Progress<DownloadProgress>(p => lastFraction = Math.Max(lastFraction, p.Fraction));

        var report = await new SyncService().SyncAsync(manifest, _local, progress: progress);

        Assert.True(report.Success, report.Message);
        Assert.Equal(6, report.Download!.Ok);
        Assert.Equal(0, report.Download.Failed);
        Assert.Equal(1.0, lastFraction, precision: 3);
    }

    [Fact]
    public async Task Download_retries_then_fails_on_permanently_bad_hash()
    {
        WriteCdn("x.dat", "real content");
        var manifest = await new ManifestClient().FetchAsync(await BuildManifestAsync("x.dat"));

        // портим ожидаемый хэш в манифесте -> скачанный файл никогда не сойдётся
        manifest.Files[0].Sha256 = new string('0', 64);

        var dl = new FileDownloader(options: new DownloaderOptions { MaxRetries = 2, PerRequestTimeout = TimeSpan.FromSeconds(10) });
        var outcome = await dl.DownloadAsync(_local, manifest, manifest.Files);

        Assert.Equal(1, outcome.Failed);
        Assert.False(File.Exists(Path.Combine(_local, "x.dat")));      // .part подчищен, файл не подставлен
        Assert.Contains("mismatch", outcome.Errors[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Manifest_without_baseUrl_resolves_against_its_own_folder()
    {
        WriteCdn("thing.bin", "data-data");
        var m = new Manifest { Version = "v", Branch = "release" }; // BaseUrl пуст
        m.Files.Add(new ManifestEntry
        {
            Path = "thing.bin",
            Size = new FileInfo(Path.Combine(_cdn, "thing.bin")).Length,
            Sha256 = await ContentHasher.Sha256FileAsync(Path.Combine(_cdn, "thing.bin")),
        });
        var mp = Path.Combine(_cdn, "manifest.json");
        await File.WriteAllTextAsync(mp, System.Text.Json.JsonSerializer.Serialize(m));

        var report = await new SyncService().SyncAsync(mp, _local);

        Assert.True(report.Success, report.Message);
        Assert.True(File.Exists(Path.Combine(_local, "thing.bin")));
    }
}
