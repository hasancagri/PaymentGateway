using AgentSkill = A2A.AgentSkill;

namespace Payment.Agent;

/// <summary>
/// A2A Agent Card sözleşmesi — Payment.Agent'ın <c>/.well-known/agent-card.json</c>'da ilan ettiği
/// opak yüzey. E-ticaret agent'ı yalnız skill'leri görür; iç MCP tool adları/banka/POS ayrıntısı
/// kartta YOKTUR. Şema tam kart alanı (PAN/CVV/expiry) içermez (SC-006). <c>pay-with-token</c> YOK
/// (çekim ertelendi).
/// </summary>
public static class PaymentAgentCard
{
    public static AgentCard Create(string agentUrl) => new()
    {
        Name = "PaymentAgent",
        Description = "Kayıtlı kart token'ı ile taksit seçeneklerini getirir ve seçimi kaydeder " +
                      "(Model A: kullanıcı sepet tutarını öder). Tam kart verisi (PAN/CVV) KABUL ETMEZ.",
        Version = "0.7.0",
        DefaultInputModes = ["text"],
        DefaultOutputModes = ["text"],
        Capabilities = new AgentCapabilities
        {
            Streaming = true,
            PushNotifications = false
        },
        Skills =
        [
            new AgentSkill
            {
                Id = "installment_quote",
                Name = "BIN ile taksit seçeneklerini getir",
                Description = "Kartın BIN'i (ilk 6 hane, hassas değil) + sepet tutarı ile desteklenen " +
                              "taksit seçeneklerini Model A tutarlarıyla (her satır = sepet tutarı) READ-ONLY " +
                              "döner. Token/PAN/CVV KABUL ETMEZ; oturum açmaz. (024 e-ticaret quote-only akışı.)",
                Tags = ["payment", "installments", "quote", "bin"],
                Examples = ["Default kartımla sepetimdeki tutar için taksitleri göster"]
            },
            new AgentSkill
            {
                Id = "quote-installments",
                Name = "Taksit seçeneklerini getir",
                Description = "Kayıtlı kart token'ı + sepet tutarı ile desteklenen taksit seçeneklerini " +
                              "Model A tutarlarıyla (her satır = sepet tutarı) döner.",
                Tags = ["payment", "installments", "quote"],
                Examples = ["Hesabımdaki kartla sepetimi almak istiyorum"]
            }
        ],
        SupportedInterfaces =
        [
            new AgentInterface
            {
                Url = agentUrl,
                ProtocolBinding = ProtocolBindingNames.JsonRpc,
                ProtocolVersion = "1.0"
            }
        ]
    };
}