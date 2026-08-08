# Phase 1 Data Model — Merchant Onboarding (013)

Aggregate / entity / value object / event. Anayasa: private setter + statik `Create` fabrikası
+ davranış metodu; invariant aggregate'te; koleksiyon private + read-only expose. Marten
document/event store (BC başına şema). Cross-BC yalnız `Shared` integration event.

---

## Merchant BC (`merchantDb`)

### RegisterRequest (YENİ aggregate)

Başvuru — merchant'tan AYRI. Merchant ancak onayla bundan doğar.

| Alan | Tip | Not |
|------|-----|-----|
| `Id` | Guid | aggregate kimliği |
| `Domain` | string | aday alan adı (normalize, lower) — mükerrer anahtarı |
| `LegalName` | string | descriptor'dan (doğrulanmış kopya) |
| `TaxId` | string | descriptor beyanı |
| `ContactEmail` | string | descriptor; aktivasyon maili buraya |
| `WebhookUrl` | string | descriptor (beyan; charge G5'te kullanılır) |
| `ChallengeResult` | enum `ChallengeOutcome` | Passed (talep ancak Passed ile oluşur) |
| `Status` | enum `RegisterRequestStatus` | Pending / Approved / Rejected |
| `ReviewedAtUtc` | DateTime? | karar zamanı |
| `ReviewNote` | string? | opsiyonel admin notu (özellikle Rejected) |
| `CreatedMerchantId` | Guid? | Approved'da doğan merchant |
| `CreatedTime/UpdatedTime` | audit | AggregateRoot |

**Statik Create**: `RegisterRequest.Create(domain, descriptorCopy, challengeOutcome)` —
`challengeOutcome != Passed` ise Result hata (talep oluşmaz, FR-003). Domain normalize.

**Davranış**:
- `Approve(merchantId, note?)` — yalnız Pending→Approved; `CreatedMerchantId` set; aksi RET.
- `Reject(note?)` — yalnız Pending→Rejected.
- Idempotent: zaten Approved/Rejected ise tekrar aynı işlem RET (beklenen hata Result).

**Invariant**: Pending dışı talep karar alamaz. Approved talep merchant'a bağlıdır.

**Enum**:
- `RegisterRequestStatus : Enumeration` — Pending(1), Approved(2), Rejected(3).
- `ChallengeOutcome : Enumeration` — Pending(1), Passed(2), Failed(3), Expired(4).

---

### DomainControlChallenge (YENİ — RegisterRequest akışından ÖNCE)

Tek-kullanımlık, süreli sahiplik kanıtı bileti. RegisterRequest'ten önce yaşar (doğrulama
geçmeden talep yok). Ayrı Marten document (kısa ömürlü).

| Alan | Tip | Not |
|------|-----|-----|
| `Id` | Guid | |
| `Domain` | string | hangi alan adı için |
| `Token` | string | tek-kullanım, URL-güvenli (challenge dosya adı) |
| `ExpectedValue` | string | aday sitede yayınlanacak keyed değer |
| `ExpiresAtUtc` | DateTime | TTL ~1 saat |
| `Status` | enum | Issued / Consumed / Expired |
| `CreatedTime` | audit | |

**Statik Create**: `Issue(domain)` — token + expectedValue + ExpiresAt üretir.
**Davranış**: `Verify(fetchedValue, nowUtc)` — süre + değer eşleşmesi; başarı → Consumed,
sonuç `ChallengeOutcome`. Tek kullanım: Consumed/Expired tekrar Verify edilemez.

---

### Merchant (mevcut aggregate — GENİŞLER)

Mevcut: Name, Email, Phone, CountryCode, CityCode, Mcc, WebhookUrl, MerchantKey, Status
(Active/Passive/Suspended), IsActive. Eklenenler:

