using Commission.Api.Domains.CommissionPolicies.ValueObjects;
using Xunit;

namespace Commission.Api.Tests;

public class MarginRuleTests
{
    [Fact]
    public void Create_GecerliDeger_Basarili()
    {
        var result = MarginRule.Create(0.015m, 0.50m);

        Assert.True(result.IsSuccess);
        Assert.Equal(0.015m, result.Data!.RatePercent);
        Assert.Equal(0.50m, result.Data!.FixedFee);
    }

    [Fact]
    public void Create_NegatifOran_Reddedilir()
    {
        var result = MarginRule.Create(-0.01m, 0m);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Create_NegatifUcret_Reddedilir()
    {
        var result = MarginRule.Create(0.01m, -1m);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Create_OranUstSiniriAsar_Reddedilir()
    {
        var result = MarginRule.Create(MarginRule.MaxRatePercent + 0.01m, 0m);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Create_UcretUstSiniriAsar_Reddedilir()
    {
        var result = MarginRule.Create(0.01m, MarginRule.MaxFixedFee + 1m);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Create_SinirDegerleri_Basarili()
    {
        Assert.True(MarginRule.Create(0m, 0m).IsSuccess);
        Assert.True(MarginRule.Create(MarginRule.MaxRatePercent, MarginRule.MaxFixedFee).IsSuccess);
    }
}
