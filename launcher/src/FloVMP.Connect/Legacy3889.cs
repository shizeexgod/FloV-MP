using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

namespace FloVMP.Connect;

/// <summary>
/// Запуск FloV:MP на GTA V Legacy 1.0.3889.0.
///
/// Клиент alt:V 16.4.39 работает только с b3521, поэтому для b3889 у
/// платформы собственный клиент — FloVMP.asi на ScriptHookV. Коннектор:
///   1) проверяет версию игры;
///   2) проверяет ScriptHookV и ASI-загрузчик (по флагу --install-scripthook
///      сам скачивает их с официального сайта и сверяет контрольную сумму);
///   3) кладёт/обновляет FloVMP.asi в папке игры;
///   4) оставляет запрос на подключение (connect.txt, действует 10 минут);
///   5) запускает игру в сюжетном режиме — клиент сам заходит на сервер.
/// Файлы игры (exe, rpf) не меняются.
/// </summary>
public static class Legacy3889
{
    public const string GameVersion = "1.0.3889.0";
    public const string AsiName = "FloVMP.asi";
    public const string ScriptHookUrl = "http://www.dev-c.com/files/ScriptHookV_3889.0_1158.13.zip";
    public const string ScriptHookPage = "http://www.dev-c.com/gtav/scripthookv/";

    // Контрольные суммы официальных архивов ScriptHookV, с которыми клиент проверен.
    private static readonly HashSet<string> KnownScriptHookZips = new(StringComparer.OrdinalIgnoreCase)
    {
        "b64c97c3353906f14621e7e9511e4aec2a7d436ecc21ed124d3816585e2e6188", // v3889.0/1158.13
    };

    public static bool IsLegacy3889(string gtaDir)
    {
        var exe = Path.Combine(gtaDir, "GTA5.exe");
        if (!File.Exists(exe)) return false;
        try { return FileVersionInfo.GetVersionInfo(exe).FileVersion?.Trim() == GameVersion; }
        catch { return false; }
    }

    /// <summary>Версия ScriptHookV в папке игры поддерживает b3889 (3889.x и новее).</summary>
    public static bool HasScriptHook(string gtaDir, out string detail)
    {
        var dll = Path.Combine(gtaDir, "ScriptHookV.dll");
        if (!File.Exists(dll)) { detail = "ScriptHookV.dll не установлен"; return false; }
        var version = FileVersionInfo.GetVersionInfo(dll).FileVersion?.Trim() ?? "";
        var major = int.TryParse(version.Split('.')[0], out var m) ? m : 0;
        if (major < 3889) { detail = $"ScriptHookV {version} не поддерживает 1.0.3889.0 — обновите"; return false; }
        var hasLoader = File.Exists(Path.Combine(gtaDir, "dinput8.dll")) || File.Exists(Path.Combine(gtaDir, "xinput1_4.dll")) ||
                        File.Exists(Path.Combine(gtaDir, "version.dll")) || File.Exists(Path.Combine(gtaDir, "dsound.dll"));
        if (!hasLoader) { detail = "нет ASI-загрузчика (dinput8.dll из комплекта ScriptHookV)"; return false; }
        detail = "ScriptHookV " + version;
        return true;
    }

    /// <summary>Скачать официальный ScriptHookV и поставить ScriptHookV.dll + dinput8.dll.</summary>
    public static async Task<string?> InstallScriptHookAsync(string gtaDir)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        // Сайт отвечает 406 без заголовков обычного браузера.
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0 Safari/537.36");
        http.DefaultRequestHeaders.Referrer = new Uri(ScriptHookPage);
        byte[] zip;
        try { zip = await http.GetByteArrayAsync(ScriptHookUrl); }
        catch (Exception ex) { return "не удалось скачать ScriptHookV: " + ex.Message; }

        var sha = Convert.ToHexString(SHA256.HashData(zip));
        if (!KnownScriptHookZips.Contains(sha))
            return $"архив ScriptHookV не совпал с проверенным (SHA-256 {sha}). Установите вручную с {ScriptHookPage}";

