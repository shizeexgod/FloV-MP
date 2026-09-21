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
}
