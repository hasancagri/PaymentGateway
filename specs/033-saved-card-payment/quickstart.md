# Quickstart: Kayıtlı Kartla Ödeme (033)

## Ön koşullar

1. AppHost ayakta; Payment.Api :5201; iyzico sandbox key user-secrets'ta (032'den).
2. Aktif merchant (ECommerce Demo `4d1fd0ea...`) + MerchantKey. **Merchant Active olduğundan
   payment.charge scope'unu almış olmalı** (Identity restart sonrası; token'da scope kontrol edilir).
3. **Bir kart 032 ile saklanmış olmalı** → vault token elde (S0).

## S0 — Kart sakla (032, önkoşul)

```bash
MT=$(curl -sk https://localhost:5101/connect/token -d "grant_type=client_credentials&client_id=<m>&client_secret=<mk>&scope=cards.write payment.charge" | jq -r .access_token)
VTOKEN=$(curl -s -X POST "http://localhost:5201/api/v1.0/merchants/<m>/vault/cards" -H "Authorization: Bearer $MT" -H "Content-Type: application/json" -d '{"pan":"5528790000000008","expiry":"12/30","holderName":"CARD HOLDER"}' | jq -r .token)
```

## S1 — Taksit sorgusu (US2)

```bash
curl -s -X POST "http://localhost:5201/api/v1.0/merchants/<m>/payments/installment-options" \
  -H "Authorization: Bearer $MT" -H "Content-Type: application/json" -d '{"bin":"552879","price":100.0}'
```
**Beklenen**: `installmentDetails[]` — taksit sayıları + totalPrice (vade farklı). En az `{1,100.0}`.

## S2 — Çekim (US1)

```bash
curl -s -X POST "http://localhost:5201/api/v1.0/merchants/<m>/payments" \
  -H "Authorization: Bearer $MT" -H "Content-Type: application/json" \
  -d "{\"vaultToken\":\"$VTOKEN\",\"price\":100.0,\"paidPrice\":100.0,\"installment\":1,
       \"buyer\":{\"name\":\"Ada\",\"surname\":\"Yilmaz\",\"email\":\"ada@example.com\",\"gsmNumber\":\"+905551112233\",\"identityNumber\":\"11111111110\",\"registrationAddress\":\"Istanbul\",\"city\":\"Istanbul\",\"country\":\"Turkiye\",\"ip\":\"85.34.78.112\"},
       \"basketItems\":[{\"id\":\"SKU1\",\"name\":\"Urun\",\"category1\":\"Genel\",\"price\":100.0}]}"
```
**Beklenen**: `{ paymentId, providerPaymentId, status:"Success", ... }`.

**Gateway DB (FR-003)**: `mt_doc_payment` satırında ProviderPaymentId + IyzicoCommission + IyzicoFee +
Status Success. **PAN/CVC sızma (SC-002)**: `data::text like '%5528790000000008%'` → 0.
**Event (SC-005)**: RabbitMQ / consumer log — 1 PaymentChargedEvent.

**Negatifler**: iptal edilmiş (Revoked) kart token'ı → 400; yabancı merchantId → 403; scope'suz
token (cards.write ama payment.charge yok) → 403; taksitli çekim (installment=3, paidPrice=106) →
Success.

## S3 — ECommerce checkout uçtan uca (US3)

1. İki AppHost; ECommerce'te giriş yapmış kullanıcı, kayıtlı kart var (032/S0)
2. Sepete ürün → ödeme adımı → kayıtlı kart seç → taksit seçenekleri görünür → taksit seç → öde
3. **Beklenen**: sipariş "ödendi"; gateway'de Payment kaydı; ECommerce logunda gateway charge başarılı
4. ECommerce'te CVC/kart no girilmez (kayıtlı kart)

## Kapanış

- `dotnet build` 0 hata; `dotnet test tests/Payment.Api.Tests` yeşil (Payment aggregate testleri) +
  Merchant/Commission regresyon. Canlı S1+S2 (curl) + S3 (ECommerce) doğrulanır.
