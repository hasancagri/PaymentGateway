# Feature Specification: Payouts Yapısal DDD Geçişi

**Feature Branch**: `027-payouts-ddd-restructure`

**Created**: 2026-08-13

**Status**: Draft

**Input**: User description: "027 Payouts yapısal DDD geçişi — Commission.Api/Domains/Payouts altındaki
8 iyzico istemci/wire tipini kullanıcının DDD/anti-anemik konvansiyonlarına göre YAPISAL yeniden
düzenlemek. 025/026 ile birebir aynı desen. DAVRANIŞ yok, ayrı iş."

## Overview

Bu iş bir **yapısal/konvansiyon geçişidir** (025 SubMerchants + 026 TransactionReports ile birebir
aynı desen), yeni iş yeteneği değil. `Commission.Api/Domains/Payouts/` altında iyzico SDK'sından gelen
8 anemik wire/istemci tipi CP.VPOS-sınırı + İlke II'yi ihlal ederek `Domains/` içinde davranışsız
duruyor:

- **Resource + canlı çağrı** (`: ProviderResourceV2`): `PayoutCompletedTransactionList`
  (`/reporting/settlement/payoutcompleted`), `BouncedBankTransferList` (`/reporting/settlement/bounced`),
  `CrossBookingToSubMerchant` (`/crossbooking/send`), `CrossBookingFromSubMerchant`
  (`/crossbooking/receive`).
- **İstek wire (PKI imzalı, `: BaseRequestV2`)**: `RetrieveTransactionsRequest`,
  `CreateCrossBookingRequest`.
- **Nested wire DTO**: `PayoutCompletedTransaction`, `BankTransfer`.

Bu iş material'i projenin DDD/anti-anemik kurallarına göre yeniden yerleştirir: sağlayıcı/wire
tipleri sınıra (`Commission.Api/Provider/Payout/`, namespace `Commission.Api.Provider.Payout`)
taşınır, `Domains/Payouts/` klasörü dağıtılır. **Davranış — iyzico'dan gerçek payout/settlement
çekimi, cross-booking icrası — BU İŞTE YOK**; yapı hazır olunca davranış dolgusu ayrı spec'e kalır.

**Aktör**: kod bakımcısı/geliştirici. **Değer**: iyzico material'i kurallara uyar → sonraki davranış
işi (payout takibi + settlement mutabakatı) temiz zeminde başlar; anayasa İlke II/CP.VPOS-sınırı
ihlali giderilir. Bu, iyzico SDK yapısal uyarlama roadmap'inin SON geçişidir (025 SubMerchants +
026 TransactionReports tamam).

## Clarifications

### Session 2026-08-13 (ön mutabakat — zemin)

- Q: Bu iş neyi kapsar? → A: YALNIZ yapı — sınır yerleşimi, isimlendirme. DAVRANIŞ (canlı payout/
  settlement çekimi, cross-booking icrası) hariç, ayrı iş.
- Q: 024 CommissionPolicy? → A: DOKUNULMAZ; Payouts tiplerini referanslamaz.
- Q: 026 (TransactionReports)? → A: Zaten `Provider/Reporting/`'e taşındı; bu iş yalnız `Payouts/`.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Sağlayıcı/wire tipleri sınıra taşınır (Priority: P1)

Geliştirici, iyzico'ya özel payout/settlement/crossbooking wire/istemci tiplerinin `Domains/` içinde
davranışsız durmasını istemez; bunlar sağlayıcı sınırına ait (CP.VPOS-sınırı). 8 tip provider tarafına
taşınır; domain bu tipleri doğrudan görmez.

**Why this priority**: Anayasa İlke II + CP.VPOS-sınırı ihlalinin asıl kaynağı bu tiplerin yeridir.
Taşıma tek başına ihlali giderir — MVP.

**Independent Test**: `Commission.Api/Domains/` altında `BaseRequestV2`/`ProviderResourceV2` türeyen
hiçbir tip kalmadığını doğrula (026'dan sonra TransactionReports gitti; bu iş Payouts'u da götürür →
`Domains/` sağlayıcı-türeyenden TAMAMEN arınır); çözüm derlenir, Commission testleri (20/20) yeşil.

**Acceptance Scenarios**:

1. **Given** wire/istemci tipleri `Domains/Payouts/` altında, **When** yapısal geçiş uygulanır,
   **Then** 8 tip provider sınırına (`Provider/Payout/`) taşınır ve `Domains/` altında sağlayıcı-
   türeyen tip HİÇ kalmaz.
2. **Given** taşıma tamam, **When** çözüm derlenir ve testler koşulur, **Then** derleme 0 hata,
   Commission testleri yeşil (024 dokunulmadı).

---

### User Story 2 - Klasör dağıtımı + konvansiyon doğrulaması (Priority: P2)

Geliştirici, tüm tipler taşındıktan sonra `Domains/Payouts/` klasörünün dağıtılmasını (aggregate-
klasör kuralı) ve yapısal kuralların (grep) geçmesini ister.

**Why this priority**: Wire tipleri çıkınca aykırı klasör kalır; dağıtmak kuralı geri getirir. P1'e
bağımlı.

**Independent Test**: `Domains/Payouts/` klasörü yok; `Domains/` altında sağlayıcı-türeyen tip 0;
`Provider/Payout/` 8 dosya; çözüm derlenir.

**Acceptance Scenarios**:

