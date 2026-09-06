using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FloVMP.Core.Licensing;

public sealed record RemoteCommand(int Id, string Command, string? Payload);

/// <summary>
/// Агент удалённого управления сервером (txAdmin-стиль).
/// Опрашивает облачный шлюз на наличие команд от владельца проекта и вызывает зарегистрированные обработчики.
/// </summary>
public class RemoteServerAgent : IDisposable
{
    private static readonly HttpClient DefaultHttp = new() { Timeout = TimeSpan.FromSeconds(5) };
    private readonly HttpClient _http;
    private readonly string _agentToken;
    private readonly string _commandApiUrl;
    private readonly int _pollIntervalSec;
    private CancellationTokenSource? _cts;
    private Task? _pollTask;

    public event Func<string?, Task<string>>? OnRestartRequested;
    public event Func<string?, Task<string>>? OnStopRequested;
    public event Func<string, Task<string>>? OnBroadcastRequested;
    public event Func<string, Task<string>>? OnResourceStartRequested;
    public event Func<string, Task<string>>? OnResourceStopRequested;
    public event Func<string, Task<string>>? OnResourceRestartRequested;
    public event Func<string, string?, Task<string>>? OnCustomCommandReceived;

    public RemoteServerAgent(
        string agentToken,
        string commandApiUrl = "http://localhost:3000/api/v1/agent/command",
        int pollIntervalSec = 10,
        HttpClient? http = null)
    {
        _agentToken = agentToken ?? throw new ArgumentNullException(nameof(agentToken));
        _commandApiUrl = commandApiUrl;
        _pollIntervalSec = Math.Max(3, pollIntervalSec);
        _http = http ?? DefaultHttp;
    }

    public void Start()
    {
        if (_pollTask != null) return;
        _cts = new CancellationTokenSource();
        _pollTask = Task.Run(() => PollLoopAsync(_cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _pollTask = null;
    }

    private async Task PollLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_pollIntervalSec), token);
                await PollAndExecuteAsync();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // Suppress background errors
            }
        }
    }

    public async Task<int> PollAndExecuteAsync()
    {
        try
        {
            var url = $"{_commandApiUrl}?token={Uri.EscapeDataString(_agentToken)}";
            var res = await _http.GetAsync(url);
            if (!res.IsSuccessStatusCode) return 0;

            var json = await res.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("commands", out var cmds) || cmds.ValueKind != JsonValueKind.Array)
            {
                return 0;
            }

            int executedCount = 0;
            foreach (var elem in cmds.EnumerateArray())
            {
                int cmdId = elem.GetProperty("id").GetInt32();
                string cmdName = elem.GetProperty("command").GetString() ?? "";
                string? payload = elem.TryGetProperty("payload", out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

                string status = "executed";
                string result = "OK";

                try
                {
                    switch (cmdName.ToLowerInvariant())
                    {
                        case "restart":
                            result = OnRestartRequested != null ? await OnRestartRequested(payload) : "Restart trigger received";
                            break;
                        case "stop":
                            result = OnStopRequested != null ? await OnStopRequested(payload) : "Stop trigger received";
                            break;
                        case "broadcast":
                            result = OnBroadcastRequested != null && payload != null ? await OnBroadcastRequested(payload) : "Broadcast sent";
                            break;
                        case "resource_start":
                            result = OnResourceStartRequested != null && payload != null ? await OnResourceStartRequested(payload) : $"Started resource {payload}";
                            break;
                        case "resource_stop":
                            result = OnResourceStopRequested != null && payload != null ? await OnResourceStopRequested(payload) : $"Stopped resource {payload}";
                            break;
                        case "resource_restart":
                            result = OnResourceRestartRequested != null && payload != null ? await OnResourceRestartRequested(payload) : $"Restarted resource {payload}";
                            break;
                        default:
                            result = OnCustomCommandReceived != null ? await OnCustomCommandReceived(cmdName, payload) : $"Command '{cmdName}' processed";
                            break;
                    }
                }
                catch (Exception ex)
                {
                    status = "failed";
                    result = ex.Message;
                }

                await ReportResultAsync(cmdId, status, result);
                executedCount++;
            }

            return executedCount;
        }
        catch
        {
            return 0;
        }
    }

    public async Task<bool> ReportResultAsync(int commandId, string status, string result)
    {
        try
        {
            var payload = new { commandId, status, result };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var req = new HttpRequestMessage(new HttpMethod("PATCH"), _commandApiUrl)
            {
                Content = content
            };
            var res = await _http.SendAsync(req);
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
