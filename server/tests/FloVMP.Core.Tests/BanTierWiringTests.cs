using FloVMP.Core.Security;
using Xunit;

namespace FloVMP.Core.Tests;

/// <summary>
/// Семантика многоуровневых блокировок в том виде, в каком её используют
/// команды сервера.
///
/// Причина существования этих тестов. Команды /banip, /bansc, /hwidban,
/// /macban и /hardban писали в чат «заблокирован по IP / по железу», но на
/// деле ставили флаг ТОЛЬКО на аккаунте: ни IP, ни HWID, ни MAC нигде не
/// сохранялись и нигде не проверялись. Читер регистрировал новый аккаунт и
/// возвращался, а администратор был уверен, что забанил машину. Это хуже
/// отсутствия функции — администратор принимает решения по ложным данным.
///
/// Здесь закреплено главное обещание каждой команды: с НОВОГО аккаунта, но с
/// тем же железом/IP, вход должен отклоняться.
/// </summary>
public class BanTierWiringTests
{
    private const string Ip = "203.0.113.77";
    private const string Sc = "SC_1234567";
    private const string Hwid = "A1B2C3D4E5F60718";
    private const string Mac = "0718F6E5D4C3B2A1";

    private static MultiTierBanService Service() => new();

    /// <summary>Вход «того же человека», но под другим, новым аккаунтом.</summary>
    private static BanCheckResult CheckAsNewAccount(MultiTierBanService svc,
                                                    string? ip = Ip, string? sc = Sc,
                                                    string? hwid = Hwid, string? mac = Mac) =>
        svc.CheckConnection(accountId: 0, ip, sc, hwid, mac, HwidPolicyMode.Strict);

    [Fact]
    public void IpBan_BlocksSameIp_FromNewAccount()
    {
        var svc = Service();
        svc.CreateBan(1, "Читер", Ip, Sc, Hwid, Mac, BanTier.IpBan, "Админ", "чит", 7);

        Assert.True(CheckAsNewAccount(svc).IsBlocked);
    }

    [Fact]
    public void IpBan_DoesNotBlockDifferentPersonOnDifferentIp()
    {
        var svc = Service();
        svc.CreateBan(1, "Читер", Ip, Sc, Hwid, Mac, BanTier.IpBan, "Админ", "чит", 7);

        var clean = svc.CheckConnection(0, "198.51.100.10", "SC_OTHER", "OTHERHWID", "OTHERMAC",
                                        HwidPolicyMode.Strict);
        Assert.False(clean.IsBlocked);
    }

    [Fact]
    public void HardwareBan_BlocksSameMachine_EvenFromDifferentIp()
    {
        // Смысл бана по железу: смена интернета и аккаунта не помогает.
        var svc = Service();
        svc.CreateBan(1, "Читер", Ip, Sc, Hwid, Mac, BanTier.HardwareBan, "Админ", "чит", 30);

        Assert.True(CheckAsNewAccount(svc, ip: "198.51.100.10", sc: "SC_NEW").IsBlocked);
    }

    [Fact]
    public void SocialClubBan_BlocksSameLicense()
    {
        var svc = Service();
        svc.CreateBan(1, "Читер", Ip, Sc, Hwid, Mac, BanTier.SocialClubBan, "Админ", "чит", 30);

        Assert.True(CheckAsNewAccount(svc, ip: "198.51.100.10", hwid: "NEWHWID", mac: "NEWMAC").IsBlocked);
    }

    [Fact]
    public void HardBan_BlocksEveryIdentifierIndependently()
    {
        var svc = Service();
        svc.CreateBan(1, "Вредитель", Ip, Sc, Hwid, Mac, BanTier.HardBan, "Админ", "рейд", 0);

        // Совпадения достаточно по любому одному признаку.
        Assert.True(CheckAsNewAccount(svc, sc: "SC_NEW", hwid: "NEW", mac: "NEW").IsBlocked);  // по IP
        Assert.True(CheckAsNewAccount(svc, ip: "1.2.3.4", hwid: "NEW", mac: "NEW").IsBlocked); // по SC
        Assert.True(CheckAsNewAccount(svc, ip: "1.2.3.4", sc: "SC_NEW", mac: "NEW").IsBlocked); // по HWID
        Assert.True(CheckAsNewAccount(svc, ip: "1.2.3.4", sc: "SC_NEW", hwid: "NEW").IsBlocked); // по MAC
    }

    [Fact]
    public void HardBan_IsPermanent()
    {
        // durationDays 0 = навсегда. Раньше /hardban ставил «10 лет» на
        // аккаунте и не блокировал железо вообще.
        var svc = Service();
        var rec = svc.CreateBan(1, "Вредитель", Ip, Sc, Hwid, Mac, BanTier.HardBan, "Админ", "рейд", 0);
        Assert.Null(rec.ExpiresAtUtc);
    }

    [Fact]
    public void StandardBan_DoesNotBlockNewAccountFromSameMachine()
    {
        // Обычный /ban намеренно НЕ трогает железо: это разные инструменты, и
        // администратор должен понимать разницу. Тест фиксирует границу, чтобы
        // «на всякий случай» её не размыли обратно.
        var svc = Service();
        svc.CreateBan(7, "Нарушитель", Ip, Sc, Hwid, Mac, BanTier.StandardBan, "Админ", "оскорбления", 3);

        Assert.False(CheckAsNewAccount(svc).IsBlocked);
        Assert.True(svc.CheckConnection(7, Ip, Sc, Hwid, Mac, HwidPolicyMode.Strict).IsBlocked);
    }

    [Fact]
    public void ExpiredBan_StopsBlocking()
    {
        var svc = Service();
        svc.CreateCustomBan(1, "Отсиделся", Ip, Sc, Hwid, Mac,
                            MultiTierBanService.TierToFlags(BanTier.HardBan),
                            "Админ", "срок вышел", DateTime.UtcNow.AddSeconds(-1));

        Assert.False(CheckAsNewAccount(svc).IsBlocked);
    }

    [Fact]
    public void Unban_ByIp_LiftsTheBlock()
    {
        // /unban принимает не только ник: администратор часто знает только IP
        // или HWID из лога.
        var svc = Service();
        svc.CreateBan(1, "Читер", Ip, Sc, Hwid, Mac, BanTier.HardBan, "Админ", "чит", 0);

        Assert.Equal(1, svc.Unban(Ip));
        Assert.False(CheckAsNewAccount(svc).IsBlocked);
    }

    [Fact]
    public void Unban_ByHwid_LiftsTheBlock()
    {
        var svc = Service();
        svc.CreateBan(1, "Читер", Ip, Sc, Hwid, Mac, BanTier.HardwareBan, "Админ", "чит", 0);

        Assert.Equal(1, svc.Unban(Hwid));
        Assert.False(CheckAsNewAccount(svc).IsBlocked);
    }

    [Fact]
    public void Unban_UnknownQuery_LiftsNothing()
    {
        var svc = Service();
        svc.CreateBan(1, "Читер", Ip, Sc, Hwid, Mac, BanTier.HardBan, "Админ", "чит", 0);

        Assert.Equal(0, svc.Unban("кого-то-нет"));
        Assert.True(CheckAsNewAccount(svc).IsBlocked);
    }

    [Fact]
    public void CleanPlayer_IsNeverBlocked()
    {
        var svc = Service();
        Assert.False(svc.CheckConnection(5, "192.0.2.1", "SC_CLEAN", "CLEANHWID", "CLEANMAC",
                                         HwidPolicyMode.Strict).IsBlocked);
    }
}
