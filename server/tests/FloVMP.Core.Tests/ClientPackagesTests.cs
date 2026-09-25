using System.Net;
using System.Text;
using FloVMP.Core.Mods;
using Xunit;

namespace FloVMP.Core.Tests;

/// <summary>
/// Клиентские пакеты (server/client_packages): тот же сервер раздачи, что у
/// модов, но своя область /client/ и свой набор расширений. Главное: код
/// раздаётся только из client_packages, в /mods/ он по-прежнему запрещён.
/// </summary>
public class ClientPackagesTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "flovmp-cpkg-" + Guid.NewGuid().ToString("N"));
    private readonly string _mods;
    private readonly string _client;

    public ClientPackagesTests()
    {
        _mods = Path.Combine(_root, "mods");
        _client = Path.Combine(_root, "client_packages");
        Directory.CreateDirectory(Path.Combine(_client, "ui"));
        Directory.CreateDirectory(_mods);
        File.WriteAllText(Path.Combine(_client, "index.js"), "mp.events.add('render', () => {});");
        File.WriteAllText(Path.Combine(_client, "ui", "index.html"), "<html></html>");
        File.WriteAllText(Path.Combine(_client, "ui", "app.css"), "body{}");
        File.WriteAllBytes(Path.Combine(_client, "evil.dll"), new byte[] { 1, 2, 3 });
        File.WriteAllBytes(Path.Combine(_client, "evil.asi"), new byte[] { 1, 2, 3 });
        File.WriteAllText(Path.Combine(_mods, "script.js"), "alert(1)");
        File.WriteAllBytes(Path.Combine(_mods, "map.rpf"), new byte[] { 9, 9 });
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }

    private ModFileServer Server(out ModManifest mods, out ModManifest client)
    {
        var m = ModManifest.Build(_mods);
        var c = ModManifest.Build(_client, ModManifest.ClientPackageExtensions);
        mods = m;
        client = c;
        return new ModFileServer(IPAddress.Loopback, 0, new[]
        {
            new FileArea("mods", _mods, () => m),
            new FileArea("client", _client, () => c),
        }, _ => { });
    }

    private static HttpRequestLine Get(string path) => HttpRequestLine.Parse($"GET {path} HTTP/1.1\r\nHost: x");

    [Fact]
    public void Скрипты_и_интерфейс_попадают_в_клиентский_список()
    {
        var c = ModManifest.Build(_client, ModManifest.ClientPackageExtensions);
        Assert.Equal(new[] { "index.js", "ui/app.css", "ui/index.html" }, c.Files.Select(f => f.Path));
    }

    [Fact]
    public void Исполняемые_файлы_Windows_не_попадают_даже_в_клиентский_список()
    {
        var c = ModManifest.Build(_client, ModManifest.ClientPackageExtensions);
        Assert.DoesNotContain(c.Files, f => f.Path.EndsWith(".dll") || f.Path.EndsWith(".asi"));
        Assert.Contains(c.Skipped, s => s.StartsWith("evil.dll"));
        Assert.Contains(c.Skipped, s => s.StartsWith("evil.asi"));
    }

    [Fact]
    public void Скрипт_в_папке_модов_по_прежнему_не_раздаётся()
    {
        var m = ModManifest.Build(_mods);
        Assert.Equal(new[] { "map.rpf" }, m.Files.Select(f => f.Path));
        Assert.False(ModManifest.ValidPath("script.js"));
        Assert.True(ModManifest.ValidPath("script.js", ModManifest.ClientPackageExtensions));
    }

    [Fact]
    public void Клиентский_файл_отдаётся_из_своей_области()
    {
        using var server = Server(out _, out _);
        var plan = server.Plan(Get("/client/files/index.js"));
        Assert.StartsWith("200", plan.Status);
        Assert.EndsWith("index.js", plan.FilePath);
    }

    [Fact]
    public void Файл_из_одной_области_не_достаётся_через_другую()
    {
        using var server = Server(out _, out _);
        Assert.StartsWith("404", server.Plan(Get("/mods/files/index.js")).Status);
        Assert.StartsWith("404", server.Plan(Get("/client/files/map.rpf")).Status);
    }

    [Fact]
    public void Выход_за_папку_через_путь_невозможен()
    {
        using var server = Server(out _, out _);
        Assert.StartsWith("404", server.Plan(Get("/client/files/..%2Fmods%2Fmap.rpf")).Status);
        Assert.StartsWith("404", server.Plan(Get("/client/files/../mods/map.rpf")).Status);
    }

    [Fact]
    public void Список_для_клиента_строками_с_отпечатком_первой_строкой()
    {
        using var server = Server(out _, out var client);
        var plan = server.Plan(Get("/client/manifest.txt"));
        Assert.StartsWith("200", plan.Status);
        var lines = Encoding.UTF8.GetString(plan.Body!).TrimEnd('\n').Split('\n');
        Assert.Equal("digest\t" + client.Digest, lines[0]);
        Assert.Equal(client.Files.Count + 1, lines.Length);
        var first = lines[1].Split('\t');
        Assert.Equal("index.js", first[0]);
        Assert.Equal(client.Files[0].Size.ToString(), first[1]);
        Assert.Equal(64, first[2].Length);
    }

    [Fact]
    public void Старый_конструктор_раздаёт_только_моды()
    {
        var m = ModManifest.Build(_mods);
        using var server = new ModFileServer(IPAddress.Loopback, 0, _mods, () => m, _ => { });
        Assert.StartsWith("200", server.Plan(Get("/mods/files/map.rpf")).Status);
        Assert.StartsWith("404", server.Plan(Get("/client/files/index.js")).Status);
    }
}
