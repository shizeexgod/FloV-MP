using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FloVMP.Launcher.Native.Models;
using FloVMP.Launcher.Native.Services;

namespace FloVMP.Launcher.Native;

/// <summary>
/// Нативный мост между Electron-оболочкой лаунчера и Windows-специфичной логикой
/// (реестр, поиск GTA V, запуск через FloVMP.Connect). Протокол — NDJSON по stdio:
/// на каждую строку запроса {"id":N,"cmd":"...","...":...} пишется ровно одна
/// строка ответа {"id":N,"ok":true,"result":...} или {"id":N,"ok":false,"error":"..."}.
/// Electron спавнит этот процесс один раз и держит его живым на всё время работы.
/// </summary>
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // NDJSON-протокол с Electron — строго UTF-8 в обе стороны. Без этого на
        // русской Windows stdin/stdout берут OEM-кодировку (CP866) и любой
        // кириллический ник/путь превращается в мусор при первом же round-trip
        // (ник «Игрок» → «╤В╨е╨╕…» в settings.json).
        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        Console.InputEncoding = utf8;
        Console.OutputEncoding = utf8;

        var stdout = Console.Out;
        string? line;
        while ((line = Console.In.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            JsonNode? request;
            try { request = JsonNode.Parse(line); }
            catch { WriteError(stdout, null, "bad-json"); continue; }

            var id = request?["id"]?.GetValue<int?>();
            var cmd = request?["cmd"]?.GetValue<string>();

            try
            {
                var result = Dispatch(cmd, request);
                WriteResult(stdout, id, result);
            }
            catch (Exception ex)
            {
                WriteError(stdout, id, ex.Message);
            }
        }
    }

    private static object? Dispatch(string? cmd, JsonNode? req) => cmd switch
    {
        "getSettings" => SettingsService.Load(),

        "saveSettings" => SaveSettings(req),

        "detectGta" => DetectGta(),

        "validateGta" => ValidateGta(req?["path"]?.GetValue<string>() ?? ""),

        "browseFolder" => BrowseFolder(),

        "serverStatus" => ServerStatusService
            .CheckAsync(req?["host"]?.GetValue<string>() ?? "188.127.229.224", req?["port"]?.GetValue<int>() ?? 7788)
            .GetAwaiter().GetResult(),

        "play" => PlayService.Launch(
            req?["gtaPath"]?.GetValue<string>() ?? "",
            req?["host"]?.GetValue<string>() ?? "188.127.229.224",
            req?["port"]?.GetValue<int>() ?? 7788,
            req?["nickname"]?.GetValue<string>() ?? "Игрок"),

        "deviceInfo" => DeviceInfoService.Collect(),

        "detectGpu" => GpuDetectionService.Detect(),

        "deployUpscaler" => UpscalerDeploymentService.Deploy(
            req?["gtaPath"]?.GetValue<string>() ?? "",
            SettingsService.Load()),

        "cleanupUpscaler" => new { success = UpscalerDeploymentService.Cleanup(req?["gtaPath"]?.GetValue<string>() ?? "") },

        "ping" => new { pong = true },

        _ => throw new InvalidOperationException($"unknown command: {cmd}"),
    };

    private static object SaveSettings(JsonNode? req)
    {
        var data = req?["data"];
        var settings = data != null
            ? JsonSerializer.Deserialize<LauncherSettings>(data.ToJsonString()) ?? new LauncherSettings()
            : new LauncherSettings();
        SettingsService.Save(settings);
        return settings;
    }

    private static object? DetectGta()
    {
        var found = GtaLocatorService.TryLocate();
        if (found == null) return null;
        return new
        {
            path = found.Value.Path,
            source = found.Value.Source,
            edition = GtaLocatorService.DetectVersion(found.Value.Path),
        };
    }

    private static object ValidateGta(string path)
    {
        var valid = GtaLocatorService.IsValidGtaFolder(path);
        return new { valid, edition = valid ? GtaLocatorService.DetectVersion(path) : null };
    }

    private static string? BrowseFolder()
    {
        using var dlg = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Папка с GTA5.exe / GTA5_Enhanced.exe",
        };
        return dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK ? dlg.SelectedPath : null;
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static void WriteResult(TextWriter stdout, int? id, object? result)
    {
        var payload = JsonSerializer.Serialize(new { id, ok = true, result }, JsonOpts);
        stdout.WriteLine(payload);
        stdout.Flush();
    }

    private static void WriteError(TextWriter stdout, int? id, string error)
    {
        var payload = JsonSerializer.Serialize(new { id, ok = false, error }, JsonOpts);
        stdout.WriteLine(payload);
        stdout.Flush();
    }
}
