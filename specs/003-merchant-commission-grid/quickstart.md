# Quickstart / Doğrulama: Merchant Komisyon Grid

Feature'ın uçtan uca çalıştığını gösteren doğrulama senaryoları. Ayrıntılar için
[contracts/merchant-commissions-api.md](./contracts/merchant-commissions-api.md) ve
[data-model.md](./data-model.md).

## Önkoşul

```bash
dotnet run --project src/aspire/AppHost/AppHost.csproj   # Postgres + RabbitMQ + servisler
```

- Commission.Api, Merchant.Api ve Admin Aspire üzerinden ayakta.
- En az bir merchant kayıtlı (Merchant.Api / 001), en az birkaç `BankCommission` girili (002 grid) —
  tavan-altı ve aralık senaryolarını görebilmek için.

## Birim testleri (host'suz)

```bash
dotnet test tests/Commission.Api.Tests/Commission.Api.Tests.csproj
```

Kapsam:
- `MerchantCommission.Create`: `rate > 0`, `merchantId != Guid.Empty`, `criteria` zorunlu.
- `MerchantCommission.UpdateRate`: `rate > 0`.
- `GetMerchantCommissions` tavan-altı/aralık hesabı: `rate == bankMax`, `rate > bankMax`, banka yok
  (kenar durumlar; belowBankCeiling doğruluğu).

## Senaryo 1 — Tek kombinasyona oran (upsert)

```bash
# Oluştur
curl -X POST http://<commission>/api/v1/merchant-commissions -H 'Content-Type: application/json' -d '{
  "merchantId":"<GUID>",
  "criteria":{"cardBrand":"Visa","cardType":"Bireysel","transactionRegion":"Yurtici","installmentCount":6},
  "rate":3.20 }'
# → 200 { "id": "..." }

# Aynı kombinasyona tekrar POST → yeni kayıt değil, oran güncellenir (upsert).
```

**Beklenen**: İkinci POST kopya oluşturmaz; `GET` tek satır döner. `rate: 0` → 400
(`COMMON_MESSAGE_INVALID_RANGE`).

## Senaryo 2 — Enriched GET (banka aralığı + tavan-altı)

```bash
curl "http://<commission>/api/v1/merchant-commissions?merchantId=<GUID>"
```

**Beklenen**:
- Merchant oranı olan ve/veya banka servisli her kombinasyon bir satır.
- Bir kombinasyonu iki banka farklı oranlarla servisliyorsa `bankMin`/`bankMax` o iki oranın min/max'ı.
- `rate <= bankMax` olan satır `belowBankCeiling: true`; üstündeki `false`.
- Hiç banka servislemeyen kombinasyon `bankMin/bankMax: null`, `belowBankCeiling: false`.
- Oranı girilmemiş satır `rate: null`, `isMissing: true`.

## Senaryo 3 — Retroaktif banka değişimi (read-time tazelik)

1. Merchant oranı `2.00`, o kombinasyonun tek banka oranı `1.50` → `belowBankCeiling: false`.
2. 002 grid'inden o bankanın oranını `2.50`'ye yükselt.
3. Merchant `GET`'i tekrar çağır.

**Beklenen**: Merchant oranı değişmese de `belowBankCeiling: true` olur (işaret bayatlamaz, read-time).

## Senaryo 4 — Toplu upsert (grid, atomik)

```bash
curl -X POST http://<commission>/api/v1/merchant-commissions/bulk -H 'Content-Type: application/json' -d '{
  "merchantId":"<GUID>",
  "items":[
    {"criteria":{"cardBrand":"Visa","cardType":"Bireysel","transactionRegion":"Yurtici","installmentCount":6},"rate":3.20},
    {"criteria":{"cardBrand":"MasterCard","cardType":"Ticari","transactionRegion":"Yurtdisi","installmentCount":1},"rate":2.10}
  ]}'
# → 200 { "created": n, "updated": m }
```

**Beklenen**: Karışık create/update doğru sayılır. Bir item'da `rate: 0` → **tüm** istek 400, hiçbir
kayıt yazılmaz (atomik geri sarma). Boş `items` → `{ created:0, updated:0 }`.

## Senaryo 5 — Admin grid (elle)

1. Admin → **Merchant Komisyon Grid** → bir merchant seç (liste `GET /api/v1/merchants`'ten).
2. Grid marka × tip × bölge × taksit(1..15) tüm kombinasyonları listeler; eksik hücreler işaretli.
3. Her satırda banka aralığı (`bankMin–bankMax`) görünür; tavan-altı satır kırmızı; banka yoksa "banka yok".
4. Eksen filtreleri (marka/tip/bölge/taksit) satırları daraltır; "boşları doldur" görünen boş alanları
   tek değerle doldurur (dolular korunur); sayfa başına 20 satır.
5. **Kaydet** → tek `POST /bulk` çağrısı; sayfa yenilenince değerler dolu, tavan-altı işaretleri güncel.

**Beklenen**: Girişten kayda tek atomik işlem; banka maliyeti giriş anında görünür (SC-001/SC-002).