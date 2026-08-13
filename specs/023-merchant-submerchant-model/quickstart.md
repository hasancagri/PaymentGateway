# Quickstart: Merchant SubMerchant Model (023)

Canlı doğrulama rehberi — uçtan uca CRUD + kimlik zinciri (SC-001..SC-005).
Sözleşme ayrıntısı: [contracts/merchants-api.md](contracts/merchants-api.md);
alan/kural seti: [data-model.md](data-model.md).

## Önkoşullar

- .NET 10 SDK, Docker (Aspire: Postgres + RabbitMQ)
- Identity.Server dev cert güvenilir (`dotnet dev-certs https --trust`)

## Derleme + birim testleri (SC-005)

```bash
dotnet build          # 0 hata beklenir
dotnet test           # tests/Merchant.Api.Tests — tümü yeşil, DB/ağ bağımlılığı yok
```

## Sistemi başlat

```bash
dotnet run --project src/aspire/AppHost/AppHost.csproj
```

Dashboard'da `merchant-api`, `identity-server`, `postgres`, `rabbitmq` Running olmalı.
Aşağıda `MERCHANT_API` = merchant-api'nin dashboard'daki http adresi.

## S1 — Admin token al

```bash
ADMIN_TOKEN=$(curl -sk https://localhost:5101/connect/token \
  -d grant_type=client_credentials -d client_id=admin-ui \
  -d client_secret=<Clients:admin-ui:Secret> \
  -d scope="merchant.read merchant.write" | jq -r .access_token)
```

## S2 — CRUD döngüsü (SC-001, US1)

```bash
# Oluştur (Personal): yanıtta merchantId + merchantKey (TEK sefer) beklenir
curl -s -X POST $MERCHANT_API/api/v1/merchants \
  -H "Authorization: Bearer $ADMIN_TOKEN" -H "Content-Type: application/json" \
  -d '{"type":"Personal","name":"Ahmet Ticaret","email":"ahmet@ornek.com",
       "gsmNumber":"+905551112233","address":"İstanbul","iban":"TR330006100519786457841326",
       "contactName":"Ahmet","contactSurname":"Yılmaz","identityNumber":"11111111110"}'
# → merchantId=MID, merchantKey=MKEY kaydet

# Tekil getir: alanlar görünür, merchantKey alanı YOK (SC-004)
curl -s $MERCHANT_API/api/v1/merchants/$MID -H "Authorization: Bearer $ADMIN_TOKEN"

# Güncelle: ad değişir; kimlik + (yaratılıştaki) merchantKey değişmez
curl -s -X PUT $MERCHANT_API/api/v1/merchants/$MID ... # aynı gövde, name güncel

# Listele: kayıt listede, merchantKey yok
curl -s $MERCHANT_API/api/v1/merchants -H "Authorization: Bearer $ADMIN_TOKEN"
```

## S3 — Doğrulama redleri (SC-002, US1)

Her biri `IsSuccess=false` + alan bazlı Türkçe mesaj döner, kayıt oluşmaz:

1. `type=LimitedOrJointStockCompany` + `taxNumber`/`legalCompanyTitle` boş → tip-uyum reddi.
2. `iban="TR00INVALID"` → IBAN biçim reddi.
3. `email="bozuk"` → e-posta biçim reddi.
4. `type=Personal` + vergi alanları boş → BAŞARILI (matris: şahısta vergi zorunlu değil).

## S4 — Kimlik zinciri (SC-003, US2)

```bash
# 1) Oluşturma sonrası Identity log'unda: "Merchant istemcisi oluşturuldu: <MID> (status: Active)"
#    (consumer log'unda "Successfully processed message" var, "No known handler" YOK)

# 2) Merchant kendi token'ını alır (Active → verilir)
M_TOKEN=$(curl -sk https://localhost:5101/connect/token \
  -d grant_type=client_credentials -d client_id=$MID -d client_secret=$MKEY \
  -d scope="merchant.read merchant.write" | jq -r .access_token)

# 3) Merchant kendi kaydını okur → 200; BAŞKA bir merchantId → 403 (MerchantScoped)
curl -s $MERCHANT_API/api/v1/merchants/$MID -H "Authorization: Bearer $M_TOKEN"

# 4) Merchant token'ı ile liste ve statü ucu → 403 (AdminPlaneOnly, US2 senaryo 4)
curl -s $MERCHANT_API/api/v1/merchants -H "Authorization: Bearer $M_TOKEN" -o /dev/null -w "%{http_code}\n"
curl -s -X PUT $MERCHANT_API/api/v1/merchants/$MID/status -H "Authorization: Bearer $M_TOKEN" \
  -H "Content-Type: application/json" -d '{"status":"Active"}' -o /dev/null -w "%{http_code}\n"

# 5) Admin statüyü Passive yapar → Identity izinleri kapatır
curl -s -X PUT $MERCHANT_API/api/v1/merchants/$MID/status \
  -H "Authorization: Bearer $ADMIN_TOKEN" -H "Content-Type: application/json" \
  -d '{"status":"Passive"}'

# 6) Merchant token isteği artık REDDEDİLİR (unauthorized_client)
curl -sk https://localhost:5101/connect/token \
  -d grant_type=client_credentials -d client_id=$MID -d client_secret=$MKEY \
  -d scope="merchant.read"

# 7) Aynı statüye tekrar geçiş (Passive→Passive) → başarı, Identity log'unda YENİ satır yok (idempotent no-op)
```

## Beklenen sonuç özeti

| Senaryo | Kanıt |
|---------|-------|
| SC-001 | S2 döngüsü uçtan uca tamam |
| SC-002 | S3'ün 1-3'ü redded, 4 geçer; hiçbir red kayıt üretmez |
| SC-003 | S4: Active token alır, Passive sonrası red |
| SC-004 | merchantKey yalnız S2 oluşturma yanıtında; tekil/liste yanıt gövdesinde alan yok |
| SC-005 | `dotnet build` 0 hata + `dotnet test` yeşil |
