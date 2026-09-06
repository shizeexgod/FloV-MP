using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace FloVMP.Core.Security
{
    public enum HwidPolicyMode
    {
        Strict,
        Lenient,
        Disabled
    }

    public record PlayerIdentification(
        string FlovId,
        string HwidHash,
        string IpAddress,
        string SocialClubName
    );

    public record PolicyCheckResult(
        bool Allowed,
        string Reason,
        bool Flagged,
        string AuditTag
    );

    /// <summary>
    /// FloV:ID & HWID Policy Service.
    /// Allows server/project owners to customize how HWID bans, multi-accounts,
    /// and reconnects are handled (Strict, Lenient, or Disabled).
    /// </summary>
    public class FlovIdPolicyService
    {
        private readonly ConcurrentDictionary<string, string> _bannedHwids = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, HashSet<string>> _hwidToAccounts = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _lock = new();

        public HwidPolicyMode PolicyMode { get; private set; } = HwidPolicyMode.Lenient;
        public bool AllowVpn { get; private set; } = true;
        public int MaxAccountsPerHwid { get; private set; } = 3;

        public void ConfigurePolicy(HwidPolicyMode mode, bool allowVpn, int maxAccountsPerHwid)
        {
            PolicyMode = mode;
            AllowVpn = allowVpn;
            MaxAccountsPerHwid = Math.Max(1, maxAccountsPerHwid);
        }

        public void BanHwid(string hwidHash, string reason)
        {
            if (string.IsNullOrWhiteSpace(hwidHash)) return;
            _bannedHwids[hwidHash.Trim()] = reason ?? "Banned by administrator";
        }

        public bool UnbanHwid(string hwidHash)
        {
            if (string.IsNullOrWhiteSpace(hwidHash)) return false;
            return _bannedHwids.TryRemove(hwidHash.Trim(), out _);
        }

        public bool IsHwidBanned(string hwidHash)
        {
            if (string.IsNullOrWhiteSpace(hwidHash)) return false;
            return _bannedHwids.ContainsKey(hwidHash.Trim());
        }

        public PolicyCheckResult EvaluateConnection(PlayerIdentification player)
        {
            if (player == null)
            {
                return new PolicyCheckResult(false, "Invalid player credentials", false, "NullPayload");
            }

            string cleanHwid = player.HwidHash?.Trim() ?? string.Empty;
            string cleanId = player.FlovId?.Trim() ?? string.Empty;

            // 1. Check if HWID is in banned list
            if (!string.IsNullOrEmpty(cleanHwid) && _bannedHwids.TryGetValue(cleanHwid, out var banReason))
            {
                switch (PolicyMode)
                {
                    case HwidPolicyMode.Strict:
                        return new PolicyCheckResult(false, $"Connection rejected: HWID banned ({banReason})", true, "HWID_BAN_STRICT");
                    case HwidPolicyMode.Lenient:
                        return new PolicyCheckResult(true, "Allowed under lenient policy (flagged for review)", true, "HWID_BAN_LENIENT_BYPASS");
                    case HwidPolicyMode.Disabled:
                        return new PolicyCheckResult(true, "Allowed (HWID ban policy disabled by project)", false, "HWID_BAN_DISABLED");
                }
            }

            // 2. Check Multi-Account Threshold
            if (!string.IsNullOrEmpty(cleanHwid) && !string.IsNullOrEmpty(cleanId))
            {
                lock (_lock)
                {
                    if (!_hwidToAccounts.TryGetValue(cleanHwid, out var accounts))
                    {
                        accounts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        _hwidToAccounts[cleanHwid] = accounts;
                    }

                    if (!accounts.Contains(cleanId) && accounts.Count >= MaxAccountsPerHwid)
                    {
                        switch (PolicyMode)
                        {
                            case HwidPolicyMode.Strict:
                                return new PolicyCheckResult(false, $"Connection rejected: Exceeded limit of {MaxAccountsPerHwid} accounts per hardware ID", true, "MULTI_ACCOUNT_LIMIT_EXCEEDED");
                            case HwidPolicyMode.Lenient:
                                accounts.Add(cleanId);
                                return new PolicyCheckResult(true, "Allowed under lenient policy (multi-account limit exceeded)", true, "MULTI_ACCOUNT_WARNING");
                            case HwidPolicyMode.Disabled:
                                accounts.Add(cleanId);
                                return new PolicyCheckResult(true, "Allowed (multi-account limit disabled)", false, "MULTI_ACCOUNT_DISABLED");
                        }
                    }

                    accounts.Add(cleanId);
                }
            }

            return new PolicyCheckResult(true, "Connection approved", false, "OK");
        }

        public int GetAssociatedAccountsCount(string hwidHash)
        {
            if (string.IsNullOrWhiteSpace(hwidHash)) return 0;
            if (_hwidToAccounts.TryGetValue(hwidHash.Trim(), out var accounts))
            {
                lock (_lock)
                {
                    return accounts.Count;
                }
            }
            return 0;
        }
    }
}
