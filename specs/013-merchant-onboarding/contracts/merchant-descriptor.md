    # Contract — Merchant Descriptor + Domain-Control Challenge

Aday sitenin (E1) sunduğu iki public dosya + gateway'in doğrulama sözleşmesi. Aday tarafı
kapsam dışı (simüle) — bkz. `../ecommerce-side-notes.md`.

## 1. Descriptor — `GET /.well-known/merchant-descriptor.json`

Public, statik, sırsız, additive.

```json
{
  "schemaVersion": "1.0",
  "domain": "shop.example.com",
  "legalName": "Örnek Ticaret A.Ş.",
  "taxId": "1234567890",
  "contactEmail": "onboarding@example.com",
  "webhookUrl": "https://shop.example.com/gateway/webhook",
  "agent": { "a2aCardUrl": "https://shop.example.com/.well-known/agent-card.json" }
}
```

Gateway'in **zorunlu** okuduğu: `legalName`, `taxId`, `contactEmail`, `webhookUrl`.
`schemaVersion` + `domain` ileri-uyum/eşleme. Eksik zorunlu alan / erişilemez dosya →
başvuru **talep oluşturmadan** reddedilir (FR-002). `contactEmail` geçerli e-posta,
`webhookUrl` mutlak HTTPS olmalı.

## 2. Challenge — `GET /.well-known/merchant-challenge/{token}`

Descriptor'dan AYRI, dinamik, başvuru anında.

- Gateway başvuruda `token` (URL-güvenli, tek-kullanım) + `expectedValue` üretir.
- Aday, `/.well-known/merchant-challenge/{token}` yolunda **düz metin** `expectedValue` döner.
- Gateway **senkron GET** ile çeker, `expectedValue` ile karşılaştırır.
- Eşleşme + süre (TTL ~1 saat) geçerli → `ChallengeOutcome.Passed` → RegisterRequest oluşur.
- Eşleşmez / süre dolmuş / dosya yok → `Failed`/`Expired`, talep OLUŞMAZ.

`expectedValue` biçimi (basitleştirilmiş HTTP-01): `"{token}.{gatewayFingerprint}"` — gateway
tarafında deterministik üretilir; aday yalnız kopyalar. Tam byte eşleşmesi aranır.

## Doğrulama kuralları (özet)

| Durum | Sonuç |
|-------|-------|
| Descriptor erişilemez / zorunlu alan eksik | Talep yok, anlaşılır hata (FR-002) |
| Challenge dosyası yok / değer yanlış | Talep yok (FR-003) |
| Challenge süresi dolmuş | Reddedilir; yeni başvuru → yeni token (FR-003, Edge) |
| Aynı domain: Pending talep VEYA kayıtlı merchant var | Mükerrer talep yok (FR-020) |
| Hepsi geçer | RegisterRequest(Pending) + admin bildirim maili |