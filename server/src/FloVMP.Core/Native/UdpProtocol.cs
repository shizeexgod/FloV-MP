using System.Buffers.Binary;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace FloVMP.Core.Native;

/// <summary>FLOV/3 wire format. This is a codec only; it does not open a UDP socket.</summary>
public static class UdpProtocol
{
    public const byte Version = 3;
    public const int HeaderSize = 32;
    public const int MaxDatagramBytes = 1200;
    public const int MaxPayloadBytes = MaxDatagramBytes - HeaderSize;

    public enum PacketType : byte
    {
        Hello = 1, Cookie = 2, Connect = 3, Challenge = 4,
        Auth = 5, Accept = 6, Reliable = 7, State = 8, Ack = 9,
    }

    public readonly record struct Header(PacketType Type, ulong ConnectionId,
        uint Sequence, uint Ack, uint AckBits, ushort PayloadLength);

    public static bool TryRead(ReadOnlySpan<byte> packet, out Header header, out ReadOnlySpan<byte> payload)
    {
        header = default;
        payload = default;
        if (packet.Length is < HeaderSize or > MaxDatagramBytes ||
            !packet[..4].SequenceEqual("FLV3"u8) || packet[4] != Version ||
            packet[5] is < 1 or > 9 || packet[6] != 0 || packet[7] != 0 ||
            packet[30] != 0 || packet[31] != 0) return false;

        var length = BinaryPrimitives.ReadUInt16BigEndian(packet[28..]);
        if (length > MaxPayloadBytes || packet.Length != HeaderSize + length) return false;
        header = new Header((PacketType)packet[5],
            BinaryPrimitives.ReadUInt64BigEndian(packet[8..]),
            BinaryPrimitives.ReadUInt32BigEndian(packet[16..]),
            BinaryPrimitives.ReadUInt32BigEndian(packet[20..]),
            BinaryPrimitives.ReadUInt32BigEndian(packet[24..]), length);
        payload = packet[HeaderSize..];
        return true;
    }

    public static bool TryWrite(Span<byte> destination, PacketType type, ulong connectionId,
        uint sequence, uint ack, uint ackBits, ReadOnlySpan<byte> payload, out int written)
    {
        written = 0;
        if ((byte)type is < 1 or > 9 || payload.Length > MaxPayloadBytes ||
            destination.Length < HeaderSize + payload.Length) return false;
        var packet = destination[..(HeaderSize + payload.Length)];
        packet[..HeaderSize].Clear();
        "FLV3"u8.CopyTo(packet);
        packet[4] = Version;
        packet[5] = (byte)type;
        BinaryPrimitives.WriteUInt64BigEndian(packet[8..], connectionId);
        BinaryPrimitives.WriteUInt32BigEndian(packet[16..], sequence);
        BinaryPrimitives.WriteUInt32BigEndian(packet[20..], ack);
        BinaryPrimitives.WriteUInt32BigEndian(packet[24..], ackBits);
        BinaryPrimitives.WriteUInt16BigEndian(packet[28..], (ushort)payload.Length);
        payload.CopyTo(packet[HeaderSize..]);
        written = packet.Length;
        return true;
    }
}

/// <summary>
/// Stateless address proof for the first two packets. A cookie is valid only
/// for the same IP, UDP port, client nonce and the current/previous 10s bucket.
/// It is not an authentication token: the existing client-key signature still
/// has to be checked before the game session is created.
/// </summary>
public sealed class UdpCookieProtector
{
    public const int NonceSize = 16;
    public const int CookieSize = 24; // 8-byte time bucket + 16-byte HMAC tag
    public const int BucketSeconds = 10;
    private static readonly byte[] Domain = Encoding.ASCII.GetBytes("FLOVMP-UDP-COOKIE-v3");
    private readonly byte[] _secret;

    public UdpCookieProtector(ReadOnlySpan<byte> secret)
    {
        if (secret.Length != 32) throw new ArgumentException("cookie secret must be 32 bytes", nameof(secret));
        _secret = secret.ToArray();
    }

    public static UdpCookieProtector CreateRandom() => new(RandomNumberGenerator.GetBytes(32));

    public bool TryCreate(IPAddress address, ushort port, ReadOnlySpan<byte> nonce,
        DateTimeOffset now, Span<byte> cookie)
    {
        if (nonce.Length != NonceSize || cookie.Length < CookieSize) return false;
        var bucket = now.ToUnixTimeSeconds() / BucketSeconds;
        BinaryPrimitives.WriteInt64BigEndian(cookie, bucket);
        ComputeTag(address, port, nonce, bucket, cookie.Slice(8, 16));
        return true;
    }

    public bool Validate(IPAddress address, ushort port, ReadOnlySpan<byte> nonce,
        ReadOnlySpan<byte> cookie, DateTimeOffset now)
    {
        if (nonce.Length != NonceSize || cookie.Length != CookieSize) return false;
        var bucket = BinaryPrimitives.ReadInt64BigEndian(cookie);
        var current = now.ToUnixTimeSeconds() / BucketSeconds;
        if (bucket != current && bucket != current - 1) return false;
        Span<byte> expected = stackalloc byte[16];
        ComputeTag(address, port, nonce, bucket, expected);
        return CryptographicOperations.FixedTimeEquals(expected, cookie[8..]);
    }

    private void ComputeTag(IPAddress address, ushort port, ReadOnlySpan<byte> nonce,
        long bucket, Span<byte> tag)
    {
        Span<byte> input = stackalloc byte[Domain.Length + 1 + 16 + 2 + NonceSize + 8];
        Domain.CopyTo(input);
        var offset = Domain.Length;
        input[offset++] = UdpProtocol.Version;
        address.MapToIPv6().TryWriteBytes(input[offset..], out _);
        offset += 16;
        BinaryPrimitives.WriteUInt16BigEndian(input[offset..], port);
        offset += 2;
        nonce.CopyTo(input[offset..]);
        offset += NonceSize;
        BinaryPrimitives.WriteInt64BigEndian(input[offset..], bucket);
        Span<byte> hash = stackalloc byte[32];
        HMACSHA256.HashData(_secret, input, hash);
        hash[..tag.Length].CopyTo(tag);
        CryptographicOperations.ZeroMemory(hash);
    }
}
