namespace FloVMP.Core.Characters;

/// <summary>
/// Элемент одежды или аксессуара персонажа.
/// </summary>
public sealed class ClothingComponent
{
    public int ComponentId { get; set; }
    public int Drawable { get; set; }
    public int Texture { get; set; }
    public int Palette { get; set; }

    public ClothingComponent() { }

    public ClothingComponent(int componentId, int drawable, int texture, int palette = 0)
    {
        ComponentId = componentId;
        Drawable = drawable;
        Texture = texture;
        Palette = palette;
    }
}

/// <summary>
/// Пропсы персонажа (головной убор, очки, часы, браслеты).
/// </summary>
public sealed class PropComponent
{
    public int PropId { get; set; }
    public int Drawable { get; set; }
    public int Texture { get; set; }

    public PropComponent() { }

    public PropComponent(int propId, int drawable, int texture)
    {
        PropId = propId;
        Drawable = drawable;
        Texture = texture;
    }
}
