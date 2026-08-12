namespace Commission.Api.Tests;

/// <summary>
/// GetMerchantCommissions read-time tavan işareti (saf yardımcı). 019: MerchantCommissionGrid /
/// GridStatus testleri SÖKÜLDÜ (FR-013) — kalan tek grid-bağımsız saf hesap bu.
/// </summary>
public class ComputeBelowBankCeilingTests
{
    [Theory]
    [InlineData(2.0, 3.0, true)]   // rate <= ceiling → below ceiling (geçerli)
    [InlineData(3.0, 3.0, true)]   // eşit → geçerli
    [InlineData(4.0, 3.0, false)]  // tavan aşımı → ihlal
    public void ComputeBelowBankCeiling_dogru(decimal rate, decimal bankMax, bool expected)
    {
        Assert.Equal(expected, GetMerchantCommissions.ComputeBelowBankCeiling(rate, bankMax));
    }

    [Fact]
    public void ComputeBelowBankCeiling_rate_yoksa_false()
    {
        Assert.False(GetMerchantCommissions.ComputeBelowBankCeiling(null, 3.0m));
    }
}
