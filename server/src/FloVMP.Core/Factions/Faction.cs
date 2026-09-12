using System.Collections.Generic;

namespace FloVMP.Core.Factions;

/// <summary>
/// Описание фракции (организации) в FloV:MP.
/// </summary>
public sealed class Faction
{
    public int Id { get; set; }
    public string Tag { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public FactionType Type { get; set; }
    public long TreasuryBalance { get; set; }
    public int LeaderAccountId { get; set; }
    public Dictionary<int, FactionRank> Ranks { get; set; } = new();

    public bool IsGovernment => Type is FactionType.Government
        or FactionType.Police
        or FactionType.SecurityService
        or FactionType.Army
        or FactionType.Hospital;

    public bool IsCrime => Type is FactionType.Mafia or FactionType.Gang;

    public Faction() { }

    public Faction(int id, string tag, string name, FactionType type, long initialTreasury = 0)
    {
        Id = id;
        Tag = tag;
        Name = name;
        Type = type;
        TreasuryBalance = initialTreasury;
    }

    public FactionRank? GetRank(int level) =>
        Ranks.TryGetValue(level, out var rank) ? rank : null;

    public void AddRank(int level, string name, long salary, FactionPermissions permissions)
    {
        Ranks[level] = new FactionRank(level, name, salary, permissions);
    }
}
