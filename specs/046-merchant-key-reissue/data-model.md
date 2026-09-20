# Data Model — Merchant Key Yenileme

## Merchant (mevcut aggregate — genişletme)

- **Yeni davranış**: `ReissueKey() : ResultDomain` — yeni `MerchantKey = "mk_" + Guid` üretir; yalnız
  Active merchant'ta çalışır (değilse Result error). Dendenen tek yerden (handler) çağrılır.
- Değişmez alanlar aynı; yalnız `MerchantKey` değişir. `Create` fabrikası dokunulmaz.

## MerchantKeyReissued (yeni integration event — Shared)

- `MerchantKeyReissued(Guid MerchantId, string MerchantKey)` — yeni key'i taşır.
- Yayıncı: Merchant.Api (reissue handler, `[Transactional]` outbox). Exchange = mevcut
  `MerchantLifecycle` fanout (MerchantCreated ile aynı kanal; binding'i tüketici kurar).
- Tüketiciler: Identity.Server (`MerchantClientEventHandler` → client_secret update),
  Payment.Api (`MerchantApiConsumers` → hash REPLACE).

## MerchantKeyReissueLog (yeni salt-append doc — Merchant.Api)

| Alan | Tip | Not |
|---|---|---|
| Id | Guid | doc id |
| MerchantId | Guid | hedef merchant |
| ReissuedAt | DateTimeOffset | zaman |
| InitiatedBy | string | tetikleyen aktör (m2m/store kimliği) |
| Reason | string? | opsiyonel neden notu (davranış değiştirmez) |

- Yalnız `session.Store` (insert); güncelleme/silme YOK. Sorgu: merchantId ile liste (P2 geçmiş).

## CredentialRevealLink (mevcut — reuse)

- `Create(merchantId, merchantKey, lifetime)` yeni key ile; önceki merchant linkleri `Kill()`.
- `Consume()` tek-kullanımlık gösterim (mevcut `/onboarding/reveal/{token}` sayfası).

## Durum geçişi (özet)

`ReissueKey` → MerchantKey değişir → `MerchantKeyReissued` yayınlanır → (Identity secret, Payment hash
GÜNCELLENİR: eski anında geçersiz) + yeni reveal link doğar + log yazılır → merchant reveal'dan okur.
