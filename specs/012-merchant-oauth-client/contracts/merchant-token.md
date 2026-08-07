# Contract: Merchant token alma (`connect/token`)

Uç mevcut (Identity.Server, `https://localhost:5101/connect/token`); bu sözleşme merchant
istemcisinin kullanımını tanımlar.

## İstek

```
POST /connect/token
Content-Type: application/x-www-form-urlencoded

grant_type=client_credentials
&client_id=<merchantId>            # Guid
&client_secret=<MerchantKey>       # mk_<guid-N>
&scope=merchant.read merchant.write
```

## Başarılı yanıt (200)

```json
{
  "access_token": "<jwt>",
  "token_type": "Bearer",
  "expires_in": 900
}
```

JWT claim'leri: bkz. [data-model.md §3](../data-model.md) — `sub`, `merchant_id`,
`scope` (JSON dizi), `aud: merchant.api`, 15 dk ömür.

## Hata durumları

| Durum | HTTP | error |
|---|---|---|
| Bilinmeyen merchantId | 401 | `invalid_client` |
| Yanlış MerchantKey | 401 | `invalid_client` |
| Merchant Passive/Suspended (izinler kapalı) | 400 | `unauthorized_client` |
| İzinsiz scope isteği (ör. payment.write) | 400 | `invalid_request` (011'de kabul edilen OpenIddict etiketi) |

Not: 011'deki gibi OpenIddict hata etiketleri standarttan sapabilir; doğrulama ölçütü
"token verilmedi" davranışıdır, etiket birebir değil.

## Entegrasyon kuralları (merchant sistemine rehber)

- Token SAKLANMAZ-varsayımıyla tasarla: her çağrıda cache'ten iste; süresi dolmuşsa
  MerchantKey ile anında taze al; 401 gelirse BİR KEZ taze token'la tekrarla.
- MerchantKey YALNIZ bu uca gönderilir; BC API'lerine asla header/param olarak taşınmaz.