        using var archive = new ZipArchive(new MemoryStream(zip));
        foreach (var name in new[] { "ScriptHookV.dll", "dinput8.dll" })
        {
            var entry = archive.Entries.FirstOrDefault(e => e.FullName.Replace('\\', '/').EndsWith("bin/" + name, StringComparison.OrdinalIgnoreCase));
            if (entry is null) return $"в архиве ScriptHookV нет {name}";
            var target = Path.Combine(gtaDir, name);
            var tmp = target + ".flovmp-tmp";
            entry.ExtractToFile(tmp, overwrite: true);
            File.Move(tmp, target, overwrite: true);
        }
        return null;
    }

    /// <summary>Положить FloVMP.asi из комплекта в папку игры, если там другой или его нет.</summary>
    public static string? EnsureClient(string gtaDir, out bool updated)
    {
        updated = false;
        var source = new[]
        {
            Path.Combine(AppContext.BaseDirectory, AsiName),
            Path.Combine(AppContext.BaseDirectory, "client-b3889", AsiName),
        }.FirstOrDefault(File.Exists);
        var target = Path.Combine(gtaDir, AsiName);
        if (source is null)
            return File.Exists(target) ? null : $"рядом с коннектором нет {AsiName}";
        if (File.Exists(target) && Hash(target) == Hash(source)) return null;
        try
        {
            var tmp = target + ".flovmp-tmp";
            File.Copy(source, tmp, overwrite: true);
            File.Move(tmp, target, overwrite: true);
            updated = true;
            return null;
        }
        catch (IOException) when (File.Exists(target))
        {
            // Файл занят запущенной игрой — работаем с установленной версией.
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return $"нет прав на запись в папку игры ({gtaDir}). Запустите от имени администратора один раз.";
        }
    }

    /// <summary>Запрос клиенту: к какому серверу подключиться после загрузки игры.</summary>
    public static void WriteConnectRequest(string address, string? name)
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FloVMP");
        Directory.CreateDirectory(dir);
        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var text = $"address={address}\nname={(name ?? "").Replace("\n", " ").Replace("\r", " ")}\ncreated={created}\n";
        File.WriteAllText(Path.Combine(dir, "connect.txt"), text, new UTF8Encoding(false));
    }

    /// <summary>"1.2.3.4:7788" → адрес шлюза клиентов b3889 (порт игры + 10, если не указан --native-port).</summary>
    public static string NativeAddress(string connect, int? nativePort)
    {
        var host = connect;
        var port = 7788;
        var colon = connect.LastIndexOf(':');
        if (colon > 0 && connect.IndexOf(':') == colon && int.TryParse(connect[(colon + 1)..], out var p))
        {
            host = connect[..colon];
            port = p;
        }
        return $"{host}:{nativePort ?? port + 10}";
    }

    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    private static void Fail(string message)
    {
        Console.Error.WriteLine("[b3889] " + message);
        try { MessageBox.Show(message, "FloV:MP", MessageBoxButtons.OK, MessageBoxIcon.Warning); } catch { }
    }

    public static async Task<int> RunAsync(string connect, string gtaDir, string? nickname, string? gameArgs,
                                           bool installScriptHook, int? nativePort, bool noLaunch)
    {
        if (!IsLegacy3889(gtaDir))
        {
            Fail($"В папке {gtaDir} нет GTA V Legacy {GameVersion}.");
            return 2;
        }

        if (!HasScriptHook(gtaDir, out var shv))
        {
            if (!installScriptHook)
            {
                Fail($"Для FloV:MP на GTA {GameVersion} нужен ScriptHookV: {shv}.\n\n" +
                     $"Скачайте его с {ScriptHookPage} и скопируйте ScriptHookV.dll и dinput8.dll в папку игры,\n" +
                     "или запустите коннектор с флагом --install-scripthook.");
                return 5;
            }
            Console.WriteLine("[b3889] Устанавливаю ScriptHookV с официального сайта...");
            var error = await InstallScriptHookAsync(gtaDir);
            if (error is not null) { Fail(error); return 5; }
            if (!HasScriptHook(gtaDir, out shv)) { Fail(shv); return 5; }
        }
        Console.WriteLine("[b3889] " + shv);

        var clientError = EnsureClient(gtaDir, out var updated);
        if (clientError is not null) { Fail(clientError); return 6; }
        Console.WriteLine(updated ? "[b3889] FloVMP.asi обновлён в папке игры." : "[b3889] FloVMP.asi на месте.");

        var address = NativeAddress(connect, nativePort);
        WriteConnectRequest(address, nickname);
        Console.WriteLine($"[b3889] Запрос на подключение: {address}");
        if (noLaunch) return 0;

        if (Process.GetProcessesByName("GTA5").Length > 0)
        {
            // Игра уже запущена: клиент подключится по F9 (адрес подставлен).
            Fail("GTA V уже запущена. Нажмите F9 в игре, чтобы подключиться к серверу.");
            return 0;
        }

        // Сюжетный режим без BattlEye — официальный параметр Rockstar для игры с модами;
        // GTA Online при этом недоступна, файлы игры не меняются.
        var args = string.IsNullOrWhiteSpace(gameArgs) ? "-nobattleye" : gameArgs;
        if (!args.Contains("-nobattleye", StringComparison.OrdinalIgnoreCase)) args += " -nobattleye";
        try
        {
            using var game = Process.Start(new ProcessStartInfo(Path.Combine(gtaDir, "GTA5.exe"), args)
            {
                WorkingDirectory = gtaDir,
                UseShellExecute = false,
            });
            if (game is null) { Fail("Не удалось запустить GTA5.exe."); return 3; }
            Console.WriteLine($"[b3889] GTA V запущена (PID {game.Id}). Клиент подключится после загрузки сюжетного режима.");
            await game.WaitForExitAsync();
            return 0;
        }
        catch (Exception ex)
        {
            Fail("Не удалось запустить GTA5.exe: " + ex.Message);
            return 3;
        }
    }
}
