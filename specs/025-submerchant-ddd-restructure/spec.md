# Feature Specification: SubMerchants Yapısal DDD Geçişi

**Feature Branch**: `025-submerchant-ddd-restructure`

**Created**: 2026-08-13

**Status**: Draft

**Input**: User description: "025 SubMerchants yapısal DDD geçişi — Merchant.Api/Domains/SubMerchants
altındaki iyzico istemci/wire tiplerini kullanıcının DDD/anti-anemik konvansiyonlarına göre YAPISAL
olarak yeniden düzenlemek. KAPSAM = yalnız yapı/sınır/isimlendirme/ValueObjects/iskelet; DAVRANIŞ
(iyzico'ya gerçek kayıt akışı) ayrı iş."

## Overview

Bu iş bir **yapısal/konvansiyon geçişidir**, yeni iş yeteneği değil. `Merchant.Api/Domains/SubMerchants/`
altında iyzico SDK'sından gelen beş anemik wire/istemci tipi (`SubMerchant : ProviderResourceV2` —
canlı `/onboarding/submerchant` HTTP çağrılarını taşıyan; `CreateSubMerchantRequest`/
`UpdateSubMerchantRequest`/`RetrieveSubMerchantRequest : BaseRequestV2` — PKI imza formatlı;
`SubMerchantType` enum) proje konvansiyonlarını ihlal ederek `Domains/` içinde davranışsız duruyor.

Bu iş bu material'i projenin DDD/anti-anemik kurallarına göre yeniden yerleştirir: sağlayıcı/wire
tipleri sınıra (provider tarafına) taşınır, domain-kavramı olan yer domain temsiliyle (ValueObject/
iskelet) ifade edilir, aggregate-klasör ve isimlendirme kuralları uygulanır. **Davranış — iyzico'ya
gerçek sub-merchant kaydı, `SubMerchantKey` doldurma iş mantığı — BU İŞTE YOK**; yapı hazır olunca
davranış dolgusu ayrı bir spec'e kalır.

**Aktör**: kod bakımcısı/geliştirici (bu bir iç yapısal düzenleme; son-kullanıcıya dönük davranış
değişmez). **Değer**: iyzico material'i kurallara uyar → sonraki davranış işi (kayıt akışı) temiz
zeminde başlar; anayasa İlke II/CP.VPOS-sınırı ihlali giderilir.

## Clarifications

### Session 2026-08-13 (ön mutabakat — brainstorm)

- Q: Bu iş neyi kapsar? → A: YALNIZ yapı — sınır yerleşimi, isimlendirme, ValueObjects, iskelet.
  DAVRANIŞ (canlı iyzico kaydı) hariç, ayrı iş.
- Q: iyzico modeli? → A: Gateway = iyzico ana üye (facilitator); her merchant = tam 1 iyzico
  SubMerchant (DÜZ, tek seviye — iç içe yok; iyzico native böyle). `SubMerchantType`
  (PERSONAL/PRIVATE_COMPANY/LIMITED_OR_JOINT_STOCK_COMPANY) 023 `MerchantType` matrisiyle hizalı.
- Q: 024? → A: Dokunulmaz.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Sağlayıcı/wire tipleri sınıra taşınır (Priority: P1)

Geliştirici, iyzico'ya özel wire/istemci tiplerinin (PKI imzalı istek tipleri + canlı HTTP çağrısı
taşıyan yanıt tipi) `Domains/` içinde davranışsız durmasını istemez; bunlar projede sağlayıcı
sınırına ait (CP.VPOS-sınırı kuralı). Bu material provider tarafına taşınır; domain bu tipleri
doğrudan görmez.

**Why this priority**: Anayasa İlke II (anemik aggregate yasak) + CP.VPOS-sınırı ihlalinin asıl
kaynağı bu tiplerin yeridir. Taşıma tek başına ihlali giderir ve domain sınırını netleştirir — MVP.

**Independent Test**: `Merchant.Api/Domains/` altında `BaseRequestV2`/`ProviderResourceV2` türeyen
hiçbir tip kalmadığını doğrula; çözüm derlenir, mevcut Merchant testleri yeşil kalır.

**Acceptance Scenarios**:

1. **Given** wire/istemci tipleri `Domains/SubMerchants/` altında, **When** yapısal geçiş uygulanır,
   **Then** bu tipler provider sınırına taşınır ve `Domains/` altında sağlayıcı-türeyen tip kalmaz.
