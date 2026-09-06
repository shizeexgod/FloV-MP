using FloVMP.Core.Characters;
using Xunit;

namespace FloVMP.Core.Tests;

public class CharacterTests
{
    [Fact]
    public void DefaultAppearances_AreValid()
    {
        var male = CharacterAppearance.CreateDefaultMale();
        Assert.Equal(0, male.Gender);
        Assert.NotNull(male.HeadBlend);
        Assert.True(male.Clothes.ContainsKey(11)); // Верх
        Assert.True(male.Clothes.ContainsKey(4));  // Брюки

        var female = CharacterAppearance.CreateDefaultFemale();
        Assert.Equal(1, female.Gender);
        Assert.True(female.Clothes.ContainsKey(11));
    }

    [Fact]
    public void Appearance_SerializationRoundTrip()
    {
        var original = CharacterAppearance.CreateDefaultMale();
        original.HairModel = 5;
        original.HairColor = 2;
        original.EyeColor = 3;
        original.FaceFeatures[0] = 0.75f; // Nose width

        var json = original.ToJson();
        Assert.NotEmpty(json);

        var restored = CharacterAppearance.FromJson(json);
        Assert.NotNull(restored);
        Assert.Equal(original.Gender, restored.Gender);
        Assert.Equal(5, restored.HairModel);
        Assert.Equal(2, restored.HairColor);
        Assert.Equal(0.75f, restored.FaceFeatures[0]);
    }

    [Fact]
    public void PoliceUniform_AppliesRankSpecificClothing()
    {
        var app = CharacterAppearance.CreateDefaultMale();

        // Patrol officer (Rank 2)
        FactionUniformService.ApplyUniform(app, factionId: 2, rankLevel: 2);
        Assert.Equal(55, app.Clothes[11].Drawable); // Полицейская рубашка ППСП
        Assert.Equal(10, app.Clothes[9].Drawable);  // Бронежилет МВД
        Assert.True(app.Props.ContainsKey(0));      // Фуражка

        // Senior officer (Rank 7)
        FactionUniformService.ApplyUniform(app, factionId: 2, rankLevel: 7);
        Assert.Equal(26, app.Clothes[11].Drawable); // Парадный китель
    }

    [Fact]
    public void FsbUniform_AppliesTacticalGear()
    {
        var app = CharacterAppearance.CreateDefaultMale();

        FactionUniformService.ApplyUniform(app, factionId: 3, rankLevel: 3);
        Assert.Equal(53, app.Clothes[11].Drawable); // Спецназ ФСБ
        Assert.Equal(52, app.Clothes[1].Drawable);  // Балаклава
        Assert.Equal(39, app.Props[0].Drawable);    // Шлем
    }

    [Fact]
    public void HospitalUniform_RemovesHeadwear()
    {
        var app = CharacterAppearance.CreateDefaultMale();
        app.SetProp(0, 10, 0); // Hat

        FactionUniformService.ApplyUniform(app, factionId: 4, rankLevel: 3);
        Assert.Equal(249, app.Clothes[11].Drawable); // Медицинский халат
        Assert.False(app.Props.ContainsKey(0));       // Головной убор снят
    }
}
