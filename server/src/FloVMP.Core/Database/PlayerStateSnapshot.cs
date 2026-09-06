namespace FloVMP.Core.Database;

/// <summary>
/// Точечный снимок состояния персонажа (Time-Machine Snapshot) для точечного отката при багах, дюпах или крашах.
/// </summary>
public sealed class PlayerStateSnapshot
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N")[..12];
    public ulong PlayerId { get; init; }
    public string CharacterName { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    public string Reason { get; init; } = "Periodic";

    // Экономика
    public long BankBalance { get; init; }
    public long CashBalance { get; init; }
    public long DirtyCashBalance { get; init; }

    // Пространственное положение
    public int Dimension { get; init; }
    public float PositionX { get; init; }
    public float PositionY { get; init; }
    public float PositionZ { get; init; }
    public float Heading { get; init; }

    // Состояние персонажа
    public int Health { get; init; } = 100;
    public int Armor { get; init; } = 0;

    // Инвентарь и метаданные
    public string InventoryJson { get; init; } = "[]";
    public IReadOnlyList<string> Licenses { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> OwnedVehicles { get; init; } = Array.Empty<string>();

    public override string ToString() =>
        $"[Snapshot {Id}] Player={PlayerId} ({CharacterName}) Reason='{Reason}' Bank={BankBalance:N0} Cash={CashBalance:N0} Dim={Dimension} Time={CreatedAtUtc:HH:mm:ss}";
}
