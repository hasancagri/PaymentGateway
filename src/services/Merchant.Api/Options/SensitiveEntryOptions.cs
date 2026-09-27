using System.ComponentModel.DataAnnotations;

namespace Merchant.Api.Options;

// 048 D6: hassas-veri hosted sayfası config'i — section "SensitiveEntryOptions". Link tabanı
// HttpContext'ten ALINMAZ (MCP çağrısı store fasadı/gateway proxy'sinden gelir; istek base'i
// Aspire iç adresidir, tarayıcıda çözülmez) → dışarıdan erişilir adres config'te (045 emsali).
public class SensitiveEntryOptions
{
    // Merchant.Api'nin tarayıcıdan erişilir taban adresi (link: {PublicBaseUrl}/merchants/sensitive/{token}).
    [Required] public string PublicBaseUrl { get; set; } = string.Empty;

    // Hosted link'in ömrü (üretimden itibaren); tek kullanımlık token bu süre sonunda ölür (mutlak son-kullanım).
    public TimeSpan LinkLifetime { get; set; } = TimeSpan.FromMinutes(15);
}
