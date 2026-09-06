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
    public void UniformRegistry_AppliesRankSpecificClothing()
    {
        var service = new FactionUniformService();
        service.RegisterUniform(2, (app, rank) =>
        {
            if (rank >= 6)
            {
                app.SetCloth(11, 26, 0); // Senior jacket
            }
            else
            {
                app.SetCloth(11, 55, 0); // Patrol shirt
                app.SetCloth(9, 10, 0);  // Vest
                app.SetProp(0, 46, 0);   // Cap
            }
        });

        var app = CharacterAppearance.CreateDefaultMale();

        // Patrol officer (Rank 2)
        Assert.True(service.ApplyUniform(app, factionId: 2, rankLevel: 2));
        Assert.Equal(55, app.Clothes[11].Drawable);
        Assert.Equal(10, app.Clothes[9].Drawable);
        Assert.True(app.Props.ContainsKey(0));

        // Senior officer (Rank 7)
        Assert.True(service.ApplyUniform(app, factionId: 2, rankLevel: 7));
        Assert.Equal(26, app.Clothes[11].Drawable);
    }

    [Fact]
    public void UniformRegistry_UnregisteredFaction_ReturnsFalse()
    {
        var service = new FactionUniformService();
        var app = CharacterAppearance.CreateDefaultMale();
        Assert.False(service.ApplyUniform(app, factionId: 999, rankLevel: 1));
        Assert.False(service.HasUniform(999));
    }

    [Fact]
    public void UniformRegistry_UnregisterAndClear_Work()
    {
        var service = new FactionUniformService();
        service.RegisterUniform(1, (app, rank) => app.SetCloth(11, 10, 0));
        Assert.True(service.HasUniform(1));

        Assert.True(service.UnregisterUniform(1));
        Assert.False(service.HasUniform(1));

        service.RegisterUniform(1, (app, rank) => app.SetCloth(11, 10, 0));
        service.RegisterUniform(2, (app, rank) => app.SetCloth(11, 20, 0));
        service.Clear();
        Assert.False(service.HasUniform(1));
        Assert.False(service.HasUniform(2));
    }
}
