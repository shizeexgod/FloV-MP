using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace FloVMP.Launcher.Services.Cdn;

/// <summary>
/// Загрузка манифеста. Принимает http(s)-URL, локальный путь к файлу или
/// file://. Если у манифеста не задан BaseUrl — подставляет каталог, из
/// которого манифест взят (чтобы относительные ссылки резолвились).
/// </summary>
public sealed class ManifestClient
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _http;

    public ManifestClient(HttpClient? http = null)
    {
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public async Task<Manifest> FetchAsync(string source, CancellationToken ct = default)
    {
        string json;
        string implicitBase;

        if (Uri.TryCreate(source, UriKind.Absolute, out var uri) && (uri.Scheme is "http" or "https"))
        {
            json = await _http.GetStringAsync(uri, ct);
            implicitBase = new Uri(uri, ".").ToString();
        }
        else
        {
            var path = uri is { IsFile: true } ? uri.LocalPath : source;
            json = await File.ReadAllTextAsync(path, ct);
            implicitBase = Path.GetDirectoryName(Path.GetFullPath(path))!;
        }

        var manifest = JsonSerializer.Deserialize<Manifest>(json, JsonOpts)
                       ?? throw new InvalidDataException("манифест не разобран");

        if (string.IsNullOrWhiteSpace(manifest.BaseUrl))
            manifest.BaseUrl = implicitBase;

        return manifest;
    }
}