1. **Given** 8 tip Provider'a taşındı, **When** klasör dağıtılır, **Then** `Domains/Payouts/` silinir
   ve `Commission.Api/Domains/` yalnız gerçek domain (`CommissionPolicies`) içerir.

---

### Edge Cases

- Resource'lar hem wire-şekli hem canlı HTTP static metotlarını taşıyor (SDK deseni) — sağlayıcı
  sınırında birleşik desen korunur (025/026 deseni; DTO/çağrı zorla ayrılmaz — YAGNI).
- Nested DTO'lar (`PayoutCompletedTransaction`, `BankTransfer`) base tip taşımaz — resource'larıyla
  birlikte taşınır.
- CrossBooking tipleri "SubMerchant" adını içerir (`CrossBookingToSubMerchant`); 025'te taşınan
  `SubMerchant` (Merchant.Api) FARKLI BC — burada yalnız isim benzerliği, çapraz-referans yok.
- Material şu an uyuyor (referanssız); dış "Payout" geçişleri yalnız `GlobalUsings.cs` satırı +
  024'ün `NetPayout`/DTO alan adlarındaki substring (tip kullanımı DEĞİL) → taşıma güvenli.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sistem, iyzico payout/settlement/crossbooking wire/istemci tiplerini (8 tip: 4 resource,
  2 PKI istek, 2 nested DTO) proje sağlayıcı sınırına (`Commission.Api/Provider/Payout/`, namespace
  `Commission.Api.Provider.Payout`) MUST taşısın; `Domains/` altında davranışsız kalmamalı.
- **FR-002**: Sistem, canlı iyzico çağrılarını (`/reporting/settlement/*`, `/crossbooking/*`) taşıyan
  istemci sorumluluğunu sağlayıcı sınırına MUST yerleştirsin; `Domains/` domain tipine karışmamalı.
  SDK resource+çağrı birleşik deseni korunur.
- **FR-003**: Sistem, `Domains/Payouts/` klasörünü (tüm tipler taşındıktan sonra) MUST dağıtsın;
  aggregate-klasör kuralı (bir `Domains/<X>/` tek `: AggregateRoot`) korunur.
- **FR-004**: Geçiş DAVRANIŞ İÇERMEMELİ: iyzico'dan gerçek payout/settlement çekme, cross-booking
  icrası, yeni iş-kuralı MUST NOT eklensin. Yalnız yapı/taşıma.
- **FR-005**: Geçiş, 024 `CommissionPolicy` domain'ini ve dış yüzeyi MUST değiştirmesin.
- **FR-006**: Geçiş, mevcut derlemeyi + testleri MUST kırmasın: çözüm derlenir, `Commission.Api.Tests`
  (20/20) yeşil kalır.
- **FR-007**: Geçiş sonrası kod, yapısal doğrulama kurallarını (`Domains/` altında sağlayıcı-türeyen
  tip = 0 — bu iş 026 sonrası kalan son ihlali de kapatır; aggregate-klasör tek-kök) MUST geçsin.

### Key Entities *(include if feature involves data)*

- **Payout wire/istemci tipleri** (sağlayıcı sınırı, 8 tip): iyzico payout/settlement/crossbooking
  API'sinin istek/yanıt şekilleri + PKI imza + canlı HTTP + nested DTO'lar. Domain DEĞİL; sağlayıcı
  tarafına taşınır, gelecekte handler sınırında domain temsiline çevrilir (davranış işi).
- **024 CommissionPolicy** (dokunulmaz): efektif komisyon domain'i. Bu iş onunla ilişki kurmaz.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: `Commission.Api/Domains/` altında `BaseRequestV2`/`ProviderResourceV2` türeyen tip
  sayısı = 0 (grep — 026+027 sonrası TAM temiz).
- **SC-002**: Aggregate-klasör kuralı korunur: `grep -rlE "class .*: AggregateRoot"
  src/services/Commission.Api/Domains` → yalnız `CommissionPolicies/CommissionPolicy.cs`.
- **SC-003**: `dotnet build` 0 hata; `dotnet test tests/Commission.Api.Tests` yeşil (20/20).
- **SC-004**: `Provider/Payout/` altında 8 dosya; `Domains/Payouts/` klasörü yok.
- **SC-005**: Yeni uç/handler EKLENMEZ; `Domains/CommissionPolicies/` diff = 0 (yeni iş-kuralı yok).

## Assumptions

- Sağlayıcı tipleri mevcut `Provider/` yapısına (`Provider/Payout/` alt-klasörü) taşınır; ayrı proje
  açılmaz. Klasör adı `Payout` (iyzico settlement/crossbooking payout grubu; tip adıyla çakışmaz).
- Material davranışsal olarak uyuyor; referanslar yalnız `GlobalUsings.cs` satırı (024'teki
  `NetPayout`/`MerchantPayoutAmount` yalnız substring, tip kullanımı değil). Çapraz-ref yok → güvenli.
- iyzico'dan gerçek payout çekme + settlement mutabakatı + cross-booking icrası davranışı AYRI spec'te.
- Test: yeni test gerekmez; doğrulama derleme + mevcut testler + grep.
- Bu, iyzico SDK yapısal uyarlama roadmap'inin (025→026→027) SON geçişi.

## Dependencies

- 024 Commission BC (`CommissionPolicies` — dokunulmaz).
- Commission.Api `Provider/` çekirdeği — wire tiplerinin taşınacağı sınır.
- Ortak zemin: [[decisions_iyzico_sdk_ddd_adaptation]]. Önceki geçişler: 025 SubMerchants, 026
  TransactionReports (birebir desen).
