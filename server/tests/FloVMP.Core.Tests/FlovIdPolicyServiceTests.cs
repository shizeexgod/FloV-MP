using FloVMP.Core.Security;
using Xunit;

namespace FloVMP.Core.Tests
{
    public class FlovIdPolicyServiceTests
    {
        [Fact]
        public void EvaluateConnection_WhenHwidBanned_StrictPolicyRejects()
        {
            var service = new FlovIdPolicyService();
            service.ConfigurePolicy(HwidPolicyMode.Strict, allowVpn: true, maxAccountsPerHwid: 3);
            service.BanHwid("BANNED-HWID-001", "Cheating detected");

            var player = new PlayerIdentification("FLV-100", "BANNED-HWID-001", "127.0.0.1", "Player1");
            var result = service.EvaluateConnection(player);

            Assert.False(result.Allowed);
            Assert.True(result.Flagged);
            Assert.Equal("HWID_BAN_STRICT", result.AuditTag);
            Assert.Contains("Cheating detected", result.Reason);
        }

        [Fact]
        public void EvaluateConnection_WhenHwidBanned_LenientPolicyAllowsWithWarning()
        {
            var service = new FlovIdPolicyService();
            service.ConfigurePolicy(HwidPolicyMode.Lenient, allowVpn: true, maxAccountsPerHwid: 3);
            service.BanHwid("BANNED-HWID-002", "Ban evasion test");

            var player = new PlayerIdentification("FLV-101", "BANNED-HWID-002", "127.0.0.1", "Player2");
            var result = service.EvaluateConnection(player);

            Assert.True(result.Allowed);
            Assert.True(result.Flagged);
            Assert.Equal("HWID_BAN_LENIENT_BYPASS", result.AuditTag);
        }

        [Fact]
        public void EvaluateConnection_WhenHwidBanned_DisabledPolicyAllowsCleanly()
        {
            var service = new FlovIdPolicyService();
            service.ConfigurePolicy(HwidPolicyMode.Disabled, allowVpn: true, maxAccountsPerHwid: 3);
            service.BanHwid("BANNED-HWID-003", "Old ban");

            var player = new PlayerIdentification("FLV-102", "BANNED-HWID-003", "127.0.0.1", "Player3");
            var result = service.EvaluateConnection(player);

            Assert.True(result.Allowed);
            Assert.False(result.Flagged);
            Assert.Equal("HWID_BAN_DISABLED", result.AuditTag);
        }

        [Fact]
        public void EvaluateConnection_MultiAccountThreshold_RespectedPerPolicy()
        {
            var service = new FlovIdPolicyService();
            service.ConfigurePolicy(HwidPolicyMode.Strict, allowVpn: true, maxAccountsPerHwid: 2);

            var p1 = new PlayerIdentification("ACC-1", "HWID-MULTI", "1.1.1.1", "User1");
            var p2 = new PlayerIdentification("ACC-2", "HWID-MULTI", "1.1.1.1", "User2");
            var p3 = new PlayerIdentification("ACC-3", "HWID-MULTI", "1.1.1.1", "User3");

            Assert.True(service.EvaluateConnection(p1).Allowed);
            Assert.True(service.EvaluateConnection(p2).Allowed);

            // 3rd account on HWID with max=2 should be rejected in strict mode
            var res3 = service.EvaluateConnection(p3);
            Assert.False(res3.Allowed);
            Assert.Equal("MULTI_ACCOUNT_LIMIT_EXCEEDED", res3.AuditTag);

            // Switch to Lenient -> Should allow with warning
            service.ConfigurePolicy(HwidPolicyMode.Lenient, allowVpn: true, maxAccountsPerHwid: 2);
            var res3Lenient = service.EvaluateConnection(p3);
            Assert.True(res3Lenient.Allowed);
            Assert.True(res3Lenient.Flagged);
        }
    }
}
