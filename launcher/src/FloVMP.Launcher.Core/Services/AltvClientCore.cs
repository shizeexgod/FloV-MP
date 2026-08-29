using System.Diagnostics;
using System.IO;

namespace FloVMP.Launcher.Services;

public sealed record CoreValidation(bool Ok, IReadOnlyList<string> Missing)
{
    public string Summary => Ok
        ? "ядро клиента: файлы на месте"
        : $"ядро клиента: не хватает {Missing.Count} файл(ов)";
}

/// <summary>
/// Работа с ядром клиента alt:V: проверка комплектности папки и сборка
/// команды прямого подключения.
///
/// ВАЖНО (разведка 2026-08-27): бэкап клиента в C:\ViMP backup неполный —
/// отсутствуют CEF-локали, V8-снапшоты, crypto-DLL, legacy.dll и т.д.
/// Пока полный набор файлов клиента 16.4.39 не собран, живой запуск игры
/// невозможен, но лаунчер уже умеет всё, кроме этого.
/// </summary>
public static class AltvClientCore
{
    /// <summary>
    /// Минимально необходимые файлы, без которых altv.exe не поднимет игру.
    /// Список выведен из update.json клиента 16.4.39 (ветка release).
    /// </summary>
    public static readonly string[] RequiredFiles =
    {
        "altv.exe",
        "altv-client.dll",
        "altv-launcher-patcher.dll",
        @"libs\legacy.dll",             // поддержка legacy-GTA5.exe (наш случай — Epic legacy)
        @"libs\chrome_elf.dll",
        @"libs\libce2.dll",
        @"libs\libcrypto-3-x64.dll",
        @"libs\libssl-3-x64.dll",
        @"libs\d3dcompiler_47.dll",
        @"libs\resources.pak",
        @"libs\icudtl.dat",
        @"libs\snapshot_blob.bin",
        @"libs\v8_context_snapshot.bin",
        @"libs\vulkan-1.dll",
        @"cef\altv-webengine.exe",
        @"cef\resources.pak",
        @"cef\locales\en-US.pak",
    };

    public static CoreValidation Validate(string? coreDir)
    {
        if (string.IsNullOrWhiteSpace(coreDir) || !Directory.Exists(coreDir))
        {
            return new CoreValidation(false, new[] { "(папка ядра не задана / не существует)" });
        }

        var missing = RequiredFiles
            .Where(rel => !File.Exists(Path.Combine(coreDir, rel)))
            .ToList();

        return new CoreValidation(missing.Count == 0, missing);
    }

    public static string BuildConnectUrl(string host, int port, string? nickname, string? password)
    {
        var url = $"altv://connect/{host}:{port}";
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(password)) query.Add("password=" + Uri.EscapeDataString(password));
        if (!string.IsNullOrWhiteSpace(nickname)) query.Add("nickname=" + Uri.EscapeDataString(nickname));
        if (query.Count > 0) url += "?" + string.Join("&", query);
        return url;
    }

    /// <summary>
    /// Аргументы запуска altv.exe для прямого подключения без launcher-UI
    /// и без обновления с (мёртвого) CDN alt:V.
    /// </summary>
    public static string BuildArgs(string connectUrl, string branch, bool allowMultiple, string? gameExecutable = null)
    {
        var args = new List<string>
        {
            $"-connecturl \"{connectUrl}\"",
            "-noupdate",
            $"-branch {branch}",
        };
        if (!string.IsNullOrWhiteSpace(gameExecutable))
            args.Add($"-gtaexe \"{gameExecutable}\"");
        if (allowMultiple)
        {
            args.Add("-skipprocesscheck");
            args.Add("-skipprocessconfirmation");
        }
        return string.Join(" ", args);
    }

    public static Process Launch(string coreDir, string args)
    {
        var exe = Path.Combine(coreDir, "altv.exe");
        var psi = new ProcessStartInfo(exe, args)
        {
            WorkingDirectory = coreDir,
            UseShellExecute = false,
        };
        return Process.Start(psi) ?? throw new InvalidOperationException("Process.Start вернул null");
    }

    // --- Глобальный %LOCALAPPDATA%\altv\altv.toml -----------------------

    public static string GlobalAltvTomlPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "altv", "altv.toml");

    public static string? ReadAltvTomlValue(string key)
    {
        if (!File.Exists(GlobalAltvTomlPath)) return null;
        foreach (var line in File.ReadAllLines(GlobalAltvTomlPath))
        {
            var t = line.Trim();
            if (t.StartsWith(key + " ", StringComparison.Ordinal) || t.StartsWith(key + "=", StringComparison.Ordinal))
            {
                var eq = t.IndexOf('=');
                if (eq < 0) continue;
                return t[(eq + 1)..].Trim().Trim('\'', '"');
            }
        }
        return null;
    }

    /// <summary>
    /// Минимальная правка глобального altv.toml: выставить gtapath / branch
    /// (и name, если задан), не трогая остальные ключи. Делает .bak.
    /// Вызывается только если пользователь явно включил синхронизацию.
    /// </summary>
    public static void SyncAltvToml(string gtaPath, string branch, string? nickname)
    {
        var path = GlobalAltvTomlPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var lines = File.Exists(path) ? File.ReadAllLines(path).ToList() : new List<string>();
        if (File.Exists(path)) File.Copy(path, path + ".bak", overwrite: true);

        void SetKey(string key, string value)
        {
            var rendered = $"{key} = '{value}'";
            var idx = lines.FindIndex(l =>
            {
                var t = l.TrimStart();
                return t.StartsWith(key + " ", StringComparison.Ordinal) || t.StartsWith(key + "=", StringComparison.Ordinal);
            });
            if (idx >= 0) lines[idx] = rendered;
            else lines.Add(rendered);
        }

        SetKey("gtapath", gtaPath);
        SetKey("branch", branch);
        if (!string.IsNullOrWhiteSpace(nickname)) SetKey("name", nickname);

        File.WriteAllLines(path, lines);
    }
}
