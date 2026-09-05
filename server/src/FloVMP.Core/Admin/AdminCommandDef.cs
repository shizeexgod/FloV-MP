namespace FloVMP.Core.Admin;

/// <summary>
/// Определение команды администратора.
/// </summary>
public sealed record AdminCommandDef(
    string Name,
    int MinLevel,
    string Usage,
    string Description);