| Yeni alan | Tip | Not |
|-----------|-----|-----|
| `Status` | enum — **Provisioning eklenir** | Provisioning(4) + mevcut Active(1)/Passive(2)/Suspended(3) |
| `ReturnUrl` | string? | geçerli HTTPS; Active koşulu #3 |
| `ExternalRef` | string? | opak; sakla/aynen dön |
| `HasSettlementAccount` | bool (türetilebilir/flag) | Active koşulu #1 |
| `CommissionGridReady` | bool | Active koşulu #2 (event ile set) |
| `ActivatedAtUtc` | DateTime? | key teslim (Provisioning'e geçiş) anı |

**Statü default değişikliği**: Onboarding'de merchant **Provisioning** doğar (mevcut default
Active — onboarding yolu explicit Provisioning verir; doğrudan admin create yolu korunursa
Active kalabilir, ama 013 onboarding hattı Provisioning kullanır).

**Yeni davranışlar**:
- `Provision()` — aktivasyon bileti kullanılınca; statü Provisioning'e sabitler, `ActivatedAtUtc`
  set. `MerchantProvisioned` domain olayına kaynak.
- `SetReturnUrl(url)` — HTTPS doğrula (aksi Result hata); koşul #3. Sonra `TryActivate()`.
- `MarkSettlementAccountPresent()` — settlement eklendiğinde koşul #1. Sonra `TryActivate()`.
- `MarkCommissionGridReady()` — grid-ready event'i geldiğinde koşul #2. Sonra `TryActivate()`.
- `TryActivate()` — **saf iç metot**: 3 koşul (settlement + gridReady + ReturnUrl) doluysa
  statü Provisioning→Active, `MerchantStatusChanged(Active)` kaynak. **İdempotent**: zaten
  Active ise no-op. Koşullar eksikse statü değişmez.

**Invariant**:
- Provisioning'de charge yetkisi verilmez (scope demeti kademesi — Identity tarafı).
- Active geçişi yalnız 3 koşul + yalnız Provisioning'den (Passive/Suspended ayrı admin akışı).
- ReturnUrl HTTPS değilse set edilmez.

**Enum**: `MerchantStatus : Enumeration` — Active(1), Passive(2), Suspended(3), **Provisioning(4)**.

---

### ActivationTicket (YENİ — key teslim bileti)

Tek-kullanımlık, süreli key-teslim bileti. Onayla merchant oluşunca üretilir; aktivasyon
sayfası bunu redeem eder.

| Alan | Tip | Not |
|------|-----|-----|
| `Id` | Guid | |
| `MerchantId` | Guid | |
| `Token` | string | aktivasyon linki nonce'u (tek-kullanım) |
| `ExpiresAtUtc` | DateTime | TTL ~24 saat |
| `Status` | enum | Issued / Redeemed / Expired |
| `RedeemedAtUtc` | DateTime? | |

**Statik Create**: `Issue(merchantId)`.
**Davranış**: `Redeem(nowUtc)` — süre + tek-kullanım; başarı → Redeemed. İkinci redeem RET
(FR-009, key yeniden gösterilmez). Süre dolmuş → Expired, RET (admin yeni bilet üretebilir).

---

### OnboardingNotification (YENİ — mail gönderim kaydı, FR-019)

Gönderilen/denenmiş mailin izlenebilir kaydı. Mail çökse akış sessizce "başarılı" saymaz.

