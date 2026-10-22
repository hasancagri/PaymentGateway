using System.ComponentModel.DataAnnotations;

namespace Common.Options;

// EC→PG dış çağrı API key'i. SIR → secret-store (user-secrets/env), appsettings'e YAZILMAZ (İlke VI;
// log/trace/DTO'da görünmez). ECommerce aynı key'i gönderir, PG sabit-zamanlı karşılaştırır. Rotate elli (iki taraf).
public class StoreApiKeyOptions
{
    /// <summary>EC'den beklenen paylaşılan API key (X-Api-Key header değeri).</summary>
    [Required]
    public required string ExpectedKey { get; set; }
}
