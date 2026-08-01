# Data Model: Merchant Key

Phase 1 — spec + research'ten türetilen veri modeli. Yalnız Merchant.Api etkilenir.

## Aggregate: Merchant (değişiklik)

Mevcut `Merchant : AggregateRoot` aggregate'i tek yeni alan kazanır.

### Yeni alan

| Alan | Tip | Görünürlük | Açıklama |
|------|-----|------------|----------|
| `MerchantKey` | `string` | `public get; private set;` | Gateway'in onboarding'de ürettiği benzersiz, değişmez, açık dış kimlik. Örn. `mk_9f1c2a7b8d3e4f5061728394a5b6c7d8`. |

Mevcut `Id` (Guid, `AggregateRoot`'tan) iç kimlik olarak kalır; `MerchantKey` dış kimliktir.

### Invariant'lar (aggregate'te uygulanır)

- **INV-1 (presence)**: `MerchantKey` boş/whitespace olamaz. `Create` içinde doğrulanır; ihlalde
  `ResultDomain<Merchant>.Error` (`MessageItem.Code = COMMON_MESSAGE_VALUE_IS_REQUIRED`).
- **INV-2 (immutability)**: `MerchantKey` yalnız `Create`'te atanır. Onu değiştiren hiçbir metot
  yoktur; `UpdateProfile` ve status metotları ona dokunmaz.

### `Create` imza değişikliği

`MerchantKey` yeni bir **parametre** olarak eklenir (handler üretip geçirir):

```
static ResultDomain<Merchant> Create(
    string merchantKey,     // YENİ — ilk parametre
    string name, string email, string phone,
    string countryCode, string cityCode, string mcc, string webhookUrl)
```

Mevcut format doğrulamaları (`Validate`) korunur; başına `merchantKey` boş-değil kontrolü eklenir.

### Değişmeyen metotlar

`UpdateProfile`, `Activate`, `Deactivate`, `Suspend` — imza/davranış aynı, `MerchantKey`'e yazmaz.

## Domain helper: MerchantKeyGenerator (yeni, saf)

Kimlik/aggregate değil — üretim mantığını izole eden saf statik yardımcı.

| Üye | İmza | Davranış |
|-----|------|----------|
| `Generate` | `static string Generate()` | `"mk_" + Guid.NewGuid().ToString("N")` döndürür. URL-güvenli, benzersiz, boşluksuz. |

Benzersizlik *garantisi* handler'daki üret-kontrol döngüsündedir (bkz. research R4); generator yalnız
aday üretir.

## Read models (query response değişiklikleri)

Aşağıdaki response'lara `MerchantKey` (string) alanı eklenir:

- **CreateMerchantResponse**: mevcut `Id`'ye ek `MerchantKey`.
- **GetMerchantResponse**: mevcut alanlara ek `MerchantKey`.
- **GetAllMerchants** öğe response'u: satır başına `MerchantKey`.

Yeni read model:

- **GetMerchantByKeyResponse**: `GetMerchantResponse` ile aynı şekil (Id, MerchantKey, temel bilgiler,
  status, CreatedTime). Bulunamazsa `FeatureObjectResultModel<T>.NotFound()`.

## Persistence

- Marten document store; `Merchant` dokümanına yeni string özellik serileşir. Şema migration'ı
  gerekmez (Marten şemasız doküman; henüz seed/prod veri yok → backfill yok).
- Benzersizlik uygulama katmanında (handler query) korunur; DB unique index bu dilimde eklenmez
  (research R4 — hacim artarsa gelecekte değerlendirilir).

## Durum / yaşam döngüsü

`MerchantKey` durumsuzdur — üretildikten sonra sabit. Merchant status geçişleri (Active/Passive/
Suspended) key'i etkilemez; key ile arama merchant'ı mevcut status'üyle döndürür (çağıran değerlendirir).