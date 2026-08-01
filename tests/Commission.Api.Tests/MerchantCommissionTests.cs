using Commission.Api.Domains.MerchantCommissions;
using Commission.Api.Domains.MerchantCommissions.Features.Queries;
using Commission.Api.Domains.SharedKernel;
using Xunit;

namespace Commission.Api.Tests;

public class MerchantCommissionTests
{
    private static Criteria SampleCriteria() =>
        Criteria.Create(CardBrand.VISA, CardType.CREDIT, TransactionRegion.DOMESTIC, 6).Data!;

    [Fact]
    public void Create_gecerli_girdi_Ok()
    {
        var result = MerchantCommission.Create(Guid.NewGuid(), SampleCriteria(), 2.40m);

        Assert.True(result.IsSuccess);
        Assert.Equal(2.40m, result.Data!.Rate);
        Assert.Equal(SampleCriteria(), result.Data!.Criteria);
    }

    [Fact]
    public void Create_rate_sifir_veya_negatif_Error()
    {
        foreach (var rate in new[] { 0m, -1.5m })
        {
            var result = MerchantCommission.Create(Guid.NewGuid(), SampleCriteria(), rate);

            Assert.False(result.IsSuccess);
            Assert.Contains(result.Messages!, m =>
                m.Property == nameof(MerchantCommission.Rate) &&
                m.Code == CommonResourceConstants.COMMON_MESSAGE_INVALID_RANGE);
        }
    }

    [Fact]
    public void Create_bos_merchantId_Error()
    {
        var result = MerchantCommission.Create(Guid.Empty, SampleCriteria(), 2.40m);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Messages!, m =>
            m.Property == nameof(MerchantCommission.MerchantId) &&
            m.Code == CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED);
    }

    [Fact]
    public void Create_null_criteria_Error()
    {
        var result = MerchantCommission.Create(Guid.NewGuid(), null!, 2.40m);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Messages!, m =>
            m.Property == nameof(MerchantCommission.Criteria) &&
            m.Code == CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED);
    }

    [Fact]
    public void UpdateRate_gecerli_gunceller()
    {
        var mc = MerchantCommission.Create(Guid.NewGuid(), SampleCriteria(), 2.40m).Data!;

        Assert.True(mc.UpdateRate(2.60m).IsSuccess);
        Assert.Equal(2.60m, mc.Rate);
    }

    [Fact]
    public void UpdateRate_sifir_veya_negatif_Error_oran_degismez()
    {
        var mc = MerchantCommission.Create(Guid.NewGuid(), SampleCriteria(), 2.40m).Data!;

        Assert.False(mc.UpdateRate(0m).IsSuccess);
        Assert.False(mc.UpdateRate(-3m).IsSuccess);
        Assert.Equal(2.40m, mc.Rate);
    }
}

public class MerchantCommissionCeilingTests
{
    [Theory]
    [InlineData(2.95, 2.95, true)]   // rate == bankMax → tavan-altı
    [InlineData(2.50, 2.95, true)]   // rate < bankMax → tavan-altı
    [InlineData(3.20, 2.95, false)]  // rate > bankMax → üstünde
    public void ComputeBelowBankCeiling_banka_varken(decimal rate, decimal bankMax, bool expected)
    {
        Assert.Equal(expected, GetMerchantCommissions.ComputeBelowBankCeiling(rate, bankMax));
    }

    [Fact]
    public void ComputeBelowBankCeiling_banka_yoksa_false()
    {
        Assert.False(GetMerchantCommissions.ComputeBelowBankCeiling(2.50m, null));
    }

    [Fact]
    public void ComputeBelowBankCeiling_merchant_orani_yoksa_false()
    {
        Assert.False(GetMerchantCommissions.ComputeBelowBankCeiling(null, 2.95m));
    }
}