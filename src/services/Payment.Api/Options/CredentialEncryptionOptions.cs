using System.ComponentModel.DataAnnotations;

namespace Payment.Api.Options;

// 091 (İlke VI, anayasa v3.1.0 runtime-teslim sır): per-store banka credential'ı RUNTIME webhook'la gelir →
// statik secret-store'a yazılamaz → at-rest AES-GCM şifreli saklanır. Şifre anahtarı (DEK) burada; SIR →
// yalnız secret-store (user-secrets/env), appsettings'e ASLA yazılmaz, log/trace/DTO'da görünmez. Ciphertext
// DEK olmadan kullanılamaz → ciphertext sır değil; plaintext yalnız çekim anında bellekte çözülür.
public class CredentialEncryptionOptions
{
    /// <summary>At-rest AES-256-GCM veri şifreleme anahtarı (DEK), Base64 32 byte. SIR — secret-store.</summary>
    [Required]
    public required string Key { get; set; }
}
