using Payment.Api.Constants;

namespace Payment.Api.Tests;

// 089 (İlke VIII Domain-TDD): PaymentLink yaşam döngüsü + invariant'lar (aktif-tekil handler'da;
// burada saf domain davranışı: Create/Open/Expire/IsPayable/Cancel + terminal tek-yön).
public class PaymentLinkTests
{
    private const string OrderRef = "ORD-001";
    private const string Currency = "TRY";
    private const string ReturnUrl = "https://store.example/return";
    private const string StoreName = "Kolay Fırsat";
    private const string OrderSummary = "1x Kablosuz Kulaklık";
    private const string OpenToken = "tok-abc";

    private static readonly TimeSpan Grace = TimeSpan.FromMinutes(5);

    // Varsayılan: ExpiresAt = şimdi + 30 dk (gelecekte).
    private static PaymentLink Pending(decimal amount = 100m, DateTime? expiresAt = null)
        => PaymentLink.Create(Guid.NewGuid(), OrderRef, amount, Currency, ReturnUrl, StoreName, OrderSummary,
            OpenToken, expiresAt ?? DateTime.UtcNow.AddMinutes(30)).Data!;

    // --- Create (US1) ---

    [Fact]
    public void Create_GecerliIstek_PendingDogar()
    {
        var merchantId = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow.AddMinutes(30);

        var r = PaymentLink.Create(merchantId, OrderRef, 149.90m, "TRY", ReturnUrl, StoreName, OrderSummary, OpenToken, expiresAt);

        Assert.True(r.IsSuccess);
        var link = r.Data!;
        Assert.Equal(merchantId, link.MerchantId);
        Assert.Equal(OrderRef, link.OrderRef);
        Assert.Equal(149.90m, link.Amount);
        Assert.Equal("TRY", link.Currency);
        Assert.Equal(StoreName, link.StoreName);
        Assert.Equal(OrderSummary, link.OrderSummary);
        Assert.Equal(OpenToken, link.OpenToken);
        Assert.Equal(expiresAt, link.ExpiresAt);
        Assert.Equal(PaymentLinkStatus.Pending, link.Status);
        Assert.False(link.IsTerminal);
    }

