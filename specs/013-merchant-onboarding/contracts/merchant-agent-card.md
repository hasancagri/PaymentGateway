# Contract — Merchant.Agent A2A Card

Yeni `src/agents/Merchant.Agent` (Payment.Agent şablonu). A2A host + LLM router + MCP client.
Card `/.well-known/agent-card.json`. LLM yalnız tool sırası kurar; sır/karar/kimlik üretmez.

## Agent Card (013 kapsamı)

```
Name:            MerchantAgent
Version:         0.1.0
Description:     "Merchant adaylarının gateway'e kayıt başvurusunu A2A ile alır;
                  alan adı sahipliğini doğrular. Komisyon/pazarlık bu sürümde YOK."
DefaultInputModes: ["text"]
Streaming:       true
PushNotifications: false
ProtocolBinding: JSON-RPC, ProtocolVersion "1.0"
```

## Skills (013)

### skill: `register`
- **Girdi**: aday alan adı (`domain`).
- **Akış**: agent → Merchant.Api `/mcp` `submit_registration(domain)` (descriptor çek +
  challenge ver/doğrula + RegisterRequest). Sonuç: talep durumu + (challenge yayını gerekliyse)
  yayınlanacak token/değer talimatı.
- **Örnek**: "shop.example.com sitemle gateway'inize kayıt olmak istiyorum."

### skill: `registration_status` (opsiyonel)
- **Girdi**: `domain` (veya talep referansı).
- **Akış**: agent → `registration_status(domain)` → Pending/Approved/Rejected.
- **Örnek**: "Başvurum ne durumda?"

## KAPSAM DIŞI (013)

- Komisyon `get/accept/reject_commission_terms` skills → **014** (e-posta + Excel + ML-intent
  pazarlık). 013'te komisyon gateway-otoriter, merchant agent'la müzakere ETMEZ.

## Router instructions (özet)

LLM'e: yalnız `register` → (gerekirse) `registration_status` tool sırasını kur. Domain/token/
değer üretme (domain kullanıcıdan; token/değer domain'den). Kimlik/sır üretme.

## Token

Agent → Merchant.Api `/mcp` çağrısı **agent'ın kendi client_credentials** token'ıyla (yeni
Identity client `merchant-agent` benzeri, scope `merchant.write`), `AgentTokenHandler` deseni
(−30sn yenileme). Merchant token'ı bu iç yüzeye girmez (dışa kapalı).