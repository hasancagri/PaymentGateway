namespace Payment.Api.Tests;

// 090 (İlke VIII Domain-TDD): BankChargeRouter — 2-aşama aday seçimi (yetenek eleme + on-us + maliyet) +
// sıralı failover kararı (Technical/Config → sıradaki; Issuer/Fraud/Approved → dur). Fake adapter double'ları
// ile saf karar (DB/host yok).
public class BankChargeRouterTests
{
    // --- Fake adapter ---
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
            new PosAccountProfile { BankCode = "SIM-BANK-1", SupportedCardTypes = new[] { "credit", "debit" }, SupportsNonSecure = true, CostPriority = 10 },
            new PosAccountProfile { BankCode = "SIM-BANK-2", SupportedCardTypes = new[] { "credit", "debit" }, SupportsNonSecure = true, CostPriority = 20 },
            new PosAccountProfile { BankCode = "SIM-BANK-3", SupportedCardTypes = new[] { "credit" }, SupportsNonSecure = true, CostPriority = 30 }
        }
    };

    private static BankRoutingOptions Routing() => new() { OrderedBankCodes = { "SIM-BANK-1", "SIM-BANK-2", "SIM-BANK-3" } };

    private static BankChargeRequest Request(string pan)
        => new(new CardData(pan, "AD SOYAD", "12", "30", "123"), 100m, "TRY", "ORD-1", Guid.NewGuid());

    private static readonly IReadOnlyList<BinCard> NoBins = new List<BinCard>();

    // --- Failover kararı (T022 + T032) ---

    [Fact]
    public async Task Route_IssuerDecline_DururSiradakiCagrilmaz()
    {
        var bank2 = new FakeAdapter("SIM-BANK-2", Approved());
        var router = new BankChargeRouter(
            new[] { new FakeAdapter("SIM-BANK-1", Decline(DeclineClass.IssuerDecline, "51")), bank2 },
            PosAll(), Routing());

        var attempts = await router.RouteAsync(Request("9999000000000000"), NoBins, CancellationToken.None);

        Assert.Single(attempts);
        Assert.Equal("SIM-BANK-1", attempts[0].BankCode);
        Assert.Equal(0, bank2.Calls); // failover YOK (aynı issuer)
    }

    [Fact]
    public async Task Route_FraudDecline_DururSiradakiCagrilmaz()
    {
        var bank2 = new FakeAdapter("SIM-BANK-2", Approved());
        var router = new BankChargeRouter(
            new[] { new FakeAdapter("SIM-BANK-1", Decline(DeclineClass.FraudDecline, "59")), bank2 },
            PosAll(), Routing());

        var attempts = await router.RouteAsync(Request("9999000000000000"), NoBins, CancellationToken.None);

        Assert.Single(attempts);
        Assert.Equal(0, bank2.Calls);
    }

    [Fact]
    public async Task Route_TechnicalFailure_SiradakiBankayaGecer()
    {
        var bank2 = new FakeAdapter("SIM-BANK-2", Approved());
        var router = new BankChargeRouter(
            new[] { new FakeAdapter("SIM-BANK-1", Decline(DeclineClass.TechnicalFailure, "96")), bank2 },
            PosAll(), Routing());

        var attempts = await router.RouteAsync(Request("9999000000000000"), NoBins, CancellationToken.None);

        Assert.Equal(2, attempts.Count);
        Assert.True(attempts[^1].Outcome.IsApproved);
        Assert.Equal("SIM-BANK-2", attempts[^1].BankCode);
        Assert.Equal(1, bank2.Calls);
    }

    [Fact]
    public async Task Route_ConfigError_SiradakiBankayaGecer()
    {
        var router = new BankChargeRouter(
            new[]
            {
                new FakeAdapter("SIM-BANK-1", Decline(DeclineClass.ConfigError, "CFG")),
                new FakeAdapter("SIM-BANK-2", Approved())
            },
            PosAll(), Routing());

        var attempts = await router.RouteAsync(Request("9999000000000000"), NoBins, CancellationToken.None);

        Assert.Equal(2, attempts.Count);
        Assert.True(attempts[^1].Outcome.IsApproved);
    }

    [Fact]
    public async Task Route_HepsiTechnical_SonTechnicalDoner()
    {
        var router = new BankChargeRouter(
            new[]
            {
                new FakeAdapter("SIM-BANK-1", Decline(DeclineClass.TechnicalFailure, "96")),
                new FakeAdapter("SIM-BANK-2", Decline(DeclineClass.TechnicalFailure, "96")),
                new FakeAdapter("SIM-BANK-3", Decline(DeclineClass.TechnicalFailure, "96"))
            },
            PosAll(), Routing());

        var attempts = await router.RouteAsync(Request("9999000000000000"), NoBins, CancellationToken.None);

        Assert.Equal(3, attempts.Count);
        Assert.False(attempts[^1].Outcome.IsApproved);
        Assert.Equal(DeclineClass.TechnicalFailure, attempts[^1].Outcome.DeclineClass);
    }

    [Fact]
    public async Task Route_IlkApproved_KalanCagrilmaz()
    {
        var bank2 = new FakeAdapter("SIM-BANK-2", Approved());
        var router = new BankChargeRouter(
            new[] { new FakeAdapter("SIM-BANK-1", Approved()), bank2 },
            PosAll(), Routing());

        var attempts = await router.RouteAsync(Request("9999000000000000"), NoBins, CancellationToken.None);

        Assert.Single(attempts);
        Assert.Equal(0, bank2.Calls);
    }

    // --- Aday seçimi (T026): yetenek eleme + on-us + maliyet + bilinmeyen BIN fail-open ---

    private static BankChargeRouter CandidateRouter() => new(
        new[]
        {
            new FakeAdapter("SIM-BANK-1", Approved()),
            new FakeAdapter("SIM-BANK-2", Approved()),
            new FakeAdapter("SIM-BANK-3", Approved())
        },
        PosAll(), Routing());

    [Fact]
    public void Select_OnUs_IssuerBankasiOne()
    {
        var bins = new List<BinCard> { new() { Bin = "454360", IssuerBankCode = "SIM-BANK-2", Scheme = "Visa", CardType = "credit" } };
        var candidates = CandidateRouter().SelectCandidates("4543600000000000", bins);

        Assert.Equal("SIM-BANK-2", candidates[0].BankCode); // on-us (maliyeti yüksek olsa da öne)
        Assert.True(candidates[0].IsOnUs);
        Assert.Equal("SIM-BANK-1", candidates[1].BankCode);  // sonra maliyet sırası (10 < 30)
        Assert.Equal("SIM-BANK-3", candidates[2].BankCode);
    }

    [Fact]
    public void Select_YetenekEleme_DesteklemeyenBankaElenur()
    {
        // Debit kart → SIM-BANK-3 (yalnız credit) aday listesinden çıkar.
        var bins = new List<BinCard> { new() { Bin = "979200", IssuerBankCode = "SIM-BANK-1", Scheme = "Troy", CardType = "debit" } };
        var candidates = CandidateRouter().SelectCandidates("9792000000000000", bins);

        Assert.DoesNotContain(candidates, c => c.BankCode == "SIM-BANK-3");
        Assert.Equal("SIM-BANK-1", candidates[0].BankCode); // on-us (debit destekler)
    }

    [Fact]
    public void Select_BilinmeyenBin_FailOpenMaliyetSirasi()
    {
        var candidates = CandidateRouter().SelectCandidates("9999000000000000", NoBins);

        Assert.Equal(3, candidates.Count);                    // kart tipi bilinmiyor → yetenek eleme atlanır
        Assert.All(candidates, c => Assert.False(c.IsOnUs));  // on-us yok
        Assert.Equal("SIM-BANK-1", candidates[0].BankCode);   // maliyet sırası (10,20,30)
        Assert.Equal("SIM-BANK-2", candidates[1].BankCode);
        Assert.Equal("SIM-BANK-3", candidates[2].BankCode);
    }
}
