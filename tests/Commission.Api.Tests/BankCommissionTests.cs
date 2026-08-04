using Commission.Api.Domains.BankCommissions;
using Commission.Api.Domains.SharedKernel;
using Xunit;

namespace Commission.Api.Tests;

public class BankCommissionTests
{
    private static Criteria ValidCriteria() =>
        Criteria.Create(CardBrand.Visa, CardType.Credit, TransactionRegion.DOMESTIC, 6).Data!;

    [Fact]
    public void Create_gecerli_Ok()
    {
        var result = BankCommission.Create("0062", ValidCriteria(), 1.75m);

        Assert.True(result.IsSuccess);
        Assert.Equal("0062", result.Data!.BankCode);
        Assert.Equal(1.75m, result.Data.Rate);
    }

    [Theory]
    [InlineData("062")]
    [InlineData("00622")]
    [InlineData("")]
    public void Create_bankCode_4_hane_degil_Error(string bankCode)
    {
        var result = BankCommission.Create(bankCode, ValidCriteria(), 1.75m);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Messages!, m =>
            m.Property == nameof(BankCommission.BankCode) &&
            m.Code == CommonResourceConstants.COMMON_MESSAGE_INVALID_FORMAT);
    }

    [Fact]
    public void Create_negatif_rate_Error()
    {
        var result = BankCommission.Create("0062", ValidCriteria(), -0.01m);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Messages!, m =>
            m.Property == nameof(BankCommission.Rate) &&
            m.Code == CommonResourceConstants.COMMON_MESSAGE_INVALID_RANGE);
    }

    [Fact]
    public void Criteria_taksit_sifir_Error()
    {
        var result = Criteria.Create(CardBrand.Visa, CardType.Credit, TransactionRegion.DOMESTIC, 0);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Messages!, m => m.Code == CommonResourceConstants.COMMON_MESSAGE_INVALID_RANGE);
    }

    [Fact]
    public void UpdateRate_negatif_Error_gecerli_Ok()
    {
        var bank = BankCommission.Create("0062", ValidCriteria(), 1.75m).Data!;

        Assert.False(bank.UpdateRate(-1m).IsSuccess);

        Assert.True(bank.UpdateRate(2.10m).IsSuccess);
        Assert.Equal(2.10m, bank.Rate);
    }
}