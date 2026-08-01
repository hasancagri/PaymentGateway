# Phase 1 Data Model: Bank Referansı + Komisyon Grid

## Yeni aggregate: Bank

Commission BC (`commission` şeması). `AggregateRoot` (BaseModel'den: `Id` Guid, `IsActive`,
`IsDeleted`, `CreatedTime`, `UpdatedTime`, `DeletedTime`, audit alanları).

| Alan | Tip | Görünürlük | Kural |
|------|-----|-----------|-------|
| `Code` | string | private set | Zorunlu, tam 4 hane. İş anahtarı, benzersiz (`!IsDeleted`). İmmutable (Create sonrası değişmez). Kanonik katalogda bulunmalı. |
| `Name` | string | private set | Katalogdan türer (Create'te Code'a göre set edilir). Elle girilmez, immutable. |
| `SupportedInstallments` | `List<int>` | private set | Boş değil; her değer 1..`MaxInstallment`; distinct + artan sıralı. |
| `IsActive` | bool | miras (public set BaseModel) | Aggregate metotları üzerinden set edilir. Pasif = grid seçiminde varsayılan gizli. |

Sabit: `MaxInstallment = 15`.

### Davranışlar

- `static ResultDomain<Bank> Create(string code, IEnumerable<int> installments)`
  - Doğrula: code 4 hane (`COMMON_MESSAGE_INVALID_FORMAT`); code katalogda mı
    (`BANK_NOT_IN_CATALOG`); installments normalize (boş → `VALUE_IS_REQUIRED`, aralık dışı →
    `INVALID_RANGE`). `Name` katalogdan (`BankCatalog`) alınır — parametre değildir.
- `ResultDomain Update(bool isActive, IEnumerable<int> installments)`
  - `Code` ve `Name` değişmez. installments aynı doğrulama. `UpdatedTime = UtcNow`.
- `void SoftDelete()` — `IsDeleted = true`, `DeletedTime = UtcNow`.
- private `NormalizeInstallments` — tekilleştir, sırala, aralık doğrula.

## Yeni kanonik referans: BankCatalog

Uygulamaya gömülü statik liste (belge değil, kalıcı değil). CP.VPOS `BankService.AllBanks`'ten
kopyalanan `(Code, Name)` çiftleri (48 banka). Commission.Api içinde `static` sınıf.

- `IReadOnlyList<CatalogEntry> All` — tüm katalog (`CatalogEntry(string Code, string Name)`).
- `bool TryGetName(string code, out string name)` — Create doğrulaması + Name türetimi.
- CP.VPOS'a runtime bağımlılık yok (`AllBanks` `internal`; değerler elle kopyalandı).
- Değişiklik nadir; yeni banka gerekince katalog sabiti elle güncellenir.

### İlişkiler

- `Bank.Code` ↔ `BankCommission.BankCode` (string eşleşme; FK yok, aynı BC içinde referans).
- Silme guard: `BankCommission` (aynı `BankCode`, `!IsDeleted`) varsa `Bank` silinemez.

## Mevcut aggregate: BankCommission (değişmez)

`BankCode` (4 hane), `Criteria` (VO), `Rate` (decimal ≥ 0). Bu dilim yeni bir **toplu upsert**
davranışı ekler (Create + `UpdateRate` mevcut). Şema değişmez.

### Criteria (mevcut VO)

`CardBrand` × `CardType` × `TransactionRegion` × `InstallmentCount`. Değer eşitliği; benzersizlik
`(BankCode, Criteria)`.

- `CardBrand`: VISA, MASTERCARD, TROY, AMEX
- `CardType`: CREDIT, DEBIT, PREPAID
- `TransactionRegion`: DOMESTIC, INTERNATIONAL

## Grid türetimi (kalıcı değil, hesaplanır)

Bir banka için grid satır kümesi = kartesyen:
`CardBrand(4) × CardType(3) × TransactionRegion(2) × Bank.SupportedInstallments(n)`.

Her satır mevcut `BankCommission` ile `(BankCode, Criteria)` üzerinden eşleşir:
- eşleşme var → `Rate` dolu
- yok → **eksik** (UI işaretler)

Grid bir belge değildir; `GetBank` (taksitler) + `GetBankCommissions?bankCode` (mevcut oranlar)
birleşiminden UI tarafında üretilir.

## Yeni resource kodu

`CommissionResourceConstants.BANK_HAS_COMMISSIONS = "BANK_HAS_COMMISSIONS"` — bağlı komisyonu olan
banka silinmeye çalışıldığında.

`CommissionResourceConstants.BANK_NOT_IN_CATALOG = "BANK_NOT_IN_CATALOG"` — kanonik katalogda
bulunmayan bir kodla banka eklenmeye çalışıldığında.