using FloVMP.Core.AntiCheat;
using FloVMP.Core.Spatial;
using FloVMP.Core.Voice;
using Xunit;

namespace FloVMP.Core.Tests;

public sealed class VoiceGridRouterTests
{
    [Fact]
    public void RouteSpatialVoice_Whisper_OnlyHearsNearby()
    {
        var grid = new SpatialHashGrid<ulong>(cellSize: 32.0f);
        var router = new VoiceGridRouter(grid);

        // Speaker at (10, 10, 0)
        grid.InsertOrUpdate(100, new Vector3D(10f, 10f, 0f));
        // Listener close (11, 10, 0) -> dist = 1.0m
        grid.InsertOrUpdate(101, new Vector3D(11f, 10f, 0f));
        // Listener further away (14, 10, 0) -> dist = 4.0m (outside whisper 2.5m)
        grid.InsertOrUpdate(102, new Vector3D(14f, 10f, 0f));

        var recipients = router.RouteSpatialVoice(
            speakerId: 100,
            speakerPos: new Vector3D(10f, 10f, 0f),
            dimension: 0,
            rangeMode: VoiceRangeMode.Whisper
        );

        Assert.Single(recipients);
        Assert.Equal(101ul, recipients[0].ListenerId);
        Assert.True(recipients[0].Volume > 0.4f); // 1m out of 2.5m should have volume
        Assert.Equal(VoiceTransmissionType.Proximity3D, recipients[0].TransmissionType);
    }

    [Fact]
    public void RouteSpatialVoice_DimensionIsolation()
    {
        var grid = new SpatialHashGrid<ulong>(cellSize: 32.0f);
        var router = new VoiceGridRouter(grid);

        grid.InsertOrUpdate(100, new Vector3D(0f, 0f, 0f), dimension: 0);
        grid.InsertOrUpdate(101, new Vector3D(1f, 0f, 0f), dimension: 0);
        grid.InsertOrUpdate(102, new Vector3D(1f, 0f, 0f), dimension: 1); // Другой dimension

        var recipients = router.RouteSpatialVoice(
            speakerId: 100,
            speakerPos: new Vector3D(0f, 0f, 0f),
            dimension: 0,
            rangeMode: VoiceRangeMode.Normal
        );

        Assert.Single(recipients);
        Assert.Equal(101ul, recipients[0].ListenerId);
    }

    [Fact]
    public void RouteSpatialVoice_ServerMute_SilencesSpeaker()
    {
        var grid = new SpatialHashGrid<ulong>();
        var router = new VoiceGridRouter(grid);

        grid.InsertOrUpdate(100, new Vector3D(0f, 0f, 0f));
        grid.InsertOrUpdate(101, new Vector3D(2f, 0f, 0f));

        router.SetServerMute(100, true);
        Assert.True(router.IsServerMuted(100));

        var recipients = router.RouteSpatialVoice(100, new Vector3D(0f, 0f, 0f), 0, VoiceRangeMode.Normal);
        Assert.Empty(recipients);
    }

    [Fact]
    public void RouteSpatialVoice_PersonalMute_IgnoresSpeaker()
    {
        var grid = new SpatialHashGrid<ulong>();
        var router = new VoiceGridRouter(grid);

        grid.InsertOrUpdate(100, new Vector3D(0f, 0f, 0f));
        grid.InsertOrUpdate(101, new Vector3D(2f, 0f, 0f));
        grid.InsertOrUpdate(102, new Vector3D(3f, 0f, 0f));

        // Игрок 101 замьютил игрока 100
        router.SetPlayerMute(listenerId: 101, targetSpeakerId: 100, isMuted: true);
        Assert.True(router.IsPlayerMutedBy(101, 100));

        var recipients = router.RouteSpatialVoice(100, new Vector3D(0f, 0f, 0f), 0, VoiceRangeMode.Normal);

        // Только игрок 102 должен услышать
        Assert.Single(recipients);
        Assert.Equal(102ul, recipients[0].ListenerId);
    }

    [Fact]
    public void Radio_FrequencyRouting_AndEncryption()
    {
        var grid = new SpatialHashGrid<ulong>();
        var router = new VoiceGridRouter(grid);

        // Игроки на волне 101.5 с ключом "police_key"
        router.TuneRadio(101, 101.5f, "police_key");
        router.TuneRadio(102, 101.5f, "police_key");

        // Игрок на волне 101.5 с неверным ключом
        router.TuneRadio(103, 101.5f, "wrong_key");

        // Игрок на другой волне 102.0
        router.TuneRadio(104, 102.0f, "police_key");

        // Игрок 101 говорит в рацию 101.5
        var recipients = router.RouteRadioVoice(speakerId: 101, frequency: 101.5f, speakerKey: "police_key");

        // Только 102 должен услышать (101 - сам говорящий, 103 - неверный ключ, 104 - другая волна)
        Assert.Single(recipients);
        Assert.Equal(102ul, recipients[0].ListenerId);
        Assert.Equal(VoiceTransmissionType.Radio, recipients[0].TransmissionType);
    }

    [Fact]
    public void PhoneCall_RoutesToCallParticipantsOnly()
    {
        var grid = new SpatialHashGrid<ulong>();
        var router = new VoiceGridRouter(grid);

        string callId = "call_abc123";
        router.JoinPhoneCall(callId, 101);
        router.JoinPhoneCall(callId, 102);
        router.JoinPhoneCall(callId, 103); // Конференция на 3 человека

        var recipients = router.RoutePhoneVoice(101);

        Assert.Equal(2, recipients.Count);
        Assert.Contains(recipients, r => r.ListenerId == 102ul);
        Assert.Contains(recipients, r => r.ListenerId == 103ul);
        Assert.All(recipients, r => Assert.Equal(VoiceTransmissionType.PhoneCall, r.TransmissionType));

        // Выход одного игрока
        router.LeavePhoneCall(103);
        var afterLeaveRecipients = router.RoutePhoneVoice(101);
        Assert.Single(afterLeaveRecipients);
        Assert.Equal(102ul, afterLeaveRecipients[0].ListenerId);
    }

    [Fact]
    public void RemovePlayer_Cleans_All_Channels_And_Mutes()
    {
        var grid = new SpatialHashGrid<ulong>();
        var router = new VoiceGridRouter(grid);

        router.TuneRadio(101, 105.0f);
        router.TuneRadio(102, 105.0f);
        router.JoinPhoneCall("call_xyz", 101);
        router.SetServerMute(101, true);
        router.SetPlayerMute(101, 102, true);

        Assert.True(router.IsServerMuted(101));
        Assert.True(router.IsPlayerMutedBy(101, 102));

        router.RemovePlayer(101);

        Assert.False(router.IsServerMuted(101));
        Assert.False(router.IsPlayerMutedBy(101, 102));

        // When 102 talks on radio 105.0, 101 does not receive it
        var recs = router.RouteRadioVoice(102, 105.0f);
        Assert.DoesNotContain(recs, r => r.ListenerId == 101ul);
    }
}

