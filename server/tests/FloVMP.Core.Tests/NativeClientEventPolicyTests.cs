using FloVMP.Core.Native;
using Xunit;

namespace FloVMP.Core.Tests;

public sealed class NativeClientEventPolicyTests
{
    [Fact]
    public void AcceptsRageStyleEventsAndJsonArray()
    {
        Assert.True(NativeClientEventPolicy.IsValid("hud:money", "[1000,\"рубли\"]", false));
        Assert.True(NativeClientEventPolicy.IsValid("hud:ready", "[]", true));
        Assert.True(NativeClientEventPolicy.IsValid("hud:ready", null, false));
    }

    [Theory]
    [InlineData("bad\nname", "[]")]
    [InlineData("", "[]")]
    [InlineData("ok", "{\"key\":1}")]
    [InlineData("ok", "[1,")]
    public void RejectsInvalidNamesAndJson(string name, string json)
    {
        Assert.False(NativeClientEventPolicy.IsValid(name, json, false));
    }

    [Fact]
    public void RejectsUtf8AndEscapingThatCrossWireLimit()
    {
        Assert.False(NativeClientEventPolicy.IsValid("e", "[\"" + new string('я', 2050) + "\"]", false));
        Assert.False(NativeClientEventPolicy.IsValid("e", "[\"" + new string('\\', 2050) + "\"]", false));
        Assert.True(NativeClientEventPolicy.IsValid("e", "[\"" + new string('a', 100) + "\"]", false));
    }
}
