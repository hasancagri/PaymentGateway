# Contract — Integration Events (013)

`src/others/Shared/IntegrationEvents.cs` + `RabbitMqConstants.cs`. Wolverine fanout, Marten
outbox (dual-write yok — research D13). Tüketici **tekil `...Handler`** (CLAUDE.md kuralı;
çoğul "Handlers" sessizce keşfedilmez).

## YENİ event'ler

### `MerchantProvisioned`
```
MerchantProvisioned(Guid MerchantId, string MerchantKey, string Status)  // Status = "Provisioning"
```
- **Yayınlayan**: Merchant.Api (aktivasyon redeem, `Merchant.Provision()` içinden).
- **Tüketen**: Identity.Server `MerchantClientEventHandler` — OpenIddict client kur/güncelle
  (client_id=merchantId, secret=MerchantKey, **Provisioning scope demeti** = merchant.read/write).
- **Exchange**: `merchant.lifecycle` (mevcut; MerchantCreated/StatusChanged ile aynı).
- **Not**: Onboarding'de MerchantCreated'ın YERİNİ alır (client'ı aktivasyonda provision eder;
  aktivasyon öncesi client YOK → token YOK). Secret yalnız Identity'ye; BC API'lerine gitmez.

### `MerchantCommissionGridReady`
```
MerchantCommissionGridReady(Guid MerchantId)
```
- **Yayınlayan**: Commission.Api — grid **finalize** edilip **Ready** olunca (aynı
  `[Transactional]` = outbox). Draft'ta yayınlanMAZ (erken tetikleme önlenir).
- **Tüketen**: Merchant.Api `MerchantCommissionGridReadyHandler` (tekil) — `MarkCommissionGridReady`
  + `TryActivate` (idempotent).
- **Exchange**: `merchant.commission` (YENİ fanout). Merchant.Api durable queue.

## Mevcut event'ler (kullanım)

### `MerchantStatusChanged` (mevcut)
```
MerchantStatusChanged(Guid MerchantId, string NewStatus)
```
- **Yeni kullanım**: `TryActivate()` Active'e geçince yayınlanır → Identity tam scope demetine
  yükseltir (mevcut 012 statü-kapılı hattı). Passive/Suspended admin akışı DEĞİŞMEZ.

### `MerchantCreated` (mevcut) — onboarding'de KULLANILMAZ
- Onboarding hattı MerchantProvisioned kullanır. Doğrudan admin-create yolu korunursa
  MerchantCreated hâlâ geçerli (o yol 013 dışı). Çift provision önlenir: onboarding merchant'ı
  Provisioning doğar, MerchantCreated yaymaz.

## Exchange / queue özeti

| Exchange | Tip | Yayınlayan | Tüketen (queue) |
|----------|-----|-----------|------------------|
| `merchant.lifecycle` | fanout (mevcut) | Merchant.Api | Identity.Server (`identity.merchant-sync`) — MerchantProvisioned + MerchantStatusChanged |
| `merchant.commission` | fanout (YENİ) | Commission.Api | Merchant.Api (yeni durable queue) — MerchantCommissionGridReady |

## İdempotenlik / sıra

- Tüm tüketiciler at-least-once varsayar: "zaten yapılmış mı?" kontrol (client var, statü Active,
  flag set → no-op).
- `MerchantCommissionGridReady` tek-yön: grid ready olunca ready kalır; tekrar gelirse no-op.
- Eventual consistency: Active geçişi event gecikmesi kadar (SC-007 ≤1dk; outbox relay saniyeler).