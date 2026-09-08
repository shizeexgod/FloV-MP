namespace FloVMP.Core.Items;

/// <summary>Описание типа предмета (справочник, не экземпляр).</summary>
public sealed record ItemDef(
    string Id,
    string Name,
    double Weight,
    int MaxStack)
{
    public bool Stackable => MaxStack > 1;
}

/// <summary>
/// Справочник предметов. Каркас Фазы 3 — минимальный набор; позже грузится
/// из данных/БД. Id — стабильный ключ (в сети и в хранилище).
/// </summary>
public static class ItemCatalog
{
    private static readonly Dictionary<string, ItemDef> Defs = new[]
    {
        new ItemDef("water",      "Вода",                0.5,  10),
        new ItemDef("bread",      "Хлеб",                0.3,  10),
        new ItemDef("phone",      "Телефон",             0.2,  1),
        new ItemDef("bandage",    "Бинт",                0.1,  20),
        new ItemDef("medkit",     "Большая аптечка",     0.8,  5),
        new ItemDef("repairkit",  "Ремкомплект авто",    3.0,  3),
        new ItemDef("fuelcan",    "Канистра с бензином", 4.0,  2),
        new ItemDef("cash",       "Наличные",            0.0,  1_000_000),
        new ItemDef("pistol",     "Пистолет",            1.1,  1),
        new ItemDef("ammo9",      "Патроны 9мм",         0.01, 250),
    }.ToDictionary(d => d.Id);

    public static IReadOnlyCollection<ItemDef> All => Defs.Values;

    public static ItemDef? Get(string id) => Defs.GetValueOrDefault(id);

    public static bool Exists(string id) => Defs.ContainsKey(id);
}
