using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using FloVMP.ServerHost;

// FloVMP-Server.exe — запуск сервера на Windows одним файлом.
//
//   * при первом запуске создаёт config\flovmp.env, server\server.toml, voice\voice.toml;
//   * запускает голосовой сервер (скрыто) и игровой сервер в этом же окне —
//     команды сервера (setadmin, kick, ...) вводятся прямо сюда;
//   * Ctrl+C или закрытие окна останавливают оба процесса;
//   * если сервер остановился сам — показывает причину и предлагает перезапуск.

try
{
    Console.OutputEncoding = Encoding.UTF8;
    Console.InputEncoding = Encoding.UTF8;
}
catch { /* вывод без UTF-8 тоже читаем */ }

var root = Path.GetFullPath(AppContext.BaseDirectory);
Console.Title = "FloV:MP Server";

if (!File.Exists(Path.Combine(root, "server", "flovmp-server.exe")))
{
    Error(@"Не найден server\flovmp-server.exe. Распакуйте архив полностью и запускайте FloVMP-Server.exe из его корня.");
    return Pause(2);
}

// Сервер не запускается из папки с кириллицей в пути — работаем через
// латинскую ссылку на ту же папку (см. AsciiPath).
var runRoot = AsciiPath.Resolve(root, out var pathError);
if (runRoot is null)
{
    Error("Путь к папке сервера содержит не-латинские символы, а обойти это не удалось: " + pathError);
    Error(@"Перенесите папку сервера в путь из латинских букв, например C:\FloVMP\server.");
    return Pause(2);
}
if (!string.Equals(runRoot, Path.TrimEndingDirectorySeparator(root), StringComparison.OrdinalIgnoreCase))
    Info($"Путь содержит не-латинские символы — сервер запускается через ссылку {runRoot}");

var serverDir = Path.Combine(runRoot, "server");
var voiceDir = Path.Combine(runRoot, "voice");
var serverExe = Path.Combine(serverDir, "flovmp-server.exe");
var voiceExe = Path.Combine(voiceDir, "altv-voice-server.exe");

try
{
    var init = ServerConfig.Initialize(root);
    if (init.CreatedEnv) Ok("Создан config\\flovmp.env (ваши настройки: база данных, владелец сервера, лицензия).");
    if (init.CreatedToml) Ok("Созданы server\\server.toml и voice\\voice.toml (название, порт, слоты, голос).");
    if (Workspace.CreateGamemodeIfMissing(root))
        Ok(@"Создана папка gamemode — здесь пишется ваш сервер (C# + JavaScript). Описание: gamemode\README.md, сборка: gamemode\build.cmd.");
    if (Workspace.EnsureResourceEnabled(root, "gamemode"))
        Ok(@"Собранный ресурс gamemode подключён в server\server.toml.");
}
catch (Exception ex)
{
    Error("Не удалось создать настройки: " + ex.Message);
    return Pause(2);
}

var serverToml = Path.Combine(serverDir, "server.toml");
var env = new Dictionary<string, string>();
var name = "FloV:MP Server";
var port = 7788;
string? voicePort = null, voiceHost = null;

// Настройки читаются перед каждым запуском: при перезапуске клавишей R
// правки flovmp.env и server.toml применяются без закрытия окна.
void ReadSettings()
{
    env = ServerConfig.LoadEnv(root);
    name = ServerConfig.ReadToml(serverToml, "name") ?? "FloV:MP Server";
    port = int.TryParse(ServerConfig.ReadToml(serverToml, "port"), out var p) ? p : 7788;
    voicePort = ServerConfig.ReadToml(serverToml, "externalPublicPort", "voice");
    voiceHost = ServerConfig.ReadToml(serverToml, "externalPublicHost", "voice");
}
ReadSettings();

Console.Title = $"FloV:MP Server — {name} :{port}";

using var job = new JobObject();
using var dailyBackup = new DailyBackup(root, () => env, text => Info(text));
var stopRequested = false;
Process? server = null;
Process? voice = null;

Console.CancelKeyPress += (_, e) =>
{
    // Ctrl+C получает и игровой сервер (одно окно) — он завершается штатно.
    // Сами не выходим, пока он не закончит, иначе оборвём запись логов.
    e.Cancel = true;
    if (stopRequested) return;
    stopRequested = true;
    Info("Останавливаю сервер...");
};

