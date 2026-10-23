namespace Payment.Api.Tests;

// 092/093 (İlke VIII Domain-TDD): BankChargeRouter — kart-ailesi + per-taksit EFEKTİF MALİYET aday seçimi
// (en düşük oran önce; on-us yalnız eşit maliyette tiebreaker); FAILOVER YOK (yalnız gösterilen/en-avantajlı
// acquirer denenir). Fake adapter double'ları ile saf karar (DB/host yok).
public class BankChargeRouterTests
{
    private sealed class FakeAdapter : IBankPaymentAdapter
    {
        private readonly BankChargeOutcome _outcome;
        public int Calls;
        public FakeAdapter(string bankCode, BankChargeOutcome outcome) { BankCode = bankCode; _outcome = outcome; }
        public string BankCode { get; }
        public Task<ResultDomain<BankChargeOutcome>> ChargeAsync(BankChargeRequest request, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(ResultDomain<BankChargeOutcome>.Ok(_outcome));
        }
        public Task<ResultDomain<BankChargeOutcome>> QueryStatusAsync(string orderId, CancellationToken ct)
            => Task.FromResult(ResultDomain<BankChargeOutcome>.Ok(BankChargeOutcome.Declined(DeclineClass.TechnicalFailure, "NOTCHARGED")));
        public Task<ResultDomain<BankChargeOutcome>> VoidAsync(string bankOrderId, CancellationToken ct)
            => Task.FromResult(ResultDomain<BankChargeOutcome>.Ok(BankChargeOutcome.Declined(DeclineClass.ConfigError, "NOTSUPPORTED")));
        public Task<ResultDomain<BankChargeOutcome>> RefundAsync(string bankOrderId, decimal amount, CancellationToken ct)
            => Task.FromResult(ResultDomain<BankChargeOutcome>.Ok(BankChargeOutcome.Declined(DeclineClass.ConfigError, "NOTSUPPORTED")));
    }

    private static BankChargeOutcome Approved() => BankChargeOutcome.Approved("REF", "AUTH", "00");
    private static BankChargeOutcome Decline(DeclineClass c, string code) => BankChargeOutcome.Declined(c, code);

    private static PosAccountOptions PosAll() => new()
    {
        Accounts =
        {
            new PosAccountProfile { BankCode = "SIM-BANK-1", SupportedCardTypes = new[] { "credit", "debit" }, SupportsNonSecure = true },
            new PosAccountProfile { BankCode = "SIM-BANK-2", SupportedCardTypes = new[] { "credit", "debit" }, SupportsNonSecure = true },
            new PosAccountProfile { BankCode = "SIM-BANK-3", SupportedCardTypes = new[] { "credit" }, SupportsNonSecure = true }
        }
    };

    private static BankRoutingOptions Routing() => new() { OrderedBankCodes = { "SIM-BANK-1", "SIM-BANK-2", "SIM-BANK-3" } };

    private static CommissionRateRow Row(int inst, decimal rate)
        => new() { Scheme = "Visa", CardType = "credit", Installment = inst, Rate = rate };

    // inst2: BANK-1 en ucuz (0.01); inst3: BANK-1 & BANK-2 eşit (0.02, on-us tiebreaker); inst6: BANK-3 en ucuz (0.02).
    private static List<BankCommissionRate> Rates() => new()
    {
        new() { BankCode = "SIM-BANK-1", DefaultCommissionRate = 0.03m, Rates = { Row(2, 0.01m), Row(3, 0.02m), Row(6, 0.05m) } },
        new() { BankCode = "SIM-BANK-2", DefaultCommissionRate = 0.03m, Rates = { Row(2, 0.02m), Row(3, 0.02m), Row(6, 0.04m) } },
        new() { BankCode = "SIM-BANK-3", DefaultCommissionRate = 0.03m, Rates = { Row(2, 0.03m), Row(6, 0.02m) } }
    };

    // issuer = SIM-BANK-1 (on-us), Visa credit.
    private static IReadOnlyList<BinCard> VisaCreditBins() => new List<BinCard>
    {
        new() { Bin = "411111", IssuerBankCode = "SIM-BANK-1", Scheme = "Visa", CardType = "credit" }
    };
    private const string VisaPan = "4111110000000000";

    private static BankChargeRequest Request(string pan, int installment)
        => new(new CardData(pan, "AD SOYAD", "12", "30", "123"), 100m, "TRY", "ORD-1", Guid.NewGuid(), installment);

    private static readonly IReadOnlyList<BinCard> NoBins = new List<BinCard>();

    private static BankChargeRouter Router(params FakeAdapter[] adapters) => new(adapters, PosAll(), Routing());
    private static BankChargeRouter AllApproved() => Router(
        new FakeAdapter("SIM-BANK-1", Approved()), new FakeAdapter("SIM-BANK-2", Approved()), new FakeAdapter("SIM-BANK-3", Approved()));

    // --- US1: per-taksit en düşük maliyet + kart ailesi ---

    [Fact]
    public void Select_PerTaksit_EnAvantajliBankaDegisir()
    {
        var c2 = AllApproved().SelectCandidates(VisaPan, VisaCreditBins(), 100m, 2, Rates());
        var c6 = AllApproved().SelectCandidates(VisaPan, VisaCreditBins(), 100m, 6, Rates());

        Assert.Equal("SIM-BANK-1", c2[0].BankCode); // 2 takside en ucuz (0.01)
        Assert.Equal("SIM-BANK-3", c6[0].BankCode); // 6 takside en ucuz (0.02) — on-us SIM-BANK-1'i (0.05) geçer
    }

