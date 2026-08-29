using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace FloVMP.Launcher.Services.Compat;

public enum CompatVerdict
{
    /// <summary>Версия известна и поддерживается — запускаемся как есть (Вариант 1).</summary>
    Ok,
    /// <summary>Версия известна, но не проверена — пускаем с предупреждением.</summary>
    Untested,
    /// <summary>Версия известна и сломана — нужен Вариант 2 (подмена exe).</summary>
    NeedsFallback,
    /// <summary>Версия неизвестна (игра свежее манифеста) — предупредить, не пускать по умолчанию.</summary>
    Unknown,
    /// <summary>Манифест не загрузился — работаем вслепую.</summary>
    NoManifest,
}

public sealed record CompatResult(
    CompatVerdict Verdict,
    string Message,
    CompatEntry? Entry = null,
    CachedBuild? FallbackBuild = null);

/// <summary>
/// Сопоставляет установленную версию GTA5.exe с манифестом совместимости
/// и выдаёт вердикт: запускаться как есть, предупредить или уходить в
/// Вариант 2.
/// </summary>
public sealed class CompatService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _http;

    public CompatService(HttpClient? http = null)
    {
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
    }

    public async Task<CompatManifest?> FetchAsync(string source, CancellationToken ct = default)
    {
        try
        {
            string json;
            if (Uri.TryCreate(source, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
                json = await _http.GetStringAsync(uri, ct);
            else
                json = await File.ReadAllTextAsync(uri is { IsFile: true } ? uri.LocalPath : source, ct);

            return JsonSerializer.Deserialize<CompatManifest>(json, JsonOpts);
        }
        catch
        {
            return null;
        }
    }

    public async Task<CompatResult> EvaluateAsync(
        string manifestSource, GameVersion game, CancellationToken ct = default)
    {
        var manifest = await FetchAsync(manifestSource, ct);
        if (manifest is null)
            return new CompatResult(CompatVerdict.NoManifest,
                "манифест совместимости не загружен — запуск на свой риск");

        var entry = await MatchAsync(manifest, game, ct);
        if (entry is null)
            return new CompatResult(CompatVerdict.Unknown,
                $"версия GTA V ({game.FileVersion}, {game.Size} Б) не в манифесте — вероятно, игра обновилась. Обнови лаунчер/подожди патч совместимости.");

        return entry.Status switch
        {
            CompatStatus.Supported => new CompatResult(CompatVerdict.Ok,
                $"версия {entry.GtaFileVersion} поддерживается", entry),

            CompatStatus.Untested => new CompatResult(CompatVerdict.Untested,
                $"версия {entry.GtaFileVersion} известна, но не проверена: {entry.Note}", entry),

            CompatStatus.Broken => new CompatResult(CompatVerdict.NeedsFallback,
                $"версия {entry.GtaFileVersion} несовместима: {entry.Note}", entry,
                manifest.CachedBuilds.FirstOrDefault(b => b.Id == entry.FallbackBuildId)),

            _ => new CompatResult(CompatVerdict.Unknown, "статус версии неизвестен", entry),
        };
    }

    private static async Task<CompatEntry?> MatchAsync(
        CompatManifest manifest, GameVersion game, CancellationToken ct)
    {
        // SHA-256 is authoritative when a profile provides it. Calculate it
        // only for candidates that ask for it; normal version checks remain
        // cheap for manifests that contain only metadata.
        var hashCandidates = manifest.Versions
            .Where(v => !string.IsNullOrWhiteSpace(v.GtaSha256) &&
                        (v.Edition is null || v.Edition == game.Edition) &&
                        string.Equals(v.GtaFileVersion, game.FileVersion, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (hashCandidates.Count > 0)
        {
            var actualHash = await game.Sha256Async(ct);
            var byHash = hashCandidates.FirstOrDefault(v =>
                string.Equals(v.GtaSha256, actualHash, StringComparison.OrdinalIgnoreCase));
            if (byHash is not null) return byHash;

            // A profile with an exact hash must never silently downgrade to a
            // weaker version/size match after the hash disagrees.
            return null;
        }

        // 1) точное совпадение по размеру + версии
        var byKey = manifest.Versions.FirstOrDefault(v =>
            (v.Edition is null || v.Edition == game.Edition) &&
            v.GtaSize == game.Size &&
            string.Equals(v.GtaFileVersion, game.FileVersion, StringComparison.OrdinalIgnoreCase));
        if (byKey is not null) return byKey;

        // 2) только по FileVersion (размер мог чуть отличаться между сторами)
        return manifest.Versions.FirstOrDefault(v =>
            (v.Edition is null || v.Edition == game.Edition) &&
            !string.IsNullOrEmpty(v.GtaFileVersion) &&
            string.Equals(v.GtaFileVersion, game.FileVersion, StringComparison.OrdinalIgnoreCase));
    }
}
