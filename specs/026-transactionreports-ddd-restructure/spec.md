# Feature Specification: TransactionReports Yapısal DDD Geçişi

**Feature Branch**: `026-transactionreports-ddd-restructure`

**Created**: 2026-08-13

**Status**: Draft

**Input**: User description: "026 TransactionReports yapısal DDD geçişi — Commission.Api/Domains/
TransactionReports altındaki 13 iyzico istemci/wire tipini kullanıcının DDD/anti-anemik
konvansiyonlarına göre YAPISAL yeniden düzenlemek. 025 SubMerchants geçişinin birebir aynı deseni.
DAVRANIŞ (gerçek rapor çekimi) YOK, ayrı iş."

## Overview

Bu iş bir **yapısal/konvansiyon geçişidir** (025 SubMerchants ile birebir aynı desen), yeni iş
yeteneği değil. `Commission.Api/Domains/TransactionReports/` altında iyzico SDK'sından gelen 13
anemik wire/istemci tipi proje konvansiyonlarını ihlal ederek `Domains/` içinde davranışsız duruyor:

- **Resource + canlı çağrı**: `TransactionReportResource`/`TransactionDetailResource :
  ProviderResourceV2`; `TransactionReport`/`TransactionDetail` (canlı `/v2/reporting/payment/
  transactions` HTTP Retrieve çağrıları).
- **İstek wire (PKI imzalı)**: `RetrieveTransactionReportRequest`/
  `RetrieveScrollTransactionReportRequest`/`RetrieveTransactionDetailRequest : BaseRequestV2`.
- **Nested wire DTO**: `TransactionReportItem`, `TransactionDetailItem`, `TransactionDetailCancelItem`,
  `PaymentTxDetailItem`, `RefundDetailItem`, `ConvertedPayout`.

Bu iş material'i projenin DDD/anti-anemik kurallarına göre yeniden yerleştirir: sağlayıcı/wire
tipleri sınıra (`Commission.Api/Provider/Reporting/`, namespace `Commission.Api.Provider.Reporting`)
taşınır, `Domains/TransactionReports/` klasörü dağıtılır. **Davranış — iyzico'dan gerçek rapor
çekimi, 024 `CommissionPolicy.CalculateEffectiveCommission`'a gerçek maliyet besleme — BU İŞTE YOK**;
yapı hazır olunca davranış dolgusu ayrı bir spec'e kalır.

**Aktör**: kod bakımcısı/geliştirici (iç yapısal düzenleme; son-kullanıcı davranışı değişmez).
**Değer**: iyzico material'i kurallara uyar → sonraki davranış işi (rapor çekimi + gerçek maliyet
beslemesi) temiz zeminde başlar; anayasa İlke II/CP.VPOS-sınırı ihlali giderilir.

## Clarifications

### Session 2026-08-13 (ön mutabakat — zemin)

- Q: Bu iş neyi kapsar? → A: YALNIZ yapı — sınır yerleşimi, isimlendirme. DAVRANIŞ (canlı rapor
  çekimi + 024'e gerçek maliyet beslemesi) hariç, ayrı iş.
- Q: 024 CommissionPolicy? → A: DOKUNULMAZ; maliyeti string girdi almaya devam eder,
  `TransactionReportItem` tiplerini referanslamaz (yalnız bir doc-yorumda adı geçer).
- Q: Payouts? → A: Ayrı geçiş (sonraki iş); bu iş yalnız TransactionReports.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Sağlayıcı/wire tipleri sınıra taşınır (Priority: P1)

Geliştirici, iyzico'ya özel rapor wire/istemci tiplerinin (resource + canlı HTTP çağrısı + PKI imzalı
istek + nested DTO) `Domains/` içinde davranışsız durmasını istemez; bunlar sağlayıcı sınırına ait
(CP.VPOS-sınırı). 13 tip provider tarafına taşınır; domain bu tipleri doğrudan görmez.

**Why this priority**: Anayasa İlke II (anemik yasak) + CP.VPOS-sınırı ihlalinin asıl kaynağı bu
tiplerin yeridir. Taşıma tek başına ihlali giderir ve domain sınırını netleştirir — MVP.

