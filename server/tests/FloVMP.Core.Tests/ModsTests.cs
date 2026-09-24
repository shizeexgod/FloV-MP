using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using FloVMP.Core.Mods;
using Xunit;

namespace FloVMP.Core.Tests;

public sealed class ModsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "flovmp-mods-" + Guid.NewGuid().ToString("N"));

    public ModsTests() => Directory.CreateDirectory(_root);

    public void Dispose() { try { Directory.Delete(_root, true); } catch { } }

    private string Put(string rel, byte[] data)
    {
        var p = Path.Combine(_root, rel.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(p)!);
        File.WriteAllBytes(p, data);
        return p;
    }

    private static byte[] Bytes(int n, int seed = 1)
    {
        var b = new byte[n];
        new Random(seed).NextBytes(b);
        return b;
    }

    // ------------------------------------------------------------ список

    [Fact]
    public void Manifest_ListsGameData_AndRefusesExecutables()
    {
        Put("update/x64/dlcpacks/moscow/dlc.rpf", Bytes(1000));
        Put("x64/levels/map.ymap", Bytes(10));
        Put("evil.asi", Bytes(5));
        Put("scripts/hack.dll", Bytes(5));
        Put("run.bat", Bytes(5));
        Put("Thumbs.db", Bytes(5));
        Put(".git/config", Bytes(5));
        var m = ModManifest.Build(_root);
        Assert.Equal(new[] { "update/x64/dlcpacks/moscow/dlc.rpf", "x64/levels/map.ymap" }, m.Files.Select(f => f.Path));
        Assert.Equal(1010, m.TotalSize);
        Assert.Equal(3, m.Skipped.Count);                    // asi, dll, bat — с причиной; мусор молча
        Assert.All(m.Skipped, s => Assert.Contains("не раздаётся", s));
        var rpf = m.Files[0];
        Assert.Equal(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(_root, "update", "x64", "dlcpacks", "moscow", "dlc.rpf")))).ToLowerInvariant(), rpf.Sha256);
    }

    [Theory]
    [InlineData("a/b.rpf", true)]
    [InlineData("../b.rpf", false)]
    [InlineData("a/../b.rpf", false)]
    [InlineData("/abs.rpf", false)]
    [InlineData("C:/x.rpf", false)]
    [InlineData("a\\b.rpf", false)]
    [InlineData("a/b.rpf:stream", false)]
    [InlineData("a./b.rpf", false)]
    [InlineData("a/b.exe", false)]
    [InlineData("a/b.ASI", false)]
    [InlineData("a/B.YTD", true)]
    public void Manifest_PathRules(string path, bool ok) => Assert.Equal(ok, ModManifest.ValidPath(path));

    [Fact]
    public void Manifest_DigestChangesWithContent_AndJsonRoundTrips()
    {
        Put("a.rpf", Bytes(100, 1));
        var first = ModManifest.Build(_root);
        var parsed = ModManifest.Parse(first.ToJson());
        Assert.NotNull(parsed);
        Assert.Equal(first.Digest, parsed!.Digest);

        Put("a.rpf", Bytes(100, 2));
        Assert.NotEqual(first.Digest, ModManifest.Build(_root).Digest);
        // Подменённый список (файл другой, отпечаток прежний) не принимается.
        Assert.Null(ModManifest.Parse(first.ToJson().Replace(first.Files[0].Sha256, new string('0', 64))));
        Assert.Null(ModManifest.Parse(first.ToJson().Replace("a.rpf", "../a.rpf")));
    }

    [Fact]
    public void HashCache_SkipsUnchangedFiles_AndSurvivesRestart()
    {
        var path = Put("big.rpf", Bytes(5000));
        var cacheFile = Path.Combine(_root, "..", Path.GetFileName(_root) + "-cache.json");
        try
        {
            var cache = new ModHashCache(cacheFile);
            var sha = ModManifest.Build(_root, cache).Files[0].Sha256;
            cache.Save();

            // Кэш верит размеру и времени: подложим в кэш «чужой» хэш и убедимся,
            // что второй раз файл действительно не читался.
            var reloaded = new ModHashCache(cacheFile);
            var info = new FileInfo(path);
            Assert.Equal(sha, reloaded.Get("big.rpf", info.Length, info.LastWriteTimeUtc.Ticks));
            reloaded.Put("big.rpf", info.Length, info.LastWriteTimeUtc.Ticks, new string('a', 64));
            Assert.Equal(new string('a', 64), ModManifest.Build(_root, reloaded).Files[0].Sha256);

            // Файл изменился — время другое, хэш пересчитан.
            File.WriteAllBytes(path, Bytes(5000, 9));
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(1));
            Assert.NotEqual(new string('a', 64), ModManifest.Build(_root, reloaded).Files[0].Sha256);
        }
        finally { File.Delete(cacheFile); }
    }

    // ------------------------------------------------------------ раздача

    private (ModFileServer Server, ModManifest Manifest) StartServer()
    {
        var m = ModManifest.Build(_root);
        var server = new ModFileServer(IPAddress.Loopback, 0, _root, () => m, _ => { });
        server.Start();
        return (server, m);
    }

    private static async Task<(string Head, byte[] Body)> Get(int port, string request)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port);
        var stream = client.GetStream();
        await stream.WriteAsync(Encoding.ASCII.GetBytes(request));
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms).WaitAsync(TimeSpan.FromSeconds(10));
        var all = ms.ToArray();
        var split = IndexOf(all, "\r\n\r\n"u8.ToArray());
        return (Encoding.ASCII.GetString(all, 0, split), all[(split + 4)..]);
    }

    private static int IndexOf(byte[] data, byte[] what)
    {
        for (var i = 0; i + what.Length <= data.Length; i++)
            if (data.AsSpan(i, what.Length).SequenceEqual(what)) return i;
        return -1;
    }

    [Fact]
    public async Task Server_FullFile_Range_And416()
    {
        var data = Bytes(200_000);
        Put("dlc/big.rpf", data);
        var (server, m) = StartServer();
        using (server)
        {
            var (head, body) = await Get(server.Port, "GET /mods/files/dlc/big.rpf HTTP/1.1\r\nHost: x\r\nConnection: close\r\n\r\n");
            Assert.StartsWith("HTTP/1.1 200", head);
            Assert.Contains($"ETag: \"{m.Files[0].Sha256}\"", head);
            Assert.Equal(data, body);

            (head, body) = await Get(server.Port, "GET /mods/files/dlc/big.rpf HTTP/1.1\r\nRange: bytes=150000-\r\nConnection: close\r\n\r\n");
            Assert.StartsWith("HTTP/1.1 206", head);
            Assert.Contains("Content-Range: bytes 150000-199999/200000", head);
            Assert.Equal(data[150_000..], body);

            (head, _) = await Get(server.Port, "GET /mods/files/dlc/big.rpf HTTP/1.1\r\nRange: bytes=200000-\r\nConnection: close\r\n\r\n");
            Assert.StartsWith("HTTP/1.1 416", head);

            // Файл поменялся между кусками (другой ETag) — отдаётся целиком заново.
            (head, body) = await Get(server.Port, "GET /mods/files/dlc/big.rpf HTTP/1.1\r\nRange: bytes=10-\r\nIf-Range: \"old\"\r\nConnection: close\r\n\r\n");
            Assert.StartsWith("HTTP/1.1 200", head);
            Assert.Equal(data.Length, body.Length);
        }
    }

    [Fact]
    public async Task Server_OnlyListedFiles_NoTraversal_NoExecutables()
    {
        Put("ok.rpf", Bytes(10));
        Put("evil.asi", Bytes(10));
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(_root)!, "secret.rpf"), "secret");
        var (server, _) = StartServer();
        using (server)
        {
            foreach (var path in new[] { "evil.asi", "../secret.rpf", "..%2Fsecret.rpf", "%2E%2E/secret.rpf", "ok.rpf/../ok.rpf" })
            {
                var (head, _) = await Get(server.Port, $"GET /mods/files/{path} HTTP/1.1\r\nConnection: close\r\n\r\n");
                Assert.StartsWith("HTTP/1.1 404", head);
            }
            var (h, _) = await Get(server.Port, "POST /mods/manifest.json HTTP/1.1\r\nConnection: close\r\n\r\n");
            Assert.StartsWith("HTTP/1.1 405", h);
        }
    }

    [Fact]
    public async Task Server_KeepAlive_ManifestThenFile_AndHead()
    {
        var data = Bytes(1234);
        Put("a.ytd", data);
        var (server, m) = StartServer();
        using (server)
        {
            var (head, body) = await Get(server.Port,
                "GET /mods/manifest.json HTTP/1.1\r\n\r\n" +
                "HEAD /mods/files/a.ytd HTTP/1.1\r\n\r\n" +
                "GET /mods/files/a.ytd HTTP/1.1\r\nConnection: close\r\n\r\n");
            Assert.StartsWith("HTTP/1.1 200", head);
            var text = Encoding.UTF8.GetString(body);
            Assert.Contains(m.Digest, text);
            // После списка — ответ на HEAD (только заголовки) и файл.
            Assert.Equal(2, text.Split("HTTP/1.1 200").Length - 1);
            Assert.True(body.AsSpan(body.Length - data.Length).SequenceEqual(data));
        }
    }
}
