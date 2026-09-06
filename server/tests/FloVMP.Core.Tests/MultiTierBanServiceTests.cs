using System;
using FloVMP.Core.Security;
using Xunit;

namespace FloVMP.Core.Tests
{
    public class MultiTierBanServiceTests
    {
        [Fact]
        public void StandardBan_OnlyBlocksMatchingAccount()
        {
            var service = new MultiTierBanService();
            service.CreateBan(
                accountId: 42,
                username: "BadBoy",
                ip: "192.168.1.50",
                socialClubId: "SC_12345",
                hwidHash: "HWID_AAA",
                macAddress: "00:11:22:33:44:55",
                tier: BanTier.StandardBan,
                adminUsername: "AdminAlex",
                reason: "DM in green zone",
                durationDays: 7);

            // Тот же аккаунт блокируется
            var res1 = service.CheckConnection(42, "192.168.1.50", "SC_12345", "HWID_AAA", "00:11:22:33:44:55", HwidPolicyMode.Strict);
            Assert.True(res1.IsBlocked);
            Assert.Equal(BanFlags.Account, res1.MatchedFlag);

            // Новый аккаунт с того же железа/IP — НЕ блокируется при StandardBan
            var res2 = service.CheckConnection(99, "192.168.1.50", "SC_12345", "HWID_AAA", "00:11:22:33:44:55", HwidPolicyMode.Strict);
            Assert.False(res2.IsBlocked);
        }

        [Fact]
        public void HardwareBan_StrictPolicyBlocksAnyAccountFromSameHwid()
        {
            var service = new MultiTierBanService();
            service.CreateBan(
                accountId: 50,
                username: "Cheater50",
                ip: "10.0.0.5",
                socialClubId: "SC_9999",
                hwidHash: "HWID_CHEATER_PC",
                macAddress: "AA:BB:CC:DD:EE:FF",
                tier: BanTier.HardwareBan,
                adminUsername: "ChiefAdmin",
                reason: "Speedhack + Aim",
                durationDays: 0); // Permanent

            // Попытка войти с совершенно нового аккаунта (accountId: 777) на том же ПК
            var res = service.CheckConnection(777, "10.0.0.99", "SC_NEW", "HWID_CHEATER_PC", "AA:BB:CC:DD:EE:FF", HwidPolicyMode.Strict);
            Assert.True(res.IsBlocked);
            Assert.Equal(BanFlags.Hwid, res.MatchedFlag);
            Assert.Contains("Обход блокировки", res.Reason);
        }

        [Fact]
        public void HardwareBan_LenientPolicyAllowsConnectionWithAdminAlert()
        {
            var service = new MultiTierBanService();
            service.CreateBan(
                accountId: 60,
                username: "Cheater60",
                ip: "10.0.0.6",
                socialClubId: "SC_6666",
                hwidHash: "HWID_LENIENT_TEST",
                macAddress: "11:22:33:44:55:66",
                tier: BanTier.HardwareBan,
                adminUsername: "ChiefAdmin",
                reason: "RVO violation",
                durationDays: 30);

            // В Lenient-режиме (сервер с дефицитом онлайна) вход разрешён, но администрация оповещена
            var res = service.CheckConnection(888, "10.0.0.7", "SC_NEW_ACC", "HWID_LENIENT_TEST", "11:22:33:44:55:66", HwidPolicyMode.Lenient);
            Assert.False(res.IsBlocked);
            Assert.True(res.ShouldAlertAdmins);
            Assert.Equal(BanFlags.Hwid, res.MatchedFlag);
        }

        [Fact]
        public void HardBan_CoversAllFlagsSimultaneously()
        {
            var service = new MultiTierBanService();
            var ban = service.CreateBan(
                accountId: 70,
                username: "SuperToxic",
                ip: "85.10.20.30",
                socialClubId: "SC_SUPER_TOXIC",
                hwidHash: "HWID_HARD_PC",
                macAddress: "FF:EE:DD:CC:BB:AA",
                tier: BanTier.HardBan,
                adminUsername: "Owner",
                reason: "Selling server currency / Doxxing",
                durationDays: 365);

            Assert.True(ban.Flags.HasFlag(BanFlags.Account));
            Assert.True(ban.Flags.HasFlag(BanFlags.Ip));
            Assert.True(ban.Flags.HasFlag(BanFlags.SocialClub));
            Assert.True(ban.Flags.HasFlag(BanFlags.Hwid));
            Assert.True(ban.Flags.HasFlag(BanFlags.Mac));
            Assert.True(ban.Flags.HasFlag(BanFlags.Subnet));
        }

        [Fact]
        public void Unban_DeactivatesBanByUsernameOrHwid()
        {
            var service = new MultiTierBanService();
            service.CreateBan(
                accountId: 80,
                username: "ReformedPlayer",
                ip: "192.168.1.80",
                socialClubId: "SC_8080",
                hwidHash: "HWID_REFORMED",
                macAddress: "AA:11:BB:22:CC:33",
                tier: BanTier.HardwareBan,
                adminUsername: "Admin",
                reason: "Temp ban",
                durationDays: 14);

            int unbannedCount = service.Unban("ReformedPlayer");
            Assert.Equal(1, unbannedCount);

            var res = service.CheckConnection(80, "192.168.1.80", "SC_8080", "HWID_REFORMED", "AA:11:BB:22:CC:33", HwidPolicyMode.Strict);
            Assert.False(res.IsBlocked);
        }
    }
}
