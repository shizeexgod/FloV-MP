using FloVMP.Core.Diagnostics;
using Xunit;

namespace FloVMP.Core.Tests;

public class ClientCrashTests
{
    private static string[] Line(params string[] rest) => new[] { "CRASH" }.Concat(rest).ToArray();

    [Fact]
    public void Parse_ValidReport()
    {
        Assert.True(ClientCrashReport.TryParse(Line("0xC0000005", "GTA5.exe", "0x1A2B3C", "1.0.6-beta", "120", "1.0.3889.0"), out var r));
        Assert.Equal(0xC0000005u, r.Code);
        Assert.Equal("GTA5.exe+0x1A2B3C", r.Where);
        Assert.Equal("нарушение доступа к памяти", r.CodeName);
        Assert.Contains("2 мин назад", r.Describe("Nick [3]"));
        Assert.Contains("1.0.3889.0", r.Describe("Nick [3]"));
    }

    [Fact]
    public void Parse_GameVersionOptional()
    {
        Assert.True(ClientCrashReport.TryParse(Line("C00000FD", "FloVMP.asi", "ff", "1.0.6-beta", "0"), out var r));
        Assert.Equal("?", r.GameVersion);
        Assert.Equal("FloVMP.asi+0xFF", r.Where);
    }

    [Theory]
    [InlineData("zz", "GTA5.exe", "0x10", "1.0.6", "1")]          // код не hex
    [InlineData("0x1FFFFFFFF", "GTA5.exe", "0x10", "1.0.6", "1")]  // код шире 32 бит
    [InlineData("0xC0000005", "..\\evil", "0x10", "1.0.6", "1")]   // модуль с путём
    [InlineData("0xC0000005", "a b", "0x10", "1.0.6", "1")]        // пробел в имени
    [InlineData("0xC0000005", "GTA5.exe", "", "1.0.6", "1")]       // пустое смещение
    [InlineData("0xC0000005", "GTA5.exe", "0x10", "1.0.6\n[x]", "1")]  // перевод строки в версии
    [InlineData("0xC0000005", "GTA5.exe", "0x10", "1.0.6", "-5")]  // отрицательный возраст
    [InlineData("0xC0000005", "GTA5.exe", "0x10", "1.0.6", "99999999")] // старше месяца
    public void Parse_RejectsGarbage(string code, string module, string offset, string version, string age)
    {
        Assert.False(ClientCrashReport.TryParse(Line(code, module, offset, version, age), out _));
    }

    [Fact]
    public void Parse_RejectsShortLine() => Assert.False(ClientCrashReport.TryParse(Line("0xC0000005", "GTA5.exe"), out _));

    [Fact]
    public void Stats_GroupsSamePlace_AndIsBounded()
    {
        var stats = new ClientCrashStats();
        ClientCrashReport.TryParse(Line("0xC0000005", "GTA5.exe", "0x10", "1.0.6-beta", "1"), out var a);
        ClientCrashReport.TryParse(Line("0xC0000005", "FloVMP.asi", "0x20", "1.0.6-beta", "1"), out var b);
        for (var i = 0; i < 3; i++) stats.Add(a, i);
        stats.Add(b, 10);
        var top = stats.Top(5);
        Assert.Equal(2, top.Count);
        Assert.Equal(3, top[0].Count);
        Assert.Contains("GTA5.exe+0x10", top[0].Place);
        Assert.Equal(4, stats.Total);

        for (var i = 0; i < ClientCrashStats.MaxPlaces + 50; i++)
        {
            ClientCrashReport.TryParse(Line("0xC0000005", "m.dll", "0x" + i.ToString("X"), "1.0.6-beta", "1"), out var r);
            stats.Add(r, 100 + i);
        }
        Assert.True(stats.Top(10_000).Count <= ClientCrashStats.MaxPlaces);
    }
}
