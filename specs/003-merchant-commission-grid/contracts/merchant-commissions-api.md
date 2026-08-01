# HTTP Kontratı: Merchant Commissions API

Servis: `Commission.Api`. Taban: `api/v{version:apiVersion}/merchant-commissions` (v1.0). Tag:
`merchant-commissions`. Tüm gövdeler JSON (System.Text.Json, invariant kültür → oran ondalığı `.`).
Hata gövdesi `FeatureObjectResultModel` (`isSuccess`, `messages[]` → `{ property, code }`). Yetki YOK
(proje geneli erteleme).

`Criteria` DTO (tüm uçlarda ortak, string enum kodları + int taksit):

```json
{ "cardBrand": "Visa", "cardType": "Bireysel", "transactionRegion": "Yurtici", "installmentCount": 6 }
```

---

## GET `/` — merchant komisyonlarını (enriched) getir

Bir merchant'ın grid satırlarını, banka aralığı + tavan-altı işaretiyle döner (read-time hesap).

**Query**: `merchantId` (Guid, zorunlu)

**200** `GetMerchantCommissionsResponse`:

```json
{
  "items": [
    {
      "id": "3f2b...",                // merchant oranı yoksa null
      "merchantId": "aa11...",
      "criteria": { "cardBrand": "Visa", "cardType": "Bireysel", "transactionRegion": "Yurtici", "installmentCount": 6 },
      "rate": 3.20,                   // girilmemişse null
      "bankMin": 1.80,                // banka yoksa null
      "bankMax": 2.95,                // banka yoksa null
      "belowBankCeiling": false,      // rate != null && bankMax != null && rate <= bankMax
      "isMissing": false              // rate == null
    }
  ]
}
```

- Satır kümesi: merchant'ın oranı olan kombinasyonlar ∪ en az bir bankanın servislediği kombinasyonlar.
- **Banka kodu filtresi YOK** (bilinçli).
- Başka merchant'ın verisi sızmaz (düz `MerchantId` filtresi).

**400**: geçersiz istek. **500**: beklenmeyen.

---

## POST `/` — tek komisyon oluştur/güncelle (upsert)

**Body** `CreateMerchantCommissionCommand`:

```json
{ "merchantId": "aa11...", "criteria": { "cardBrand": "Visa", "cardType": "Bireysel", "transactionRegion": "Yurtici", "installmentCount": 6 }, "rate": 3.20 }
```

- `(merchantId, criteria)` varsa → oran güncellenir; yoksa yeni kayıt.
- Doğrulama: `merchantId != Guid.Empty`; `criteria` enum parse + `installmentCount >= 1`; `rate > 0`.
- Banka yüklenmez, banka bağı kurulmaz.

**200** `{ "id": "3f2b..." }` — **400**: doğrulama (`rate <= 0` → `COMMON_MESSAGE_INVALID_RANGE`; geçersiz
enum → `COMMON_MESSAGE_INVALID_ENUM_TYPE`). **500**.

---

## PUT `/{id}` — oran güncelle

**Path**: `id` (Guid). **Body** `UpdateMerchantCommissionCommand`:

```json
{ "rate": 3.45 }
```

- Kayıt bulunamazsa → `COMMON_MESSAGE_RECORD_NOT_FOUND`. `rate > 0`. `Criteria`/`MerchantId` değişmez.

**200** `{ "id": "3f2b..." }` — **400** — **500**.

---

## POST `/bulk` — toplu upsert (grid)

Seçili merchant + doldurulan hücreler tek atomik işlemde upsert (`[Transactional]`).

**Body** `BulkUpsertMerchantCommissionsCommand`:

```json
{
  "merchantId": "aa11...",
  "items": [
    { "criteria": { "cardBrand": "Visa", "cardType": "Bireysel", "transactionRegion": "Yurtici", "installmentCount": 6 }, "rate": 3.20 },
    { "criteria": { "cardBrand": "MasterCard", "cardType": "Ticari", "transactionRegion": "Yurtdisi", "installmentCount": 1 }, "rate": 2.10 }
  ]
}
```

- Her item: `(merchantId, criteria)` varsa `UpdateRate`, yoksa `Create`.
- Aynı istekte tekrarlanan `criteria` → son değer geçerli (bellekte izlenir).
- Herhangi bir item geçersizse (`rate <= 0`, geçersiz enum) → **tümü** geri sarılır (atomik).
- Boş `items` → hatasız no-op (`{ created: 0, updated: 0 }`).

**200** `BulkUpsertMerchantCommissionsResponse`:

```json
{ "created": 1, "updated": 1 }
```

**400**: doğrulama. **500**.

---

## Notlar

- Endpoint deseni 002 `BankCommissions` slice'larıyla birebir (Minimal API + `IMessageBus.InvokeAsync` +
  `MapToApiVersion(1,0)`).
- Grid'in eksen seçenekleri mevcut `GET /bank-commissions/criteria-options`'tan gelir (yeniden kullanım).
- Merchant listesi bu servisten DEĞİL, `GET /api/v1/merchants` (Merchant.Api) üzerinden Admin'de alınır.