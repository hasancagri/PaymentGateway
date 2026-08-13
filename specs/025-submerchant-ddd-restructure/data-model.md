# Phase 1 Data Model: SubMerchants Yapısal DDD Geçişi

Bu iş **yeni domain entity üretmez** — mevcut wire tiplerini sağlayıcı sınırına taşır ve domain
temsilini (023 `Merchant`) korur. "Data model" = yapısal eşleme + tip envanteri.

## Taşıma eşlemesi (Domains → Provider)

| Tip | Şu an (namespace) | Hedef (namespace) | Rol |
|-----|-------------------|-------------------|-----|
| `SubMerchant` | `Merchant.Api.Domains.SubMerchants` | `Merchant.Api.Provider.Onboarding` | iyzico wire yanıt DTO + canlı Create/Update/Retrieve HTTP çağrıları (`ProviderResourceV2` türevi) |
| `CreateSubMerchantRequest` | `...Domains.SubMerchants` | `...Provider.Onboarding` | iyzico PKI imzalı istek (`BaseRequestV2` türevi) |
| `UpdateSubMerchantRequest` | `...Domains.SubMerchants` | `...Provider.Onboarding` | iyzico PKI imzalı istek |
| `RetrieveSubMerchantRequest` | `...Domains.SubMerchants` | `...Provider.Onboarding` | iyzico PKI imzalı istek |
| `SubMerchantType` (enum) | `...Domains.SubMerchants` | `...Provider.Onboarding` | iyzico wire vocab (PERSONAL/PRIVATE_COMPANY/LIMITED_OR_JOINT_STOCK_COMPANY) |

Sonuç: `Domains/SubMerchants/` klasörü BOŞALIR → silinir. `Domains/` altında `BaseRequestV2`/
`ProviderResourceV2` türeyen tip KALMAZ (SC-001).

## Domain temsili — DEĞİŞMEZ (023 `Merchant`)

Sub-merchant bağının domain-tarafı temsili zaten var, dokunulmaz:

| Domain öğesi | Yer | Bu işteki durum |
|--------------|-----|-----------------|
| `Merchant.SubMerchantKey` (`string?`, private setter) | `Domains/Merchants/Merchant.cs` | KORUNUR — nullable, hep null (davranış spec'i doldurur) |
| `MerchantType` (enum: Personal/PrivateCompany/LimitedOrJointStockCompany) | `Domains/Merchants/MerchantType.cs` | KORUNUR — tip matrisi; iyzico `SubMerchantType` wire vocab'ının domain karşılığı |
| GetMerchant/UpdateMerchant `SubMerchantKey` yanıt alanı | `Merchants/Features/...` | KORUNUR — yüzey değişmez (FR-006) |

> **Neden yeni VO yok**: `SubMerchantKey`'i bir `SubMerchantRegistration` VO'ya sarmak (a) mevcut
> testleri (`Assert.Null(merchant.SubMerchantKey)`) + yanıt yüzeyini + Marten şeklini kırar (FR-006
> ihlali), (b) kimsenin tüketmediği spekülatif yapı (YAGNI). Richer VO, kaydı DOLDURAN davranış
> spec'inde (alanı gerçekten kullanınca) doğar. Bkz research.md R3.

## Sınır çevirisi (davranış spec'ine bırakılan — BU İŞTE YOK)

Bu geçiş yalnız YERLEŞİMİ düzeltir. Aşağıdakiler davranış spec'inin işi (referans için):

- `Merchant` (domain) → `CreateSubMerchantRequest` (wire) eşleme + PKI imza + `SubMerchant.Create`
  canlı çağrısı.
- iyzico yanıtındaki `SubMerchantKey` → `Merchant.SubMerchantKey` doldurma (davranış).
- `MerchantType` (domain) → `SubMerchantType` (wire vocab) çevirisi.

## Doğrulama kuralları (bu işin çıktısı için)

- `Domains/` altında sağlayıcı-türeyen tip = 0 (SC-001).
- Aggregate-klasör tek-kök korunur (SC-002).
- `SubMerchantType` 3 değer korunur, `MerchantType` ile hizalı (SC-004).
- Yeni endpoint/handler/iş-kuralı = 0 (SC-005).
- Build 0 hata + mevcut Merchant testleri yeşil (SC-003).
