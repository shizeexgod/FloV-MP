using FloVMP.Connect;
using Xunit;

namespace FloVMP.Launcher.Tests;

/// <summary>
/// Генерация altv.toml клиента.
///
/// Почему это стоит тестов. Файл формируется конкатенацией строк, а ник и путь
/// к игре приходят снаружи — от игрока и из реестра. Одна кривая строка здесь
/// означает не «некрасиво», а «клиент не стартует вовсе»: alt:V падает на
/// разборе TOML ещё до подключения, и игрок видит только закрывшееся окно.
/// До этого файла у логики не было ни одного теста.
/// </summary>
public class AltvTomlTests : IDisposable
{
    private readonly string _dir;
    private readonly string _gtaDir;

    public AltvTomlTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "flovmp-toml", Guid.NewGuid().ToString("N"));
        _gtaDir = Path.Combine(_dir, "gta");
        Directory.CreateDirectory(_dir);
        Directory.CreateDirectory(_gtaDir);
    }

    public void Dispose()
    {
        foreach (var v in new[] { "FLOVMP_VOICE", "FLOVMP_VOICE_ACTIVATION", "FLOVMP_BRANCH" })
            Environment.SetEnvironmentVariable(v, null);
        try { Directory.Delete(_dir, true); } catch { /* временный каталог */ }
    }

    private string Write(string? nickname = null, bool debug = false, string? platform = null)
    {
        AltvToml.Write(_dir, _gtaDir, debug, platform, nickname);
        return File.ReadAllText(Path.Combine(_dir, "altv.toml"));
    }

    /// <summary>Значение ключа из строки вида <c>key = value</c>.</summary>
    private static string Value(string toml, string key)
    {
        foreach (var line in toml.Split('\n'))
        {
            var t = line.Trim();
            if (t.StartsWith(key + " ", StringComparison.Ordinal) || t.StartsWith(key + "=", StringComparison.Ordinal))
            {
                var idx = t.IndexOf('=');
                if (idx > 0) return t[(idx + 1)..].Trim();
            }
        }
        return "";
    }

    // ---------- ник: самый опасный вход ----------

    [Fact]
    public void Nickname_WithApostrophe_DoesNotBreakTheFile()
    {
        // Ник идёт в TOML literal-строку 'name'. Одинарная кавычка внутри
        // такой строки неэкранируема — ник вроде O'Brien сломал бы весь файл
        // и клиент не запустился бы вообще.
        var toml = Write("O'Brien");
        Assert.DoesNotContain("O'Brien", toml);
        Assert.Equal("'OBrien'", Value(toml, "name"));
    }

    [Fact]
    public void Nickname_WithNewline_CannotInjectAnotherKey()
    {
        // Перевод строки в нике позволил бы дописать в конфиг чужой ключ.
        var toml = Write("Игрок'\ngtapath = 'C:/evil");
        Assert.Equal(1, toml.Split('\n').Count(l => l.TrimStart().StartsWith("gtapath")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("'''")]
    [InlineData("!!!")]
    public void Nickname_EmptyOrFullyStripped_FallsBackToDefault(string? nickname)
    {
        Assert.Equal("'Player'", Value(Write(nickname), "name"));
    }

    [Fact]
    public void Nickname_IsLengthLimited()
    {
        var toml = Write(new string('A', 200));
        var name = Value(toml, "name").Trim('\'');
        Assert.True(name.Length <= 32, $"ник длиной {name.Length} — ограничение не сработало");
    }

    [Fact]
    public void Nickname_KeepsCyrillicAndSafePunctuation()
    {
        Assert.Equal("'Иван_Петров-2.0'", Value(Write("Иван_Петров-2.0"), "name"));
    }

    // ---------- голос ----------

    [Fact]
    public void Voice_IsEnabledByDefault()
    {
        // Раньше здесь было зашито false: сервер мог быть настроен идеально, а
        // игроки всё равно не слышали друг друга.
        Environment.SetEnvironmentVariable("FLOVMP_VOICE", null);
        var toml = Write("Player");
        Assert.Equal("true", Value(toml, "voiceEnabled"));
        Assert.Equal("true", Value(toml, "autoFindMic"));
    }

    [Fact]
    public void Voice_CanBeDisabledByEnv()
    {
        Environment.SetEnvironmentVariable("FLOVMP_VOICE", "0");
        var toml = Write("Player");
        Assert.Equal("false", Value(toml, "voiceEnabled"));
        Assert.Equal("false", Value(toml, "autoFindMic"));
    }

    [Fact]
    public void VoiceActivation_IsOffByDefault()
    {
        // Активация по уровню звука означает открытый микрофон у половины
        // сервера — включаться должна только осознанно.
        Environment.SetEnvironmentVariable("FLOVMP_VOICE_ACTIVATION", null);
        Assert.Equal("false", Value(Write("Player"), "voiceActivationEnabled"));
    }

    [Fact]
    public void VoiceActivation_RequiresVoiceItself()
    {
        // Активация без включённого голоса бессмысленна и не должна включаться.
        Environment.SetEnvironmentVariable("FLOVMP_VOICE", "0");
        Environment.SetEnvironmentVariable("FLOVMP_VOICE_ACTIVATION", "1");
        Assert.Equal("false", Value(Write("Player"), "voiceActivationEnabled"));
    }

    // ---------- ветка клиента ----------

    [Theory]
    [InlineData(null, "'release'")]
    [InlineData("", "'release'")]
    [InlineData("internal", "'release'")]   // невалидна: клиент падает 0x30,012
    [InlineData("нечто", "'release'")]
    [InlineData("dev", "'dev'")]
    [InlineData("RC", "'rc'")]
    public void Branch_OnlyAcceptsKnownValues(string? env, string expected)
    {
        Environment.SetEnvironmentVariable("FLOVMP_BRANCH", env);
        Assert.Equal(expected, Value(Write("Player"), "branch"));
    }

    // ---------- пути и платформа ----------

    [Fact]
    public void GtaPath_UsesForwardSlashes_AndNoTrailingSeparator()
    {
        AltvToml.Write(_dir, _gtaDir + "\\", false, null, "Player");
        var toml = File.ReadAllText(Path.Combine(_dir, "altv.toml"));
        var path = Value(toml, "gtapath").Trim('\'');
        Assert.DoesNotContain("\\", path);
        Assert.False(path.EndsWith("/"), "лишний разделитель в конце пути");
    }

    [Fact]
    public void PlatformOverride_IsRespected_AndLowercased()
    {
        Assert.Equal("'steam'", Value(Write("Player", platform: "STEAM"), "gtaPlatform"));
    }

    [Fact]
    public void BothFiles_AreWritten_AndIdentical()
    {
        // Клиент читает altv.toml, наш коннектор — flovmp.toml. Расхождение
        // между ними означало бы, что игра стартует с другими настройками.
        AltvToml.Write(_dir, _gtaDir, false, null, "Player");
        var a = File.ReadAllText(Path.Combine(_dir, "altv.toml"));
        var b = File.ReadAllText(Path.Combine(_dir, "flovmp.toml"));
        Assert.Equal(a, b);
    }

    [Fact]
    public void Debug_FlagIsReflected()
    {
        Assert.Equal("true", Value(Write("Player", debug: true), "debug"));
        Assert.Equal("false", Value(Write("Player", debug: false), "debug"));
    }

    [Fact]
    public void EveryLine_IsKeyValueOrEmpty()
    {
        // Грубая проверка формата: alt:V не прощает мусорных строк.
        foreach (var raw in Write("Игрок O'Hara").Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            Assert.Contains("=", line);
        }
    }

    // ---------- определение платформы ----------

    [Fact]
    public void DetectPlatform_Steam_ByApiDll()
    {
        File.WriteAllText(Path.Combine(_gtaDir, "steam_api64.dll"), "");
        Assert.Equal("steam", AltvToml.DetectPlatform(_gtaDir));
    }

    [Fact]
    public void DetectPlatform_Rgl_ByLauncherPair()
    {
        File.WriteAllText(Path.Combine(_gtaDir, "PlayGTAV.exe"), "");
        File.WriteAllText(Path.Combine(_gtaDir, "GTAVLauncher.exe"), "");
        Assert.Equal("rgl", AltvToml.DetectPlatform(_gtaDir));
    }

    [Fact]
    public void DetectPlatform_Egs_ByEosSdk()
    {
        File.WriteAllText(Path.Combine(_gtaDir, "EOSSDK-Win64-Shipping.dll"), "");
        Assert.Equal("egs", AltvToml.DetectPlatform(_gtaDir));
    }

    [Fact]
    public void DetectPlatform_MissingDirectory_FallsBackToEgs()
    {
        Assert.Equal("egs", AltvToml.DetectPlatform(Path.Combine(_dir, "нет-такой-папки")));
    }
}