2. **Given** taşıma tamam, **When** çözüm derlenir ve testler koşulur, **Then** derleme 0 hata,
   Merchant testleri yeşil (davranış/dış-yüzey değişmedi).

---

### User Story 2 - Domain-tarafı sub-merchant temsili (iskelet, davranışsız) (Priority: P2)

Geliştirici, sub-merchant kavramının domain'de proje kurallarıyla ifade edilmesini ister:
identity'siz bir kavramsa `ValueObjects/` altında, aggregate kavramıysa aggregate-klasör kuralıyla;
isimlendirme + metot-notu konvansiyonlarına uygun **iskelet**. İş mantığı (kayıt akışı) doldurulmaz —
yalnız şekil, sonraki spec'e hazır.

**Why this priority**: Wire tipleri taşındıktan sonra domain'in "sub-merchant"ı temsil eden bir
yeri olmalı (yoksa sonraki davranış işi tutunacak yapı bulamaz). P1'e bağımlı; şekil önce, davranış
sonra.

**Independent Test**: Domain temsili (VO/iskelet) proje konvansiyonlarına uyar (aggregate-klasör
tek-kök, ValueObjects yeri, isim/nota kuralı); iskelet davranış içermez (canlı çağrı/iş kuralı yok);
çözüm derlenir.

**Acceptance Scenarios**:

1. **Given** sub-merchant domain kavramı, **When** domain temsili eklenir, **Then** identity'siz
   kısım `ValueObjects/` altında, konvansiyon kurallarıyla (tek-kök klasör, isimlendirme) ifade
   edilir.
2. **Given** domain iskeleti, **When** incelenir, **Then** iş mantığı/canlı iyzico çağrısı
   İÇERMEZ (davranış hariç); yalnız yapı/iskelet.

---

### Edge Cases

- `SubMerchant : ProviderResourceV2` tipi hem wire-şekli hem canlı HTTP static metotlarını (Create/
  Update/Retrieve) taşıyor — bu iki sorumluluk sağlayıcı sınırında ayrışmalı (wire modeli vs istemci
  çağrısı).
- Domain temsili ayrı aggregate mı yoksa `Merchant`'a ait ValueObject mı (1 merchant = 1 sub-merchant,
  flat) — plan aşamasında netleşir; spec yalnız "konvansiyona uygun temsil" der.
- `SubMerchantExternalId` = gateway'in merchant'ı iyzico'ya bağladığı dış kimlik — domain'de
  `MerchantId` ile ilişki kurulur (eşleme, davranış değil).
- Material şu an uyuyor (referanssız olabilir); taşıma/silme mevcut derlemeyi kırmamalı.
- 023 `Merchant` aggregate'i `SubMerchantKey` (nullable) taşıyor — bu alan bu işte davranışsal
  doldurulmaz; yalnız domain temsiliyle ilişkilendirilebilir.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sistem, iyzico'ya özel istek wire tiplerini (`CreateSubMerchantRequest`/
  `UpdateSubMerchantRequest`/`RetrieveSubMerchantRequest`, `BaseRequestV2` türevi PKI imzalı) proje
  sağlayıcı sınırına MUST taşısın; `Domains/` altında davranışsız wire tipi olarak kalmamalı.
- **FR-002**: Sistem, canlı iyzico çağrılarını (`/onboarding/submerchant` Create/Update/Retrieve)
  taşıyan istemci sorumluluğunu sağlayıcı sınırına MUST yerleştirsin; bu sorumluluk `Domains/` domain
  tipine karışmamalı (wire-modeli vs istemci-çağrısı ayrışır).
- **FR-003**: Sistem, sub-merchant domain kavramını proje konvansiyonlarıyla MUST ifade etsin:
  identity'siz kısım `ValueObjects/` altında; aggregate-klasör kuralı korunur (bir `Domains/<X>/`
  klasörü tek `: AggregateRoot`); isimlendirme + metot-notu konvansiyonları uygulanır.
- **FR-004**: Domain temsili DAVRANIŞ İÇERMEMELİ (bu iş kapsamı): canlı iyzico kaydı, `SubMerchantKey`
  doldurma iş mantığı, yeni iş-kuralı MUST NOT eklensin. Yalnız yapı/iskelet.