    [Theory]
    [InlineData("USD")]
    [InlineData("EUR")]
    public void Create_TryDisiParaBirimi_Reddedilir(string currency)
    {
        var r = PaymentLink.Create(Guid.NewGuid(), OrderRef, 100m, currency, ReturnUrl, StoreName, OrderSummary, OpenToken, DateTime.UtcNow.AddMinutes(30));
        Assert.False(r.IsSuccess);
        Assert.Equal(PaymentLinkResourceConstants.UNSUPPORTED_CURRENCY, r.Messages[0].Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Create_TutarSifirVeyaNegatif_Reddedilir(decimal amount)
        => Assert.False(PaymentLink.Create(Guid.NewGuid(), OrderRef, amount, "TRY", ReturnUrl, StoreName, OrderSummary, OpenToken, DateTime.UtcNow.AddMinutes(30)).IsSuccess);

    [Fact]
    public void Create_BosZorunluAlan_Reddedilir()
    {
        Assert.False(PaymentLink.Create(Guid.NewGuid(), "  ", 100m, "TRY", ReturnUrl, StoreName, OrderSummary, OpenToken, DateTime.UtcNow.AddMinutes(30)).IsSuccess);
        Assert.False(PaymentLink.Create(Guid.NewGuid(), OrderRef, 100m, "TRY", "  ", StoreName, OrderSummary, OpenToken, DateTime.UtcNow.AddMinutes(30)).IsSuccess);
        Assert.False(PaymentLink.Create(Guid.NewGuid(), OrderRef, 100m, "TRY", ReturnUrl, "  ", OrderSummary, OpenToken, DateTime.UtcNow.AddMinutes(30)).IsSuccess);
        Assert.False(PaymentLink.Create(Guid.NewGuid(), OrderRef, 100m, "TRY", ReturnUrl, StoreName, "  ", OpenToken, DateTime.UtcNow.AddMinutes(30)).IsSuccess);
        Assert.False(PaymentLink.Create(Guid.NewGuid(), OrderRef, 100m, "TRY", ReturnUrl, StoreName, OrderSummary, "  ", DateTime.UtcNow.AddMinutes(30)).IsSuccess);
    }

    [Fact]
    public void Create_GecmisExpiresAt_Reddedilir()
        => Assert.False(PaymentLink.Create(Guid.NewGuid(), OrderRef, 100m, "TRY", ReturnUrl, StoreName, OrderSummary, OpenToken, DateTime.UtcNow.AddMinutes(-1)).IsSuccess);

    // --- Expire (US1, lazy) ---

    [Fact]
    public void Expire_AktifVeSuresiGecti_Expired()
    {
        // Gelecekte oluşturulan link, ExpiresAt'ten SONRAKİ bir erişim anında (now) lazy expire olur.
        var expiresAt = DateTime.UtcNow.AddMinutes(10);
        var link = Pending(expiresAt: expiresAt);
        var r = link.Expire(expiresAt.AddMinutes(1));
        Assert.True(r.IsSuccess);
        Assert.Equal(PaymentLinkStatus.Expired, link.Status);
        Assert.True(link.IsTerminal);
    }

    [Fact]
    public void Expire_SuresiGecmemis_NoOp()
    {
        var link = Pending();
        link.Expire(DateTime.UtcNow);
        Assert.Equal(PaymentLinkStatus.Pending, link.Status);
    }

    [Fact]
    public void Expire_Terminal_NoOp()
    {
        var link = Pending();
        link.Cancel();
        link.Expire(DateTime.UtcNow.AddHours(1));
        Assert.Equal(PaymentLinkStatus.Cancelled, link.Status); // Expired'a dönüşmez
    }

    // --- Open (US2) ---

    [Fact]
    public void Open_Pending_Opened()
    {
        var link = Pending();
        var r = link.Open(DateTime.UtcNow);
        Assert.True(r.IsSuccess);
        Assert.Equal(PaymentLinkStatus.Opened, link.Status);
    }

    [Fact]
    public void Open_ZatenOpened_Idempotent()
    {
        var link = Pending();
        link.Open(DateTime.UtcNow);
        var r = link.Open(DateTime.UtcNow);
        Assert.True(r.IsSuccess);
        Assert.Equal(PaymentLinkStatus.Opened, link.Status);
    }

    [Fact]
    public void Open_SuresiGecti_ExpiredVeRed()
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(10);
        var link = Pending(expiresAt: expiresAt);
        var r = link.Open(expiresAt.AddMinutes(1));
        Assert.False(r.IsSuccess);
        Assert.Equal(PaymentLinkResourceConstants.LINK_EXPIRED, r.Messages[0].Code);
        Assert.Equal(PaymentLinkStatus.Expired, link.Status);
    }

    [Fact]
    public void Open_Cancelled_Red()
    {
        var link = Pending();
        link.Cancel();
        var r = link.Open(DateTime.UtcNow);
        Assert.False(r.IsSuccess);
        Assert.Equal(PaymentLinkResourceConstants.LINK_CANCELLED, r.Messages[0].Code);
    }

    // --- IsPayable (US2, grace) ---

    [Fact]
    public void IsPayable_OpenedVeSureIcinde_Ok()
    {
        var link = Pending();
        link.Open(DateTime.UtcNow);
        Assert.True(link.IsPayable(DateTime.UtcNow, Grace).IsSuccess);
    }

    [Fact]
    public void IsPayable_SureGectiGraceIcinde_Ok()
    {
        // now = ExpiresAt + 2 dk; grace 5 dk → hâlâ ödenebilir (grace penceresi içinde).
        var expiresAt = DateTime.UtcNow.AddMinutes(10);
        var link = Pending(expiresAt: expiresAt);
        Assert.True(link.IsPayable(expiresAt.AddMinutes(2), Grace).IsSuccess);
    }

    [Fact]
    public void IsPayable_GraceDisi_ExpiredVeRed()
    {
        // now = ExpiresAt + 10 dk; grace 5 dk → grace dışı → Expired + red.
        var expiresAt = DateTime.UtcNow.AddMinutes(10);
        var link = Pending(expiresAt: expiresAt);
        var r = link.IsPayable(expiresAt.AddMinutes(10), Grace);
        Assert.False(r.IsSuccess);
        Assert.Equal(PaymentLinkResourceConstants.LINK_EXPIRED, r.Messages[0].Code);
        Assert.Equal(PaymentLinkStatus.Expired, link.Status);
    }

    // --- Cancel (US3) ---

    [Fact]
    public void Cancel_Pending_Cancelled()
    {
        var link = Pending();
        var r = link.Cancel();
        Assert.True(r.IsSuccess);
        Assert.Equal(PaymentLinkStatus.Cancelled, link.Status);
        Assert.True(link.IsTerminal);
    }

    [Fact]
    public void Cancel_Opened_Cancelled()
    {
        var link = Pending();
        link.Open(DateTime.UtcNow);
        var r = link.Cancel();
        Assert.True(r.IsSuccess);
        Assert.Equal(PaymentLinkStatus.Cancelled, link.Status);
    }

    [Fact]
    public void Cancel_Terminal_TerminalViolation()
    {
        var link = Pending();
        link.Cancel();
        var r = link.Cancel();
        Assert.False(r.IsSuccess);
        Assert.Equal(PaymentLinkResourceConstants.TERMINAL_VIOLATION, r.Messages[0].Code);
    }

    // --- RecordAttempt (090 çekim) ---

    private static PaymentLink Opened(decimal amount = 100m)
    {
        var link = Pending(amount);
        link.Open(DateTime.UtcNow);
        return link;
    }

    [Fact]
    public void Create_VarsayilanPaymentModel_NonSecure()
        => Assert.Equal(PaymentModel.NonSecure, Pending().PaymentModel);

    [Fact]
    public void RecordAttempt_Onay_PaidVeAttemptEklenir()
    {
        var link = Opened();
        var now = DateTime.UtcNow;
        var outcome = BankChargeOutcome.Approved("SIM-REF-1", "AUTH42", "00");

        var r = link.RecordAttempt("SIM-BANK-1", outcome, now, Grace);

        Assert.True(r.IsSuccess);
        Assert.Equal(PaymentLinkStatus.Paid, link.Status);
        Assert.True(link.IsTerminal);
        var attempt = Assert.Single(link.Attempts);
        Assert.True(attempt.IsApproved);
        Assert.Equal("SIM-BANK-1", attempt.BankCode);
        Assert.Equal("AUTH42", attempt.AuthCode);
        Assert.Null(attempt.DeclineClass);
    }

    [Fact]
    public void RecordAttempt_Red_OpenedKalirVeAttemptEklenir()
    {
        var link = Opened();
        var outcome = BankChargeOutcome.Declined(DeclineClass.IssuerDecline, "51");

        var r = link.RecordAttempt("SIM-BANK-1", outcome, DateTime.UtcNow, Grace);

        Assert.True(r.IsSuccess);                              // red outcome'da taşınır, Error değil (İlke IV)
        Assert.Equal(PaymentLinkStatus.Opened, link.Status);  // Opened kalır (yeni deneme mümkün)
        var attempt = Assert.Single(link.Attempts);
        Assert.False(attempt.IsApproved);
        Assert.Equal(DeclineClass.IssuerDecline, attempt.DeclineClass);
    }

    [Fact]
    public void RecordAttempt_CokluRedSonrasiOnay_PaidVeTumDenemelerKaydedilir()
    {
        var link = Opened();
        var now = DateTime.UtcNow;
        link.RecordAttempt("SIM-BANK-3", BankChargeOutcome.Declined(DeclineClass.TechnicalFailure, "96"), now, Grace);
        var r = link.RecordAttempt("SIM-BANK-1", BankChargeOutcome.Approved("REF", "AUTH", "00"), now, Grace);

        Assert.True(r.IsSuccess);
        Assert.Equal(PaymentLinkStatus.Paid, link.Status);
        Assert.Equal(2, link.Attempts.Count);
    }

    [Fact]
    public void RecordAttempt_PaidSonrasiIkinciDeneme_TerminalViolation()
    {
        var link = Opened();
        var now = DateTime.UtcNow;
        link.RecordAttempt("SIM-BANK-1", BankChargeOutcome.Approved("REF", "AUTH", "00"), now, Grace);

        // Çift-çekim guard: Paid link ikinci çekim YAPAMAZ (FR-002).
        var r = link.RecordAttempt("SIM-BANK-2", BankChargeOutcome.Approved("REF2", "AUTH2", "00"), now, Grace);

        Assert.False(r.IsSuccess);
        Assert.Equal(PaymentLinkResourceConstants.TERMINAL_VIOLATION, r.Messages[0].Code);
        Assert.Single(link.Attempts);                         // ikinci deneme eklenmez
    }

    [Fact]
    public void RecordAttempt_Cancelled_Red()
    {
        var link = Pending();
        link.Cancel();
        var r = link.RecordAttempt("SIM-BANK-1", BankChargeOutcome.Approved("REF", "AUTH", "00"), DateTime.UtcNow, Grace);
        Assert.False(r.IsSuccess);
        Assert.Equal(PaymentLinkResourceConstants.LINK_CANCELLED, r.Messages[0].Code);
        Assert.Empty(link.Attempts);
    }

    // --- BankChargeOutcome VO (T009) ---

    [Fact]
    public void BankChargeOutcome_Approved_Alanlar()
    {
        var o = BankChargeOutcome.Approved("REF-9", "AUTH-9", "00");
        Assert.True(o.IsApproved);
        Assert.Null(o.DeclineClass);
        Assert.Equal("REF-9", o.ProviderRef);
        Assert.Equal("AUTH-9", o.AuthCode);
        Assert.Equal("00", o.RawCode);
    }

    [Fact]
    public void BankChargeOutcome_Declined_Alanlar()
    {
        var o = BankChargeOutcome.Declined(DeclineClass.FraudDecline, "59", "REF-X");
        Assert.False(o.IsApproved);
        Assert.Equal(DeclineClass.FraudDecline, o.DeclineClass);
        Assert.Equal("59", o.RawCode);
        Assert.Equal("REF-X", o.ProviderRef);
        Assert.Null(o.AuthCode);
    }
}
