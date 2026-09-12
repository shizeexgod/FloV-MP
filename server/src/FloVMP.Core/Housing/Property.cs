using System.Collections.Generic;
using FloVMP.Core.AntiCheat;

namespace FloVMP.Core.Housing;

/// <summary>
/// Объект жилой недвижимости или гаража в FloV:MP.
/// </summary>
public sealed class Property
{
    public int Id { get; set; }
    public int? OwnerAccountId { get; set; }
    public string Address { get; set; } = string.Empty;
    public PropertyType Type { get; set; }
    public long Price { get; set; }
    public Vector3D EntrancePosition { get; set; }
    public Vector3D InteriorPosition { get; set; }
    public int Dimension { get; set; }
    public bool IsLocked { get; set; } = true;
    public long SafeCash { get; set; }
    public HashSet<int> Roommates { get; set; } = new();

    public bool HasOwner => OwnerAccountId.HasValue;

    public Property() { }

    public Property(
        int id,
        string address,
        PropertyType type,
        long price,
        Vector3D entrance,
        Vector3D interior,
        int dimension)
    {
        Id = id;
        Address = address;
        Type = type;
        Price = price;
        EntrancePosition = entrance;
        InteriorPosition = interior;
        Dimension = dimension;
        IsLocked = true;
    }

    public bool HasAccess(int accountId)
    {
        if (OwnerAccountId == accountId) return true;
        lock (Roommates)
        {
            return Roommates.Contains(accountId);
        }
    }
}
