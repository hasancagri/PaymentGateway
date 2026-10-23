namespace Payment.Api.Domains.StoreOnboarding.BankOnboarding;

/// <summary>
/// 091 (İlke VII port/adapter): ortak banka ONBOARDING portu — charge portundan (IBankPaymentAdapter) AYRI.
/// Her banka kendi adapter'ıyla gerçekler; wire istek/cevap tipleri adapter'ın İÇİNDE private nested kalır
/// (domain sınırını geçmez). Paylaşılan SDK ÜRETİLMEZ. Adapter STATELESS (durum StoreOnboarding aggregate'te).
/// İki yön anti-corruption: canonical→wire (SubmitReferral), wire→canonical (NormalizeCallback). Onay kararı +
/// credential bankaya aittir (banka OTORİTE); PG yalnız sonucu yansıtır.
/// </summary>
public interface IBankOnboardingAdapter
{
    /// <summary>Bu adapter'ın bankası (StoreOnboardingOptions.BankCodes + InterVposBankProfile.BankCode ile eşleşir).</summary>
    string BankCode { get; }

    /// <summary>Canonical referral'ı bankanın intake wire'ına çevirip gönderir. Banka-özel zorunlu alan çekirdekte
    /// YOKSA wire'a gitmeden Rejected(BANK_FIELD_MISSING) döner (B kararı). Ağ/teknik hata → Rejected (reconcile
    /// toparlar). Başarılı kuyruklama → Submitted(bankApplicationRef). Asıl onay sonra webhook'la gelir.</summary>
    Task<ResultDomain<OnboardingOutcome>> SubmitReferralAsync(OnboardingReferral request, CancellationToken ct);

    /// <summary>Reconcile (watchdog): bankadaki başvurunun güncel durumunu sorar → Approved(cred,ref) /
    /// Rejected(reason) / hâlâ sürüyor (Submitted). Kaçan webhook'u buradan toparlar (FR-012).</summary>
    Task<ResultDomain<OnboardingOutcome>> QueryApplicationStatusAsync(string bankApplicationRef, CancellationToken ct);

    /// <summary>Banka inbound webhook ham gövdesini normalize OnboardingOutcome'a çevirir (anti-corruption).
    /// Approved'da credential'ı çıkarır (yalnız bu yolda). Her bankanın gövdesi farklı → adapter çözer.</summary>
    ResultDomain<OnboardingOutcome> NormalizeCallback(string rawBody);
}

// Canonical referral (domain-görünür). Banka wire'ı adapter İÇİNDE türetilir (nested). CallbackUrl + token =
// bankanın sonucu geri postalayacağı PG ucu (per-session, path-gated — İlke V).
public sealed record OnboardingReferral(StoreProfile Profile, string BankCode, string CallbackUrl, string CallbackToken);

// Normalize onboarding sonucu (İlke VII anti-corruption). Credential YALNIZ Approved + yalnız NormalizeCallback
// yolunda dolu (webhook gövdesinden). VO: record + private ctor + static factory.
public sealed record OnboardingOutcome
{
    public OnboardingOutcomeKind Kind { get; }
    public string? ReasonCode { get; }
    public string? BankApplicationRef { get; }
    public BankMerchantCredential? Credential { get; }

    private OnboardingOutcome(OnboardingOutcomeKind kind, string? reasonCode, string? bankApplicationRef, BankMerchantCredential? credential)
    {
        Kind = kind;
        ReasonCode = reasonCode;
        BankApplicationRef = bankApplicationRef;
        Credential = credential;
    }

    public static OnboardingOutcome Submitted(string bankApplicationRef)
        => new(OnboardingOutcomeKind.Submitted, null, bankApplicationRef, null);

    public static OnboardingOutcome Approved(BankMerchantCredential credential, string bankApplicationRef)
        => new(OnboardingOutcomeKind.Approved, null, bankApplicationRef, credential);

    public static OnboardingOutcome Rejected(string reasonCode)
        => new(OnboardingOutcomeKind.Rejected, reasonCode, null, null);
}

public enum OnboardingOutcomeKind
{
    Submitted = 0,
    Approved = 1,
    Rejected = 2
}
