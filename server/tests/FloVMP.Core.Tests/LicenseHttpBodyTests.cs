using System.Text;
using FloVMP.LicenseAuthority;
using Xunit;

namespace FloVMP.Core.Tests;

public sealed class LicenseHttpBodyTests
{
    [Theory]
    [InlineData(8192, true)]
    [InlineData(8193, false)]
    [InlineData(20000, false)]
    public void Body_limit_applies_even_without_content_length(int size, bool accepted)
    {
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(new string('a', size)));
        Assert.Equal(accepted, HttpApi.ReadBoundedBody(input) is not null);
    }

    [Fact]
    public void Invalid_utf8_is_rejected()
    {
        using var input = new MemoryStream(new byte[] { 0xC3, 0x28 });
        Assert.Throws<DecoderFallbackException>(() => HttpApi.ReadBoundedBody(input));
    }
}
