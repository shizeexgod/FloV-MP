using FloVMP.ServerHost;
using Xunit;

namespace FloVMP.Core.Tests;

/// <summary>
/// Настройки, которые FloVMP-Server.exe создаёт и правит у владельца сервера:
/// ошибка здесь ломает установку молча (сервер без ресурса, без базы, без голоса).
/// </summary>
public sealed class ServerHostConfigTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "flovmp-host-tests", Guid.NewGuid().ToString("N"));

    public ServerHostConfigTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "server", "resources"));
        Directory.CreateDirectory(Path.Combine(_root, "voice"));
        Directory.CreateDirectory(Path.Combine(_root, "config"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, true); } catch { }
    }

    private const string Toml = "name = \"Test\"\nport = 7788\n\nresources = [\n    \"flovmp-starter\",\n    \"flovmp-client\",\n]\n\n[voice]\nexternalSecret = 1\nexternalPublicHost = \"127.0.0.1\"\nexternalPublicPort = 7895\n";

    private string ServerToml => Path.Combine(_root, "server", "server.toml");

    private void BuildGamemode()
    {
        var dir = Path.Combine(_root, "server", "resources", "gamemode");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "resource.toml"), "type = \"csharp\"\n");
    }

    [Fact]
    public void EnsureResourceEnabled_AddsOnceBeforeClosingBracket()
    {
        File.WriteAllText(ServerToml, Toml);
        BuildGamemode();

        Assert.True(Workspace.EnsureResourceEnabled(_root, "gamemode"));
        Assert.False(Workspace.EnsureResourceEnabled(_root, "gamemode")); // повтор ничего не меняет

        var text = File.ReadAllText(ServerToml);
        Assert.Contains("    \"flovmp-client\",\n    \"gamemode\",\n]", text);
        Assert.Equal(1, CountOf(text, "\"gamemode\""));
        // ключи до первой таблицы: секция [voice] не задета
        Assert.True(text.IndexOf("\"gamemode\"", StringComparison.Ordinal) < text.IndexOf("[voice]", StringComparison.Ordinal));
    }

    [Fact]
    public void EnsureResourceEnabled_KeepsCrlfLineEndings()
    {
        File.WriteAllText(ServerToml, Toml.Replace("\n", "\r\n"));
        BuildGamemode();

        Assert.True(Workspace.EnsureResourceEnabled(_root, "gamemode"));
        Assert.Contains("    \"gamemode\",\r\n]", File.ReadAllText(ServerToml));
    }

    [Fact]
    public void EnsureResourceEnabled_SkipsUnbuiltResource()
    {
        // Ресурс, которого нет на диске, в список не добавляется: сервер ругался бы
        // на отсутствующий ресурс при каждом старте.
        File.WriteAllText(ServerToml, Toml);
        Assert.False(Workspace.EnsureResourceEnabled(_root, "gamemode"));
        Assert.DoesNotContain("gamemode", File.ReadAllText(ServerToml));
    }

    [Fact]
    public void CreateGamemodeIfMissing_CopiesTemplateOnlyOnce()
    {
        var template = Path.Combine(_root, "sdk", "template", "src");
        Directory.CreateDirectory(template);
        File.WriteAllText(Path.Combine(template, "GamemodeResource.cs"), "// шаблон");

        Assert.True(Workspace.CreateGamemodeIfMissing(_root));
        var owned = Path.Combine(_root, "gamemode", "src", "GamemodeResource.cs");
        File.WriteAllText(owned, "// код владельца");

        Assert.False(Workspace.CreateGamemodeIfMissing(_root));
        Assert.Equal("// код владельца", File.ReadAllText(owned)); // не перезаписан
    }

    [Fact]
    public void ReadToml_ReadsTopLevelAndSectionKeys()
    {
        File.WriteAllText(ServerToml, Toml);
        Assert.Equal("Test", ServerConfig.ReadToml(ServerToml, "name"));
        Assert.Equal("7788", ServerConfig.ReadToml(ServerToml, "port"));
        Assert.Equal("127.0.0.1", ServerConfig.ReadToml(ServerToml, "externalPublicHost", "voice"));
        Assert.Null(ServerConfig.ReadToml(ServerToml, "externalPublicHost")); // не на верхнем уровне
    }

    [Fact]
    public void LoadEnv_ParsesQuotesAndIgnoresComments()
    {
        File.WriteAllText(Path.Combine(_root, "config", "flovmp.env"),
            "# комментарий\nFLOVMP_DB_PASSWORD=\"p@ss word;$x\"\nFLOVMP_OWNER_SC=123\n  \nnot a pair\nFLOVMP_NAME='RP Сервер'\n");
        var env = ServerConfig.LoadEnv(_root);
        Assert.Equal("p@ss word;$x", env["FLOVMP_DB_PASSWORD"]);
        Assert.Equal("123", env["FLOVMP_OWNER_SC"]);
        Assert.Equal("RP Сервер", env["FLOVMP_NAME"]);
        Assert.Equal(3, env.Count);
    }

    [Fact]
    public void SetEnvValue_ReplacesExistingOrAppends()
    {
        var text = "A=1\nFLOVMP_SETUP_TOKEN=\nB=2\n";
        var updated = ServerConfig.SetEnvValue(text, "FLOVMP_SETUP_TOKEN", "FLV-1");
        Assert.Contains("FLOVMP_SETUP_TOKEN=FLV-1\n", updated);
        Assert.Equal(1, CountOf(updated, "FLOVMP_SETUP_TOKEN="));

        var appended = ServerConfig.SetEnvValue("A=1", "NEW", "x");
        Assert.EndsWith("NEW=x" + Environment.NewLine, appended);
    }

    [Fact]
    public void Initialize_CreatesMatchingVoiceSecretAndKeepsExistingFiles()
    {
        File.WriteAllText(Path.Combine(_root, "server", "server.toml.example"),
            "name = \"__FLOVMP_NAME__\"\nport = __FLOVMP_PORT__\nplayers = __FLOVMP_PLAYERS__\n[voice]\nexternalSecret = __FLOVMP_VOICE_SECRET__\nexternalPort = __FLOVMP_VOICE_PORT__\nexternalPublicHost = \"__FLOVMP_VOICE_PUBLIC_HOST__\"\nexternalPublicPort = __FLOVMP_VOICE_PUBLIC_PORT__\n");
        File.WriteAllText(Path.Combine(_root, "voice", "voice.toml.example"),
            "secret = __FLOVMP_VOICE_SECRET__\nport = __FLOVMP_VOICE_PORT__\nplayerPort = __FLOVMP_VOICE_PUBLIC_PORT__\n");
        File.WriteAllText(Path.Combine(_root, "config", "flovmp.env.example"), "FLOVMP_SETUP_TOKEN=\n");

        var first = ServerConfig.Initialize(_root);
        Assert.True(first.CreatedEnv && first.CreatedToml);

        var serverText = File.ReadAllText(ServerToml);
        var voiceText = File.ReadAllText(Path.Combine(_root, "voice", "voice.toml"));
        Assert.DoesNotContain("__FLOVMP_", serverText + voiceText);
        var secret = ServerConfig.ReadToml(ServerToml, "externalSecret", "voice");
        Assert.Equal(secret, ServerConfig.ReadToml(Path.Combine(_root, "voice", "voice.toml"), "secret"));
        Assert.Matches("^FLV-[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{4}$",
            ServerConfig.LoadEnv(_root)["FLOVMP_SETUP_TOKEN"]);

        // Владелец поменял файл — повторный запуск его не трогает.
        File.WriteAllText(ServerToml, serverText.Replace("7788", "7790"));
        var second = ServerConfig.Initialize(_root);
        Assert.False(second.CreatedEnv || second.CreatedToml);
        Assert.Contains("7790", File.ReadAllText(ServerToml));
    }

    [Fact]
    public void Initialize_RecreatedVoiceTomlReusesExistingSecret()
    {
        // Удалил только voice.toml — новый обязан получить секрет из server.toml,
        // иначе голосовой сервер не соединится с игровым.
        File.WriteAllText(Path.Combine(_root, "voice", "voice.toml.example"), "secret = __FLOVMP_VOICE_SECRET__\n");
        File.WriteAllText(ServerToml, "name = \"x\"\n[voice]\nexternalSecret = 424242\n");
        ServerConfig.Initialize(_root);
        Assert.Equal("424242", ServerConfig.ReadToml(Path.Combine(_root, "voice", "voice.toml"), "secret"));
    }

    private static int CountOf(string text, string value)
    {
        int count = 0, i = 0;
        while ((i = text.IndexOf(value, i, StringComparison.Ordinal)) >= 0) { count++; i += value.Length; }
        return count;
    }
}

