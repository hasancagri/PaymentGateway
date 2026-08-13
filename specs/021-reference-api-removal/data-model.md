# Data Model: Reference.Api Removal (021)

**Date**: 2026-08-13 | **Plan**: [plan.md](plan.md)

Yeni varlık YOK; model değişimi silme + sadeleşme.

## Silinen varlıklar

| Varlık | Yer | Not |
|--------|-----|-----|
| `Bank`, `Country`, `City`, `Mcc` (kaynak-of-truth) | Reference.Api `Domains/` | Proje kökten silinir; seed verisi dahil (FR-009) |
| `ReferenceCountry`, `ReferenceCity`, `ReferenceMcc`, `ReferenceBank` | Merchant.Api `ReadModels/` | + `ReferenceEventHandler`, `ReferenceKey`; Marten `Identity(Code)` kayıtları Program.cs'ten |
| `ReferenceBank` | Commission.Api `ReadModels/` | + `ReferenceEventHandler`; şema kaydı Program.cs'ten |
| `IntegrationEvents.ReferenceDataUpdated`, `ReferenceItem` | Shared | Yayıncı/tüketici kalmaz (FR-002) |
| `RabbitMqConstants.ReferenceDataUpdated` (`reference.data-updated` exchange) | Shared | Declare/bind blokları iki Program.cs'ten |

## Davranışı değişen varlıklar (şema aynı)

### SettlementAccount (Merchant BC)

- Alanlar DEĞİŞMEZ (`BankCode` saklanmaya devam eder; `BankName` zaten saklanmıyordu).
- Create/Update: `ReferenceBank` varlık kontrolü (NotFound dalı) KALKAR; `BankCode` serbest
  kod. KORUNAN kurallar: IBAN normalize + mod-97 (aggregate), merchant-varlık, IBAN
  benzersizliği, statü kuralları.
- Sorgu yanıtları: `BankName` alanı ÇIKAR (kaynağı katalogdu) — yalnız `BankCode` döner.

### Bank (Commission BC)

- Alanlar DEĞİŞMEZ (`Code`, `Name`, `SupportedInstallments`); `Bank.Create` imzası aynı.
- Ad kaynağı değişir: katalog türetmesi yerine `CreateBankCommand.Name` (kullanıcı girdisi).
- Katalog-varlık reddi KALKAR; kod benzersizliği (silinmemişlerde) AYNEN kalır.

### Merchant sorgu yanıtları (Merchant BC)

- `GetMerchant`/`GetMerchantByKey`/`GetMerchantForAgent`: Country/City/MCC **ad** alanları
  ÇIKAR; ham kod alanları (`CountryCode`, `CityCode`, `Mcc`) kalır. Aggregate değişmez.

### CommissionProposal teklif akışı (Commission BC)

- Banka adı kaynağı `ReferenceBank` → Commission'ın kendi `Bank` dokümanları (Code+Name).
  Teklif/draft şemaları değişmez.

## Korunan (dokunulmaz)

- SharedKernel `CardTaxonomy/{CardBrand,CardType}` — Payment + Commission tüketiyor (FR-007).
- Diğer integration event'ler (`merchant.lifecycle`, `merchant.commission`, `mail.delivery`,
  Payment event'leri) ve tüm endpoint policy'leri.

## Durum geçişleri

Yok — hiçbir aggregate durum makinesi değişmiyor (merchant aktivasyon zinciri, settlement
statüleri, teklif yaşam döngüsü aynen).