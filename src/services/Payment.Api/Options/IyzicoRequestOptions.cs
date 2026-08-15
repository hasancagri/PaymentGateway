using System.ComponentModel.DataAnnotations;

namespace Payment.Api.Options;

// iyzico isteklerinin sabit (kullanıcıdan gelmeyen) protokol alanları. Runtime IConfiguration okuması
// yasak (CLAUDE.md) — bu POCO Program.cs'te BindConfiguration(nameof(IyzicoRequestOptions)) ile bağlanır;
// handler düz POCO inject eder. Secret DEĞİL (appsettings.json). Transport secret'ları ayrı:
// IyzicoProviderSettings.
public class IyzicoRequestOptions
{
    /// <summary>iyzico locale (ör. "tr").</summary>
    [Required]
    public required string Locale { get; set; }

    /// <summary>Korelasyon etiketi — iyzico istek/yanıtta echo'lar (trace).</summary>
    [Required]
    public required string ConversationId { get; set; }
}