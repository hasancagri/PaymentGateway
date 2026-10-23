using Common.Domains;

namespace Payment.Api.Domains.StoreOnboarding;

/// <summary>
/// 091: Bir store'un (ör. ECommerceAgent sitesi) internet satışına açılma sürecinin tutarlılık sınırı.
/// Yaşam döngüsü: Draft → Submitted → Live. Store, PG hosted formunu (OpenToken) doldurur; başvuru
/// yapılandırılmış her bankaya fan-out edilir (banka başına BankApplication). Banka async inceler; sonuç
/// banka→PG webhook (CallbackToken + HMAC) ile iner. İLK banka onayında store Live (ilk onayda satışa açılır).
/// Per-banka durum izole (bir red ötekini + Live'ı etkilemez). Credential aggregate'e GİRMEZ — yalnız opak
/// CredentialRef (İlke VI). Süre dolumu LAZY (erişim anında). Komisyon YOK (PG yalnız durumu yansıtır).
/// </summary>
public class StoreOnboarding : AggregateRoot
{
    // Marten Newtonsoft private-setter deserializasyonu için parametresiz ctor (NonPublicSetters).
    private StoreOnboarding() { }

    public Guid MerchantId { get; private set; }
    public string CorrelationId { get; private set; } = string.Empty;
    public string OpenToken { get; private set; } = string.Empty;
    public string CallbackToken { get; private set; } = string.Empty;
    public string StoreName { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public OnboardingStatus Status { get; private set; }
    public StoreProfile? Profile { get; private set; }

    // İlke II — koleksiyon private, okuma IReadOnlyList, mutasyon yalnız aggregate metotlarından.
    private readonly List<BankApplication> _applications = new();
    public IReadOnlyList<BankApplication> Applications => _applications;

    /// <summary>Store canlı satışa açık mı — saf getter (CreatePaymentLink Live-guard'ı kullanır, FR-010).</summary>
    public bool IsLive => Status == OnboardingStatus.Live;

    /// <summary>Yeni onboarding süreci (Draft). Zorunlu alan + expiresAt gelecekte. OpenToken + CallbackToken
    /// tahmin-edilemez sırlar (handler üretir); expiresAt = CreatedTime + Ttl.</summary>
    public static ResultDomain<StoreOnboarding> Create(
        Guid merchantId, string correlationId, string storeName, string openToken, string callbackToken, DateTime expiresAt)
    {
        if (merchantId == Guid.Empty || string.IsNullOrWhiteSpace(correlationId) ||
            string.IsNullOrWhiteSpace(storeName) || string.IsNullOrWhiteSpace(openToken) ||
            string.IsNullOrWhiteSpace(callbackToken))
            return ResultDomain<StoreOnboarding>.Error(new MessageItem { Code = StoreOnboardingResourceConstants.INVALID_PROFILE });

        var onboarding = new StoreOnboarding
        {
            MerchantId = merchantId,
            CorrelationId = correlationId,
            StoreName = storeName,
            OpenToken = openToken,
            CallbackToken = callbackToken,
            Status = OnboardingStatus.Draft
        };

        if (expiresAt <= onboarding.CreatedTime)
            return ResultDomain<StoreOnboarding>.Error(new MessageItem { Code = StoreOnboardingResourceConstants.INVALID_PROFILE });

        onboarding.ExpiresAt = expiresAt;
        return ResultDomain<StoreOnboarding>.Ok(onboarding);
    }

    /// <summary>Formdan gelen profili bağlar + başvuruyu bankalara fan-out eder (Draft→Submitted). Draft değil →
    /// ALREADY_SUBMITTED; now &gt; ExpiresAt → lazy LINK_EXPIRED. fanout = handler'ın her banka adapter'ından
    /// topladığı (bankCode, referral sonucu) — aggregate wire görmez (İlke VII). Her sonuç bir BankApplication:
    /// Submitted→Referred, Rejected→Rejected (banka-özel eksik alan, FR-006). Referral anında credential YOK.</summary>
    public ResultDomain SubmitProfile(StoreProfile profile, IReadOnlyList<(string BankCode, OnboardingOutcome Result)> fanout, DateTime now)
    {
        if (Status != OnboardingStatus.Draft)
            return ResultDomain.Error(new MessageItem { Code = StoreOnboardingResourceConstants.ALREADY_SUBMITTED });

        if (now > ExpiresAt)
        {
            // Lazy expiry: süre dolmuş Draft submit edilemez (FR-002).
            return ResultDomain.Error(new MessageItem { Code = StoreOnboardingResourceConstants.LINK_EXPIRED });
        }

        Profile = profile;
        foreach (var (bankCode, result) in fanout)
            _applications.Add(BankApplication.From(bankCode, result, now));

        Status = OnboardingStatus.Submitted;
        UpdatedTime = now;
        return ResultDomain.Ok();
    }

    /// <summary>091 (FR-009): Bir bankanın async sonucunu kaydeder (webhook ya da reconcile'dan). İlgili banka
    /// başvurusu yoksa BANK_NOT_FOUND. Approved → başvuru Approved + credentialRef set; **ilk Approved'da
    /// Status=Live** (store satışa açılır). Zaten Live ise Live kalır (çift-aktivasyon YOK). Aynı banka zaten
    /// Approved iken tekrar Approved → idempotent no-op (webhook+reconcile çifte-teslimi). Rejected → başvuru
    /// Rejected + sebep; diğer bankalar + store Live'lığı etkilenmez (izolasyon, FR-011). credentialRef =
    /// handler'ın credential-store'a yazıp verdiği opak Guid (yalnız Approved yolunda).</summary>
    public ResultDomain RecordBankOutcome(string bankCode, OnboardingOutcome outcome, Guid? credentialRef, DateTime now)
    {
        var app = _applications.FirstOrDefault(a => a.BankCode == bankCode);
        if (app is null)
            return ResultDomain.Error(new MessageItem { Code = StoreOnboardingResourceConstants.BANK_NOT_FOUND });

        // Idempotent: zaten Approved banka için tekrar Approved → no-op (çifte webhook/reconcile).
        if (app.Status == BankApplicationStatus.Approved && outcome.Kind == OnboardingOutcomeKind.Approved)
            return ResultDomain.Ok();

        app.Apply(outcome, credentialRef, now);

        // İlk onayda (ya da herhangi bir onay varken) store Live — tek-yön, çift-aktivasyon tetiklemez.
        if (outcome.Kind == OnboardingOutcomeKind.Approved && Status != OnboardingStatus.Live)
            Status = OnboardingStatus.Live;

        UpdatedTime = now;
        return ResultDomain.Ok();
    }

    /// <summary>091 (FR-011): Reddedilen bir bankaya yeniden başvuru. Banka başvurusu yoksa BANK_NOT_FOUND;
    /// Rejected değilse ALREADY_SUBMITTED (yalnız red sonrası resubmit). result = handler'ın yeni referral
    /// sonucu (Submitted→Referred). Diğer bankalar + store durumu etkilenmez.</summary>
    public ResultDomain Resubmit(string bankCode, OnboardingOutcome result, DateTime now)
    {
        var app = _applications.FirstOrDefault(a => a.BankCode == bankCode);
        if (app is null)
            return ResultDomain.Error(new MessageItem { Code = StoreOnboardingResourceConstants.BANK_NOT_FOUND });
        if (app.Status != BankApplicationStatus.Rejected)
            return ResultDomain.Error(new MessageItem { Code = StoreOnboardingResourceConstants.ALREADY_SUBMITTED });

        app.Apply(result, null, now);
        UpdatedTime = now;
        return ResultDomain.Ok();
    }
}

// Enum aggregate dosyasında (conventions — ayrı dosya/Enumeration base yok).
public enum OnboardingStatus
{
    Draft = 0,
    Submitted = 1,
    Live = 2
}

public enum BankApplicationStatus
{
    Referred = 0,
    Approved = 1,
    Rejected = 2
}
