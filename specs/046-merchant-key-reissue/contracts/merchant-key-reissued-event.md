# Contract — MerchantKeyReissued event

`MerchantKeyReissued(Guid MerchantId, string MerchantKey)` — Shared.IntegrationEvents.

- **Yayıncı**: Merchant.Api reissue handler ([Transactional] outbox), `MerchantLifecycle` fanout.
- **Tüketici → etki**:
  - Identity.Server: OpenIddict client_secret = yeni key (eski secret geçersiz).
  - Payment.Api: `MerchantApiKeyReference` KeyHash REPLACE (eski hash silinir → eski X-Api-Key 401).
- Binding'i tüketiciler kurar (soğuk-açılış kuralı). Key sır taşır — yalnız bu iç event'te.
