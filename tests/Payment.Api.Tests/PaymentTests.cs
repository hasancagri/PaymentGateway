using Payment.Api.Domains.Payments;
using PaymentAgg = Payment.Api.Domains.Payments.Payment;

namespace Payment.Api.Tests;

// 033: Payment aggregate saf testleri — çekim sonucu kaydı (iyzico'suz).
public class PaymentTests
{
    [Fact]
    public void Succeeded_GecerliAlanlar_SuccessVeAlanlarDogru()
    {
        var merchantId = Guid.NewGuid();
        var result = PaymentAgg.Succeeded(merchantId, "card_1", 100m, 106m, 3, "iyz-pay-1", "2.75", "0.25");

        Assert.True(result.IsSuccess);
        var p = result.Data!;
        Assert.Equal(PaymentStatus.Success, p.Status);
        Assert.Equal(merchantId, p.MerchantId);
        Assert.Equal("iyz-pay-1", p.ProviderPaymentId);
        Assert.Equal(106m, p.PaidPrice);
        Assert.Equal(3, p.Installment);
        Assert.Equal("2.75", p.ProviderCommission);
        Assert.Equal("0.25", p.ProviderFee);
    }

    [Fact]
    public void Succeeded_BosProviderPaymentId_Reddedilir()
    {
        var result = PaymentAgg.Succeeded(Guid.NewGuid(), "card_1", 100m, 100m, 1, "  ", "1", "0");

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Succeeded_BosMerchantId_Reddedilir()
    {
        var result = PaymentAgg.Succeeded(Guid.Empty, "card_1", 100m, 100m, 1, "iyz-1", "1", "0");

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Failed_GecerliAlanlar_FailedVeProviderIdBos()
    {
        var result = PaymentAgg.Failed(Guid.NewGuid(), "card_1", 100m, 1);

        Assert.True(result.IsSuccess);
        Assert.Equal(PaymentStatus.Failed, result.Data!.Status);
        Assert.Equal(string.Empty, result.Data!.ProviderPaymentId);
    }

    [Fact]
    public void Failed_BosVaultToken_Reddedilir()
    {
        var result = PaymentAgg.Failed(Guid.NewGuid(), "  ", 100m, 1);

        Assert.False(result.IsSuccess);
    }
}
