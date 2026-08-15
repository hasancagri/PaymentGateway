using System.ComponentModel.DataAnnotations;

namespace Payment.Api.Options;

// iyzico isteklerinin sabit (kullanıcıdan gelmeyen) protokol/uygulama alanları. Kural: handler metodu
// içinde dışarıdan (Command/Query) gelmeyen HİÇBİR değer literal olarak yazılmaz — hepsi buradan okunur.
// Runtime IConfiguration okuması yasak (CLAUDE.md) — Program.cs'te BindConfiguration(nameof(...)) ile
// bağlanır; handler düz POCO inject eder. Secret DEĞİL (appsettings.json). Transport secret'ları ayrı:
// IyzicoProviderSettings.
public class IyzicoRequestOptions
{
    /// <summary>iyzico locale (ör. "tr").</summary>
    [Required]
    public required string Locale { get; set; }

    /// <summary>Korelasyon etiketi — iyzico istek/yanıtta echo'lar (trace).</summary>
    [Required]
    public required string ConversationId { get; set; }

    /// <summary>Ödeme kanalı (ör. "WEB").</summary>
    [Required]
    public required string PaymentChannel { get; set; }

    /// <summary>Ödeme grubu (ör. "PRODUCT").</summary>
    [Required]
    public required string PaymentGroup { get; set; }

    /// <summary>Para birimi (ör. "TRY").</summary>
    [Required]
    public required string Currency { get; set; }

    /// <summary>Sepet kalemi tipi (ör. "PHYSICAL").</summary>
    [Required]
    public required string ItemType { get; set; }

    /// <summary>iyzico "başarılı" yanıt durumu etiketi (ör. "success").</summary>
    [Required]
    public required string SuccessStatus { get; set; }

    /// <summary>Saklı Kart uç yolu (create/delete) — BaseUrl'e eklenir (ör. "/cardstorage/card").</summary>
    [Required]
    public required string CardStoragePath { get; set; }

    /// <summary>Taksit sorgu uç yolu — BaseUrl'e eklenir (ör. "/payment/iyzipos/installment").</summary>
    [Required]
    public required string InstallmentPath { get; set; }

    /// <summary>NonSecure ödeme uç yolu — BaseUrl'e eklenir (ör. "/payment/auth").</summary>
    [Required]
    public required string PaymentAuthPath { get; set; }

    /// <summary>Saklı kart alias'ı (ör. "dropshop-card").</summary>
    [Required]
    public required string CardAlias { get; set; }

    /// <summary>Vault e-posta local-part öneki (ör. "vault").</summary>
    [Required]
    public required string EmailLocalPrefix { get; set; }

    /// <summary>Vault e-posta domaini (ör. "dropshop.com").</summary>
    [Required]
    public required string EmailDomain { get; set; }

    /// <summary>Sepet no öneki (ör. "B-").</summary>
    [Required]
    public required string BasketIdPrefix { get; set; }

    /// <summary>Alıcı kimlik no öneki (ör. "BY-").</summary>
    [Required]
    public required string BuyerIdPrefix { get; set; }
}