**Independent Test**: `Commission.Api/Domains/` altında `BaseRequestV2`/`ProviderResourceV2` türeyen
hiçbir tip kalmadığını doğrula; çözüm derlenir, mevcut Commission testleri (20/20) yeşil kalır.

**Acceptance Scenarios**:

1. **Given** wire/istemci tipleri `Domains/TransactionReports/` altında, **When** yapısal geçiş
   uygulanır, **Then** 13 tip provider sınırına (`Provider/Reporting/`) taşınır ve `Domains/` altında
   sağlayıcı-türeyen tip kalmaz.
2. **Given** taşıma tamam, **When** çözüm derlenir ve testler koşulur, **Then** derleme 0 hata,
   Commission testleri yeşil (davranış/dış-yüzey değişmedi, 024 dokunulmadı).

---

### User Story 2 - Klasör dağıtımı + konvansiyon doğrulaması (Priority: P2)

Geliştirici, tüm tipler taşındıktan sonra `Domains/TransactionReports/` klasörünün dağıtılmasını
(aggregate-klasör kuralı: `Domains/<X>/` tek `: AggregateRoot`; bu klasörde aggregate YOK) ve
yapısal kuralların (grep) geçmesini ister.

**Why this priority**: Wire tipleri çıkınca boş/aykırı klasör kalır; dağıtmak aggregate-klasör
kuralını geri getirir. P1'e bağımlı (son tip çıkınca).

**Independent Test**: `Domains/TransactionReports/` klasörü yok; `Domains/` altında sağlayıcı-türeyen
tip 0; `Provider/Reporting/` 13 dosya; çözüm derlenir.

**Acceptance Scenarios**:

1. **Given** 13 tip Provider'a taşındı, **When** klasör dağıtılır, **Then** `Domains/TransactionReports/`
   silinir ve aggregate-klasör kuralı korunur (grep).

---

### Edge Cases

- `TransactionReport`/`TransactionDetail` resource'ları hem wire-şekli hem canlı HTTP static
  metotlarını taşıyor (SDK deseni) — sağlayıcı sınırında bu birleşik desen korunur (025 R5; DTO/çağrı
  zorla ayrılmaz — YAGNI + diğer Provider malzemesiyle tutarlı).
- Nested DTO'lar (`TransactionReportItem` vb.) base tip taşımaz (düz wire kayıt) — hepsi sağlayıcı
  tarafına, resource'larıyla birlikte taşınır.
- `TransactionReport.cs` içinde `private static GetQueryParams` yardımcısı var — sağlayıcı-içi
  helper, taşınır (domain kuralı değil, provider kodu).
- 024 `CommissionPolicy` bir doc-yorumda `TransactionReportItem` adını anıyor (tip kullanımı DEĞİL) —
  taşıma derlemeyi kırmaz; yorum güncellenebilir ama domain diff'i tutmak için gerekmez.
- Material şu an uyuyor (referanssız); taşıma/silme mevcut derlemeyi + 024 testlerini kırmamalı.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sistem, iyzico rapor wire/istemci tiplerini (13 tip: 2 resource + türevleri, 3 PKI
  istek, 6 nested DTO) proje sağlayıcı sınırına (`Commission.Api/Provider/Reporting/`, namespace
  `Commission.Api.Provider.Reporting`) MUST taşısın; `Domains/` altında davranışsız kalmamalı.
- **FR-002**: Sistem, canlı iyzico çağrılarını (`/v2/reporting/payment/transactions` Retrieve) taşıyan
  istemci sorumluluğunu sağlayıcı sınırına MUST yerleştirsin; bu sorumluluk `Domains/` domain tipine
  karışmamalı. SDK'nın resource+çağrı birleşik deseni korunur.
- **FR-003**: Sistem, `Domains/TransactionReports/` klasörünü (tüm tipler taşındıktan sonra) MUST
  dağıtsın; aggregate-klasör kuralı (bir `Domains/<X>/` tek `: AggregateRoot`) korunur.
- **FR-004**: Geçiş DAVRANIŞ İÇERMEMELİ (kapsam): iyzico'dan gerçek rapor çekme akışı, 024'e gerçek
  maliyet besleme, yeni iş-kuralı MUST NOT eklensin. Yalnız yapı/taşıma.
