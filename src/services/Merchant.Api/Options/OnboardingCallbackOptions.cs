using System.ComponentModel.DataAnnotations;

namespace Merchant.Api.Options;

// 087: store↔PG onboarding makine-handoff sırları. İki ayrı ömür (research Karar 3) — biri rotate
// edilince öteki etkilenmez; ikisi de MerchantKey'den (per-merchant, ödeme) AYRI. IConfiguration
// doğrudan okuması yasak (CLAUDE.md) — Program.cs BindConfiguration(nameof(OnboardingCallbackOptions)).
// Dev user-secrets, prod vault (S5 borcuyla aynı).
public class OnboardingCallbackOptions
{
    /// <summary>Register ucu (`POST /onboarding/register`) `X-Registration-Key` doğrulaması —
    /// ortak, platform-seviyesi bootstrap sır (kayıtta henüz MerchantKey yok, chicken-egg).</summary>
    [Required]
    public required string BootstrapRegistrationKey { get; set; }

    /// <summary>PG→store credential callback HMAC-SHA256 imzası (041 CallbackSecret emsali);
    /// MerchantKey'den ayrı sır.</summary>
    [Required]
    public required string CallbackSecret { get; set; }
}