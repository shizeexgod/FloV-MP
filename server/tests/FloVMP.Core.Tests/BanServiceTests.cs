using System;
using FloVMP.Core.Security;
using Xunit;

namespace FloVMP.Core.Tests;

public class BanServiceTests
{
    private static MultiTierBanService NewService() => new();

    [Fact]
    public void AccountBan_Blocks_Same_Account_Always()
    {
        var svc = NewService();
        svc.CreateBan(42, "Cheater", "1.2.3.4", null, null, null, BanTier.StandardBan, "admin", "чит", 0);

        var res = svc.CheckConnection(42, "9.9.9.9", null, null, null, HwidPolicyMode.Disabled);
        Assert.True(res.IsBlocked);
        Assert.Equal(BanFlags.Account, res.MatchedFlag);
    }

    [Fact]
    public void AccountBan_Does_Not_Block_Other_Account()
    {
        var svc = NewService();
        svc.CreateBan(42, "Cheater", "1.2.3.4", null, null, null, BanTier.StandardBan, "admin", "чит", 0);

        var res = svc.CheckConnection(43, "1.2.3.4", null, null, null, HwidPolicyMode.Strict);
        Assert.False(res.IsBlocked); // другой аккаунт, StandardBan только по аккаунту
    }

    [Fact]
    public void IpBan_Blocks_Same_Ip_Different_Account_In_Strict()
    {
        var svc = NewService();
        svc.CreateBan(42, "Cheater", "1.2.3.4", null, null, null, BanTier.IpBan, "admin", "чит", 0);

        var res = svc.CheckConnection(99, "1.2.3.4", null, null, null, HwidPolicyMode.Strict);
        Assert.True(res.IsBlocked);
        Assert.Equal(BanFlags.Ip, res.MatchedFlag);
    }

    [Fact]
    public void HwidBan_Blocks_Same_Hwid()
    {
        var svc = NewService();
        svc.CreateBan(42, "Cheater", "1.2.3.4", null, "HWID_ABC", "AA:BB", BanTier.HardwareBan, "admin", "чит", 0);

        var res = svc.CheckConnection(99, "5.5.5.5", null, "HWID_ABC", null, HwidPolicyMode.Strict);
        Assert.True(res.IsBlocked);
        Assert.Equal(BanFlags.Hwid, res.MatchedFlag);
    }

    [Fact]
    public void Subnet_Ban_Blocks_Same_24_Different_Ip()
    {
        // Регресс: hardban ставит флаг Subnet, но раньше он не проверялся —
        // забаненный заходил с того же /24 с другим IP.
        var svc = NewService();
        svc.CreateBan(42, "Cheater", "185.20.44.10", "SC1", "HWID", "MAC", BanTier.HardBan, "admin", "обход", 0);

        // другой аккаунт, другой IP, но тот же /24 (185.20.44.x)
        var res = svc.CheckConnection(99, "185.20.44.200", null, null, null, HwidPolicyMode.Strict);
        Assert.True(res.IsBlocked);
        Assert.Equal(BanFlags.Subnet, res.MatchedFlag);
    }

    [Fact]
    public void Subnet_Ban_Allows_Different_24()
    {
        var svc = NewService();
        svc.CreateBan(42, "Cheater", "185.20.44.10", "SC1", "HWID", "MAC", BanTier.HardBan, "admin", "обход", 0);

        var res = svc.CheckConnection(99, "185.20.45.10", null, null, null, HwidPolicyMode.Strict);
        Assert.False(res.IsBlocked); // соседняя подсеть /24 — не блок
    }

    [Fact]
    public void Disabled_Policy_Skips_Hardware_Bans_But_Not_Account()
    {
        var svc = NewService();
        svc.CreateBan(42, "Cheater", "1.2.3.4", null, "HWID_X", null, BanTier.HardwareBan, "admin", "чит", 0);

        // тот же HWID, другой аккаунт, политика Disabled → аппаратный бан игнорируется
        var res = svc.CheckConnection(99, "9.9.9.9", null, "HWID_X", null, HwidPolicyMode.Disabled);
        Assert.False(res.IsBlocked);
    }

    [Fact]
    public void Lenient_Policy_Allows_But_Flags_For_Alert()
    {
        var svc = NewService();
        svc.CreateBan(42, "Cheater", "1.2.3.4", null, null, null, BanTier.IpBan, "admin", "чит", 0);

        var res = svc.CheckConnection(99, "1.2.3.4", null, null, null, HwidPolicyMode.Lenient);
        Assert.False(res.IsBlocked);
        Assert.True(res.ShouldAlertAdmins);
    }

    [Fact]
    public void Expired_Ban_Does_Not_Block()
    {
        var svc = NewService();
        // срок истёк минуту назад
        svc.CreateCustomBan(42, "Cheater", "1.2.3.4", null, null, null,
            BanFlags.Account | BanFlags.Ip, "admin", "чит", DateTime.UtcNow.AddMinutes(-1));

        var res = svc.CheckConnection(42, "1.2.3.4", null, null, null, HwidPolicyMode.Strict);
        Assert.False(res.IsBlocked);
    }

    [Fact]
    public void Unban_By_Ip_Deactivates_Ban()
    {
        var svc = NewService();
        svc.CreateBan(42, "Cheater", "1.2.3.4", null, null, null, BanTier.IpBan, "admin", "чит", 0);

        var n = svc.Unban("1.2.3.4");
        Assert.True(n >= 1);

        var res = svc.CheckConnection(42, "1.2.3.4", null, null, null, HwidPolicyMode.Strict);
        Assert.False(res.IsBlocked);
    }

    [Fact]
    public void SameSubnet24_Helper_Edge_Cases()
    {
        Assert.True(MultiTierBanService.SameSubnet24("10.0.0.1", "10.0.0.254"));
        Assert.False(MultiTierBanService.SameSubnet24("10.0.0.1", "10.0.1.1"));
        Assert.False(MultiTierBanService.SameSubnet24(null, "10.0.0.1"));
        Assert.False(MultiTierBanService.SameSubnet24("not-an-ip", "10.0.0.1"));
        Assert.False(MultiTierBanService.SameSubnet24("10.0.0.1", ""));
    }
}
