namespace Payment.Api.Options;

// 090: Banka yönlendirme politikası (non-secret, appsettings). Router aday sıralamasında kullanacağı
// fallback sırası + sağlık/circuit seam'i. Handler/router'da literal yasak → hepsi buradan (CLAUDE.md).
public class BankRoutingOptions
{
    /// <summary>BIN/maliyet çözülemediğinde (bilinmeyen BIN + eşit maliyet) kullanılacak kararlı fallback
    /// sırası (banka kodları). Boşsa router yalnız CostPriority'ye göre sıralar.</summary>
    public List<string> OrderedBankCodes { get; set; } = new();

    // TODO(health/circuit — sonraki versiyon, plan D4): banka sağlık durumu + circuit-breaker eşiği burada
    // yapılandırılacak; sağlıksız banka aday listesinden proaktif elenecek. Bu versiyonda failover reaktif
    // (yalnız TechnicalFailure/ConfigError dönüşünde sıradaki adaya geçilir).
}
