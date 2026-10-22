using System.ComponentModel.DataAnnotations;

namespace Payment.Api.Options;

// 089: PaymentLink (pay-by-link) yüzeyinin sabit (kullanıcıdan gelmeyen) uygulama alanları. Kural: handler
// metodu içinde dışarıdan gelmeyen HİÇBİR değer literal yazılmaz — hepsi buradan (CLAUDE.md). IConfiguration
// doğrudan okuma yasak → Program.cs BindConfiguration(nameof(...)); handler düz POCO inject eder.
// Bu dilimde hepsi non-secret (appsettings.json); store'a HMAC bildirim + CallbackSecret sonraki dilim.
public class PaymentLinkOptions
{
    /// <summary>Link ömrü (TTL). Pending→ExpiresAt = CreatedTime + Ttl; süre dolunca açılamaz (FR-002).</summary>
    [Required]
    public TimeSpan Ttl { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>Açılmış (Opened) oturuma, TTL dolumunda tanınan kısa ödeme penceresi (FR-009, grace).</summary>
    [Required]
    public TimeSpan GraceWindow { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Müşteri açılış linkinin mutlak tabanı (ör. "https://.../payment-links"); link = {taban}/{OpenToken}.</summary>
    [Required]
    public required string LinkBaseUrl { get; set; }

    /// <summary>Güven bağlamı sayfası başlığı (FR-006).</summary>
    [Required]
    public required string TrustPageTitle { get; set; }

    /// <summary>Güven bağlamı sayfası giriş ibaresi (kart formundan önce, FR-006).</summary>
    [Required]
    public required string TrustPageIntro { get; set; }
}
