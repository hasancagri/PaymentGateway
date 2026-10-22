using System.ComponentModel.DataAnnotations;

namespace Payment.Api.Options;

// 090 (FR-009, İlke VI): Store'a giden HMAC-imzalı dayanıklı sonuç bildirimi ayarları. CallbackUrl
// non-secret (appsettings); CallbackSecret SIR → yalnız secret-store (user-secrets/env), appsettings'e
// YAZILMAZ ve log/trace/body/dönüş DTO'sunda görünmez. Kontrol düzlemi + callback sır döndürmez (CLAUDE.md).
public class StoreCallbackOptions
{
    /// <summary>Store'un terminal sonucu alacağı webhook adresi (non-secret, appsettings).</summary>
    [Required]
    public required string CallbackUrl { get; set; }

    /// <summary>Ham gövdeyi imzalayan HMAC anahtarı. SIR — secret-store'dan gelir; appsettings'e ASLA yazılmaz.</summary>
    [Required]
    public required string CallbackSecret { get; set; }
}
