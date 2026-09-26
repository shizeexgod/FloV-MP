using FloVMP.Core.Licensing;
using FloVMP.Core.Settings;
using Xunit;

namespace FloVMP.Core.Tests;

public sealed class ServerSettingsTests
{
    [Fact]
    public void DefaultFileRoundTripsWithoutProblems()
    {
        var (s, problems, present) = ServerSettings.Parse(ServerSettings.DefaultFileContent());
        Assert.Empty(problems);
        Assert.Equal(ServerSettings.Schema.Count, present.Count);
        Assert.False(s.Bool("world.peds"));
        Assert.Equal("#ffffff", s.Get("hud.accent"));
        Assert.Single(s.SpawnPoints());
    }

    [Fact]
    public void ClientReceivesLoadingWindowAndKeySettings()
    {
        var (s, problems, _) = ServerSettings.Parse(
            "window.title = Мой сервер — {server}\nloading.tips = {ffffff}Раз | Два\nkeys.console = F10\nconsole.theme = slate\n");
        Assert.Empty(problems);
        var client = s.ClientValues().ToDictionary(kv => kv.Key, kv => kv.Value);
        Assert.Equal("Мой сервер — {server}", client["window.title"]);
        Assert.Equal("{ffffff}Раз | Два", client["loading.tips"]);
        Assert.Equal("F10", client["keys.console"]);
        Assert.Equal("slate", client["console.theme"]);
        Assert.Equal("256", client["chat.max_length"]);
        Assert.Equal("#ff3d8a", client["loading.accent"]);
    }

    [Fact]
    public void BrandingNeedsSourceKit()
    {
        var (s, _, _) = ServerSettings.Parse("window.title = Мой RP\nbranding.name = MyRP\nhud.accent = #ff0000\n");
        Assert.Equal(new[] { "window.title", "branding.name" }, s.CustomizedBrandingKeys().OrderByDescending(k => k).ToArray());
        var locked = s.ClientValues(brandingAllowed: false).ToDictionary(kv => kv.Key, kv => kv.Value);
        Assert.Equal("FloV Multiplayer — {server}", locked["window.title"]);
        Assert.Equal("FloV:MP", locked["branding.name"]);
        Assert.Equal("#ff0000", locked["hud.accent"]); // остальное владелец меняет свободно
        Assert.Equal("MyRP", s.ClientValues(brandingAllowed: true).First(kv => kv.Key == "branding.name").Value);

        LicenseInfo Lic(string plan) => new("FLV-1", "p", "o", plan, 100, 1, DateTime.UtcNow, DateTime.UtcNow.AddDays(1));
        Assert.False(Edition.BrandingAllowed(null));
        Assert.False(Edition.BrandingAllowed(Lic("business")));
        Assert.False(Edition.BrandingAllowed(Lic("enterprise")));
        Assert.True(Edition.BrandingAllowed(Lic("source-kit")));
        Assert.True(Edition.BrandingAllowed(Lic(" Source ")));
    }

    [Fact]
    public void ValuesAreNormalizedAndBadOnesFallBackToDefault()
    {
        var (s, problems, _) = ServerSettings.Parse(string.Join('\n',
            "world.peds = да",
            "nametags.distance = 45,5 # дальше обычного",
            "hud.accent = #FF3D8A",
            "chat.lines = 999",
            "voice.key = caps",
            "unknown.key = 1",
            "spawn.points = 1,2,3,90; bad; 10, 20, 30"));
        Assert.True(s.Bool("world.peds"));
        Assert.Equal(45.5f, s.Float("nametags.distance"));
        Assert.Equal("#ff3d8a", s.Get("hud.accent"));
        Assert.Equal(10, s.Int("chat.lines"));      // вне 3..30 — по умолчанию
        Assert.Equal("N", s.Get("voice.key"));       // неизвестная клавиша — по умолчанию
        Assert.Equal(3, problems.Count);
        var points = s.SpawnPoints();
        Assert.Equal(2, points.Count);
        Assert.Equal(90f, points[0].Heading);
    }

    [Fact]
    public void MissingKeysAreAppendedWithoutTouchingOwnerValues()
    {
        var dir = Path.Combine(Path.GetTempPath(), "flovmp-settings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, ServerSettings.FileName);
            File.WriteAllText(path, "# мой сервер\nworld.peds = on\n");
            var s = ServerSettings.LoadOrCreate(dir, _ => { }, out var summary);
            Assert.True(s.Bool("world.peds"));
            var text = File.ReadAllText(path);
            Assert.StartsWith("# мой сервер\nworld.peds = on\n", text);
            Assert.Contains("hud.pause_menu = off", text);
            Assert.Contains("дописано новых", summary);
            // Повторная загрузка ничего не дописывает.
            ServerSettings.LoadOrCreate(dir, _ => { }, out var again);
            Assert.DoesNotContain("дописано", again);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void KeyCodes()
    {
        Assert.Equal('N', ServerSettings.KeyCode("N"));
        Assert.Equal(0x72, ServerSettings.KeyCode("F3"));
        Assert.Equal(0x7B, ServerSettings.KeyCode("F12"));
        Assert.Equal(0, ServerSettings.KeyCode("F13"));
        Assert.Equal(0, ServerSettings.KeyCode("ESC"));
    }

    [Fact]
    public void Прежний_умолчательный_цвет_загрузки_заменяется_а_свой_владельца_нет()
    {
        var text = "loading.accent = #fbbf24\r\nhud.accent = #ffffff\r\n";
        var updated = ServerSettings.ReplaceRetiredDefaults(text, null, out var n);
        Assert.Equal(1, n);
        Assert.Contains("loading.accent = #ff3d8a\r\n", updated);
        Assert.Contains("hud.accent = #ffffff", updated);

        var own = ServerSettings.ReplaceRetiredDefaults("loading.accent = #22c55e\n", null, out var m);
        Assert.Equal(0, m);
        Assert.Equal("loading.accent = #22c55e\n", own);
    }
}
