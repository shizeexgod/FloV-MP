using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FloVMP.Core.Licensing;
using Xunit;

namespace FloVMP.Core.Tests;

public class RemoteServerAgentTests
{
    private class MockHttpMessageHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, HttpResponseMessage>? OnSend { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var res = OnSend != null ? OnSend(request) : new HttpResponseMessage(HttpStatusCode.OK);
            return Task.FromResult(res);
        }
    }

    [Fact]
    public async Task PollAndExecuteAsync_ExecutesRestartAndBroadcast()
    {
        var handler = new MockHttpMessageHandler();
        bool restartCalled = false;
        string? broadcastMsg = null;

        handler.OnSend = (req) =>
        {
            if (req.Method == HttpMethod.Get)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(@"{
                        ""success"": true,
                        ""count"": 2,
                        ""commands"": [
                            { ""id"": 10, ""command"": ""restart"", ""payload"": ""Nightly maintenance"" },
                            { ""id"": 11, ""command"": ""broadcast"", ""payload"": ""Server reboot in 5 min"" }
                        ]
                    }")
                };
            }

            if (req.Method == new HttpMethod("PATCH"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(@"{ ""success"": true }")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        };

        var client = new HttpClient(handler);
        using var agent = new RemoteServerAgent("test_token_123", "http://fake-api/commands", 5, client);

        agent.OnRestartRequested += (reason) =>
        {
            restartCalled = true;
            Assert.Equal("Nightly maintenance", reason);
            return Task.FromResult("Rebooting");
        };

        agent.OnBroadcastRequested += (msg) =>
        {
            broadcastMsg = msg;
            return Task.FromResult("Sent");
        };

        int count = await agent.PollAndExecuteAsync();

        Assert.Equal(2, count);
        Assert.True(restartCalled);
        Assert.Equal("Server reboot in 5 min", broadcastMsg);
    }

    [Fact]
    public async Task PollAndExecuteAsync_HandlesHttpErrorGracefully()
    {
        var handler = new MockHttpMessageHandler
        {
            OnSend = (_) => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        };

        var client = new HttpClient(handler);
        using var agent = new RemoteServerAgent("test_token_123", "http://fake-api/commands", 5, client);

        int count = await agent.PollAndExecuteAsync();
        Assert.Equal(0, count);
    }
}
