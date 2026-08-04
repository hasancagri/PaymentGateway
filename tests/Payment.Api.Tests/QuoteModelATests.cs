using Payment.Api.Domains.PaymentSessions.Features.Agent;
using Payment.Api.Domains.PosAccounts;

namespace Payment.Api.Tests;

public class QuoteModelATests
{
    private static PosAccount Pos(string bankCode, params (int n, decimal rate)[] rates)
    {
        var acc = PosAccount.Create(bankCode, bankCode, "merchant", "user", "pass", null, true).Data!;
        foreach (var (n, rate) in rates)
            acc.SetCommissionRate(n, rate);
        return acc;
    }

    // Kredi kartı: 0124 ve 0062 programını destekler.
    private static CardInfo CreditCard() => new("0124", true, new List<string> { "0124", "0062" });

    // Banka kartı: taksit destekleyen banka yok.
    private static CardInfo DebitCard() => new("0012", false, new List<string>());

    private static List<PosAccount> Accounts() =>
    [
        Pos("0124", (1, 2.0m), (3, 4.0m), (6, 6.0m)),
        Pos("0062", (1, 1.5m), (3, 3.5m)),
        Pos("0111", (9, 5.0m)) // 9 taksit — kartın desteklediği bankalarda yok → listede görünmemeli
    ];

    [Fact]
    public void ModelA_her_satir_tutari_sepet_tutarina_esit()
    {
        var lines = QuoteInstallmentsForSession.BuildOfferedInstallments(CreditCard(), 100m, Accounts());

        Assert.NotEmpty(lines);
        Assert.All(lines, l => Assert.Equal(100m, l.UserTotalAmount)); // sapma 0 (SC-002)
    }

    [Fact]
    public void ModelA_aylik_tutar_dogru_yuvarlanir()
    {
        var lines = QuoteInstallmentsForSession.BuildOfferedInstallments(CreditCard(), 100m, Accounts());

        Assert.Equal(100m, lines.Single(l => l.InstallmentCount == 1).MonthlyAmount);
        Assert.Equal(33.33m, lines.Single(l => l.InstallmentCount == 3).MonthlyAmount);
        Assert.Equal(16.67m, lines.Single(l => l.InstallmentCount == 6).MonthlyAmount);
    }

    [Fact]
    public void Desteklenmeyen_taksit_listede_gorunmez()
    {
        var lines = QuoteInstallmentsForSession.BuildOfferedInstallments(CreditCard(), 100m, Accounts());

        Assert.DoesNotContain(lines, l => l.InstallmentCount == 9); // SC-003
        Assert.Contains(lines, l => l.InstallmentCount == 1);
        Assert.Contains(lines, l => l.InstallmentCount == 3);
        Assert.Contains(lines, l => l.InstallmentCount == 6);
    }

    [Fact]
    public void Banka_karti_yalniz_pesin_doner()
    {
        var lines = QuoteInstallmentsForSession.BuildOfferedInstallments(DebitCard(), 100m, Accounts());

        var single = Assert.Single(lines); // yalnız peşin (AC-3, FR-009)
        Assert.Equal(1, single.InstallmentCount);
    }
}