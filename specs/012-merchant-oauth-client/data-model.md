# Data Model: Merchant OAuth İstemci Düzlemi (012)

## 1. Merchant istemci kaydı (Identity.Server — OpenIddict application, `identityDb`)

Yeni tablo YOK — OpenIddict'in mevcut application tablosu kullanılır.

| Alan (descriptor) | Değer | Not |
|---|---|---|
| ClientId | `merchantId` (Guid string) | Merchant BC'deki aggregate Id |
| ClientSecret | `MerchantKey` (`mk_<guid-N>`) | OpenIddict hash'leyerek saklar |
| ClientType | Confidential | secret zorunlu |
| ConsentType | Implicit | M2M |
| DisplayName | merchant adı | event'ten değil — `Merchant {id}` kalıbı yeterli (dev) |
| Permissions | `Endpoints.Token`, `GrantTypes.ClientCredentials`, `scp:merchant.read`, `scp:merchant.write` | Active'de dolu; Passive/Suspended'da BOŞALTILIR (status-gate, D4) |
| Properties[`merchant_id`] | `merchantId` | merchant istemcisi işareti + claim kaynağı (D6) |

**Durum geçişleri** (event tüketimiyle):

```
(yok) --MerchantCreated--> Aktif istemci (izinler dolu)
Aktif --MerchantStatusChanged(Passive|Suspended)--> Pasif istemci (izinler boş; kayıt+secret durur)
Pasif --MerchantStatusChanged(Active)--> Aktif istemci (izinler geri yazılır)
her olay tekrar işlenebilir (idempotent upsert)
```

## 2. Integration event'ler (`Shared/IntegrationEvents.cs`)

| Event | Alanlar | Yayıncı | Tüketici |
|---|---|---|---|
| `MerchantCreated` | `Guid MerchantId`, `string MerchantKey`, `string Status` | Merchant.Api (CreateMerchant handler) | Identity.Server |
| `MerchantStatusChanged` | `Guid MerchantId`, `string NewStatus` | Merchant.Api (SetMerchantStatus handler) | Identity.Server |

Status değerleri string: `"Active" | "Passive" | "Suspended"` (BC enum'u Shared'a sızmaz).
Taşıma: fanout exchange `merchant.lifecycle`, Identity kuyruk `identity.merchant-sync`
(`RabbitMqConstants`'a iki sabit eklenir).

## 3. Merchant erişim token'ı (JWT — kalıcı değil, sözleşme)

| Claim | Değer | Not |
|---|---|---|
| `sub` | merchantId | mevcut TokenEndpoint davranışı (sub=client_id) |
| `merchant_id` | merchantId | YENİ — yalnız merchant istemcilerinde (Properties'ten) |
| `scope` | `["merchant.read","merchant.write"]` | JSON dizi (ScopeClaimArrayHandler) |
| `aud` | `merchant.api` | scope→resource eşlemesinden (mevcut Config) |
| `exp` | iat + 15 dk | D5 — global ömür |

Admin-ui/payment-agent token'ları: `merchant_id` claim'i YOK, diğer her şey mevcut hali.

## 4. Merchant aggregate (Merchant BC — DEĞİŞİKLİK YOK)

`Merchant.cs` mevcut: `MerchantStatus` (Active/Passive/Suspended), `Activate()/
Deactivate()/Suspend()` metotları, immutable `MerchantKey`. G2 yalnız bu API'yi çağıran
yeni slice ekler; aggregate'e alan/metot eklenmez.

## 5. Enforcement karar modeli (Common — saf)

`MerchantScopeEvaluator.IsAllowed(string? merchantIdClaim, string? routeMerchantId)`:

| merchant_id claim | route merchantId | Sonuç |
|---|---|---|
| yok (admin/agent) | * | İZİN (mevcut davranış) |
| var | yok (route'ta parametre yok) | RET → 403 (fail-closed; listeleme/by-key/create uçlarını kapatır) |
| var | var, claim'e eşit | İZİN |
| var | var, farklı | RET → 403 |

`AdminPlaneOnly`: claim var → RET; claim yok → İZİN. (SetMerchantStatus ucu.)