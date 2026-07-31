using Commission.Api.Domains.BankCommissions;
using Commission.Api.Domains.MerchantCommissions;
using Commission.Api.Domains.SharedKernel;
using Xunit;

namespace Commission.Api.Tests;

public class MerchantCommissionTests
{
    private static BankCommission Bank(decimal rate = 1.75m) =>
        BankCommission.Create("0062",
            Criteria.Create(CardBrand.VISA, CardType.CREDIT, TransactionRegion.DOMESTIC, 6).Data!,
            rate).Data!;

    [Fact]
    public void Create_rate_bankRate_ustunde_Ok()
    {
        var bank = Bank(1.75m);

        var result = MerchantCommission.Create(Guid.NewGuid(), bank, 2.40m);

        Assert.True(result.IsSuccess);
        Assert.Equal(2.40m, result.Data!.Rate);
    }

    [Fact]
    public void Create_rate_bankRate_ile_esit_Error()
    {
        var bank = Bank(1.75m);

        var result = MerchantCommission.Create(Guid.NewGuid(), bank, 1.75m);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Messages!, m =>
            m.Code == CommissionResourceConstants.MERCHANT_RATE_MUST_EXCEED_BANK_RATE);
    }

    [Fact]
    public void Create_rate_bankRate_altinda_Error()
    {
        var bank = Bank(1.75m);

        var result = MerchantCommission.Create(Guid.NewGuid(), bank, 1.50m);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Messages!, m =>
            m.Code == CommissionResourceConstants.MERCHANT_RATE_MUST_EXCEED_BANK_RATE);
    }

    [Fact]
    public void Create_bos_merchantId_Error()
    {
        var bank = Bank(1.75m);

        var result = MerchantCommission.Create(Guid.Empty, bank, 2.40m);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Messages!, m =>
            m.Property == nameof(MerchantCommission.MerchantId) &&
            m.Code == CommonResourceConstants.COMMON_MESSAGE_VALUE_IS_REQUIRED);
    }

    [Fact]
    public void Create_snapshot_bankadan_kopyalanir()
    {
        var bank = Bank(1.75m);

        var mc = MerchantCommission.Create(Guid.NewGuid(), bank, 2.40m).Data!;

        Assert.Equal(bank.Id, mc.BankCommissionId);
        Assert.Equal(bank.BankCode, mc.BankCode);
        Assert.Equal(bank.Criteria, mc.Criteria);
    }

    [Fact]
    public void UpdateRate_ayni_invariant()
    {
        var bank = Bank(1.75m);
        var mc = MerchantCommission.Create(Guid.NewGuid(), bank, 2.40m).Data!;

        Assert.True(mc.UpdateRate(2.60m, bank).IsSuccess);
        Assert.Equal(2.60m, mc.Rate);

        Assert.False(mc.UpdateRate(1.75m, bank).IsSuccess);
        Assert.False(mc.UpdateRate(1.50m, bank).IsSuccess);
        // Reddedilen güncellemeler oranı değiştirmez.
        Assert.Equal(2.60m, mc.Rate);
    }
}