- **FR-005**: Sistem, `SubMerchantType` tip kümesini (PERSONAL/PRIVATE_COMPANY/
  LIMITED_OR_JOINT_STOCK_COMPANY) MUST korusun ve 023 `MerchantType` matrisiyle hizasını sürdürsün.
- **FR-006**: Geçiş, mevcut dış davranışı/yüzeyi MUST değiştirmesin: Merchant BC endpoint'leri,
  `Merchant` aggregate davranışı ve 024 Commission dokunulmaz kalır; çözüm derlenir ve mevcut
  testler yeşil kalır.
- **FR-007**: Geçiş sonrası kod, projenin yapısal doğrulama kurallarını (aggregate-klasör tek-kök
  grep'i; `Domains/` altında sağlayıcı-türeyen tip olmaması) MUST geçsin.

### Key Entities *(include if feature involves data)*

- **SubMerchant wire/istemci tipleri** (sağlayıcı sınırı): iyzico API istek/yanıt şekilleri + PKI
  imza + canlı HTTP çağrısı. Domain DEĞİL; sağlayıcı tarafına taşınır, handler sınırında domain
  temsiline çevrilir.
- **Sub-merchant domain temsili** (iskelet): gateway merchant'ının iyzico sub-merchant bağını ifade
  eden domain kavramı — identity'siz alanlar (tip, dış kimlik, key alanı) `ValueObjects/` altında;
  davranış sonraki spec'te. `Merchant` (023) ile `MerchantId`/`SubMerchantKey` üzerinden ilişkili.
- **SubMerchantType** (enum): iyzico tip kümesi; 023 `MerchantType` ile hizalı — tip-uyum matrisinin
  ortak anahtarı.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: `Merchant.Api/Domains/` altında `BaseRequestV2` veya `ProviderResourceV2` türeyen tip
  sayısı = 0 (grep ile doğrulanır); tümü sağlayıcı sınırında.
- **SC-002**: Aggregate-klasör kuralı %100 korunur: `grep -rlE "class .*: AggregateRoot"
  src/services/Merchant.Api/Domains` → her klasör en fazla bir dosya.
- **SC-003**: `dotnet build` 0 hata; `dotnet test tests/Merchant.Api.Tests` yeşil (mevcut test
  sayısı korunur — davranış değişmedi).
- **SC-004**: `SubMerchantType` tip kümesi (3 değer) korunur ve `MerchantType` matrisiyle birebir
  eşleşir; hiçbir tip düşmez.
- **SC-005**: Bu işte iyzico'ya CANLI çağrı yapan yeni bir uç/handler EKLENMEZ (davranış hariç):
  yeni endpoint sayısı = 0, yeni iş-kuralı = 0.

## Assumptions

- Domain temsilinin tam şekli (ayrı `SubMerchant` aggregate vs `Merchant`'a ait ValueObject) plan
  aşamasında netleşir; 1 merchant = 1 sub-merchant (flat) varsayımı ValueObject/owned-temsili öne
  çıkarır ama karar /speckit-plan'e bırakılır.
- Sağlayıcı tipleri `Merchant.Api` içindeki mevcut `Provider/` yapısına taşınır (BC-içi; ayrı
  proje/paket açılmaz).
- Material şu an davranışsal olarak uyuyor; taşıma/yeniden-şekillendirme mevcut çalışan akışları
  (023 Merchant CRUD/statü, 012 OAuth, 024 Commission) etkilemez.
- iyzico'ya gerçek kayıt akışı, `SubMerchantKey` doldurma ve `Merchant→SubMerchant` çeviri davranışı
  AYRI bir spec'te (bu işin doğal devamı) ele alınır.
- Test: saf domain birim testi konvansiyonu geçerli; yapısal geçiş için yeni davranış testi
  gerekmez (derleme + mevcut testlerin yeşilliği + grep doğrulama yeterli).

## Dependencies

- 023 Merchant BC (`Merchant` aggregate, `MerchantType` matrisi, `SubMerchantKey` alanı).
- Merchant.Api `Provider/` çekirdeği (`BaseRequestV2`, `ProviderResourceV2`, `RestHttpClientV2`,
  `ProviderOptions`, PKI/hash yardımcıları) — wire tiplerinin taşınacağı sınır.
- Ortak zemin kararı: iyzico SDK material'i yapısal DDD uyarlaması (davranış sonra) — [[decisions_iyzico_sdk_ddd_adaptation]].