public sealed class DailyBackupTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "flovmp-backup-tests", Guid.NewGuid().ToString("N"));

    public DailyBackupTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, true); } catch { }
    }

    [Fact]
    public void IsDue_OnlyWithDatabaseAndOldOrMissingBackup()
    {
        var now = DateTime.UtcNow;
        var noDb = new Dictionary<string, string>();
        var withDb = new Dictionary<string, string> { ["FLOVMP_DB_PASSWORD"] = "x", ["FLOVMP_DB_NAME"] = "rp" };

        Assert.False(FloVMP.ServerHost.DailyBackup.IsDue(_root, noDb, now));   // база не настроена
        Assert.True(FloVMP.ServerHost.DailyBackup.IsDue(_root, withDb, now));  // копий нет

        var dir = Directory.CreateDirectory(Path.Combine(_root, "backups")).FullName;
        var file = Path.Combine(dir, "db_rp_2026-01-01_00-00-00.sql");
        File.WriteAllText(file, "--");
        File.SetLastWriteTimeUtc(file, now.AddHours(-2));
        Assert.False(FloVMP.ServerHost.DailyBackup.IsDue(_root, withDb, now)); // свежая

        File.SetLastWriteTimeUtc(file, now.AddHours(-30));
        Assert.True(FloVMP.ServerHost.DailyBackup.IsDue(_root, withDb, now));  // старше суток

        // копия другой базы не считается
        var other = Path.Combine(dir, "db_other_2026-01-01_00-00-00.sql");
        File.WriteAllText(other, "--");
        Assert.True(FloVMP.ServerHost.DailyBackup.IsDue(_root, withDb, now));
    }
}

