using System.ComponentModel.DataAnnotations;

namespace Payment.Api.Options;

// 032: iyzico sağlayıcı erişim ayarları. Runtime doğrudan IConfiguration okuması yasak (CLAUDE.md) —
// bu POCO Program.cs'te BindConfiguration(nameof(IyzicoProviderSettings)) ile bağlanır; sandbox
// key/secret user-secrets'tan gelir (git'e girmez, FR-009). Handler düz ProviderOptions inject eder
// (bu POCO'dan map'lenmiş singleton).
public class IyzicoProviderSettings
{
    [Required]
    public required string ApiKey { get; set; }

    [Required]
    public required string SecretKey { get; set; }

    /// <summary>Sağlayıcı taban adresi (sandbox: https://sandbox-api.iyzipay.com).</summary>
    [Required]
    public required string BaseUrl { get; set; }
}
