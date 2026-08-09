
namespace Merchant.Api.Tests;

public class ReferenceKeyTests
{
    [Theory]
    [InlineData("tr", "TR")]
    [InlineData("  tr  ", "TR")]
    [InlineData("TR", "TR")]
    public void Country_normalize_upper_trim(string input, string expected)
    {
        Assert.Equal(expected, ReferenceKey.Country(input));
    }

    [Fact]
    public void Country_null_bos_string()
    {
        Assert.Equal(string.Empty, ReferenceKey.Country(null));
    }
}