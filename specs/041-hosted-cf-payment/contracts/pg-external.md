# Kontrat: Store ↔ PaymentGateway (PG tarafı, 041)

PG'nin **sunduğu** (store'un çağırdığı/aldığı) yüzeyler. ECom 077 `pg-external.md` ile aynadır; burası
PG-otoriter (uygulama bu repoda). iyzico wire PG içindedir → `iyzico-cf-wire.md`.

## 1. Store → PG: hosted ödeme başlat (gelen, X-Api-Key auth)

```
POST /hosted-payment
Auth: X-Api-Key: {MerchantKey}          (SHA-256 → MerchantApiKeyReference → merchant_id claim)
Body: {
  Amount: decimal,                       (> 0, TL)
  Currency: "TRY",                       (yalnız TRY; aksi 4xx UNSUPPORTED_CURRENCY)
  OrderRef: string,                      (store TxRef; merchant kapsamında tekil → conversationId)
  CallbackUrl: string                    (store'un sonuç-bildirim adresi; PG buraya imzalı POST atar)
}
200 : { HostedUrl: string, PgPaymentRef: string }
4xx : merchant Active değil / geçersiz istek / desteklenmeyen para birimi
5xx : iyzico erişilemez
```

- Policy: `HostedPaymentApiKey` (ApiKey şeması + authenticated; route'ta merchantId YOK — tenant claim'den).
- **Active statü kapısı**: merchant `Active` değilse RET (charge yalnız Active — İlke V, fail-closed).
- **Idempotent başlatma**: aynı (merchant, OrderRef) pending ise yeni kayıt açılmaz; mevcut HostedUrl döner.

## 2. PG → Store: ödeme sonucu bildirimi (giden, HMAC imzalı)

```
POST {CallbackUrl}                        (store'un 1'de verdiği adres)
Header: X-Signature: HMAC-SHA256(CallbackSecret, raw_body)
Body: { TxRef: string(=OrderRef), PgPaymentRef: string, Status: "Success"|"Failed", ReasonCode?: string }
```

- iyzico dönüşü işlenip session terminal olduktan SONRA gönderilir (durable outbox + retry — kayıpsız).
- `CallbackSecret` = MerchantKey'den AYRI, PG config (v1 paylaşılan); store aynı sırla imzayı doğrular.
- Tekrar-callback aynı sonucu tekrar taşır (idempotent; terminal durumdan dönülmez).

## 3. iyzico → PG: hosted form dönüşü (gelen, secret-token kapılı)

```
POST /internal/payments/callback/{callbackToken}   (iyzico'ya CF initialize'da verilen PG-sahipli URL)
Body (form): token={iyzico CF token}
```

- `{callbackToken}` = PG'nin Start'ta ürettiği tahmin-edilemez per-session sır (İlke V beyan-edilen yetki).
  Bilinmeyen token → 404 red. Bununla session bulunur.
- Sonra **iyzico CF retrieve** ile sonuç bağımsız doğrulanır (kaynak-of-truth); sahte POST retrieve'de düşer; idempotent.
- Sonrası: (a) store'a §2 imzalı bildirim publish; (b) müşteri tarayıcısı §4 dönüş sayfasına yönlendirilir.

## 4. PG → Müşteri: dönüş sayfası (gelen GET / yönlendirme)

```
GET /payments/return/{pgPaymentRef}
200 : minimal HTML — "ödemen alındı" (Succeeded) | "ödeme başarısız" (Failed)
```

## Referans zinciri (kimlik sızmaz)

```
store TxRef ─► OrderRef ─► iyzico conversationId
                              │ (aynen geri)
store PaymentIntent ◄── PG callback TxRef ◄── iyzico retrieve
```

## Kapsam dışı (v1)

- İade/void, ödeme-durum sorgu ucu (poll-yedek), per-merchant CallbackSecret rotasyon, iyzico webhook-imza
  doğrulaması, gerçek-buyer/KYC (sandbox sentetik buyer), PG proaktif "terk" bildirimi (store timeout eder).
