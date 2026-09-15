namespace Payment.Api.Constants;

// 041: Hosted-CF ödeme yüzeyi hata kodları (Result pattern: Code sabittir, serbest metin yasak).
// Her servis kendi kodlarına sahip (conventions — Constants/ altında, Domains/ değil).
public static class HostedPaymentResourceConstants
{
    public static readonly string MERCHANT_NOT_ACTIVE = "HOSTED_PAYMENT_MERCHANT_NOT_ACTIVE";     // charge yalnız Active (İlke V, fail-closed)
    public static readonly string UNSUPPORTED_CURRENCY = "HOSTED_PAYMENT_UNSUPPORTED_CURRENCY";   // yalnız TRY (FR-012)
    public static readonly string INVALID_REQUEST = "HOSTED_PAYMENT_INVALID_REQUEST";             // tutar/alan geçersiz
    public static readonly string PROVIDER_UNAVAILABLE = "HOSTED_PAYMENT_PROVIDER_UNAVAILABLE";   // iyzico erişilemez/timeout
    public static readonly string INITIALIZE_FAILED = "HOSTED_PAYMENT_INITIALIZE_FAILED";         // CF initialize başarısız/boş token
    public static readonly string SESSION_NOT_FOUND = "HOSTED_PAYMENT_SESSION_NOT_FOUND";         // callbackToken bilinmeyen (C1)
    public static readonly string TERMINAL_VIOLATION = "HOSTED_PAYMENT_TERMINAL_VIOLATION";       // terminal durumdan geri dönüş (FR-008)
}
