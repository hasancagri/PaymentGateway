using System.ComponentModel.DataAnnotations;

namespace Payment.Api.Options;

// 041: Hosted-CF ödeme yüzeyinin sabit (kullanıcıdan gelmeyen) uygulama alanları. Kural: handler metodu
// içinde dışarıdan gelmeyen HİÇBİR değer literal yazılmaz — hepsi buradan (CLAUDE.md). IConfiguration
// doğrudan okuma yasak → Program.cs BindConfiguration(nameof(...)); handler düz POCO inject eder.
// CallbackSecret SECRET'tir (user-secrets); diğerleri non-secret (appsettings.json).
public class HostedPaymentOptions
{
    /// <summary>Store'a giden sonuç bildiriminin imza anahtarı (HMAC-SHA256). MerchantKey'den AYRI;
    /// v1 paylaşılan (per-merchant rotasyon backlog). SECRET — user-secrets.</summary>
    [Required]
    public required string CallbackSecret { get; set; }

    /// <summary>C1: iyzico'ya verilen callback URL'inin MUTLAK tabanı (ör.
    /// "https://.../internal/payments/callback"). Runtime `+ "/" + session.CallbackToken` eklenir —
    /// per-session tahmin-edilemez sır uca beyan-edilen yetkiyi taşır (İlke V).</summary>
    [Required]
    public required string IyzicoCallbackBaseUrl { get; set; }

    /// <summary>Müşteri dönüş sayfasının mutlak tabanı (ör. "https://.../payments/return"); iyzico
    /// dönüşü işlendikten sonra tarayıcı `{taban}/{pgPaymentRef}`'e yönlendirilir (FR-010).</summary>
    [Required]
    public required string ReturnPageBaseUrl { get; set; }

    /// <summary>Başarılı ödeme dönüş sayfası metni (FR-010).</summary>
    [Required]
    public required string ReturnSuccessText { get; set; }

    /// <summary>Başarısız ödeme dönüş sayfası metni (FR-010).</summary>
    [Required]
    public required string ReturnFailureText { get; set; }
}
