using System;

namespace FloVMP.Core.Factions;

/// <summary>
/// Запись о членстве игрока во фракции.
/// </summary>
public sealed class FactionMember
{
    public int AccountId { get; set; }
    public int FactionId { get; set; }
    public int RankLevel { get; set; }
    public string CustomTag { get; set; } = string.Empty;
    public DateTime JoinDateUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastActiveUtc { get; set; } = DateTime.UtcNow;

    public FactionMember() { }

    public FactionMember(int accountId, int factionId, int rankLevel, string customTag = "")
    {
        AccountId = accountId;
        FactionId = factionId;
        RankLevel = rankLevel;
        CustomTag = customTag;
        JoinDateUtc = DateTime.UtcNow;
        LastActiveUtc = DateTime.UtcNow;
    }
}
