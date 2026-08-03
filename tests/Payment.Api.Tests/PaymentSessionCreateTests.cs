using Payment.Api.Domains.PaymentSessions;

namespace Payment.Api.Tests;

public class PaymentSessionCreateTests
{
    [Fact]
    public void Create_gecerli_girdiyle_Opened_doner()
    {
        var result = PaymentSession.Create("tok_credit_taksitli", 100m);

        Assert.True(result.IsSuccess);
        Assert.Equal(PaymentSessionStatus.Opened, result.Data!.Status);
        Assert.Equal(100m, result.Data.CartAmount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-99.99)]
    public void Create_sifir_veya_negatif_tutari_reddeder(decimal amount)
    {
        var result = PaymentSession.Create("tok_credit_taksitli", amount);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Create_bos_token_reddeder()
    {
        var result = PaymentSession.Create("  ", 100m);

        Assert.False(result.IsSuccess);
    }
}