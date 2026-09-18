using FloVMP.Core.Security;
using Xunit;

namespace FloVMP.Core.Tests;

/// <summary>
/// Мут голосового канала alt:V живёт только пока игрок в сети: нарушитель
/// выходил, заходил снова и опять говорил. Эти проверки — про то, что мут
/// переживает и переподключение, и перезапуск сервера, и при этом сам
/// заканчивается в срок.
/// </summary>
public sealed class MuteStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "flovmp-vmute", Guid.NewGuid().ToString("N"));

    private string Path_ => Path.Combine(_dir, "voice-mutes.json");

    public MuteStoreTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    [Fact]
    public void Mute_survives_restart()
    {
        var now = DateTime.UtcNow;
        var store = new MuteStore(Path_);
        store.Mute("123456789", null);

        var reopened = new MuteStore(Path_);
        Assert.True(reopened.IsMuted("123456789", now));
        Assert.False(reopened.IsMuted("999888777", now));
    }

    [Fact]
    public void Expired_mute_lifts_itself()
    {
        var now = DateTime.UtcNow;
        var store = new MuteStore(Path_);
        store.Mute("123456789", now.AddMinutes(30));

        Assert.True(store.IsMuted("123456789", now));
        Assert.True(store.IsMuted("123456789", now.AddMinutes(29)));
        Assert.False(store.IsMuted("123456789", now.AddMinutes(31)));

        // Истёкший мут удаляется, а не остаётся висеть в файле навсегда.
        Assert.Equal(0, store.Count);
    }

    [Fact]
    public void Unmute_removes_record()
    {
        var store = new MuteStore(Path_);
        store.Mute("123456789", null);

        Assert.True(store.Unmute("123456789"));
        Assert.False(store.Unmute("123456789"));
        Assert.False(store.IsMuted("123456789", DateTime.UtcNow));
    }

    [Fact]
    public void Player_without_social_club_is_never_muted_globally()
    {
        // SocialClubId = 0 у игрока без привязки. Мут по нулю заглушил бы
        // всех таких игроков разом.
        var store = new MuteStore(Path_);
        store.Mute("0", null);

        Assert.False(store.IsMuted("0", DateTime.UtcNow));
        Assert.Equal(0, store.Count);
    }

    [Fact]
    public void Broken_file_does_not_break_server_start()
    {
        File.WriteAllText(Path_, "{ это не json ]");

        var store = new MuteStore(Path_); // не бросает
        Assert.False(store.IsMuted("123456789", DateTime.UtcNow));
    }
}
