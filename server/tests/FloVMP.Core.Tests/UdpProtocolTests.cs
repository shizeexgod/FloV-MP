using System.Net;
using FloVMP.Core.Native;
using Xunit;

namespace FloVMP.Core.Tests;

public sealed class UdpProtocolTests
{
    [Fact]
    public void PacketMatchesIndependentPythonVector()
    {
        Span<byte> buffer = stackalloc byte[UdpProtocol.MaxDatagramBytes];
        Assert.True(UdpProtocol.TryWrite(buffer, UdpProtocol.PacketType.Reliable,
            0x0102030405060708, 0x11223344, 0x55667788, 0x99aabbcc,
            new byte[] { 1, 2, 3 }, out var length));
        Assert.Equal("464c5633030700000102030405060708112233445566778899aabbcc00030000010203",
            Convert.ToHexStringLower(buffer[..length]));
        Assert.True(UdpProtocol.TryRead(buffer[..length], out var header, out var payload));
        Assert.Equal(UdpProtocol.PacketType.Reliable, header.Type);
        Assert.Equal(0x0102030405060708UL, header.ConnectionId);
        Assert.Equal(0x11223344U, header.Sequence);
        Assert.Equal(0x55667788U, header.Ack);
        Assert.Equal(0x99aabbccU, header.AckBits);
        Assert.Equal(new byte[] { 1, 2, 3 }, payload.ToArray());
    }

    [Fact]
    public void ParserRejectsMalformedAndOversizedDatagrams()
    {
        var good = Convert.FromHexString("464c5633030700000102030405060708112233445566778899aabbcc00030000010203");
        Assert.False(UdpProtocol.TryRead(good[..^1], out _, out _));
        Assert.False(UdpProtocol.TryRead(good.Concat(new byte[] { 0 }).ToArray(), out _, out _));
        foreach (var offset in new[] { 0, 4, 5, 6, 7, 30, 31 })
        {
            var bad = good.ToArray();
            bad[offset] = 0xff;
            Assert.False(UdpProtocol.TryRead(bad, out _, out _));
        }
        Assert.False(UdpProtocol.TryRead(new byte[UdpProtocol.MaxDatagramBytes + 1], out _, out _));
        Assert.False(UdpProtocol.TryWrite(new byte[UdpProtocol.MaxDatagramBytes + 1],
            UdpProtocol.PacketType.State, 0, 0, 0, 0,
            new byte[UdpProtocol.MaxPayloadBytes + 1], out _));
    }

    [Fact]
    public void CookieMatchesIndependentPythonVectorAndBindsEndpoint()
    {
        var secret = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
        var nonce = Enumerable.Range(0, 16).Select(i => (byte)i).ToArray();
        var ip = IPAddress.Parse("192.0.2.7");
        var now = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);
        var protector = new UdpCookieProtector(secret);
        Span<byte> cookie = stackalloc byte[UdpCookieProtector.CookieSize];
        Assert.True(protector.TryCreate(ip, 7798, nonce, now, cookie));
        Assert.Equal("000000000a21fe80bbbb190d0c33cb6c518f314d1879226c",
            Convert.ToHexStringLower(cookie));
        Assert.True(protector.Validate(ip, 7798, nonce, cookie, now));
        Assert.True(protector.Validate(IPAddress.Parse("::ffff:192.0.2.7"), 7798,
            nonce, cookie, now.AddSeconds(10)));
        Assert.False(protector.Validate(ip, 7799, nonce, cookie, now));
        Assert.False(protector.Validate(IPAddress.Parse("192.0.2.8"), 7798, nonce, cookie, now));
        nonce[0] ^= 1;
        Assert.False(protector.Validate(ip, 7798, nonce, cookie, now));
    }

    [Fact]
    public void CookieExpiresAndRejectsTampering()
    {
        var protector = UdpCookieProtector.CreateRandom();
        var nonce = new byte[UdpCookieProtector.NonceSize];
        var ip = IPAddress.Loopback;
        var now = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);
        var cookie = new byte[UdpCookieProtector.CookieSize];
        Assert.True(protector.TryCreate(ip, 7798, nonce, now, cookie));
        Assert.False(protector.Validate(ip, 7798, nonce, cookie, now.AddSeconds(20)));
        Assert.False(protector.Validate(ip, 7798, nonce, cookie, now.AddSeconds(-10)));
        cookie[^1] ^= 1;
        Assert.False(protector.Validate(ip, 7798, nonce, cookie, now));
        Assert.False(protector.TryCreate(ip, 7798, nonce[..^1], now, cookie));
    }
}
