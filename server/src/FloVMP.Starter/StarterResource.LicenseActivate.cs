using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AltV.Net;

namespace FloVMP.Starter;

/// <summary>
/// Активация ключа из консоли сервера: <c>license activate FLV-...</c>.
///
/// Скачивает license.flv с портала, проверяет подпись, водяной знак и ключ
/// ДО записи на диск (чужой или испорченный файл не заменит рабочий),
/// сохраняет ключ в config/flovmp.env и применяет лицензию без перезапуска.
/// </summary>
public partial class StarterResource
{
    private static readonly Regex LicenseKeyShape = new(
        @"^FLV-(?:[A-Z0-9]{4}-[A-Z0-9]{4}-[A-Z0-9]{4}|[A-Z0-9]{8}-[A-Z0-9]{8}-[A-Z0-9]{8}-[A-Z0-9]{8})$",
        RegexOptions.CultureInvariant);

    private int _licenseActivating;
    private int _licenseActivated;

    /// <summary>Корень установки: папка над server/ (там license.flv и config/).</summary>
    private static string InstallRoot()
    {
        var cwd = Directory.GetCurrentDirectory();
        return Directory.GetParent(cwd)?.FullName ?? cwd;
    }

    private long _nextAutoActivateMs;

    private void ActivateLicense(string rawKey)
    {
        var key = (rawKey ?? "").Trim().ToUpperInvariant();
        if (!LicenseKeyShape.IsMatch(key))
        {
            Alt.LogWarning("[FloV:MP] [License] неверный формат ключа. Пример: license activate FLV-XXXXXXXX-XXXXXXXX-XXXXXXXX-XXXXXXXX");
            return;
        }
        if (Interlocked.Exchange(ref _licenseActivating, 1) != 0)
        {
            Alt.LogWarning("[FloV:MP] [License] активация уже идёт — дождитесь результата");
            return;
        }
        Alt.Log($"[FloV:MP] [License] активация {key}: запрос у сервера лицензий {FloVMP.Core.Licensing.LicenseConfig.FromEnvironment().AuthorityUrl}...");
        _ = Task.Run(async () =>
        {
            try { await ActivateLicenseAsync(key); }
            catch (Exception ex) { Alt.LogWarning($"[FloV:MP] [License] активация не выполнена: {ex.Message}"); }
            finally { Interlocked.Exchange(ref _licenseActivating, 0); }
        });
    }

    private async Task ActivateLicenseAsync(string key)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        var url = FloVMP.Core.Licensing.LicenseConfig.FromEnvironment().LicenseDownloadUrl(key);
        using var response = await http.GetAsync(url, _licenseRemoteCts.Token);
        var body = await response.Content.ReadAsStringAsync(_licenseRemoteCts.Token);
        if (!response.IsSuccessStatusCode)
        {
            // Сервер лицензий объясняет отказ в поле reason: не найден, приостановлен, лимит серверов...
            string reason;
            try { using var doc = System.Text.Json.JsonDocument.Parse(body); reason = doc.RootElement.GetProperty("reason").GetString() ?? ""; }
            catch (Exception) { reason = ""; }
            throw new InvalidOperationException($"сервер лицензий ответил {(int)response.StatusCode}" + (reason.Length > 0 ? ": " + reason : ""));
        }
        if (body.Length > 64 * 1024) throw new InvalidOperationException("ответ слишком большой — это не файл лицензии");

        // Проверяем то, что пришло, до записи: подпись портала, водяной знак, ключ, срок.
        var status = FloVMP.Core.Licensing.LicenseFile.EvaluateContent(body, DateTime.UtcNow, key);
        if (status.State != FloVMP.Core.Licensing.LicenseState.Valid)
            throw new InvalidOperationException(status.Message);

        var root = InstallRoot();
        var target = WriteLicenseFile(body);

        SaveEnvValue(Path.Combine(root, "config", "flovmp.env"), "FLOVMP_LICENSE_KEY", key);
        Environment.SetEnvironmentVariable("FLOVMP_LICENSE_KEY", key);
        Alt.Log($"[FloV:MP] [License] файл сохранён: {target}; ключ записан в config/flovmp.env");
        Interlocked.Exchange(ref _licenseActivated, 1); // применит главный поток в OnTick
    }

    /// <summary>Записать проверенный license.flv (атомарно, прежний — в .bak).</summary>
    private static string WriteLicenseFile(string content)
    {
        var target = FloVMP.Core.Licensing.LicenseFile.Locate() ??
                     Path.Combine(InstallRoot(), FloVMP.Core.Licensing.LicenseFile.FileName);
        var tmp = target + ".tmp";
        File.WriteAllText(tmp, content, new UTF8Encoding(false));
        if (File.Exists(target)) File.Copy(target, target + ".bak", overwrite: true);
        File.Move(tmp, target, overwrite: true);
        return target;
    }

    /// <summary>
    /// Сервер лицензий прислал license.flv новее нашего (продление, смена тарифа):
    /// заменить, если он подписан сервером лицензий и выдан на наш ключ.
    /// </summary>
    private static void RefreshLicenseFile(string? flv, string key)
    {
        if (string.IsNullOrWhiteSpace(flv) || flv.Length > 64 * 1024) return;
        var current = FloVMP.Core.Licensing.LicenseFile.Locate();
        if (current is not null && File.Exists(current) && File.ReadAllText(current).Trim() == flv.Trim()) return;
        var status = FloVMP.Core.Licensing.LicenseFile.EvaluateContent(flv, DateTime.UtcNow, key);
        if (status.State != FloVMP.Core.Licensing.LicenseState.Valid) return;
        try
        {
            WriteLicenseFile(flv);
            Alt.Log($"[FloV:MP] [License] получен обновлённый файл лицензии: {status.Message}");
        }
        catch (Exception ex) { Alt.LogWarning($"[FloV:MP] [License] не удалось обновить license.flv: {ex.Message}"); }
    }

    /// <summary>
    /// Ключ задан (установщик записал его в config/flovmp.env), а файла лицензии
    /// нет — активировать самим, повторяя раз в 10 минут.
    /// </summary>
    private void TryAutoActivate(long nowMs)
    {
        if (nowMs < _nextAutoActivateMs || _license.State != FloVMP.Core.Licensing.LicenseState.Missing) return;
        _nextAutoActivateMs = nowMs + 10 * 60 * 1000;
        var key = Environment.GetEnvironmentVariable("FLOVMP_LICENSE_KEY");
        if (string.IsNullOrWhiteSpace(key)) return;
        ActivateLicense(key);
    }

    /// <summary>КЛЮЧ=значение в env-файле: заменить строку или дописать, остальное не трогать.</summary>
    private static void SaveEnvValue(string path, string name, string value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var lines = File.Exists(path) ? new System.Collections.Generic.List<string>(File.ReadAllLines(path)) : new();
        var found = false;
        for (var i = 0; i < lines.Count; i++)
        {
            if (!lines[i].TrimStart().StartsWith(name + "=", StringComparison.Ordinal)) continue;
            lines[i] = name + "=" + value;
            found = true;
        }
        if (!found) lines.Add(name + "=" + value);
        var tmp = path + ".tmp";
        File.WriteAllLines(tmp, lines, new UTF8Encoding(false));
        File.Move(tmp, path, overwrite: true);
    }

    /// <summary>Главный поток: лицензия активирована — применить и подтвердить у портала.</summary>
    private void ApplyActivatedLicense()
    {
        if (Interlocked.Exchange(ref _licenseActivated, 0) == 0) return;
        _licenseRemoteResult = null;
        CheckLicense(logAlways: true);
        StartRemoteLicenseCheck();
    }
}
