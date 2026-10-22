# Data Model: PaymentLink Üretimi (Faz 1)

## Aggregate: `PaymentLink : AggregateRoot`

088 `HostedPaymentSession` tohumundan evrilir (research D5). Marten düz doküman; `paymentDb` + Payment şeması.

### Alanlar

| Alan | Tip | Kaynak | Not |
|---|---|---|---|
| `Id` (AggregateRoot) | `Guid` | üretilir | = opak **PgPaymentRef** (store'a döner) |
| `CreatedTime` (AggregateRoot) | `DateTime` | üretilir | TTL tabanı |
| `UpdatedTime` (AggregateRoot) | `DateTime?` | mutasyon | durum geçişinde set |
| `MerchantId` | `Guid` | istek (store) | tenant store kimliği (geçici — İlke V) |
| `OrderRef` | `string` | istek | sipariş referansı |
| `Amount` | `decimal` | istek | > 0 |
| `Currency` | `string` | istek | yalnız `TRY` (FR-004) |
| `ReturnUrl` | `string` | istek | müşteri ödeme sonrası dönüş adresi |
| `StoreName` | `string` | istek | güven bağlamı (FR-006, research D6) |
| `OrderSummary` | `string` | istek | güven bağlamı (FR-006) |
| `ExpiresAt` | `DateTime` | üretilir | `CreatedTime + Ttl` (lazy kontrol tabanı) |
| `OpenToken` | `string` | üretilir | tahmin-edilemez gizli açılış sırrı (unique) |
| `Status` | `PaymentLinkStatus` | mutasyon | durum makinesi (aşağıda) |

> **DÜŞÜRÜLEN (bu dilim)**: `CallbackToken`, `CheckoutFormToken`, `HostedUrl`, `ProviderPaymentId`, `FailureReason` — sağlayıcı/çekim sonraki dilim (research D5).

### Durum makinesi — `PaymentLinkStatus`

```
Pending(0) ──Open──▶ Opened(1) ──(Pay: Phase-1)──▶ Paid(2)   [bu dilimde ERİŞİLMEZ — stub]
   │                    │
   │                    └──(TTL+grace aşıldı)──┐
   ├──(TTL aşıldı)──────────────────────────────▶ Expired(3)   [terminal]
   │
   └──Cancel──┐    Opened ──Cancel──┐
              └──────────────────────▶ Cancelled(4)   [terminal]
```

- **Aktif** = {Pending, Opened}. **Terminal** = {Paid, Expired, Cancelled} (tek-yön).
- `Paid` bu dilimde erişilmez (Pay stub); enum'da tanımlı, geçişi sonraki dilim.

### Davranış metotları (İlke II — invariant aggregate'te)

| Metot | İmza | Kural |
|---|---|---|
| `Create` | `static ResultDomain<PaymentLink>(merchantId, orderRef, amount, currency, returnUrl, storeName, orderSummary, openToken, expiresAt)` | `amount>0`, `currency==TRY`, zorunlu alan dolu, `expiresAt>CreatedTime` değilse `INVALID_REQUEST`/`UNSUPPORTED_CURRENCY`. Pending doğar. |
| `Open` | `ResultDomain(DateTime now)` | Terminal → `LINK_...VIOLATION`. `now>ExpiresAt` → `Expire` + `LINK_EXPIRED`. Pending→Opened. Zaten Opened → idempotent `Ok`. |
| `Expire` | `ResultDomain(DateTime now)` | Aktif + `now>ExpiresAt` ise Expired'a geçir (lazy). Terminal ise no-op/uygun red. |
| `Cancel` | `ResultDomain()` | Aktif → Cancelled. Terminal → `TERMINAL_VIOLATION`. |
| `IsPayable` | `ResultDomain(DateTime now)` (saf guard) | Opened + `now<=ExpiresAt+grace` → `Ok`; past-grace → `Expire`+`LINK_EXPIRED`; aksi → uygun red. Grace parametresi handler'dan (Options). |
| `IsTerminal` | `bool` getter | `Status in {Paid,Expired,Cancelled}` |

> Not: Çekim metodu YOK (sağlayıcı yok). `PayPaymentLink` handler `IsPayable` guard'ını çağırıp `PROVIDER_UNAVAILABLE` döner; durum değişmez (research D7).

### Invariant'lar (FR eşlemesi)

- **Tek-kullanım** (FR-005): "tüketim" yalnız başarılı ödeme (Paid); aktif link süre/iptal sınırına dek tekrar açılabilir (Open idempotent).
- **TTL** (FR-002): `ExpiresAt` zorunlu; aşımda açılamaz/ödenemez.
- **Aktif-tekil** (FR-003): `(MerchantId, OrderRef)` aktif durumlarda tekil (partial unique, research D1); terminal sonrası yeni link serbest.
- **Terminal tek-yön** (FR-008/FR-009): terminale ulaşan link geri dönmez; aynı yön idempotent no-op, ters yön `TERMINAL_VIOLATION`.
- **Fail-closed açılış** (FR-007): bilinmeyen/geçersiz OpenToken hiçbir linki açamaz (handler lookup → 404).
- **Kart yok** (FR-011): aggregate PAN/CVV alanı taşımaz.

## Create akışı (idempotent aktif-tekil + lazy — research D1)

```
1. Alan doğrula (TRY, amount>0, zorunlu alanlar) → değilse Error.
2. (MerchantId, OrderRef) aktif satırı yükle (Status in {Pending,Opened}).
3a. Varsa + now<=ExpiresAt → idempotent: var olan link (LinkUrl+PgPaymentRef) döner.
3b. Varsa + now>ExpiresAt → link.Expire(now) + Store (partial slot boşalır) → 4'e devam.
3c. Yoksa → 4'e devam.
4. OpenToken üret; ExpiresAt = now + Ttl(Options); PaymentLink.Create(...); Store.
   (Yarış: ikinci eşzamanlı create partial unique'e çakışır → yakala, var olanı yükle+dön.)
5. Yanıt: LinkUrl = LinkBaseUrl/{OpenToken}, PgPaymentRef = Id.
```

## Persistence / Index (Program.cs)

- `(MerchantId, OrderRef)` → **partial unique** index, yalnız aktif durumlar (`Pending`,`Opened`) — research D1.
- `OpenToken` → unique index (lookup + çakışma koruması).
- Marten `ApplyAllDatabaseChangesOnStartup` (migration komutu yok; dev'de şema yeniden kurulur — defansif migration yok).

## Options (`PaymentLinkOptions`, non-secret)

| Alan | Varsayılan | Not |
|---|---|---|
| `Ttl` | 30 dk | link ömrü (FR-002, clarify) |
| `GraceWindow` | 5 dk | Opened oturum ödeme penceresi (clarify) |
| `LinkBaseUrl` | — | müşteri açılış linki tabanı (`{taban}/{OpenToken}`) |
| `TrustPageTexts` (başlık/ibare) | — | güven bağlamı sayfa statik metinleri |

> `CallbackSecret` (HMAC, secret) bu dilimde KULLANILMAZ (store'a sonuç bildirimi = çekim sonraki dilim); seed Options'tan düşürülür ya da sonraki dilime bırakılır.

## Hata kodları (`PaymentLinkResourceConstants`)

Mevcut (evrilir): `UNSUPPORTED_CURRENCY`, `INVALID_REQUEST`, `PROVIDER_UNAVAILABLE`, `TERMINAL_VIOLATION`. Eklenen: `LINK_NOT_FOUND` (bilinmeyen OpenToken/PgPaymentRef, SESSION_NOT_FOUND evrim), `LINK_EXPIRED`, `LINK_CANCELLED`. (`MERCHANT_NOT_ACTIVE`, `INITIALIZE_FAILED` bu dilimde düşer — merchant gate + sağlayıcı yok.)
