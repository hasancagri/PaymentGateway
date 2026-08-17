# Contract: A2A — ChatAgent → Payment.Agent (038)

**Kanal**: A2A JSON-RPC (`a2a-payment` named client, `PaymentGateway:A2AUrl`). **Auth YOK**
(Q2=B — 024 ile tutarlı; çekim güvenliği gateway-içi statü kapısı + /mcp makine token'ı).
Agent Card: `/.well-known/agent-card.json` (`payment-gateway-agent`).

## Skill'ler (Agent Card)

| Skill Id | Durum | Amaç |
|----------|-------|------|
| `installment_quote` | MEVCUT (024) — AYNEN KALIR | BIN + tutar ile READ-ONLY taksit quote'u |
| `quote-installments` | MEVCUT — bu işle CANLANIR | Vault token + tutar ile taksit seçenekleri (US1) |
| `charge_saved_card` | YENİ | Vault token ile gerçek çekim (US2) |

Kart listeleme/ekleme skill'i YOK (kart yönetimi ECommerce'de; güvenlik kararı).
`pay-with-token` yasağı kalkar — çekim artık bu skill'le var; kart VERİSİ (PAN/CVV) kabul
etmeme kuralı AYNEN sürer.

## Mesaj sözleşmesi

ChatAgent yorumlanmış isteği A2A text mesajında YAPILANDIRILMIŞ blok olarak gönderir
(mevcut A2A SendMessage düzeni — 019/024 deseni). Payment.Agent LLM'i yalnız tool sırası
kurar; alan ÜRETMEZ.

### `installments` isteği (US1)

```json
{
  "intent": "installments",
  "merchantId": "<EC config'ten>",
  "vaultToken": "<EC cüzdanından — varsayılan ya da seçilen kart>",
  "amount": 36650.60
}
```

**Yanıt**: taksit seçenekleri listesi (installmentNumber + totalPrice) — text olarak
ChatAgent'a döner; ChatAgent müşteriye numaralı liste sunar, alan uydurmaz.

### `charge` isteği (US2)

```json
{
  "intent": "charge",
  "merchantId": "<EC config'ten>",
  "vaultToken": "<seçilen kart>",
  "amount": 36650.60,
  "paidPrice": 38573.06,
  "installment": 3,
  "buyerName": "...", "buyerSurname": "...", "buyerEmail": "...", "buyerGsmNumber": "...",
  "buyerIdentityNumber": "...", "buyerRegistrationAddress": "...", "buyerCity": "...",
  "buyerCountry": "...", "buyerIp": "..."
}
```

Buyer alanları `get_payment_context`'ten VERBATIM (gerçek müşteri: profil + varsayılan
adres). `basketItems` GÖNDERİLMEZ — gateway tek sentetik kalemle sentezler.

**Önkoşul**: ChatAgent müşteriden AÇIK onay almıştır (yönerge kuralı — onay yoksa istek
gönderilmez).

**Yanıt**: `paymentId` + `providerPaymentId` + `status` (+ teyit alanları). Başarısızlıkta
kullanıcıya gösterilebilir kısa mesaj ("ödeme alınamadı") — teknik ayrıntı yok.

## Sabit kurallar

- PAN / CVC / cardUserKey / cardToken hiçbir istek-yanıt alanında OLAMAZ (SC-003).
- Buyer alanları ChatAgent tarafından `get_payment_context` çıktısından VERBATIM taşınır;
  LLM bu alanları üretmez, değiştirmez, müşteriye göstermez (R3). Sepet kalemi A2A'da yoktur.
- Tutar/taksit değerleri araç çıktılarından gelir; iki agent'ın LLM'i de değer uydurmaz
  (007 kuralı).
- A2A erişilemezse ChatAgent "bu işlem şu an yapılamıyor" der; sohbet çalışmaya devam eder.