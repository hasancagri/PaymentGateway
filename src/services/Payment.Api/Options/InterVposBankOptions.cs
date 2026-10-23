using System.ComponentModel.DataAnnotations;

namespace Payment.Api.Options;

// Phase-1: PG'nin bağlanacağı gerçek InterVpos banka(ları). Her banka = adapter örneği. BankCode routing
// kimliği; BaseUrl simülatörün mutlak adresi; ShopCode/UserCode PG'nin bankadaki üye-işyeri kimliği.
// UserPass SIR → yalnız secret-store (user-secrets: InterVposBankOptions:Banks:0:UserPass), appsettings'e YAZMA.
public class InterVposBankOptions
{
    public List<InterVposBankProfile> Banks { get; set; } = new();
}

public class InterVposBankProfile
{
    /// <summary>Routing/adapter kimliği (ör. MAVIBANK) — PosAccount.BankCode + BinCard.IssuerBankCode ile eşleşir.</summary>
    [Required]
    public string BankCode { get; set; } = string.Empty;

    /// <summary>Bankanın (simülatörün) mutlak adresi; çağrı {BaseUrl}/mpi/Default.aspx.</summary>
    [Required]
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>PG'nin bu bankadaki üye-işyeri kodu.</summary>
    [Required]
    public string ShopCode { get; set; } = string.Empty;

    /// <summary>POS kullanıcı kodu.</summary>
    [Required]
    public string UserCode { get; set; } = string.Empty;

    /// <summary>POS parolası — SIR (secret-store). Log/trace/DTO'da görünmez (İlke VI).</summary>
    [Required]
    public string UserPass { get; set; } = string.Empty;

    /// <summary>Banka protokolü: "InterVpos" (form+`;;`+SHA1) ya da "TuruncuXml" (XML+SHA512). Fan-out
    /// doğru adapter'ı seçer (İlke VII — ortak port, farklı tel).</summary>
    public string Protocol { get; set; } = "InterVpos";

    /// <summary>İmza anahtarı (SIR, secret-store) — TuruncuXml SHA512 + InterVpos 3D hash (istek + callback
    /// doğrulama) için. InterVpos NonSecure'da kullanılmaz (boş olabilir); 3DS kullanılacaksa zorunlu.</summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>Banka operasyon-API (mutabakat) abonelik anahtarı — SIR (secret-store). Ödeme yüzünden AYRI
    /// yüz (`/api/*` JSON, SubscriptionKey header). Gün-sonu mutabakat pull'u kullanır; boşsa banka atlanır.</summary>
    public string SubscriptionKey { get; set; } = string.Empty;

    /// <summary>091: banka→PG onboarding sonuç webhook'u (ReceiveBankOnboardingOutcome) imza doğrulama anahtarı
    /// — SIR (secret-store). Banka ham gövdeyi bu anahtarla HMAC imzalar; PG aynı anahtarla doğrular (FR-007,
    /// İlke VI). Boşsa o bankanın callback'i reddedilir (fail-closed).</summary>
    public string InboundCallbackSecret { get; set; } = string.Empty;
}
