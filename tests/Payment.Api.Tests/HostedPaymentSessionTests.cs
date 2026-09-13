namespace Payment.Api.Tests;

// 041 (İlke VI Domain-TDD): HostedPaymentSession yaşam döngüsü + terminal tek-yön invariant'ları.
public class HostedPaymentSessionTests
{
    private const string OrderRef = "T-001";
    private const string CallbackUrl = "https://store.example/callback";
    private const string CallbackToken = "cbtok-abc";

    private static HostedPaymentSession Pending()
        => HostedPaymentSession.Start(Guid.NewGuid(), OrderRef, 100m, CallbackUrl, CallbackToken).Data!;

    [Fact]
    public void Start_PendingDogar_AlanlarDogru()
    {
        var merchantId = Guid.NewGuid();

        var r = HostedPaymentSession.Start(merchantId, OrderRef, 100m, CallbackUrl, CallbackToken);

        Assert.True(r.IsSuccess);
        var s = r.Data!;
        Assert.Equal(merchantId, s.MerchantId);
        Assert.Equal(OrderRef, s.OrderRef);
        Assert.Equal(100m, s.Amount);
        Assert.Equal(CallbackUrl, s.CallbackUrl);
        Assert.Equal(CallbackToken, s.CallbackToken);
        Assert.Equal(HostedPaymentStatus.Pending, s.Status);
        Assert.False(s.IsTerminal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Start_TutarSifirVeyaNegatif_Reddedilir(decimal amount)
        => Assert.False(HostedPaymentSession.Start(Guid.NewGuid(), OrderRef, amount, CallbackUrl, CallbackToken).IsSuccess);

    [Fact]
    public void Start_BosOrderRef_Reddedilir()
        => Assert.False(HostedPaymentSession.Start(Guid.NewGuid(), "  ", 100m, CallbackUrl, CallbackToken).IsSuccess);

    [Fact]
    public void Start_BosCallbackUrl_Reddedilir()
        => Assert.False(HostedPaymentSession.Start(Guid.NewGuid(), OrderRef, 100m, " ", CallbackToken).IsSuccess);

    [Fact]
    public void Start_BosCallbackToken_Reddedilir()
        => Assert.False(HostedPaymentSession.Start(Guid.NewGuid(), OrderRef, 100m, CallbackUrl, "").IsSuccess);

    [Fact]
    public void AttachCheckoutForm_PendingIken_TokenBaglanir()
    {
        var s = Pending();

        Assert.True(s.AttachCheckoutForm("cf-token-1", "https://iyz/pay/1").IsSuccess);
        Assert.Equal("cf-token-1", s.CheckoutFormToken);
        Assert.Equal("https://iyz/pay/1", s.HostedUrl);
    }

    [Fact]
    public void AttachCheckoutForm_Terminal_Reddedilir()
    {
        var s = Pending();
        s.MarkSucceeded("iyz-1");

        Assert.False(s.AttachCheckoutForm("cf-token-2", "https://iyz/pay/2").IsSuccess);
    }

    [Fact]
    public void MarkSucceeded_PendingdenSucceeded_ProviderPaymentIdYazilir()
    {
        var s = Pending();

        Assert.True(s.MarkSucceeded("iyz-1").IsSuccess);
        Assert.Equal(HostedPaymentStatus.Succeeded, s.Status);
        Assert.Equal("iyz-1", s.ProviderPaymentId);
        Assert.True(s.IsTerminal);
    }

    [Fact]
    public void MarkFailed_PendingdenFailed_SebepYazilir()
    {
        var s = Pending();

        Assert.True(s.MarkFailed("CARD_DECLINED").IsSuccess);
        Assert.Equal(HostedPaymentStatus.Failed, s.Status);
        Assert.Equal("CARD_DECLINED", s.FailureReason);
        Assert.True(s.IsTerminal);
    }

    [Fact]
    public void MarkSucceeded_ZatenSucceeded_NoOpAyniSonuc()
    {
        var s = Pending();
        s.MarkSucceeded("iyz-1");

        Assert.True(s.MarkSucceeded("iyz-2").IsSuccess);          // idempotent no-op
        Assert.Equal("iyz-1", s.ProviderPaymentId);              // ilk sonuç korunur
        Assert.Equal(HostedPaymentStatus.Succeeded, s.Status);
    }

    [Fact]
    public void MarkFailed_ZatenFailed_NoOpAyniSonuc()
    {
        var s = Pending();
        s.MarkFailed("R1");

        Assert.True(s.MarkFailed("R2").IsSuccess);               // idempotent no-op
        Assert.Equal("R1", s.FailureReason);                     // ilk sebep korunur
        Assert.Equal(HostedPaymentStatus.Failed, s.Status);
    }

    [Fact]
    public void MarkFailed_SucceededIken_TerminalIhlali_Reddedilir()
    {
        var s = Pending();
        s.MarkSucceeded("iyz-1");

        Assert.False(s.MarkFailed("R1").IsSuccess);              // başarılıdan geri dönülmez (FR-008)
        Assert.Equal(HostedPaymentStatus.Succeeded, s.Status);
    }

    [Fact]
    public void MarkSucceeded_FailedIken_TerminalIhlali_Reddedilir()
    {
        var s = Pending();
        s.MarkFailed("R1");

        Assert.False(s.MarkSucceeded("iyz-1").IsSuccess);
        Assert.Equal(HostedPaymentStatus.Failed, s.Status);
    }
}
