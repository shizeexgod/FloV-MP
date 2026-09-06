using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FloVMP.Core.Licensing;

public class TelemetryReporter : IDisposable
{
    private static readonly HttpClient DefaultHttp = new() { Timeout = TimeSpan.FromSeconds(3) };
    private readonly HttpClient _http;
    private readonly LicenseConfig _config;
    private CancellationTokenSource? _cts;
    private Task? _workerTask;

    public Func<int>? GetPlayerCount { get; set; }
    public Func<int>? GetMaxPlayers { get; set; }
    public Func<int>? GetTickRate { get; set; }
    public Func<long>? GetMemoryMb { get; set; }
    public Func<int>? GetFps { get; set; }

    public TelemetryReporter(LicenseConfig config, HttpClient? http = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _http = http ?? DefaultHttp;
    }

    public void Start()
    {
        if (_workerTask != null) return;
        _cts = new CancellationTokenSource();
        _workerTask = Task.Run(() => WorkerLoopAsync(_cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _workerTask = null;
    }

    private async Task WorkerLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Max(5, _config.HeartbeatIntervalSec)), token);
                await ReportTickAsync();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // Suppress background reporter errors to not impact game loop
            }
        }
    }

    public async Task<bool> ReportTickAsync(int? players = null, int? maxPlayers = null, int? tickRate = null, long? memoryMb = null, int? fps = null)
    {
        try
        {
            var pCount = players ?? GetPlayerCount?.Invoke() ?? 0;
            var maxP = maxPlayers ?? GetMaxPlayers?.Invoke() ?? 1500;
            var tr = tickRate ?? GetTickRate?.Invoke() ?? 60;
            var mem = memoryMb ?? GetMemoryMb?.Invoke() ?? (GC.GetTotalMemory(false) / (1024 * 1024));
            var currentFps = fps ?? GetFps?.Invoke() ?? 60;

            var payload = new
            {
                licenseKey = _config.LicenseKey,
                players = pCount,
                maxPlayers = maxP,
                tickRate = tr,
                memoryMb = mem,
                fps = currentFps
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var res = await _http.PostAsync(_config.TelemetryUrl, content);
            return res.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