- **FR-005**: Geçiş, 024 `CommissionPolicy` domain'ini ve dış yüzeyi MUST değiştirmesin: aggregate,
  slice'lar, endpoint'ler dokunulmaz; `CalculateEffectiveCommission` maliyeti string girdi almaya
  devam eder.
- **FR-006**: Geçiş, mevcut derlemeyi + testleri MUST kırmasın: çözüm derlenir, mevcut
  `Commission.Api.Tests` (20/20) yeşil kalır.
- **FR-007**: Geçiş sonrası kod, projenin yapısal doğrulama kurallarını (aggregate-klasör tek-kök
  grep'i; `Domains/` altında sağlayıcı-türeyen tip olmaması) MUST geçsin.

### Key Entities *(include if feature involves data)*

- **TransactionReport wire/istemci tipleri** (sağlayıcı sınırı, 13 tip): iyzico rapor API'sinin
  istek/yanıt şekilleri + PKI imza + canlı HTTP çağrısı + nested DTO'lar. Domain DEĞİL; sağlayıcı
  tarafına taşınır, gelecekte handler sınırında domain temsiline çevrilir (davranış işi).
- **024 CommissionPolicy** (dokunulmaz): efektif komisyon domain'i; maliyeti string girdi alır. Bu
  iş onunla ilişki kurmaz; yalnız gelecekteki davranış spec'i rapor→gerçek-maliyet bağını kurar.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: `Commission.Api/Domains/` altında `BaseRequestV2` veya `ProviderResourceV2` türeyen tip
  sayısı = 0 (grep ile doğrulanır); tümü sağlayıcı sınırında.
- **SC-002**: Aggregate-klasör kuralı korunur: `grep -rlE "class .*: AggregateRoot"
  src/services/Commission.Api/Domains` → her klasör en fazla bir dosya; `TransactionReports` listede
  yok.
- **SC-003**: `dotnet build` 0 hata; `dotnet test tests/Commission.Api.Tests` yeşil (20/20 korunur —
  024 davranışı değişmedi).
- **SC-004**: `Provider/Reporting/` altında 13 dosya bulunur; `Domains/TransactionReports/` klasörü
  yok.
- **SC-005**: Bu işte iyzico'ya CANLI çağrı yapan yeni bir uç/handler EKLENMEZ ve 024 domain'i
  değişmez: yeni endpoint = 0, `Domains/CommissionPolicies/` diff = 0 (yeni iş-kuralı yok).

## Assumptions

- Sağlayıcı tipleri `Commission.Api` içindeki mevcut `Provider/` yapısına (`Provider/Reporting/` alt-
  klasörü) taşınır; ayrı proje/paket açılmaz. Klasör adı `Reporting` (iyzico `/v2/reporting/...`
  gruplaması; klasör adı bir tip adıyla çakışmaz).
- Material şu an davranışsal olarak uyuyor; referanslar yalnız `GlobalUsings.cs` satırı + 024
  `CommissionPolicy.cs`'te bir doc-yorum. Payouts çapraz-referans yok → taşıma güvenli.
- iyzico'dan gerçek rapor çekme + 024'e gerçek maliyet besleme + `TransactionReportItem`→domain
  çeviri davranışı AYRI bir spec'te (bu işin doğal devamı) ele alınır.
- Test: yeni test gerekmez; doğrulama derleme + mevcut testlerin yeşilliği + grep.
- Payouts geçişi (`Domains/Payouts/`) bu işin DIŞINDA — ayrı, sonraki spec.

## Dependencies

- 024 Commission BC (`CommissionPolicies` aggregate — dokunulmaz referans).
- Commission.Api `Provider/` çekirdeği (`BaseRequestV2`, `ProviderResourceV2`, `RestHttpClientV2`,
  `ProviderOptions`, PKI/hash yardımcıları) — wire tiplerinin taşınacağı sınır.
- Ortak zemin kararı: iyzico SDK material'i yapısal DDD uyarlaması (davranış sonra) —
  [[decisions_iyzico_sdk_ddd_adaptation]]. Önceki geçiş: 025 SubMerchants (birebir desen).
