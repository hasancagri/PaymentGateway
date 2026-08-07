# Quickstart: Merchant OAuth İstemci Düzlemi (012) — Canlı Doğrulama

Ön koşul: `dotnet run --project src/aspire/AppHost/AppHost.csproj` (Postgres + RabbitMQ +
Identity.Server 5101 + BC API'leri ayakta). Merchant.Api adresi Aspire dashboard'dan
alınır (aşağıda `$MERCHANT_API`). Admin token'ı için `admin-ui` istemcisi kullanılır
(secret: `admin-ui-dev-secret`).

Yardımcı:

```bash
IDP=https://localhost:5101
admin_token() {
  curl -sk -X POST $IDP/connect/token \
    -d "grant_type=client_credentials&client_id=admin-ui&client_secret=admin-ui-dev-secret&scope=merchant.read merchant.write" \
    | jq -r .access_token
}
```

## S1 — Onboarding → otomatik istemcileşme → token alma (US1, US3/1)

1. Admin token'ıyla merchant yarat: `POST $MERCHANT_API/api/v1/merchants/` (gövde:
   ad/e-posta/ülke/şehir/MCC — 001 quickstart gövdesi). Yanıttan `id` ve `merchantKey`
   değerlerini al.
2. Birkaç saniye bekle (event tüketimi), sonra merchant kimliğiyle token iste:

```bash
curl -sk -X POST $IDP/connect/token \
  -d "grant_type=client_credentials&client_id=<id>&client_secret=<merchantKey>&scope=merchant.read merchant.write"
```

**Beklenen**: 200; `expires_in: 900`. JWT payload'ında (jwt.io / `cut -d. -f2 | base64 -d`):
`sub=<id>`, `merchant_id=<id>`, `scope=["merchant.read","merchant.write"]`, `aud=merchant.api`.

## S2 — Yanlış kimlik reddi (US1/2,3)

Aynı istek `client_secret=mk_yanlis` ile → **401 `invalid_client`**.
Rastgele Guid `client_id` ile → **401 `invalid_client`**.

## S3 — Kendi verisi 200 / başkasının verisi 403 (US2)

İkinci bir merchant yarat (S1 tekrar, `id2`/`merchantKey2`). Merchant-1 token'ıyla:

```bash
MTOKEN=<S1 token>
curl -sk -H "Authorization: Bearer $MTOKEN" $MERCHANT_API/api/v1/merchants/<id>                                # beklenen: 200
curl -sk -H "Authorization: Bearer $MTOKEN" $MERCHANT_API/api/v1/merchants/<id>/settlement-accounts/          # beklenen: 200
curl -sk -H "Authorization: Bearer $MTOKEN" -X POST $MERCHANT_API/api/v1/merchants/<id>/settlement-accounts/ \
  -H "Content-Type: application/json" -d '{...004 quickstart gövdesi...}'                                     # beklenen: 200/201
curl -sk -H "Authorization: Bearer $MTOKEN" $MERCHANT_API/api/v1/merchants/<id2>                              # beklenen: 403
curl -sk -H "Authorization: Bearer $MTOKEN" $MERCHANT_API/api/v1/merchants/<id2>/settlement-accounts/         # beklenen: 403
```

## S4 — Fail-closed uçlar (edge case)

Merchant token'ıyla:

```bash
curl -sk -H "Authorization: Bearer $MTOKEN" $MERCHANT_API/api/v1/merchants/                     # liste → 403
curl -sk -H "Authorization: Bearer $MTOKEN" $MERCHANT_API/api/v1/merchants/by-key/<merchantKey> # by-key → 403
curl -sk -H "Authorization: Bearer $MTOKEN" -X POST $MERCHANT_API/api/v1/merchants/ -d '{...}'  # create → 403
curl -sk -H "Authorization: Bearer $MTOKEN" -X PUT $MERCHANT_API/api/v1/merchants/<id>/status \
  -H "Content-Type: application/json" -d '{"status":"Active"}'                                  # kendi status'ü bile → 403 (AdminPlaneOnly)
```

## S5 — Diğer BC'lerin reddi (SC-005)

```bash
curl -sk -H "Authorization: Bearer $MTOKEN" $PAYMENT_API/api/v1/pos-accounts/    # beklenen: 401 (audience)
curl -sk -H "Authorization: Bearer $MTOKEN" $COMMISSION_API/api/v1/banks/        # beklenen: 401 (audience)
```

## S6 — Askıya alma → token reddi → reaktivasyon (US3, FR-003)

```bash
AT=$(admin_token)
curl -sk -X PUT -H "Authorization: Bearer $AT" -H "Content-Type: application/json" \
  $MERCHANT_API/api/v1/merchants/<id>/status -d '{"status":"Suspended"}'          # 200
# birkaç saniye bekle (event), sonra merchant token isteği:
# → beklenen: 400 unauthorized_client (yeni token YOK)
# Eldeki MTOKEN süresi dolana dek çalışmaya devam eder (15 dk pencere — bilinçli):
curl -sk -H "Authorization: Bearer $MTOKEN" $MERCHANT_API/api/v1/merchants/<id>   # 200 (token ölene dek)
# Reaktivasyon:
curl -sk -X PUT -H "Authorization: Bearer $AT" -H "Content-Type: application/json" \
  $MERCHANT_API/api/v1/merchants/<id>/status -d '{"status":"Active"}'             # 200
# → merchant token isteği yeniden 200
```

## S7 — Regresyon (SC-004)

- Admin BFF ekranları (merchant liste/yarat, settlement, komisyon, bincard) çalışır —
  admin token'ı claim'siz, MerchantScoped'dan etkilenmez.
- Payment.Agent A2A akışı (011 S4 senaryosu) çalışır — agent token'ı claim'siz.
- 15 dk global ömür: Admin/Agent handler'ları proaktif yenilediği için fark hissedilmez.

## Entegrasyon kuralı (merchant sistemlerine not)

Token atılabilir cache'tir; kalıcı olan MerchantKey'dir. Her API çağrısında token'ı
cache'ten iste; süresi dolmuşsa MerchantKey ile anında taze al; 401'de bir kez yenileyip
tekrarla. MerchantKey'i `connect/token` dışında HİÇBİR uca gönderme.