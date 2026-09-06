namespace FloVMP.Core.Factions;

/// <summary>
/// Должность / ранг внутри организации со своей зарплатой и правами доступа.
/// </summary>
public sealed class FactionRank
{
    public int Level { get; set; }
    public string Name { get; set; } = string.Empty;
    public long Salary { get; set; }
    public FactionPermissions Permissions { get; set; }

    public FactionRank() { }

    public FactionRank(int level, string name, long salary, FactionPermissions permissions)
    {
        Level = level;
        Name = name;
        Salary = salary;
        Permissions = permissions;
    }
}
