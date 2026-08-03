using Payment.Api.Domains.PaymentSessions;

namespace Payment.Api.Tests;

public class PaymentSessionOfferTests
{
    private static PaymentSession OpenedSession(decimal cartAmount = 100m) =>
        PaymentSession.Create("tok_credit_taksitli", cartAmount).Data!;

    [Fact]
    public void OfferInstallments_gecerli_liste_QuoteProvided_yapar()
    {
        var session = OpenedSession(100m);

        var result = session.OfferInstallments(new[]
        {
            new OfferedInstallment(1, 100m, 100m),
            new OfferedInstallment(3, 100m, 33.33m)
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(PaymentSessionStatus.QuoteProvided, session.Status);
        Assert.Equal(2, session.OfferedInstallments.Count);
    }

    [Fact]
    public void OfferInstallments_bos_liste_Failed_yapar()
    {
        var session = OpenedSession();

        var result = session.OfferInstallments(Array.Empty<OfferedInstallment>());

        Assert.True(result.IsSuccess); // boş liste geçerli bir sonuç: oturum Failed'e alınır
        Assert.Equal(PaymentSessionStatus.Failed, session.Status);
        Assert.NotNull(session.FailReason);
    }

    [Fact]
    public void OfferInstallments_ModelA_ihlali_reddeder()
    {
        var session = OpenedSession(100m);

        // Satır tutarı sepet tutarına eşit değil (Model A ihlali, FR-010).
        var result = session.OfferInstallments(new[] { new OfferedInstallment(3, 120m, 40m) });

        Assert.False(result.IsSuccess);
        Assert.Equal(PaymentSessionStatus.Opened, session.Status); // faz değişmez
    }

    [Fact]
    public void OfferInstallments_Opened_disinda_reddeder()
    {
        var session = OpenedSession(100m);
        session.OfferInstallments(new[] { new OfferedInstallment(1, 100m, 100m) }); // QuoteProvided

        var second = session.OfferInstallments(new[] { new OfferedInstallment(2, 100m, 50m) });

        Assert.False(second.IsSuccess);
    }
}