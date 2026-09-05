using System.Net.Sockets;

namespace FloVMP.Launcher.Native.Services;

public record ServerStatusResult(bool Online, int Players, int MaxPlayers);

public static class ServerStatusService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(3) };

    public static async Task<ServerStatusResult> CheckAsync(string host, int port)
    {
        var urls = new[] { $"http://{host}:{port}/info", $"http://{host}/info" };
        foreach (var url in urls)
        {
            try
            {
                var response = await Http.GetStringAsync(url).ConfigureAwait(false);
                var players = ExtractInt(response, "\"players\"");
                var maxPlayers = ExtractInt(response, "\"maxPlayers\"");
                return new ServerStatusResult(true, players, maxPlayers > 0 ? maxPlayers : 128);
            }
            catch { }
        }
        return new ServerStatusResult(false, 0, 0);
    }

    private static int ExtractInt(string json, string key)
    {
        var idx = json.IndexOf(key, StringComparison.Ordinal);
        if (idx < 0) return 0;
        var colon = json.IndexOf(':', idx);
        if (colon < 0) return 0;
        var start = colon + 1;
        while (start < json.Length && (json[start] == ' ' || json[start] == '\t')) start++;
        var end = start;
        while (end < json.Length && char.IsDigit(json[end])) end++;
        return end > start && int.TryParse(json[start..end], out var n) ? n : 0;
    }
}