while (true)
{
    ReadSettings();
    Workspace.EnsureResourceEnabled(root, "gamemode");
    Console.Title = $"FloV:MP Server — {name} :{port}";
    if (!PreflightOk()) return Pause(1);

    PrintBanner();
    voice = StartVoice();
    server = StartServer();
    if (server is null)
    {
        StopVoice();
        return Pause(3);
    }

    // Ждём завершения игрового сервера. Если остановку запросили, а он не
    // вышел за 20 секунд — завершаем принудительно.
    while (!server.WaitForExit(500))
    {
        if (stopRequested && !server.WaitForExit(20_000))
        {
            Info("Сервер не остановился за 20 секунд — завершаю принудительно.");
            TryKill(server);
            break;
        }
    }

    var code = SafeExitCode(server);
    StopVoice();

    if (stopRequested)
    {
        Ok("Сервер остановлен.");
        return 0;
    }

    Console.WriteLine();
    Error($"Сервер остановился (код {code}). Подробности: {Rel(Path.Combine(serverDir, "server.log"))}");
    Console.WriteLine("  R — запустить снова, любая другая клавиша — выход.");
    if (Console.ReadKey(true).Key != ConsoleKey.R) return code == 0 ? 1 : code;
    Console.WriteLine();
}

// ---------------------------------------------------------------------------

bool PreflightOk()
{
    var running = Process.GetProcessesByName("flovmp-server").FirstOrDefault(pr => SamePath(pr, serverExe));
    if (running is not null)
    {
        Error($"Сервер из этой папки уже запущен (PID {running.Id}). Закройте его окно или завершите процесс.");
        return false;
    }

    // Голосовой сервер, оставшийся от прошлого запуска (например, после сбоя),
    // держит порт — без его остановки новый не поднимется.
    foreach (var stale in Process.GetProcessesByName("altv-voice-server").Where(pr => SamePath(pr, voiceExe)))
    {
        Info($"Завершаю оставшийся от прошлого запуска голосовой сервер (PID {stale.Id}).");
        TryKill(stale);
    }

    // Занятость — по списку слушающих портов системы: движок открывает сокет
    // так, что пробная привязка в Windows проходит даже при работающем сервере.
    if (IsPortListening(port, udp: true) || IsPortListening(port, udp: false))
    {
        Error($@"Порт {port} занят другой программой (возможно, запущен другой сервер). Смените port в server\server.toml или закройте её.");
        return false;
    }

    // Голосовые порты (UDP): занятый порт не мешает игре, но голос молча не поднимется.
    foreach (var (key, label) in new[] { ("externalPublicPort", "для игроков"), ("externalPort", "внутренний") })
    {
        if (!int.TryParse(ServerConfig.ReadToml(serverToml, key, "voice"), out var vp) || vp <= 0 || vp > 65535) continue;
        if (IsPortListening(vp, udp: true))
            Info($@"Голосовой порт {vp} ({label}) занят другой программой — голосовой чат не заработает. Смените порт в server\server.toml и voice\voice.toml.");
    }
    return true;
}

void PrintBanner()
{
    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Magenta;
    Console.WriteLine("  FloV:MP Server");
    Console.ResetColor();
    Console.WriteLine($"  Название:      {name}");
    Console.WriteLine($"  Подключение:   127.0.0.1:{port}  (игроки из интернета — ваш внешний IP:{port})");
    if (voicePort is not null)
    {
        Console.WriteLine($"  Голос:         {voiceHost}:{voicePort}");
        if (voiceHost is "127.0.0.1" or "localhost")
            Console.WriteLine(@"                 (голос слышно только игрокам с этого ПК; для интернета — ваш внешний IP в externalPublicHost, server\server.toml)");
    }

    var dbPassword = env.GetValueOrDefault("FLOVMP_DB_PASSWORD", "");
    Console.WriteLine(string.IsNullOrEmpty(dbPassword) && !env.ContainsKey("FLOVMP_DB_CONNECTION")
        ? "  База данных:   не настроена — данные хранятся в файлах (config\\flovmp.env)"
        : $"  База данных:   {env.GetValueOrDefault("FLOVMP_DB_NAME", "flovmp_server")} на {env.GetValueOrDefault("FLOVMP_DB_HOST", "127.0.0.1")}");

    var owner = env.GetValueOrDefault("FLOVMP_OWNER_SC", "");
    var token = env.GetValueOrDefault("FLOVMP_SETUP_TOKEN", "");
    if (!string.IsNullOrEmpty(owner))
        Console.WriteLine($"  Владелец:      SocialClubId {owner}");
    else if (!string.IsNullOrEmpty(token))
        Console.WriteLine($"  Владелец:      в игре /claimowner {token} (пока нет ни одного администратора)");

    Console.WriteLine($"  Лог:           {Rel(Path.Combine(serverDir, "server.log"))}");
    Console.WriteLine("  Остановка:     Ctrl+C или закрыть окно");
    Console.WriteLine();
}

