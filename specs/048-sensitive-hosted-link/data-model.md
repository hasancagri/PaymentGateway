# Phase 1 Data Model: Hassas Hosted Link

## Yeni Aggregate: SensitiveEntrySession

Marten dokümanı, merchantDb. Bir hosted link'in yetkisini temsil eder. ECom `CredentialEntrySession` ikizi + **hedef `MerchantId`** (görüntüleme için).

| Alan | Tip | Not |
|---|---|---|
| `Id` | Guid | AggregateRoot; doküman kimliği |
| `Token` | string | 256-bit rastgele, URL-safe base64url (`RandomNumberGenerator.GetBytes(32)`, `+/=`→`-_` trim). YETKİ. |
| `MerchantId` | Guid | Link hangi merchant için (ECom'da YOK — orada insan id girer; burada görüntüleme hedefi) |
| `RequestedByUserId` | Guid | Linki üreten admin (denetim izi; token izde YER ALMAZ) |
| `ExpiresAt` | DateTimeOffset | üretim + `LinkLifetime` (15dk). Mutlak son-kullanım. |
| `ConsumedAt` | DateTimeOffset? | Başarılı POST anı; dolu ise tüketilmiş (tek kullanım) |

### Davranış (aggregate metotları)

- `static ResultDomain<SensitiveEntrySession> Create(Guid merchantId, Guid requestedByUserId, TimeSpan lifetime)`
  - Guard: `merchantId` boş → `VALUE_IS_REQUIRED`; `requestedByUserId` boş → `VALUE_IS_REQUIRED`; `lifetime <= 0` → `INVALID_VALUE`.
  - Token üretir, `ExpiresAt = UtcNow + lifetime`, `Ok(session)`.
- `ResultDomain Consume(DateTimeOffset now)`
  - `!IsUsable(now)` → `INVALID_OPERATION_ERROR`; değilse `ConsumedAt = now`, `UpdatedTime` güncelle, `Ok()`.
- `bool IsUsable(DateTimeOffset now)` → `ConsumedAt is null && now <= ExpiresAt` (saf getter).

### Durum geçişleri

```
Created (usable)  --GET (tüketmez)-->  Created (usable)
Created (usable)  --POST başarılı-->   Consumed (dead)
Created (usable)  --15dk geçti-->      Expired (dead, pasif; kayıt durur)
Consumed/Expired  --herhangi erişim--> nötr 404
```

### Değişmezler (Domain-TDD hedefleri — test-first)

- INV-1: `Create` boş merchantId/userId veya ≤0 lifetime'ı reddeder.
- INV-2: `Create` sonrası `IsUsable(now)` true (now ≤ ExpiresAt).
- INV-3: `IsUsable` süresi geçmişte (now > ExpiresAt) false.
- INV-4: `Consume` usable oturumu tüketir, `ConsumedAt` dolar.
- INV-5: Tüketilmiş oturumda ikinci `Consume` → Error (tek kullanım).
- INV-6: Süresi geçmiş oturumda `Consume` → Error.
- INV-7: Token her `Create`'te farklı + URL-safe (base64url charset).

## Yeniden kullanılan (değişmez)

- **Merchant aggregate hassas alanları**: Email, GsmNumber, IdentityNumber, Iban, TaxNumber. `GetMerchantSensitive` (query) + `UpdateMerchantSensitive` (command, doğrulama dahil) handler'ları AYNEN kalır; yalnız çağıran değişir (BFF HTTP endpoint → token-endpoint bus çağrısı).

## Sökülen veri yüzeyi

- `src/ui/Admin` (Razor) — hassas-veri sayfası + BFF client.
- Merchant.Api BFF HTTP çifti `GET/PUT /merchants/{id}/sensitive` endpoint eşlemeleri (handler'lar kalır).
- Payment.Identity `admin-ui` client seed (ölü; Admin BFF m2m token içindi) → RetiredClientIds prune.