| Alan | Tip | Not |
|------|-----|-----|
| `Id` | Guid | |
| `MerchantId` | Guid? | (admin-bildirim'de null olabilir) |
| `Kind` | enum | AdminNewRequest / Activation (yalnız deterministik mailler; komisyon Excel maili agentik/harici LLM — domain kaydı tutmaz) |
| `Recipient` | string | |
| `Status` | enum | Pending / Sent / Failed |
| `Attempts` | int | retry sayacı |
| `LastError` | string? | |
| `CreatedTime/UpdatedTime` | audit | |

**Davranış**: `MarkSent()` / `MarkFailed(error)` (Attempts++). Admin ekranı Failed'ları görür;
retry mümkün.

---

### SettlementAccount (mevcut — DEĞİŞMEZ)

Mevcut aggregate. Onboarding'e etkisi: ilk hesap eklenince Merchant koşul #1
(`MarkSettlementAccountPresent`) tetiklenir — CreateSettlementAccount handler'ı, aynı
`[Transactional]` içinde merchant'ı yükleyip işaretler VEYA Merchant BC-içi event/mesajla
(aynı BC, in-process). Detay: contracts.

---

## Commission BC (`commissionDb`)

### MerchantCommission (mevcut — sürümleme YOK, Draft/Ready statüsü eklenir)

B kararı: grid gateway-otoriter, kabul-sürüm bağı YOK (014'e). Mevcut aggregate + BulkUpsert
korunur. Grid satırları **bankanın desteklediği tüm taksit sayılarını** kapsar
(`Bank.SupportedInstallments`; kombinasyon-bazlı, sabit taksit seti yok). Eklemeler:

1. **Grid statüsü `Draft / Ready`**: bir merchant grid'inin durumu. BulkUpsert Draft'ta hücre
   doldurur (kısmi olabilir). **Finalize** aksiyonu (yeni command) bütünlüğü doğrular
   (`IsMissing` yok, `BelowBankCeiling` yok) → Ready'e geçer. Approved/Rejected YOK.
   Yer: hafif `MerchantCommissionGrid` başlık kaydı (`MerchantId` + `Status`) veya merchant
   grid'ine türetilmiş durum — tasarım detayı; öneri hafif başlık kaydı.
2. **Grid-hazır event (deterministik)**: **finalize → Ready** anında
   `MerchantCommissionGridReady(merchantId)` yayınla (aynı `[Transactional]` = outbox).
   Idempotent; Draft'ta yayınlanMAZ (erken tetikleme önlenir).
3. **MCP read yüzeyi (agentik)**: `get_merchant_commission_grid` (Commission.Api `/mcp`) —
   harici LLM Excel için grid'i satır/sütun + statü olarak okur. Ready değilse "hazır değil"
   döner (Excel üretilmez). Commission.Api handler'ı Excel/mail ÜRETMEZ (D14).

> **Neden finalize**: BulkUpsert kademeli olabilir; yarım grid'de erken "hazır" event'i +
> Excel gitmesin. Ready'i explicit finalize tetikler, outbox-atomik, tüketici idempotent.

---

## Shared Integration Events (`src/others/Shared`)

Mevcut: MerchantCreated, MerchantStatusChanged, PaymentCompleted/Failed, ReferenceDataUpdated.

### YENİ event'ler

| Event | Alanlar | Yayınlayan → Tüketen | Exchange |
|-------|---------|----------------------|----------|
| `MerchantProvisioned` | MerchantId (Guid), MerchantKey (string), Status ("Provisioning") | Merchant.Api → Identity.Server | `merchant.lifecycle` |
| `MerchantCommissionGridReady` | MerchantId (Guid) | Commission.Api → Merchant.Api | `merchant.commission` (yeni fanout) |

**Notlar**:
- `MerchantProvisioned` = onboarding'de MerchantCreated'ın yerini alır (client'ı aktivasyonda
  provision eder). MerchantKey taşır (mevcut MerchantCreated deseni; secret yalnız Identity'ye).
  Identity `MerchantClientEventHandler` bunu da tüketir (idempotent client upsert; Provisioning
  scope demeti).
- `MerchantStatusChanged(Active)` mevcut event; Merchant.Api `TryActivate()` içinden yayınlar
  → Identity tam scope demetine yükseltir (mevcut 012 hattı, statü-kapılı).
- `MerchantCommissionGridReady` yeni fanout exchange (`merchant.commission`); Merchant.Api
  durable queue ile tüketir (tekil `...Handler`, idempotent → `MarkCommissionGridReady` +
  `TryActivate`).

---

## Value Objects / opak alanlar

- `ExternalRef` — opak string (VO gerekmez; nullable string alan + trim). Gateway anlamlandırmaz.
- Descriptor kopyası — RegisterRequest içinde düz alanlar (LegalName/TaxId/ContactEmail/
  WebhookUrl); ayrı VO opsiyonel (`MerchantDescriptor` VO — doğrulama descriptor parse'ında).

---

## Statü/geçiş özeti (Merchant)

```
(onboarding) --onay--> Provisioning --3 koşul (settlement + gridReady + returnUrl)--> Active
Active <--admin--> Passive / Suspended   (mevcut davranış, DEĞİŞMEZ)
```

Koşullar event/mesajla toplanır, `TryActivate()` idempotent değerlendirir (D10/D13).