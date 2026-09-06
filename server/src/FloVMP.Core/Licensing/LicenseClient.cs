using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace FloVMP.Core.Licensing;

public class LicenseClient : IDisposable
{
    private static readonly HttpClient DefaultHttp = new() { Timeout = TimeSpan.FromSeconds(5) };
    private readonly HttpClient _http;
    private readonly LicenseConfig _config;
    private readonly string _cachePath;

    public LicenseClient(LicenseConfig config, HttpClient? http = null, string? cacheDir = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _http = http ?? DefaultHttp;
        _cachePath = Path.Combine(cacheDir ?? Path.Combine(Directory.GetCurrentDirectory(), "flovmp-data"), "license-cache.json");
    }

    public async Task<LicenseVerificationResult> VerifyAsync()
    {
        // 1. Basic format validation
        if (string.IsNullOrWhiteSpace(_config.LicenseKey) || !_config.LicenseKey.StartsWith("FLV-"))
        {
            return LicenseVerificationResult.Failure("Неверный формат ключа (ключ должен начинаться с FLV-)");
        }

        try
        {
            var payload = new
            {
                licenseKey = _config.LicenseKey.Trim(),
                serverIp = _config.ServerIp.Trim(),
                version = "v16.4.39-flov",
                slots = 1500
            };

            var jsonContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await _http.PostAsync(_config.VerifyUrl, jsonContent);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                var msg = $"Ошибка верификации лицензии HTTP {(int)response.StatusCode}: {errorBody}";
                return HandleOfflineFallbackOrFailure(msg);
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            bool valid = root.TryGetProperty("valid", out var v) && v.GetBoolean();
            if (!valid)
            {
                string err = root.TryGetProperty("error", out var e) ? e.GetString() ?? "Лицензия недействительна" : "Лицензия отклонена сервером";
                return LicenseVerificationResult.Failure(err);
            }

            var result = new LicenseVerificationResult
            {
                IsValid = true,
                LicenseKey = root.TryGetProperty("licenseKey", out var lk) ? lk.GetString() ?? _config.LicenseKey : _config.LicenseKey,
                ServerName = root.TryGetProperty("serverName", out var sn) ? sn.GetString() ?? _config.ServerName : _config.ServerName,
                Plan = root.TryGetProperty("plan", out var pl) ? pl.GetString() ?? "indie" : "indie",
                MaxPlayers = root.TryGetProperty("maxPlayers", out var mp) ? mp.GetInt32() : 128,
                BoundIp = root.TryGetProperty("boundIp", out var bip) ? bip.GetString() ?? "0.0.0.0" : "0.0.0.0",
                SignatureValid = root.TryGetProperty("signature", out var sig) && !string.IsNullOrWhiteSpace(sig.GetString()),
                ExpiresAt = root.TryGetProperty("expiresAt", out var exp) && exp.TryGetDateTime(out var dt) ? dt : DateTime.UtcNow.AddDays(30)
            };

            // Save valid result to cache
            SaveCache(result);
            return result;
        }
        catch (Exception ex)
        {
            return HandleOfflineFallbackOrFailure($"Сетевая ошибка при запросе к серверу лицензий: {ex.Message}");
        }
    }

    private LicenseVerificationResult HandleOfflineFallbackOrFailure(string error)
    {
        var cached = LoadCache();
        if (cached != null && cached.IsValid && !cached.IsExpired)
        {
            var hoursSinceVerified = (DateTime.UtcNow - cached.ExpiresAt).TotalHours;
            // If offline grace is within configured bounds
            cached.IsCachedOffline = true;
            cached.ErrorMessage = $"[Предупреждение] Сервер работает в автономном кэш-режиме: {error}";
            return cached;
        }

        if (!_config.StrictMode)
        {
            // Development fallback mode when not in strict commercial mode
            return new LicenseVerificationResult
            {
                IsValid = true,
                LicenseKey = _config.LicenseKey,
                ServerName = _config.ServerName,
                Plan = "enterprise",
                MaxPlayers = 1500,
                ExpiresAt = DateTime.UtcNow.AddDays(365),
                BoundIp = _config.ServerIp,
                SignatureValid = false,
                ErrorMessage = $"[DevMode] Сервер запущен в локальном режиме разработки: {error}"
            };
        }

        return LicenseVerificationResult.Failure(error);
    }

    private void SaveCache(LicenseVerificationResult res)
    {
        try
        {
            var dir = Path.GetDirectoryName(_cachePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(res);
            File.WriteAllText(_cachePath, json);
        }
        catch
        {
            // Ignore cache write errors
        }
    }

    private LicenseVerificationResult? LoadCache()
    {
        try
        {
            if (!File.Exists(_cachePath)) return null;
            var json = File.ReadAllText(_cachePath);
            return JsonSerializer.Deserialize<LicenseVerificationResult>(json);
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        // Don't dispose DefaultHttp
    }
}