Process? StartVoice()
{
    if (!File.Exists(voiceExe))
    {
        Info("Голосовой сервер не найден (voice\\altv-voice-server.exe) — голосовой чат работать не будет.");
        return null;
    }
    if (!File.Exists(Path.Combine(voiceDir, "voice.toml")))
    {
        Info("Нет voice\\voice.toml — голосовой чат работать не будет.");
        return null;
    }
    try
    {
        var psi = new ProcessStartInfo(voiceExe)
        {
            WorkingDirectory = voiceDir,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        var proc = Process.Start(psi)!;
        job.Add(proc);
        // Вывод голосового сервера дублируется в voice\voice.log им самим;
        // здесь только вычитываем поток, чтобы он не заполнился и не встал.
        proc.OutputDataReceived += (_, _) => { };
        proc.ErrorDataReceived += (_, _) => { };
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();
        Ok("Голосовой сервер запущен.");
        return proc;
    }
    catch (Exception ex)
    {
        Info("Голосовой сервер не запустился: " + ex.Message);
        return null;
    }
}

Process? StartServer()
{
    try
    {
        // Вывод и ввод не перенаправляются: сервер пишет в это же окно и
        // читает из него команды консоли.
        var psi = new ProcessStartInfo(serverExe)
        {
            WorkingDirectory = serverDir,
            UseShellExecute = false,
        };
        foreach (var (k, v) in env) psi.Environment[k] = v;
        var proc = Process.Start(psi)!;
        job.Add(proc);
        return proc;
    }
    catch (Exception ex)
    {
        Error("Игровой сервер не запустился: " + ex.Message);
        return null;
    }
}

void StopVoice()
{
    if (voice is null) return;
    TryKill(voice);
    voice.Dispose();
    voice = null;
}

static bool IsPortListening(int port, bool udp)
{
    try
    {
        var props = System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties();
        var endpoints = udp ? props.GetActiveUdpListeners() : props.GetActiveTcpListeners();
        return endpoints.Any(e => e.Port == port);
    }
    catch { return false; }
}

static void TryKill(Process proc)
{
    try { if (!proc.HasExited) { proc.Kill(true); proc.WaitForExit(5000); } }
    catch { /* уже завершён */ }
}

static int SafeExitCode(Process proc)
{
    try { return proc.ExitCode; } catch { return -1; }
}

bool SamePath(Process proc, string path)
{
    try
    {
        var file = proc.MainModule?.FileName;
        if (file is null) return false;
        // Процесс мог быть запущен и по реальному пути, и через ссылку.
        var real = Path.Combine(root, Path.GetRelativePath(runRoot, path));
        return string.Equals(file, path, StringComparison.OrdinalIgnoreCase)
            || string.Equals(file, real, StringComparison.OrdinalIgnoreCase);
    }
    catch { return false; }
}

string Rel(string path) => Path.GetRelativePath(runRoot, path);

static int Pause(int code)
{
    Console.WriteLine();
    Console.WriteLine("Нажмите любую клавишу, чтобы закрыть окно...");
    try { Console.ReadKey(true); } catch { }
    return code;
}

static void Ok(string text) => Write(ConsoleColor.Green, "[FloV:MP] " + text);
static void Info(string text) => Write(ConsoleColor.Yellow, "[FloV:MP] " + text);
static void Error(string text) => Write(ConsoleColor.Red, "[FloV:MP] " + text);

static void Write(ConsoleColor color, string text)
{
    Console.ForegroundColor = color;
    Console.WriteLine(text);
    Console.ResetColor();
}
