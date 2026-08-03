# A2A Agent Card Sözleşmesi — Payment.Agent (007)

Payment.Agent, A2A yeteneklerini `/.well-known/agent-card.json`'da ilan eder
(`MapWellKnownAgentCard`). Kart, e-ticaret agent'ının **opak** gördüğü yüzeydir — iç MCP
tool'ları kartta görünmez; yalnız *skill* ilanı taşır.

## Skills (007)

| Skill id | Amaç | Girdi | Çıktı |
|----------|------|-------|-------|
| `quote-installments` | Kayıtlı kart + sepet tutarından taksit seçeneklerini getir | niyet metni + `cardToken` + `cartAmount` | taksit listesi (Model A) + `sessionId` |
| `select-installment` | Sunulan taksitlerden birini seç, oturuma yaz | `sessionId` + `installmentCount` | güncel faz (`InstallmentSelected`) |
| `payment-status` | Oturum fazını sorgula | `sessionId` | faz + seçilen taksit |

> `pay-with-token` skill'i 007'de **YOK** — çekim ertelendi; pay feature'ında eklenecek.

## Örnek AgentCard (şekil — alanlar a2a-dotnet 1.0 API'sine göre)

```json
{
  "name": "PaymentAgent",
  "description": "Kayıtlı kart token'ı ile taksit seçeneklerini getirir ve seçimi kaydeder (Model A). Kart verisi kabul etmez.",
  "url": "http://payment-agent/",
  "version": "0.7.0",
  "capabilities": { "streaming": true },
  "defaultInputModes": ["text"],
  "defaultOutputModes": ["text"],
  "skills": [
    { "id": "quote-installments", "name": "Taksit seçeneklerini getir",
      "description": "Token + sepet tutarı ile desteklenen taksitleri Model A tutarlarıyla döner." },
    { "id": "select-installment", "name": "Taksit seç",
      "description": "Sunulan listeden bir taksiti seçer ve oturuma yazar. Çekim yapmaz." },
    { "id": "payment-status", "name": "Durum sorgula",
      "description": "Ödeme oturumunun güncel fazını döner." }
  ]
}
```

## Sözleşme kuralları

- **Kart verisi yasağı (SC-006)**: input mode `text`; şema tam PAN/CVV/expiry alanı **içermez**.
  Yalnız niyet + `cardToken` + tutar + (fazına göre) taksit.
- **Opaklık**: kart, iç MCP tool adlarını (`get_installment_options` vb.) veya banka/POS
  ayrıntısını **açıklamaz**.
- **Agent Card yolu**: `/.well-known/agent-card.json` (a2a-dotnet 1.0; eski `agent.json` değil).