public sealed class PendingGamemodeTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "flovmp-pending-tests", Guid.NewGuid().ToString("N"));

    public PendingGamemodeTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, true); } catch { }
    }

    [Fact]
    public void Pending_build_replaces_running_one_on_next_start()
    {
        // Пока сервер работал, build.cmd собрал новую версию в gamemode/.pending.
        var live = Directory.CreateDirectory(Path.Combine(_root, "server", "resources", "gamemode")).FullName;
        File.WriteAllText(Path.Combine(live, "Gamemode.dll"), "старая");
        var pending = Directory.CreateDirectory(FloVMP.ServerHost.Workspace.PendingDir(_root)).FullName;
        File.WriteAllText(Path.Combine(pending, "Gamemode.dll"), "новая");
        Directory.CreateDirectory(Path.Combine(pending, "client"));
        File.WriteAllText(Path.Combine(pending, "client", "index.js"), "// клиент");

        Assert.True(FloVMP.ServerHost.Workspace.ApplyPendingGamemode(_root));

        Assert.Equal("новая", File.ReadAllText(Path.Combine(live, "Gamemode.dll")));
        Assert.True(File.Exists(Path.Combine(live, "client", "index.js")));
        Assert.False(Directory.Exists(pending)); // применённая сборка не подставляется повторно
    }

    [Fact]
    public void Nothing_pending_changes_nothing()
    {
        Assert.False(FloVMP.ServerHost.Workspace.ApplyPendingGamemode(_root));

        // Папка без Gamemode.dll — незаконченная сборка, её не подставляем.
        Directory.CreateDirectory(FloVMP.ServerHost.Workspace.PendingDir(_root));
        Assert.False(FloVMP.ServerHost.Workspace.ApplyPendingGamemode(_root));
    }
}
