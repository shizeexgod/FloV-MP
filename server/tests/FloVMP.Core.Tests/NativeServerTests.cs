using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using FloVMP.Core.Native;
using Xunit;

namespace FloVMP.Core.Tests;

public sealed class NativeServerTests
{
    /// <summary>Имитация нативного клиента: тот же протокол и подпись, что в ASI.</summary>
    internal sealed class FakeClient : IDisposable
    {
        public readonly TcpClient Tcp = new();
        public readonly ECDsa Key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        private StreamReader _reader = null!;
        private NetworkStream _stream = null!;

        public byte[] PublicKey
        {
            get
            {
                var p = Key.ExportParameters(false);
                return p.Q.X!.Concat(p.Q.Y!).ToArray();
            }
        }

        public async Task ConnectAsync(int port)
        {
            await Tcp.ConnectAsync(IPAddress.Loopback, port);
            _stream = Tcp.GetStream();
            _reader = new StreamReader(_stream, Encoding.UTF8, false, 1024, leaveOpen: true);
        }

        public Task SendAsync(string line) => _stream.WriteAsync(Encoding.UTF8.GetBytes(line + "\n")).AsTask();

        public async Task<string?> ReadAsync(int timeoutMs = 5000)
        {
            var read = _reader.ReadLineAsync();
            return await Task.WhenAny(read, Task.Delay(timeoutMs)) == read ? read.Result : throw new TimeoutException();
        }