    [Fact]
    public void Select_OnUs_YalnizEsitMaliyetteTiebreaker()
    {
        // inst3: BANK-1(on-us) ve BANK-2 eşit 0.02 → on-us önce.
        var c = AllApproved().SelectCandidates(VisaPan, VisaCreditBins(), 100m, 3, Rates());
        Assert.Equal("SIM-BANK-1", c[0].BankCode);
        Assert.True(c[0].IsOnUs);
    }

    [Fact]
    public void Select_OnUsPahali_UcuzOnce()
    {
        // inst6: on-us BANK-1 pahalı (0.05) → ucuz BANK-3 (0.02) önce (otomatik on-us önceliği YOK).
        var c = AllApproved().SelectCandidates(VisaPan, VisaCreditBins(), 100m, 6, Rates());
        Assert.Equal("SIM-BANK-3", c[0].BankCode);
        Assert.False(c[0].IsOnUs);
    }

    [Fact]
    public void Select_YetenekEleme_DebitBankaElenur()
    {
        var debitBins = new List<BinCard> { new() { Bin = "411111", IssuerBankCode = "SIM-BANK-1", Scheme = "Visa", CardType = "debit" } };
        var c = AllApproved().SelectCandidates(VisaPan, debitBins, 100m, 2, Rates());
        Assert.DoesNotContain(c, x => x.BankCode == "SIM-BANK-3"); // SIM-BANK-3 yalnız credit
    }

    [Fact]
    public void Select_BilinmeyenBin_DefaultRate_FailOpen()
    {
        // BIN yok → aile null → RateFor DefaultCommissionRate (hepsi 0.03 eşit) → on-us yok, kararlı fallback sırası.
        var c = AllApproved().SelectCandidates("9999000000000000", NoBins, 100m, 2, Rates());
        Assert.Equal(3, c.Count);
        Assert.All(c, x => Assert.False(x.IsOnUs));
        Assert.Equal("SIM-BANK-1", c[0].BankCode); // eşit maliyet → OrderedBankCodes fallback
    }

    [Fact]
    public void Select_OranDokumaniYok_BankaDusmezSonaGider()
    {
        var ratesMissing = new List<BankCommissionRate>
        {
            new() { BankCode = "SIM-BANK-1", DefaultCommissionRate = 0.03m, Rates = { Row(2, 0.02m) } }
            // SIM-BANK-2 / SIM-BANK-3 için doküman YOK → en sona, ama adaylıktan düşmez.
        };
        var c = AllApproved().SelectCandidates(VisaPan, VisaCreditBins(), 100m, 2, ratesMissing);
        Assert.Equal(3, c.Count);                 // hiçbiri düşmedi
        Assert.Equal("SIM-BANK-1", c[0].BankCode); // oranı olan en ucuz
    }

    // --- US2: RouteAsync — failover YOK (yalnız en-avantajlı acquirer denenir) ---

    [Fact]
    public async Task Route_IlkApproved_TekDeneme()
    {
        var router = Router(new FakeAdapter("SIM-BANK-1", Approved()), new FakeAdapter("SIM-BANK-2", Approved()), new FakeAdapter("SIM-BANK-3", Approved()));
        var attempts = await router.RouteAsync(Request(VisaPan, 2), VisaCreditBins(), Rates(), CancellationToken.None);
        Assert.Single(attempts);
        Assert.Equal("SIM-BANK-1", attempts[0].BankCode); // inst2 en ucuz
        Assert.True(attempts[0].Outcome.IsApproved);
    }

    [Fact]
    public async Task Route_EnUcuzRed_FailoverYok_SiradakiCagrilmaz()
    {
        var bank2 = new FakeAdapter("SIM-BANK-2", Approved());
        var bank3 = new FakeAdapter("SIM-BANK-3", Approved());
        var router = Router(new FakeAdapter("SIM-BANK-1", Decline(DeclineClass.TechnicalFailure, "96")), bank2, bank3);

        var attempts = await router.RouteAsync(Request(VisaPan, 2), VisaCreditBins(), Rates(), CancellationToken.None);

        Assert.Single(attempts); // yalnız en-avantajlı (SIM-BANK-1) denendi
        Assert.Equal("SIM-BANK-1", attempts[0].BankCode);
        Assert.False(attempts[0].Outcome.IsApproved);
        Assert.Equal(0, bank2.Calls); // FAILOVER YOK — sıradaki banka ÇAĞRILMAZ
        Assert.Equal(0, bank3.Calls);
    }

    [Fact]
    public async Task Route_AdayYok_BosListe()
    {
        // Hiç canlı adapter yok → aday yok → boş (handler PROVIDER_UNAVAILABLE).
        var router = new BankChargeRouter(Array.Empty<IBankPaymentAdapter>(), PosAll(), Routing());
        var attempts = await router.RouteAsync(Request(VisaPan, 2), VisaCreditBins(), Rates(), CancellationToken.None);
        Assert.Empty(attempts);
    }
}
