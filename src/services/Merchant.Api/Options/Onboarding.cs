using System.ComponentModel.DataAnnotations;

namespace Merchant.Api.Options;

// 045: hosted onboarding akış ayarları (form + teslim linki tabanı ve ömürleri). Runtime doğrudan
// IConfiguration okuması yasak (CLAUDE.md) — bu POCO Program.cs'te BindConfiguration(nameof(Onboarding))
// ile bağlanır. Link tabanı HttpContext'ten ALINMAZ (S2S çağrının base'i tarayıcıda çözülmez) —
// dışarıdan erişilir adres config'te (store 078 / Payment 041 emsali). Ölü ActivationBaseUrl kalktı.
public class Onboarding
{
    /// <summary>Merchant.Api'nin tarayıcıdan erişilir taban adresi (form + teslim sayfa linkleri).</summary>
    [Required]
    public required string PublicBaseUrl { get; set; }

    /// <summary>Hosted başvuru formu linkinin ömrü (tek başvuruluk).</summary>
    public TimeSpan FormLinkLifetime { get; set; } = TimeSpan.FromHours(24);

    /// <summary>Credential teslim linkinin ömrü (tek gösterimlik).</summary>
    public TimeSpan RevealLinkLifetime { get; set; } = TimeSpan.FromHours(1);
}
