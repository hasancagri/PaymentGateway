
namespace Payment.Api.Tests;

public class PaymentSessionSelectTests
{
    private static PaymentSession QuotedSession()
    {
        var session = PaymentSession.Create("tok_credit_taksitli", 100m).Data!;
        session.OfferInstallments(new[]
        {
            new OfferedInstallment(1, 100m, 100m),
            new OfferedInstallment(3, 100m, 33.33m)
        });
        return session;
    }

    [Fact]
    public void SelectInstallment_sunulan_taksiti_yazar()
    {
        var session = QuotedSession();

        var result = session.SelectInstallment(3);

        Assert.True(result.IsSuccess);
        Assert.Equal(PaymentSessionStatus.InstallmentSelected, session.Status);
        Assert.Equal(3, session.SelectedInstallmentCount);
    }

    [Fact]
    public void SelectInstallment_sunulmayan_taksiti_reddeder()
    {
        var session = QuotedSession();

        var result = session.SelectInstallment(6); // listede yok (FR-012)

        Assert.False(result.IsSuccess);
        Assert.Null(session.SelectedInstallmentCount);
    }

    [Fact]
    public void SelectInstallment_quote_yapilmamis_oturumu_reddeder()
    {
        var session = PaymentSession.Create("tok_credit_taksitli", 100m).Data!; // Opened

        var result = session.SelectInstallment(1); // FR-017

        Assert.False(result.IsSuccess);
        Assert.Equal(PaymentSessionStatus.Opened, session.Status);
    }

    [Fact]
    public void SelectInstallment_tekrar_secim_ongorulebilir_gunceller()
    {
        var session = QuotedSession();
        session.SelectInstallment(1);

        var second = session.SelectInstallment(3); // tekrar seçim: güncelle, çift faz geçişi yok

        Assert.True(second.IsSuccess);
        Assert.Equal(PaymentSessionStatus.InstallmentSelected, session.Status);
        Assert.Equal(3, session.SelectedInstallmentCount);
    }
}