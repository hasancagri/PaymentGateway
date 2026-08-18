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

    // --- 039: yapısal çekim marker + geçişleri ---

    [Fact]
    public void Begin_GecerliAlanlar_ChargingVeCorrelationKey()
    {
        var merchantId = Guid.NewGuid();
        var result = PaymentAgg.Begin(merchantId, "card_1", "corr-abc", 100m, 106m, 3);

        Assert.True(result.IsSuccess);
        var p = result.Data!;
        Assert.Equal(PaymentStatus.Charging, p.Status);
        Assert.Equal("corr-abc", p.CorrelationKey);
        Assert.Equal(merchantId, p.MerchantId);
        Assert.Equal(106m, p.PaidPrice);
        Assert.Equal(string.Empty, p.ProviderPaymentId);
    }

    [Fact]
    public void Begin_BosCorrelationKey_Reddedilir()
    {
        var result = PaymentAgg.Begin(Guid.NewGuid(), "card_1", "  ", 100m, 100m, 1);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Succeed_ChargingMarker_SuccessVeAlanlar()
    {
        var p = PaymentAgg.Begin(Guid.NewGuid(), "card_1", "corr-1", 100m, 106m, 3).Data!;

        var r = p.Succeed("iyz-pay-9", "2.75", "0.25");

        Assert.True(r.IsSuccess);
        Assert.Equal(PaymentStatus.Success, p.Status);
        Assert.Equal("iyz-pay-9", p.ProviderPaymentId);
        Assert.Equal("2.75", p.ProviderCommission);
        Assert.Equal("0.25", p.ProviderFee);
    }

    [Fact]
    public void Succeed_BosProviderPaymentId_Reddedilir()
    {
        var p = PaymentAgg.Begin(Guid.NewGuid(), "card_1", "corr-1", 100m, 100m, 1).Data!;

        var r = p.Succeed("  ", "1", "0");

        Assert.False(r.IsSuccess);
        Assert.Equal(PaymentStatus.Charging, p.Status); // geçiş olmadı
    }

    [Fact]
    public void Fail_ChargingMarker_Failed()
    {
        var p = PaymentAgg.Begin(Guid.NewGuid(), "card_1", "corr-1", 100m, 100m, 1).Data!;

        var r = p.Fail();

        Assert.True(r.IsSuccess);
        Assert.Equal(PaymentStatus.Failed, p.Status);
    }

    [Fact]
    public void Succeed_TerminalKayit_Reddedilir()
    {
        // Charging değil (zaten Success) → tekrar mutate edilemez (idempotent güvenlik).
        var p = PaymentAgg.Begin(Guid.NewGuid(), "card_1", "corr-1", 100m, 100m, 1).Data!;
        p.Succeed("iyz-1", "1", "0");

        var r = p.Succeed("iyz-2", "1", "0");

        Assert.False(r.IsSuccess);
    }
}
