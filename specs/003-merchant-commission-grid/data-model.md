# Phase 1 Data Model: Merchant Komisyon Grid

Commission BC (`commission` şeması, Marten document store). Bu feature `MerchantCommission` belgesini
yeniden şekillendirir; `BankCommission` ve `Criteria` referans/salt-okunur olarak kullanılır.

## Aggregate: `MerchantCommission` (REFACTOR)

Bir merchant'ın belirli bir kombinasyon için gateway'e ödediği komisyon oranı. Banka-bağımsız.

| Alan | Tip | Kural |
|------|-----|-------|
| `Id` | `Guid` | `AggregateRoot`'tan; Marten kimliği. |
| `MerchantId` | `Guid` | `Guid.Empty` olamaz. Opak referans (Merchant.Api'ye çağrı yok). |
| `Criteria` | `Criteria` (VO) | Zorunlu. Kart markası × tip × bölge × taksit. |
| `Rate` | `decimal` | `> 0` (kesin büyük). Yüzde. |
| `IsDeleted` / denetim | — | `AggregateRoot`'tan (soft-delete + zaman damgaları). |

**Kaldırılan alanlar** (önceki modelden): `BankCommissionId`, `BankCode`.

**Benzersizlik**: `(MerchantId, Criteria)` — aynı merchant + aynı kombinasyon tek satır. Handler kontrol
eder (upsert); Marten unique index zorunlu değil (mevcut `BankCommission` deseniyle tutarlı, bellek-içi kontrol).

**Davranışlar**:

- `static ResultDomain<MerchantCommission> Create(Guid merchantId, Criteria criteria, decimal rate)`
  - `merchantId != Guid.Empty` değilse → `COMMON_MESSAGE_VALUE_IS_REQUIRED`
  - `criteria is null` → `COMMON_MESSAGE_VALUE_IS_REQUIRED`
  - `rate <= 0` → `COMMON_MESSAGE_INVALID_RANGE`
- `ResultDomain UpdateRate(decimal rate)`
  - `rate <= 0` → `COMMON_MESSAGE_INVALID_RANGE`
  - Başarılıda `Rate` güncellenir, `UpdatedTime = DateTime.UtcNow`.

**Not**: Banka oranıyla karşılaştırma aggregate'te YOK (read-time projeksiyon; bkz. Türetilmiş görünüm).

## Value Object: `Criteria` (DEĞİŞMEZ, mevcut)

`SharedKernel/Criteria.cs` — kart markası × tip × bölge × taksit. Değer eşitliği (record). `Create` ve
`FromCodes` (enum parse + `installmentCount >= 1`) fabrikaları. Bu feature onu aynen kullanır.

## Referans: `BankCommission` (SALT-OKUNUR)

002'de tanımlı. `(BankCode, Criteria, Rate)`. Merchant grid'i onu değiştirmez; yalnız kombinasyon başına
banka oranı **aralığını** (min/max) hesaplamak için okur.

## Türetilmiş görünüm (SAKLANMAZ): Grid satırı

`GetMerchantCommissions` handler'ının okuma anında ürettiği enriched satır. Kalıcı değildir.

| Alan | Tip | Kaynak |
|------|-----|--------|
| `id` | `Guid?` | Merchant oranı varsa kaydın Id'si; yoksa `null`. |
| `merchantId` | `Guid` | Sorgu parametresi. |
| `criteria` | `CriteriaView` | Kombinasyon (marka/tip/bölge/taksit string+int). |
| `rate` | `decimal?` | Merchant oranı; girilmemişse `null`. |
| `bankMin` | `decimal?` | Bu kombinasyonu servisleyen banka oranlarının min'i; banka yoksa `null`. |
| `bankMax` | `decimal?` | Aynı kümenin max'ı; banka yoksa `null`. |
| `belowBankCeiling` | `bool` | `rate != null && bankMax != null && rate <= bankMax`. |
| `isMissing` | `bool` | `rate == null` (merchant oranı henüz girilmemiş). |

**Satır kümesi** (handler): merchant'ın oranı olan kombinasyonlar ∪ en az bir bankanın servislediği
kombinasyonlar. Her `Criteria` anahtarı için:
- merchant oranı (varsa) → `rate`, `id`
- o `Criteria`'yı servisleyen `BankCommission` oranları → `bankMin`, `bankMax`
- `belowBankCeiling`, `isMissing` yukarıdaki formüllerle.

Hiçbir eksene ait olmayan/ne merchant ne banka satırı olmayan kombinasyonlar backend'de üretilmez; grid
bunları eksen seçeneklerinden (criteria-options) tamamlayıp `isMissing = true`, `bank yok` olarak gösterir.

## Doğrulama kuralları (spec eşleme)

| Kural | Kaynak | Yer |
|-------|--------|-----|
| `rate > 0` | FR-003 | Aggregate `Create`/`UpdateRate` |
| `(MerchantId, Criteria)` tekil, upsert | FR-002 | Command handler'lar |
| Banka bağı yok | FR-004 | Aggregate (alan yok) |
| Enriched satır (bankMin/max) | FR-005 | `GetMerchantCommissions` handler |
| `belowBankCeiling` read-time | FR-006 | `GetMerchantCommissions` handler |
| Tavan-altı engellemez | FR-007 | Kayıt yolunda kontrol YOK (yalnız görünüm) |
| Banka yoksa işaretsiz | FR-008 | `bankMax == null → belowBankCeiling = false` |
| Atomik toplu upsert | FR-009 | `BulkUpsertMerchantCommissions` `[Transactional]` |
| Taksit 1..15 | FR-018 | `Criteria` + grid eksen seçenekleri |