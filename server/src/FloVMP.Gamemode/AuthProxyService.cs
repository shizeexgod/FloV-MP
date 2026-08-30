using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using AltV.Net;

namespace FloVMP.Gamemode;

/// <summary>
/// Автономный сервис проверки лицензий на порту 7799.
/// altv-server.exe перенаправляет запросы cloudauth на http://127.0.0.1:7799/.
/// Сервис подтверждает токен игрока [true], исключая зависимость от облака alt:V.
/// </summary>
public sealed class AuthProxyService : IDisposable
{
    private readonly HttpListener _listener;
    private readonly CancellationTokenSource _cts = new();

    public AuthProxyService(int port = 7799)
    {
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
    }

    public void Start()
    {
        try
        {
            _listener.Start();
            Alt.Log("[FloV:MP] Auth proxy started on http://127.0.0.1:7799");
            Task.Run(LoopAsync);
        }
        catch (Exception ex)
        {
            Alt.Log($"[FloV:MP] Auth proxy notice: {ex.Message}");
        }
    }

    private async Task LoopAsync()
    {
        while (!_cts.IsCancellationRequested && _listener.IsListening)
        {
            try
            {
                var ctx = await _listener.GetContextAsync();
                _ = Task.Run(() => Handle(ctx));
            }
            catch
            {
                break;
            }
        }
    }

    private void Handle(HttpListenerContext ctx)
    {
        try
        {
            using var reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8);
            var body = reader.ReadToEnd();
            var count = 1;
            if (body.Contains("clientTokenHashes"))
            {
                var match = Regex.Match(body, @"""clientTokenHashes""\s*:\s*\[(.*?)\]");
                if (match.Success && !string.IsNullOrWhiteSpace(match.Groups[1].Value))
                {
                    var items = match.Groups[1].Value.Trim();
                    count = items.Length > 0 ? items.Split(',').Length : 0;
                }
            }

            var responseJson = count > 0 
                ? "[" + string.Join(", ", Enumerable.Repeat("true", count)) + "]"
                : "[]";

            var bytes = Encoding.UTF8.GetBytes(responseJson);
            ctx.Response.ContentType = "application/json";
            ctx.Response.StatusCode = 200;
            ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
            ctx.Response.Close();
        }
        catch { }
    }

    public void Dispose()
    {
        _cts.Cancel();
        try { _listener.Stop(); _listener.Close(); } catch { }
        _cts.Dispose();
    }
}
