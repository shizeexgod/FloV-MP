using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FloVMP.Core.Characters;

/// <summary>
/// Полные визуальные параметры персонажа (кастомизация лица, волос, черт и одежды).
/// </summary>
public sealed class CharacterAppearance
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public int Gender { get; set; } // 0 = Male, 1 = Female
    public HeadBlendData HeadBlend { get; set; } = new(0, 0, 0, 0);
    public float[] FaceFeatures { get; set; } = new float[20];
    public Dictionary<int, HeadOverlayData> HeadOverlays { get; set; } = new();
    public int HairModel { get; set; }
    public int HairColor { get; set; }
    public int HairHighlightColor { get; set; }
    public int EyeColor { get; set; }

    public Dictionary<int, ClothingComponent> Clothes { get; set; } = new();
    public Dictionary<int, PropComponent> Props { get; set; } = new();

    public void SetCloth(int componentId, int drawable, int texture, int palette = 0)
    {
        Clothes[componentId] = new ClothingComponent(componentId, drawable, texture, palette);
    }

    public void SetProp(int propId, int drawable, int texture)
    {
        Props[propId] = new PropComponent(propId, drawable, texture);
    }

    public void ClearProp(int propId)
    {
        Props.Remove(propId);
    }

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static CharacterAppearance? FromJson(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<CharacterAppearance>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public static CharacterAppearance CreateDefaultMale()
    {
        var app = new CharacterAppearance
        {
            Gender = 0,
            HeadBlend = new HeadBlendData(0, 0, 0, 0, 0.5f, 0.5f),
            HairModel = 1,
            HairColor = 0,
            EyeColor = 0
        };

        // Базовая городская одежда (футболка, джинсы, кеды)
        app.SetCloth(11, 15, 0); // Верх (рубашка/футболка)
        app.SetCloth(3, 15, 0);  // Торс (руки)
        app.SetCloth(8, 15, 0);  // Майка
        app.SetCloth(4, 1, 0);   // Джинсы
        app.SetCloth(6, 1, 0);   // Обувь
        return app;
    }

    public static CharacterAppearance CreateDefaultFemale()
    {
        var app = new CharacterAppearance
        {
            Gender = 1,
            HeadBlend = new HeadBlendData(21, 21, 21, 21, 0.5f, 0.5f),
            HairModel = 4,
            HairColor = 0,
            EyeColor = 0
        };

        // Базовая городская одежда (женская)
        app.SetCloth(11, 5, 0);
        app.SetCloth(3, 15, 0);
        app.SetCloth(8, 14, 0);
        app.SetCloth(4, 3, 0);
        app.SetCloth(6, 3, 0);
        return app;
    }
}
