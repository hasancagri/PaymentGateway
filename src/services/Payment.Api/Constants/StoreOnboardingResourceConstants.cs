namespace Payment.Api.Constants;

// 091: Store onboarding/aktivasyon yüzeyi hata kodları (Result pattern: Code sabittir, serbest metin yasak).
// Her servis kendi kodlarına sahip (conventions — Constants/ altında, Domains/ değil).
public static class StoreOnboardingResourceConstants
{
    public static readonly string ONBOARDING_NOT_FOUND = "STORE_ONBOARDING_NOT_FOUND"; // bilinmeyen OpenToken/CallbackToken/PgStoreRef (fail-closed)
    public static readonly string LINK_EXPIRED = "STORE_ONBOARDING_LINK_EXPIRED"; // form TTL aşımı (FR-002)
    public static readonly string INVALID_PROFILE = "STORE_ONBOARDING_INVALID_PROFILE"; // zorunlu alan/IBAN/MCC geçersiz (FR-004)
    public static readonly string BANK_FIELD_MISSING = "STORE_ONBOARDING_BANK_FIELD_MISSING"; // banka-özel zorunlu alan yok → o banka submit-öncesi red (FR-006)
    public static readonly string ALREADY_SUBMITTED = "STORE_ONBOARDING_ALREADY_SUBMITTED"; // Draft değilken submit
    public static readonly string DUPLICATE_ONBOARDING = "STORE_ONBOARDING_DUPLICATE"; // aynı store için aktif süreç/Live var (FR-014)
    public static readonly string CALLBACK_REJECTED = "STORE_ONBOARDING_CALLBACK_REJECTED"; // token/imza eşleşmedi (FR-007)
    public static readonly string BANK_NOT_FOUND = "STORE_ONBOARDING_BANK_NOT_FOUND"; // callback/resubmit bilinmeyen banka
    public static readonly string STORE_NOT_LIVE = "STORE_ONBOARDING_STORE_NOT_LIVE"; // Live olmayan store için ödeme linki (FR-010)
}
