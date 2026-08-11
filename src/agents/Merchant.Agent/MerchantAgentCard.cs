using AgentSkill = A2A.AgentSkill;

namespace Merchant.Agent;

/// <summary>
/// A2A Agent Card sözleşmesi — Merchant.Agent'ın <c>/.well-known/agent-card.json</c>'da ilan ettiği
/// opak yüzey. 013: başvuru + durum. 019: komisyon teklif/pazarlık skill'leri (admin metin kanalı;
/// Commission.Api /mcp tool'ları). LLM yalnız tool sırası kurar; sır/karar/ORAN üretmez.
/// </summary>
public static class MerchantAgentCard
{
    public static AgentCard Create(string agentUrl) => new()
    {
        Name = "MerchantAgent",
        Description = "Merchant adaylarının gateway'e kayıt başvurusunu A2A ile alır; başvuru admin " +
                      "onayı bekler. 019: admin komisyon teklifini metinle yürütür (teklif sun, taslak " +
                      "revize, taslağı göster, durum sor).",
        Version = "0.2.0",
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
                              "okunur/doğrulanır ve başvuru (RegisterRequest) Pending olarak oluşturulur; " +
                              "admin onayı bekler. Kimlik/sır KABUL ETMEZ.",
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
            },
            // --- 019: komisyon teklif/pazarlık (admin metin kanalı) ---
            new AgentSkill
            {
                Id = "propose_commission",
                Name = "Komisyon teklifi sun / yeniden gönder",
                Description = "Merchant'a standart komisyon teklifini (banka grid'i + sabit marj) sunar " +
                              "veya revize edilmiş taslağı yeniden gönderir: get_merchant (isim → id + " +
                              "e-posta) → submit_commission_proposal. Excel ekli + kabul/ret linkli mail " +
                              "kuyruğa düşer.",
                Tags = ["commission", "proposal", "submit"],
                Examples = ["Kahve Dünyası'na ilk komisyon teklifimizi sun", "merchant'a gönder"]
            },
            new AgentSkill
            {
                Id = "revise_commission_draft",
                Name = "Komisyon taslağını metinle revize et",
                Description = "Taslak oranlarını admin'in AÇIK değerleriyle değiştirir (satır no, " +
                              "banka+taksit veya toplu set/delta); hesap sunucuda, dönen diff admin'e " +
                              "yankılanır. Merchant'a hiçbir şey gitmez (gönderim ayrı komut).",
                Tags = ["commission", "draft", "revise"],
                Examples = ["satır 37'yi 1.85 yap", "tüm 12 taksitleri 0.2 düşür", "Akbank 6 taksiti 1.8 yap"]
            },
            new AgentSkill
            {
                Id = "show_commission_draft",
                Name = "Komisyon taslağını göster",
                Description = "Taslağın güncel tam tablosunu (satır no, banka, taksit, oran) döner.",
                Tags = ["commission", "draft", "show"],
                Examples = ["Kahve Dünyası taslağını göster"]
            },
            new AgentSkill
            {
                Id = "commission_proposal_status",
                Name = "Teklif durumunu sorgula",
                Description = "Son komisyon teklifinin durumunu döner (yok / beklemede / kabul / " +
                              "ret + gerekçe + zaman).",
                Tags = ["commission", "proposal", "status"],
                Examples = ["Kahve Dünyası teklifi ne durumda?"]
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