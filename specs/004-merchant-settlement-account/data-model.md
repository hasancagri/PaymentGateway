# Data Model: Merchant Settlement Hesabı (004)

## Aggregate: `MerchantSettlementAccount`

`AggregateRoot`'tan türer (Guid `Id` + denetim alanları: `CreatedTime`, `UpdatedTime`, `IsActive`,
`IsDeleted`). Marten document; schema `MerchantSchemaName` (`merchantManagement`). Tutarlılık sınırı
tek hesap. `Merchant`'a `MerchantId` ile bağlı (referans, navigation değil).

### Alanlar

| Alan | Tip | Kural |
|------|-----|-------|
| `Id` | `Guid` | AggregateRoot'tan; oluşturmada üretilir |
| `MerchantId` | `Guid` | Zorunlu. Varlığı handler'da doğrulanır (bu aggregate sorgulamaz) |
| `BankCode` | `string` | Zorunlu, 4 hane. Yerel `BankCatalog`'ta bulunmalı (doğrulama handler'da) |
| `Iban` | `string` | Zorunlu. Normalize (boşluksuz, büyük harf) saklanır. TR IBAN + mod-97 geçerli |
| `AccountOwnerName` | `string` | Zorunlu, boş değil |
| `AccountNo` | `string` | Opsiyonel (boş olabilir) |
| `AccountDescription` | `string` | Opsiyonel |
| `Status` | `SettlementAccountStatus` | Oluşturmada `Active` |

**Not**: `CurrencyId`, `Swift`, şube — YOK (yalnız TL/TR IBAN; bkz. research D1/D2).

### Enum: `SettlementAccountStatus`

Düz enum (mevcut `MerchantStatus` konvansiyonu).

```
Active  = 1
Passive = 2
```

### Davranışlar (aggregate metotları)

| Metot | İmza | Kural / Invariant |
|-------|------|-------------------|
| `Create` (static factory) | `ResultDomain<MerchantSettlementAccount> Create(Guid merchantId, string bankCode, string iban, string ownerName, string accountNo, string description)` | Zorunlu alan + IBAN format/mod-97 + BankCode 4-hane biçimi doğrular; IBAN'ı normalize eder; `Status = Active`. Hata → `ResultDomain<...>.Error`. **Merchant/BankCatalog/mükerrer IBAN varlık kontrolü BURADA DEĞİL — handler'da.** |
| `UpdateDetails` | `ResultDomain UpdateDetails(string bankCode, string iban, string ownerName, string accountNo, string description)` | Create ile aynı saf doğrulamalar; alanları günceller; `UpdatedTime = UtcNow` |
| `Activate` | `void Activate()` | `Status = Active`, `IsActive = true`, `UpdatedTime` |
| `Deactivate` | `void Deactivate()` | `Status = Passive`, `IsActive = false`, `UpdatedTime` (kayıt silinmez) |

### Saf doğrulama kuralları (aggregate içi)

- **Zorunlu**: `MerchantId != Guid.Empty`, `BankCode`, `Iban`, `AccountOwnerName` boş değil →
  aksi `COMMON_MESSAGE_VALUE_IS_REQUIRED`.
- **BankCode biçimi**: tam 4 hane (`^\d{4}$`) → aksi `COMMON_MESSAGE_INVALID_FORMAT`.
  (Katalogda var mı? → handler.)
- **IBAN**: boşluk temizle + upper → `^TR\d{24}$` **ve** ISO 13616 mod-97 == 1 → aksi
  `COMMON_MESSAGE_INVALID_FORMAT`. Normalize edilmiş biçim saklanır.

### İş kuralları (handler içi — saf değil)

| Kontrol | Sonuç kodu |
|---------|-----------|
| Merchant (MerchantId) var mı | yoksa `COMMON_MESSAGE_RECORD_NOT_FOUND` (Property: MerchantId) |
| BankCode `BankCatalog`'ta var mı (`IBankCodeLookup.Exists`) | yoksa `COMMON_MESSAGE_RECORD_NOT_FOUND` (Property: BankCode) |
| Aynı merchant + aynı normalize IBAN başka kayıt var mı | varsa `COMMON_MESSAGE_RECORD_DUPLICATE` (Property: Iban) |

## Yerel referans: `BankCatalog` + `IBankCodeLookup`

- `BankCatalog` (static): Commission.Api'dakiyle **aynı** 4-hane kod+ad listesinin Merchant BC
  kopyası. `TryGetName(code, out name)` / `All`.
- `IBankCodeLookup : ISingletonDependency` — `bool Exists(string code)`, `string? NameOf(string code)`.
  Impl bellekte `Dictionary` (country/mcc lookup deseni). Scrutor otomatik kaydeder.

## İlişkiler

```
Merchant (mevcut, DOKUNULMAZ)
   │ 1
   │
   │ 0..*
MerchantSettlementAccount ── BankCode ──▶ BankCatalog (yerel statik, referans)
```

- Bir merchant'ın 0..* settlement hesabı. Her hesap tam bir merchant'a ait (`MerchantId`).
- `BankCode` → `BankCatalog` girişi (yalnız doğrulama + ad türetimi; FK değil).
- Merchant aggregate değişmez; ters navigation yok.

## Sorgu projeksiyonları (read modeli)

- **GetMerchantSettlementAccounts(merchantId)**: `Where(a => a.MerchantId == merchantId && !a.IsDeleted)`.
  Liste item: `Id, BankCode, BankName (lookup), Iban, AccountOwnerName, Status`.
- **GetSettlementAccount(id)**: tek kayıt; item + `AccountNo, AccountDescription, CreatedTime`.
- Tenant sınırı: liste her zaman `MerchantId` ile filtrelenir (SC-003).