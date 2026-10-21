namespace Payment.Api.Constants;

// 089: PaymentLink (pay-by-link) yüzeyi hata kodları (Result pattern: Code sabittir, serbest metin yasak).
// Her servis kendi kodlarına sahip (conventions — Constants/ altında, Domains/ değil).
public static class PaymentLinkResourceConstants
{
    public static readonly string UNSUPPORTED_CURRENCY = "PAYMENT_LINK_UNSUPPORTED_CURRENCY";   // yalnız TRY (FR-004)
    public static readonly string INVALID_REQUEST = "PAYMENT_LINK_INVALID_REQUEST";             // tutar/alan geçersiz
    public static readonly string PROVIDER_UNAVAILABLE = "PAYMENT_LINK_PROVIDER_UNAVAILABLE";   // tüm bankalar technical/config (FR-006) ya da aday yok
    public static readonly string CHARGE_DECLINED = "PAYMENT_LINK_CHARGE_DECLINED";             // issuer/fraud reddi — "başka kartla deneyin" (FR-012)
    public static readonly string TERMINAL_VIOLATION = "PAYMENT_LINK_TERMINAL_VIOLATION";       // terminal durumdan geçiş (FR-008)
    public static readonly string LINK_NOT_FOUND = "PAYMENT_LINK_NOT_FOUND";                     // bilinmeyen OpenToken/PgPaymentRef (fail-closed, FR-007)
    public static readonly string LINK_EXPIRED = "PAYMENT_LINK_EXPIRED";                         // TTL (+grace) aşımı (FR-009)
    public static readonly string LINK_CANCELLED = "PAYMENT_LINK_CANCELLED";                     // iptal edilmiş linke işlem (FR-008)
    public static readonly string NO_REVERSIBLE_CHARGE = "PAYMENT_LINK_NO_REVERSIBLE_CHARGE";     // void/refund için onaylı çekim yok
    public static readonly string REVERSAL_FAILED = "PAYMENT_LINK_REVERSAL_FAILED";               // banka void/refund reddetti
}
