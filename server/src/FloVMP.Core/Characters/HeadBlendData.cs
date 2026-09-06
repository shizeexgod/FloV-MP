namespace FloVMP.Core.Characters;

/// <summary>
/// Генетические параметры смешивания черт лица и цвета кожи (GTA V Head Blend).
/// </summary>
public sealed class HeadBlendData
{
    public int ShapeFirstID { get; set; }
    public int ShapeSecondID { get; set; }
    public int SkinFirstID { get; set; }
    public int SkinSecondID { get; set; }
    public float ShapeMix { get; set; } = 0.5f;
    public float SkinMix { get; set; } = 0.5f;

    public HeadBlendData() { }

    public HeadBlendData(int shapeFirst, int shapeSecond, int skinFirst, int skinSecond, float shapeMix = 0.5f, float skinMix = 0.5f)
    {
        ShapeFirstID = shapeFirst;
        ShapeSecondID = shapeSecond;
        SkinFirstID = skinFirst;
        SkinSecondID = skinSecond;
        ShapeMix = shapeMix;
        SkinMix = skinMix;
    }
}
