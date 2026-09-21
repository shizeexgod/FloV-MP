using System.Net.Sockets;
using System.Text;
using FloVMP.ServerHost;
using Xunit;

namespace FloVMP.Core.Tests;

public sealed class BridgeListenerTests
{
    [Fact]
    public async Task TwoLegacy3889SessionsExchangeState()
    {
        using var listener = new BridgeListener(0, _ => { });
        listener.Start();

        using var first = new TcpClient();
        using var second = new TcpClient();
        await first.ConnectAsync("127.0.0.1", listener.Port);
        await second.ConnectAsync("127.0.0.1", listener.Port);

        await using var firstStream = first.GetStream();
        await using var secondStream = second.GetStream();
        using var firstReader = new StreamReader(firstStream, Encoding.UTF8, false, 256, leaveOpen: true);
        using var secondReader = new StreamReader(secondStream, Encoding.UTF8, false, 256, leaveOpen: true);

        await WriteAsync(firstStream, "FLOVMP-BRIDGE/1 hello build=3889 version=1.0.3889.0 pid=1\n");
        Assert.StartsWith("FLOVMP-BRIDGE/1 WELCOME id=", await firstReader.ReadLineAsync());

        await WriteAsync(secondStream, "FLOVMP-BRIDGE/1 hello build=3889 version=1.0.3889.0 pid=2\n");
        Assert.StartsWith("FLOVMP-BRIDGE/1 WELCOME id=", await secondReader.ReadLineAsync());

        await WriteAsync(firstStream, "FLOVMP-BRIDGE/1 state x=10 y=20 z=30 heading=90\n");
        var received = await ReadUntilContainsAsync(secondReader, "state id=1 x=10 y=20 z=30 heading=90");
        Assert.Contains("FLOVMP-BRIDGE/1 state id=1 x=10 y=20 z=30 heading=90", received);

        await WriteAsync(secondStream, "FLOVMP-BRIDGE/1 heartbeat\n");
        Assert.Equal("FLOVMP-BRIDGE/1 heartbeat-ack", await ReadWithTimeoutAsync(secondReader));
    }

    private static async Task WriteAsync(NetworkStream stream, string message)
    {
        var bytes = Encoding.UTF8.GetBytes(message);
        await stream.WriteAsync(bytes);
        await stream.FlushAsync();
    }

    private static async Task<string> ReadWithTimeoutAsync(StreamReader reader)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        return (await reader.ReadLineAsync(timeout.Token)) ?? throw new EndOfStreamException();
    }

    private static async Task<string> ReadUntilContainsAsync(StreamReader reader, string expected)
    {
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var line = await ReadWithTimeoutAsync(reader);
            if (line.Contains(expected, StringComparison.Ordinal)) return line;
        }
        throw new Xunit.Sdk.XunitException($"Did not receive bridge line containing: {expected}");
    }
}
