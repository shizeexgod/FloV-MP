namespace FloVMP.Core.Characters;

/// <summary>
/// Параметры наложения на лицо (борода, брови, макияж, дефекты кожи).
/// </summary>
public sealed class HeadOverlayData
{
    public int Index { get; set; } = 255; // 255 = отключено
    public float Opacity { get; set; } = 1.0f;
    public int ColorID { get; set; }
    public int SecondColorID { get; set; }

    public HeadOverlayData() { }

    public HeadOverlayData(int index, float opacity, int colorId = 0, int secondColorId = 0)
    {
        Index = index;
        Opacity = opacity;
        ColorID = colorId;
        SecondColorID = secondColorId;
    }
}
