using AgentSkill = A2A.AgentSkill;

namespace Payment.Agent;

/// <summary>
/// A2A Agent Card sözleşmesi — Payment.Agent'ın <c>/.well-known/agent-card.json</c>'da ilan ettiği
/// opak yüzey. E-ticaret agent'ı yalnız skill'leri görür; iç MCP tool adları/sağlayıcı ayrıntısı
/// kartta YOKTUR. Şema tam kart alanı (PAN/CVV/expiry) İÇERMEZ ve KABUL ETMEZ. 038: çekim skill'i
/// eklendi (charge_saved_card); kart yönetimi (listeleme/ekleme) skill'i bilinçli YOK — kart
/// çözümü ECommerce cüzdanında, istekler hazır vault token'la gelir.
/// </summary>
public static class PaymentAgentCard
{
    public static AgentCard Create(string agentUrl) => new()
    {
        Name = "PaymentAgent",
        Description = "Kayıtlı kart vault token'ı ile taksit seçeneklerini getirir ve onaylı çekimi " +
                      "gerçekleştirir. Tam kart verisi (PAN/CVV) KABUL ETMEZ; kart yönetimi sunmaz.",
        Version = "0.8.0",
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
                Description = "Kartın BIN'i (ilk 6 hane, hassas değil) + sepet tutarı ile taksit " +
                              "quote'u (024 sözleşmesi). 038 itibarıyla arka tool'u YOK — istek " +
                              "gelirse 'şu an yapılamıyor' döner; sözleşme kırılmasın diye kartta durur.",
                Tags = ["payment", "installments", "quote", "bin"],
                Examples = ["BIN 540667 için 1000 TL taksitleri göster"]
            },
            new AgentSkill
            {
                Id = "quote-installments",
                Name = "Kayıtlı kartla taksit seçeneklerini getir",
                Description = "Kayıtlı kartın vault token'ı + sepet tutarı ile desteklenen taksit " +
                              "seçeneklerini döner (installmentNumber + o taksidin toplam tutarı; " +
                              "1 = tek çekim). READ-ONLY — çekim yapmaz. (038 US1)",
                Tags = ["payment", "installments", "quote"],
                Examples = ["intent: installments — vaultToken + amount ile taksitleri getir"]
            },
            new AgentSkill
            {
                Id = "charge_saved_card",
                Name = "Kayıtlı karttan çekim yap",
                Description = "Vault token + amount + paidPrice + installment + buyer + basketItems " +
                              "ile GERÇEK çekim. Çağıran taraf kullanıcı onayını ALMIŞ olmalı. " +
                              "Başarıda paymentId + providerPaymentId + status döner. Merchant " +
                              "Active değilse çekim gateway içinde reddedilir (fail-closed). (038 US2)",
                Tags = ["payment", "charge", "saved-card"],
                Examples = ["intent: charge — onaylı çekim isteğini işle"]
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