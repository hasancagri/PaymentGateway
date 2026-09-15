# Phase 1 Data Model: Hosted-CF Ödeme Yüzeyi (041)

BC: Payment (paymentDb, Marten). Yeni aggregate: `HostedPaymentSession`. Mevcut referanslar değişmez.

## 1. HostedPaymentSession (aggregate root)

Bir hosted ödeme denemesini temsil eder. Kart verisi TAŞIMAZ (yalnız iyzico'da). `AggregateRoot`'tan türer.

| Alan | Tip | Not |
|---|---|---|
| `Id` | `Guid` | = **PgPaymentRef** (store'a dönen referans; iyzico conversationId için de kaynak) |
| `MerchantId` | `Guid` | Tenant (X-Api-Key claim'inden) |
| `OrderRef` | `string` | Store TxRef; **(MerchantId, OrderRef) tekil** |
| `Amount` | `decimal` | Ödeme tutarı (TL); iyzico Price/PaidPrice |
| `CallbackUrl` | `string` | Store'un verdiği sonuç-bildirim adresi (PG buraya imzalı POST atar) |
| `CheckoutFormToken` | `string` | iyzico CF init token'ı (retrieve için) |
| `CallbackToken` | `string` | **C1 fix**: tahmin-edilemez per-session sır (Start'ta üretilir). iyzico'ya verilen callback URL'inin path segmenti; kimliksiz callback ucunun beyan-edilen yetkisi (İlke V). Kiracı-içi tekil. |
| `Status` | `HostedPaymentStatus` | `Pending` \| `Succeeded` \| `Failed` |
| `FailureReason` | `string?` | Başarısızsa sebep kodu (iyzico errorCode / paymentStatus) |
| `ProviderPaymentId` | `string?` | iyzico paymentId (retrieve'den; başarıda dolar) |
| `CreatedAtUtc` / `UpdatedAtUtc` | audit | AggregateRoot denetim alanları |

### Enum (aynı dosyada — `HostedPaymentSession.cs`)

```
HostedPaymentStatus { Pending, Succeeded, Failed }
```

### Davranış (ResultDomain; handler'dan çağrılır — İlke II)

- `static HostedPaymentSession Start(Guid merchantId, string orderRef, decimal amount, string callbackUrl,
  string callbackToken)` — factory. `Status = Pending`. `CallbackToken` = tahmin-edilemez sır (handler üretir,
  ör. `Guid.NewGuid("N")`). Doğrulama: amount > 0, orderRef/callbackUrl/callbackToken boş değil (guard).
- `ResultDomain AttachCheckoutForm(string token)` — CF init başarısından sonra token'ı bağlar (Pending iken).
- `ResultDomain MarkSucceeded(string providerPaymentId)` — Pending → Succeeded. **Terminal idempotent**:
  zaten Succeeded ise no-op `Ok`; Failed iken çağrılırsa terminal-ihlali RET (geri dönülmez, FR-008).
- `ResultDomain MarkFailed(string reasonCode)` — Pending → Failed. Zaten Failed ise no-op `Ok`; Succeeded
  iken çağrılırsa RET (başarılıdan geri dönülmez).
- `bool IsTerminal` (getter — muaf).

### Invariant'lar

- Terminal durumdan (Succeeded/Failed) çıkış YOK; başarılı → başarılı kalır (FR-008).
- `CheckoutFormToken` yalnız `Start` sonrası, terminal öncesi bağlanır.
- Amount > 0; para birimi kontrolü handler'da (yalnız TRY, FR-012) — session TL varsayar.

### Marten şeması (Program.cs)

```
opts.Schema.For<HostedPaymentSession>()
    .Index(x => new { x.MerchantId, x.OrderRef }, i => i.IsUnique = true)   // FR-004 tekillik
    .Index(x => x.CallbackToken, i => i.IsUnique = true);                   // C1: callback lookup + tekil sır
```

## 2. StoreCallbackDelivery mesajı (durable local — kalıcı doküman DEĞİL)

Store'a imzalı sonuç bildirimi için Wolverine local durable mesajı (outbox → retry). Payload:

| Alan | Tip |
|---|---|
| `PgPaymentRef` | `Guid` |
| `CallbackUrl` | `string` (store) |
| `TxRef` | `string` (= OrderRef) |
| `Status` | `string` (`Success` \| `Failed`) |
| `ReasonCode` | `string?` |

Handler ham JSON gövde üretir → `X-Signature = HMAC-SHA256(CallbackSecret, raw_body)` ile POST. Başarısızlıkta
Wolverine durable retry (FR-009 kayıpsız).

## 3. Mevcut referanslar (DEĞİŞMEZ — okunur)

- **MerchantStatusReference** (`{Id=MerchantId, Status, UpdatedAtUtc}`): charge statü kapısı (Active-only, D2).
- **MerchantApiKeyReference** (`{Id=MerchantId, KeyHash}`): X-Api-Key auth lookup (D1).

Her ikisi `merchant.lifecycle` fanout'undan `MerchantLifecycleEventHandler` ile beslenir; bu feature YAZMAZ.

## 4. Referans zinciri (kimlik sızmaz)

```
store TxRef  ──►  OrderRef  ──►  iyzico conversationId (= PgPaymentRef Id'den türetilir)
                                     │ (aynen geri)
store PaymentIntent ◄── PG callback TxRef(=OrderRef) ◄── iyzico retrieve sonucu
```

iyzico/PG store UserId'sini görmez; store kendi tarafında `TxRef → PaymentIntent → OrderId → UserId` türetir.

## 5. Yaşam döngüsü (durum makinesi)

```
[yok] --Start--> Pending --AttachCheckoutForm--> Pending
Pending --MarkSucceeded--> Succeeded   (terminal)
Pending --MarkFailed-----> Failed      (terminal)
Succeeded/Failed --tekrar callback--> (no-op, aynı sonuç yeniden bildirilir)
```
