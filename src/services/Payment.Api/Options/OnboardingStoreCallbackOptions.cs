using System.ComponentModel.DataAnnotations;

namespace Payment.Api.Options;

// 091 (FR-013, İlke VI): Store'a giden "Live oldun" HMAC-imzalı dayanıklı bildirim ayarları (StoreResultDelivery
// aynası, onboarding yönü). CallbackUrl non-secret (appsettings); CallbackSecret SIR → yalnız secret-store,
// appsettings'e YAZILMAZ ve log/trace/body/dönüş DTO'sunda görünmez. Credential İÇERMEZ (yalnız "Live" + banka).
public class OnboardingStoreCallbackOptions
{
    /// <summary>Store'un aktivasyon sonucunu alacağı webhook adresi (non-secret, appsettings).</summary>
    [Required]
    public required string CallbackUrl { get; set; }

    /// <summary>Ham gövdeyi imzalayan HMAC anahtarı. SIR — secret-store'dan; appsettings'e ASLA yazılmaz.</summary>
    [Required]
    public required string CallbackSecret { get; set; }
}
