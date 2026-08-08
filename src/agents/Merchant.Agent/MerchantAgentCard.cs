using AgentSkill = A2A.AgentSkill;

namespace Merchant.Agent;

/// <summary>
/// A2A Agent Card sözleşmesi — Merchant.Agent'ın <c>/.well-known/agent-card.json</c>'da ilan ettiği
/// opak yüzey (contracts/merchant-agent-card.md). 013 kapsamı: yalnız başvuru + durum. Komisyon/
/// pazarlık skill'leri YOK (014). LLM yalnız tool sırası kurar; sır/karar/kimlik üretmez.
/// </summary>
public static class MerchantAgentCard
{
    public static AgentCard Create(string agentUrl) => new()
    {
        Name = "MerchantAgent",
        Description = "Merchant adaylarının gateway'e kayıt başvurusunu A2A ile alır; alan adı " +
                      "sahipliğini doğrular. Komisyon/pazarlık bu sürümde YOK.",
        Version = "0.1.0",
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
                Id = "register",
                Name = "Gateway'e kayıt başvurusu yap",
                Description = "Aday alan adıyla (domain) gateway'e kayıt başvurusu başlatır: descriptor " +
                              "okunur, alan adı sahipliği domain-control challenge ile doğrulanır ve " +
                              "başvuru (RegisterRequest) oluşturulur. Kimlik/sır KABUL ETMEZ.",
                Tags = ["merchant", "onboarding", "register"],
                Examples = ["shop.example.com sitemle gateway'inize kayıt olmak istiyorum"]
            },
            new AgentSkill
            {
                Id = "registration_status",
                Name = "Başvuru durumunu sorgula",
                Description = "Verilen alan adı için başvurunun durumunu (Pending/Approved/Rejected) döner.",
                Tags = ["merchant", "onboarding", "status"],
                Examples = ["Başvurum ne durumda?"]
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