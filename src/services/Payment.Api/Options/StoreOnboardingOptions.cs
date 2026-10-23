using System.ComponentModel.DataAnnotations;

namespace Payment.Api.Options;

// 091: Store onboarding (aktivasyon) yüzeyinin sabit (kullanıcıdan gelmeyen) alanları. Handler'da literal
// YASAK → hepsi buradan (CLAUDE.md). Hepsi non-secret (appsettings). Sırlar ayrı: banka inbound webhook
// imza anahtarı InterVposBankProfile.InboundCallbackSecret; store bildirim HMAC OnboardingStoreCallbackOptions;
// credential at-rest DEK CredentialEncryptionOptions.
public class StoreOnboardingOptions
{
    /// <summary>Form linki ömrü (TTL). ExpiresAt = CreatedTime + Ttl; süre dolunca form açılmaz (FR-002).</summary>
    [Required]
    public TimeSpan Ttl { get; set; } = TimeSpan.FromHours(24);

    /// <summary>Hosted form linkinin mutlak tabanı; link = {taban}/{OpenToken} (FR-002/FR-003).</summary>
    [Required]
    public required string FormBaseUrl { get; set; }

    /// <summary>PG'nin banka→PG webhook'u için mutlak tabanı; CallbackUrl = {taban}/internal/onboarding/callback/{CallbackToken} (FR-007).</summary>
    [Required]
    public required string CallbackBaseUrl { get; set; }

    /// <summary>Hosted onboarding form sayfası başlığı.</summary>
    [Required]
    public required string FormPageTitle { get; set; }

    /// <summary>Hosted onboarding form sayfası giriş ibaresi.</summary>
    [Required]
    public required string FormPageIntro { get; set; }

    /// <summary>Başvurunun fan-out edileceği banka kodları (FR-005). Her kod bir IBankOnboardingAdapter ile eşleşir.</summary>
    [Required]
    [MinLength(1)]
    public List<string> BankCodes { get; set; } = new();
}