        public async Task<string> ReadUntilAsync(string prefix, int timeoutMs = 5000)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                var line = await ReadAsync((int)Math.Max(1, (deadline - DateTime.UtcNow).TotalMilliseconds));
                if (line is null) throw new IOException("closed");
                if (line.StartsWith(prefix, StringComparison.Ordinal)) return line;
            }
            throw new TimeoutException(prefix);
        }

        public async Task<string> JoinAsync(int port, string name = "Tester", string game = NativeProtocol.GameVersion,
                                            bool badSignature = false)
        {
            await ConnectAsync(port);
            await SendAsync(NativeProtocol.Format("HELLO", NativeProtocol.Version, game, "test", name,
                Convert.ToBase64String(PublicKey), "00000000000000AA", "00000000000000BB"));
            var challenge = NativeProtocol.Parse(await ReadAsync() ?? "");
            if (challenge[0] != "CHALLENGE") return string.Join('\t', challenge);
            var nonce = Convert.FromBase64String(challenge[1]);
            var data = NativeProtocol.AuthMessage(nonce, PublicKey);
            if (badSignature) data[0] ^= 1;
            var sig = Key.SignData(data, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
            await SendAsync(NativeProtocol.Format("AUTH", Convert.ToBase64String(sig)));
            // WELCOME шлёт игровой поток после проверок бана и лицензии, а не транспорт.
            return "AUTH-SENT";
        }

        public void Dispose() { Tcp.Dispose(); Key.Dispose(); }
    }

    private static NativeServer StartServer()
    {
        var server = new NativeServer(IPAddress.Loopback, 0, _ => { });
        server.Start();
        return server;
    }

    private static async Task<T> WaitEventAsync<T>(NativeServer server, int timeoutMs = 5000) where T : NativeEvent
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            while (server.Events.TryDequeue(out var ev))
                if (ev is T t) return t;
            await Task.Delay(10);
        }
        throw new TimeoutException(typeof(T).Name);
    }

    [Fact]
    public void EscapingRoundTripsTabsAndNewlines()
    {
        var text = "a\tb\nc\\d\re";
        var line = NativeProtocol.Format("MSG", "player", text);
        Assert.DoesNotContain('\n', line);
        var parts = NativeProtocol.Parse(line);
        Assert.Equal(3, parts.Length);
        Assert.Equal(text, parts[2]);
    }

    [Fact]
    public async Task SignedClientJoinsWithStableIdentity()
    {
        using var server = StartServer();
        using var client = new FakeClient();
        Assert.Equal("AUTH-SENT", await client.JoinAsync(server.Port));

        var joined = await WaitEventAsync<NativeJoined>(server);
        Assert.Equal("Tester", joined.Session.Name);
        Assert.Equal(NativeIdentity.IdFor(client.PublicKey), joined.Session.Identity);
        Assert.True(NativeIdentity.IsNativeIdentity(joined.Session.Identity));
        Assert.Equal(0xAAUL, joined.Session.HardwareId);
    }

    [Fact]
    public async Task ForgedSignatureIsRejected()
    {
        using var server = StartServer();
        using var client = new FakeClient();
        Assert.Equal("AUTH-SENT", await client.JoinAsync(server.Port, badSignature: true));
        Assert.StartsWith("REJECT", await client.ReadAsync());
        Assert.Equal(0, server.Count);
    }

    [Fact]
    public async Task WrongGameVersionIsRejectedWithReason()
    {
        using var server = StartServer();
        using var client = new FakeClient();
        var reply = await client.JoinAsync(server.Port, game: "1.0.3521.0");
        Assert.StartsWith("REJECT", reply);
        Assert.Contains("1.0.3889.0", reply);
    }

    [Fact]
    public async Task BadNameIsRejected()
    {
        using var server = StartServer();
        using var client = new FakeClient();
        var reply = await client.JoinAsync(server.Port, name: "{ff0000}Admin");
        Assert.StartsWith("REJECT", reply);
    }

    /// <summary>
    /// Клиентский скрипт на пределе (100 событий в секунду) вместе с движением
    /// (20 STATE в секунду) — это честный игрок, отключать его нельзя.
    /// </summary>
    [Fact]
    public async Task ScriptEventsAtClientLimitDoNotKick()
    {
        using var server = StartServer();
        using var client = new FakeClient();
        await client.JoinAsync(server.Port);
        await WaitEventAsync<NativeJoined>(server);

        var sb = new StringBuilder();
        for (var i = 0; i < 100; i++) sb.Append(NativeProtocol.Format("CEVS", "hud:tick", "[]")).Append('\n');
        for (var i = 0; i < 40; i++) sb.Append(NativeProtocol.Format("PING", "1")).Append('\n');
        await client.Tcp.GetStream().WriteAsync(Encoding.UTF8.GetBytes(sb.ToString()));
        await Task.Delay(300);

        while (server.Events.TryDequeue(out var ev)) Assert.IsNotType<NativeLeft>(ev);
        Assert.Equal(1, server.Count);
    }

    [Fact]
    public async Task ScriptEventFloodIsDisconnected()
    {
        using var server = StartServer();
        using var client = new FakeClient();
        await client.JoinAsync(server.Port);
        await WaitEventAsync<NativeJoined>(server);

        var sb = new StringBuilder();
        for (var i = 0; i < NativeServer.MaxScriptEventsPerSecond + 20; i++)
            sb.Append(NativeProtocol.Format("CEVS", "spam", "[]")).Append('\n');
        await client.Tcp.GetStream().WriteAsync(Encoding.UTF8.GetBytes(sb.ToString()));

        var left = await WaitEventAsync<NativeLeft>(server);
        Assert.Equal("превышен лимит сообщений", left.Reason);
    }

    [Fact]
    public async Task StateIsStoredAndChatIsQueued()
    {
        using var server = StartServer();
        using var client = new FakeClient();
        await client.JoinAsync(server.Port);
        var joined = await WaitEventAsync<NativeJoined>(server);
        joined.Session.Send("WELCOME", joined.Session.Id);
        await client.ReadUntilAsync("WELCOME");

        await client.SendAsync(NativeProtocol.Format("STATE", 100f, 200f, 30f, 90f, 0f, 0f, 0f, 0, 0u, 0, -1,
            0f, 0f, 0f, 200, 0, 0u, 1f));
        await client.SendAsync(NativeProtocol.Format("CHAT", "привет\tвсем"));
        var msg = await WaitEventAsync<NativeMessage>(server);
        Assert.Equal("CHAT", msg.Parts[0]);
        Assert.Equal("привет\tвсем", msg.Parts[1]);
        Assert.True(joined.Session.HasState);
        Assert.Equal(100f, joined.Session.State.X);
        Assert.Equal(90f, joined.Session.State.Heading);
    }

    [Fact]
    public async Task OutOfWorldStateIsIgnored()
    {
        using var server = StartServer();
        using var client = new FakeClient();
        await client.JoinAsync(server.Port);
        var joined = await WaitEventAsync<NativeJoined>(server);
        await client.SendAsync(NativeProtocol.Format("STATE", 1e9f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0u, 0, -1,
            0f, 0f, 0f, 200, 0, 0u, 0f));
        await client.SendAsync("PING\t1");
        await client.ReadUntilAsync("PONG");
        Assert.False(joined.Session.HasState);
    }

    [Fact]
    public async Task OversizedLineDisconnects()
    {
        using var server = StartServer();
        using var client = new FakeClient();
        await client.JoinAsync(server.Port);
        await WaitEventAsync<NativeJoined>(server);
        await client.SendAsync("CHAT\t" + new string('x', NativeProtocol.MaxLineBytes * 3));
        var left = await WaitEventAsync<NativeLeft>(server);
        Assert.Contains("длинное", left.Reason);
    }

    [Fact]
    public async Task SingleBufferOversizedLineDisconnects()
    {
        using var server = StartServer();
        using var client = new FakeClient();
        await client.JoinAsync(server.Port);
        await WaitEventAsync<NativeJoined>(server);
        await client.SendAsync("CHAT\t" + new string('x', NativeProtocol.MaxLineBytes));
        var left = await WaitEventAsync<NativeLeft>(server);
        Assert.Contains("длинное", left.Reason);
    }

    [Fact]
    public async Task InvalidUtf8DisconnectsWithoutDeliveringMessage()
    {
        using var server = StartServer();
        using var client = new FakeClient();
        await client.JoinAsync(server.Port);
        await WaitEventAsync<NativeJoined>(server);
        await client.Tcp.GetStream().WriteAsync(new byte[] { (byte)'C', (byte)'H', (byte)'A', (byte)'T', 9, 0xC3, 0x28, 10 });
        var left = await WaitEventAsync<NativeLeft>(server);
        Assert.Contains("UTF-8", left.Reason);
    }

    [Fact]
    public async Task FragmentedUtf8AndBufferedNextLineAreReadCorrectly()
    {
        using var server = StartServer();
        using var client = new FakeClient();
        await client.JoinAsync(server.Port);
        await WaitEventAsync<NativeJoined>(server);
        var stream = client.Tcp.GetStream();
        await stream.WriteAsync(new byte[] { (byte)'C', (byte)'H', (byte)'A', (byte)'T', 9, 0xD1 });
        await stream.WriteAsync(new byte[] { 0x8F, 10, (byte)'P', (byte)'I', (byte)'N', (byte)'G', 9, (byte)'4', (byte)'2', 10 });
        var message = await WaitEventAsync<NativeMessage>(server);
        Assert.Equal(new[] { "CHAT", "я" }, message.Parts);
        Assert.Equal("PONG\t42", await client.ReadUntilAsync("PONG"));
    }

    [Fact]
    public async Task KickDeliversReasonBeforeDisconnect()
    {
        using var server = StartServer();
        using var client = new FakeClient();
        await client.JoinAsync(server.Port);
        var joined = await WaitEventAsync<NativeJoined>(server);
        joined.Session.Close("тестовый кик");
        var kick = await client.ReadUntilAsync("KICK");
        Assert.Contains("тестовый кик", kick);
        var left = await WaitEventAsync<NativeLeft>(server);
        Assert.Equal("тестовый кик", left.Reason);
    }

    [Fact]
    public async Task IdsSkipThoseUsedByOtherPlayers()
    {
        using var server = new NativeServer(IPAddress.Loopback, 0, _ => { }, id => id is 1 or 2);
        server.Start();
        using var client = new FakeClient();
        await client.JoinAsync(server.Port);
        var joined = await WaitEventAsync<NativeJoined>(server);
        Assert.Equal(3u, joined.Session.Id);
    }

    [Fact]
    public async Task ParallelJoinsGetUniqueIds()
    {
        using var server = new NativeServer(IPAddress.Loopback, 0, _ => { });
        server.Start();
        // Подключения с самой машины сервера лимитом на один IP не режутся:
        // берём заведомо больше лимита — все должны войти и получить свой ID.
        var clients = Enumerable.Range(0, NativeServer.MaxConnectionsPerIp + 6).Select(_ => new FakeClient()).ToList();
        try
        {
            await Task.WhenAll(clients.Select(c => c.JoinAsync(server.Port)));
            var ids = new List<uint>();
            for (var i = 0; i < clients.Count; i++) ids.Add((await WaitEventAsync<NativeJoined>(server)).Session.Id);
            Assert.Equal(ids.Count, ids.Distinct().Count());
        }
        finally { foreach (var c in clients) c.Dispose(); }
    }

    [Fact]
    public async Task BurstOfMessagesArrivesWholeAndInOrder()
    {
        // Снимок мира — это тысячи строк подряд. Ни одна не должна пропасть и
        // ни одна не должна слипнуться с соседней.
        using var server = StartServer();
        using var client = new FakeClient();
        await client.JoinAsync(server.Port);
        var session = (await WaitEventAsync<NativeJoined>(server)).Session;

        const int count = 3000;
        for (var i = 0; i < count; i++)
            Assert.True(session.Send(NativeProtocol.Format("WOBJ", "item" + i, new string('y', 60))));

        var seen = 0;
        while (seen < count)
        {
            var line = await client.ReadAsync(15000);
            Assert.NotNull(line);
            if (!line!.StartsWith("WOBJ", StringComparison.Ordinal)) continue;
            var parts = NativeProtocol.Parse(line);
            Assert.Equal("item" + seen, parts[1]);   // порядок и целостность строки
            seen++;
        }
        Assert.Equal(count, seen);
    }

    [Fact]
    public async Task SlowClientIsDisconnectedInsteadOfLosingMessages()
    {
        // Очередь на переполнении раньше «принимала» строку и молча её теряла:
        // у игрока пропадали объекты мира, чат и урон. Теперь такой клиент
        // отключается с причиной — потерь без предупреждения быть не должно.
        using var server = StartServer();
        using var client = new FakeClient();
        await client.JoinAsync(server.Port);
        var session = (await WaitEventAsync<NativeJoined>(server)).Session;

        var line = "CHAT	" + new string('x', 3500);   // клиент ничего не читает
        var refused = false;
        for (var i = 0; i < NativeSession.OutboxCapacity * 4 && !refused; i++)
            refused = !session.Send(line);

        Assert.True(refused, "переполненная очередь должна отказать, а не терять строки");
        var left = await WaitEventAsync<NativeLeft>(server);
        Assert.Contains("не успевает", left.Reason);
    }

    [Fact]
    public async Task FreedIdIsNotReusedImmediately()
    {
        using var server = StartServer();
        using (var first = new FakeClient())
        {
            await first.JoinAsync(server.Port);
            Assert.Equal(1u, (await WaitEventAsync<NativeJoined>(server)).Session.Id);
        }
        await WaitEventAsync<NativeLeft>(server);
        using var second = new FakeClient();
        await second.JoinAsync(server.Port);
        // «/kick 1», набранный сразу после выхода игрока 1, не должен попасть в нового.
        Assert.Equal(2u, (await WaitEventAsync<NativeJoined>(server)).Session.Id);
    }

    private static byte[] VoicePacket(ulong token, ushort seq, int payload)
    {
        var p = new byte[10 + payload];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(p, token);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(p.AsSpan(8), seq);
        for (var i = 0; i < payload; i++) p[10 + i] = (byte)(i + 1);
        return p;
    }

    private static async Task<byte[]?> ReceiveOrNull(UdpClient udp, int ms)
    {
        var t = udp.ReceiveAsync();
        return await Task.WhenAny(t, Task.Delay(ms)) == t ? t.Result.Buffer : null;
    }

    [Fact]
    public async Task VoiceIsRelayedOnlyToNearbyAndNotWhenMuted()
    {
        using var server = StartServer();
        using var voice = new NativeVoice(IPAddress.Loopback, 0, () => server.Sessions, _ => { });
        voice.Start();
        using var a = new FakeClient();
        using var b = new FakeClient();
        await a.JoinAsync(server.Port);
        var sa = (await WaitEventAsync<NativeJoined>(server)).Session;
        await b.JoinAsync(server.Port);
        var sb = (await WaitEventAsync<NativeJoined>(server)).Session;
        var ta = Convert.ToUInt64(voice.Register(sa), 16);
        var tb = Convert.ToUInt64(voice.Register(sb), 16);

        async Task State(FakeClient c, float x) =>
            await c.SendAsync(NativeProtocol.Format("STATE", x, 0f, 30f, 0f, 0f, 0f, 0f, 0, 0u, 0, -1, 0f, 0f, 0f, 200, 0, 0u, 0f));
        await State(a, 0f);
        await State(b, 10f); // 10 м — в радиусе 25
        for (var i = 0; i < 50 && !(sa.HasState && sb.HasState); i++) await Task.Delay(20);

        using var ua = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var ub = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var dest = new IPEndPoint(IPAddress.Loopback, voice.Port);
        await ub.SendAsync(VoicePacket(tb, 0, 0), dest); // «я здесь»
        await ua.SendAsync(VoicePacket(ta, 0, 0), dest);
        await Task.Delay(100);

        await ua.SendAsync(VoicePacket(ta, 7, 40), dest);
        var got = await ReceiveOrNull(ub, 2000);
        Assert.NotNull(got);
        Assert.Equal(sa.Id, System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(got!));
        Assert.Equal(7, System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(got.AsSpan(4)));
        Assert.Equal(6 + 40, got.Length);

        // Чужой (неизвестный) токен — тишина.
        await ua.SendAsync(VoicePacket(0xDEADBEEF, 8, 40), dest);
        Assert.Null(await ReceiveOrNull(ub, 300));

        // Заглушённый — не пересылается.
        sa.VoiceMuted = true;
        await ua.SendAsync(VoicePacket(ta, 9, 40), dest);
        Assert.Null(await ReceiveOrNull(ub, 300));
        sa.VoiceMuted = false;

        // Далеко (100 м) — не слышно.
        await State(b, 100f);
        await Task.Delay(150);
        await ua.SendAsync(VoicePacket(ta, 10, 40), dest);
        Assert.Null(await ReceiveOrNull(ub, 300));

        // Другое измерение — не слышно.
        await State(b, 5f);
        sb.Dimension = 3;
        await Task.Delay(150);
        await ua.SendAsync(VoicePacket(ta, 11, 40), dest);
        Assert.Null(await ReceiveOrNull(ub, 300));
    }

    [Fact]
    public void StateParsingClampsAndNormalizes()
    {
        var parts = NativeProtocol.Parse(NativeProtocol.Format("STATE", 1f, 2f, 3f, -90f, 9999f, 0f, 0f, 0x1FFFF,
            123u, 0, 99, 0f, 0f, 0f, 5000, -5, 0u, 9f));
        Assert.True(NativePlayerState.TryParse(parts, out var s));
        Assert.Equal(270f, s.Heading);
        Assert.Equal(300f, s.Vx);
        Assert.Equal(0xFFFF, s.Flags);
        Assert.Equal(16, s.Seat);
        Assert.Equal(1000, s.Health);
        Assert.Equal(0, s.Armor);
        Assert.Equal(3f, s.Speed);
    }

    [Fact]
    public void StateParsingRejectsTruncatedPacket()
    {
        var parts = new string[18];
        parts[0] = "STATE";
        Assert.False(NativePlayerState.TryParse(parts, out _));
    }
}
