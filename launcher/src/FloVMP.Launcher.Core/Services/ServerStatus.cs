using System.Net.Http;
using System.Text.Json;

namespace FloVMP.Launcher.Services;

public enum ServerState { Unknown, Online, Offline }

public sealed record ServerStatusResult(ServerState State, string Text, int? Players = null, int? MaxPlayers = null);

/// <summary>
/// Пинг нашего alt:V-сервера по HTTP. alt:V держит HTTP и игровой трафик
/// на одном порту. На «голый» GET сервер отвечает 500 — это всё равно
/// значит «жив». Если удастся распарсить JSON статуса — покажем онлайн.
/// </summary>
public static class ServerStatus
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(3) };

    public static async Task<ServerStatusResult> CheckAsync(string host, int port)
    {
        var baseUrl = $"http://{host}:{port}";

        // 1) пробуем эндпоинт статуса
        try
        {
            using var resp = await Http.GetAsync(baseUrl + "/status.json");
            if (resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                int? players = root.TryGetProperty("players", out var p) && p.TryGetInt32(out var pv) ? pv : null;
                int? max = root.TryGetProperty("maxPlayers", out var m) && m.TryGetInt32(out var mv) ? mv : null;
                var txt = players is not null && max is not null
                    ? $"онлайн · {players}/{max}"
                    : "онлайн";
                return new ServerStatusResult(ServerState.Online, txt, players, max);
            }
        }
        catch
        {
            // нет такого эндпоинта — не страшно, идём дальше
        }

        // 2) любой HTTP-ответ на корень = сокет слушает = сервер жив
        try
        {
            using var resp = await Http.GetAsync(baseUrl + "/");
            return new ServerStatusResult(ServerState.Online, $"онлайн (HTTP {(int)resp.StatusCode})");
        }
        catch (Exception ex)
        {
            return new ServerStatusResult(ServerState.Offline, "недоступен: " + ex.GetBaseException().Message);
        }
    }
}
