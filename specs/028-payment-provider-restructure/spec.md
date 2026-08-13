# Feature Specification: Payment.Api iyzico Wire Material — Yapısal DDD Geçişi

**Feature Branch**: `028-payment-provider-restructure`

**Created**: 2026-08-13

**Status**: Draft

**Input**: User description: "Payment.Api/Domains/{Payments,Installments,StoredCards} altındaki 40
iyzico istemci/wire tipini kullanıcının DDD/anti-anemik konvansiyonlarına göre YAPISAL yeniden
düzenlemek. 025/026/027 ile birebir aynı desen. DAVRANIŞ yok, ayrı iş."

## Overview

Bu iş bir **yapısal/konvansiyon geçişidir** (025 SubMerchants + 026 TransactionReports + 027 Payouts
ile birebir desen), yeni iş yeteneği değil. `Payment.Api/Domains/` altında üç klasörde iyzico
SDK'sından gelen **40 anemik wire/istemci tipi** CP.VPOS-sınırı + İlke II'yi ihlal ederek `Domains/`
içinde davranışsız duruyor:

- **Payments** (28): `Payment`/`PaymentPreAuth`/`PaymentPostAuth`/`Cancel`/`Refund`/
  `RefundChargedFromMerchant`/`PaymentItem : ProviderResourceV2` (canlı `/payment/*` HTTP);
  `Create*Request`/`RetrievePaymentRequest`/`UpdatePaymentItemRequest : BaseRequestV2` (PKI); wire
  DTO'lar `Buyer`/`Address`/`BasketItem`/`PaymentCard`/`LoyaltyReward`/`ConvertedPayout`; enum'lar
  `BasketItemType`/`PaymentChannel`/`PaymentGroup`/`Currency`/`Locale`/`Status`/`RefundReason`.
- **Installments** (6): `InstallmentInfo`/`BinNumber : ProviderResourceV2` (canlı taksit/BIN sorgu);
  `RetrieveInstallmentInfoRequest`/`RetrieveBinNumberRequest : BaseRequestV2`; DTO `InstallmentDetail`/
  `InstallmentPrice`.
- **StoredCards** (6): `Card`/`CardList : ProviderResourceV2` (canlı kart vault); `CreateCardRequest`/
  `DeleteCardRequest`/`RetrieveCardListRequest : BaseRequestV2`; DTO `CardInformation`.

**Önemli fark**: Payment.Api'nin gerçek domain'i YOK (022 pivotu sildi) — `Domains/` %100 iyzico wire.
Üç klasör taşınınca `Payment.Api/Domains/` TAMAMEN boşalır; gerçek Payment domain'i (charge akışı)
sonraki davranış spec'inde doğar. Bu beklenen doğru son-durum.

Bu iş material'i sağlayıcı sınırına (`Payment.Api/Provider/{Payments,Installments,StoredCards}/`,
namespace `Payment.Api.Provider.X`) taşır, `Domains/` klasörlerini dağıtır. **Davranış — iyzico'ya
gerçek ödeme/taksit/kart çağrıları — BU İŞTE YOK**; ayrı spec.

**Aktör**: kod bakımcısı/geliştirici. **Değer**: iyzico material'i kurallara uyar → charge akışı
temiz zeminde kurulur; anayasa İlke II/CP.VPOS-sınırı ihlali giderilir. Bu, iyzico SDK yapısal
uyarlama roadmap'inin (025 Merchant + 026/027 Commission) Payment.Api ayağıdır.

## Clarifications

### Session 2026-08-13 (ön mutabakat — zemin)

- Q: Kapsam? → A: YALNIZ yapı — sınır yerleşimi, isim. DAVRANIŞ (canlı ödeme/taksit/kart çağrıları)
  hariç, ayrı iş.
- Q: Payment.Api gerçek domain? → A: YOK (022 pivot); `Domains/` %100 wire → taşıma sonrası boşalır.
- Q: Kaç iş? → A: Tek spec, 3 story (US1 Payments / US2 Installments / US3 StoredCards) — aynı BC,
  aynı mekanik taşıma.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Payments wire tipleri sınıra taşınır (Priority: P1)

Geliştirici, ödeme (auth/preauth/postauth/cancel/refund) wire/istemci tiplerinin `Domains/Payments/`
içinde davranışsız durmasını istemez; sağlayıcı sınırına ait. 28 tip provider tarafına taşınır.

**Why this priority**: En büyük ve merkezî grup; charge akışının çekirdeği. Taşıma İlke II/CP.VPOS-
sınırı ihlalini asıl burada giderir — MVP.

**Independent Test**: `Domains/Payments/` yok; 28 tip `Provider/Payments/`'te; çözüm derlenir.

**Acceptance Scenarios**:

1. **Given** Payments wire tipleri `Domains/Payments/` altında, **When** geçiş uygulanır, **Then** 28
   tip `Provider/Payments/`'e taşınır ve `Domains/Payments/` silinir.
2. **Given** taşıma tamam, **When** çözüm derlenir, **Then** derleme 0 hata.

---

### User Story 2 - Installments wire tipleri sınıra taşınır (Priority: P2)

Geliştirici, taksit/BIN sorgu wire tiplerinin sınıra taşınmasını ister. 6 tip provider tarafına.

**Why this priority**: Bağımsız grup (taksit/BIN sorgu). US1'den ayrı test edilebilir.

**Independent Test**: `Domains/Installments/` yok; 6 tip `Provider/Installments/`'te; derlenir.

**Acceptance Scenarios**:

1. **Given** Installments wire tipleri `Domains/Installments/` altında, **When** geçiş uygulanır,
   **Then** 6 tip `Provider/Installments/`'e taşınır ve klasör silinir.

---

### User Story 3 - StoredCards wire tipleri sınıra taşınır (Priority: P3)

Geliştirici, kart vault wire tiplerinin sınıra taşınmasını ister. 6 tip provider tarafına.

**Why this priority**: Bağımsız grup (kart saklama). En küçük, sona bırakılır.

**Independent Test**: `Domains/StoredCards/` yok; 6 tip `Provider/StoredCards/`'te; derlenir. Taşıma
sonrası `Payment.Api/Domains/` TAMAMEN boşalır.

**Acceptance Scenarios**:

1. **Given** StoredCards wire tipleri `Domains/StoredCards/` altında, **When** geçiş uygulanır,
   **Then** 6 tip `Provider/StoredCards/`'e taşınır, klasör silinir ve `Payment.Api/Domains/`
   sağlayıcı-türeyenden tamamen arınır.

---

### Edge Cases

- Resource'lar hem wire-şekli hem canlı HTTP static metotlarını taşıyor (SDK deseni) — sağlayıcı
  sınırında birleşik desen korunur (025/026/027; DTO/çağrı zorla ayrılmaz — YAGNI).
- Klasörler-arası referans (ör. `PaymentItem` `ConvertedPayout` kullanır) — üçü de sağlayıcıya, kendi
  namespace'leriyle taşınır; GlobalUsings üç yeni namespace'i de tanımlar → intra/cross referanslar çözülür.
- `ConvertedPayout` Payment.Api'de de var (Commission'daki ayrı, farklı BC) — bağımsız.
- Klasör adları origin'le aynı (çoğul) → `Payment` tip adıyla namespace-segment çakışması yok.
- Material uyuyor; dış referans yalnız 3 `GlobalUsings.cs` satırı; Program.cs/Agent/Admin kullanmaz →
  taşıma güvenli. Payment.Api test projesi yok.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sistem, Payments wire/istemci tiplerini (28) `Payment.Api/Provider/Payments/` (namespace
  `Payment.Api.Provider.Payments`) MUST taşısın; `Domains/Payments/` dağıtılır.
- **FR-002**: Sistem, Installments wire tiplerini (6) `Payment.Api/Provider/Installments/` (namespace
  `Payment.Api.Provider.Installments`) MUST taşısın; `Domains/Installments/` dağıtılır.
- **FR-003**: Sistem, StoredCards wire tiplerini (6) `Payment.Api/Provider/StoredCards/` (namespace
  `Payment.Api.Provider.StoredCards`) MUST taşısın; `Domains/StoredCards/` dağıtılır.
- **FR-004**: Sistem, canlı iyzico çağrılarını (`/payment/*`, taksit/BIN, kart vault) taşıyan istemci
  sorumluluğunu sağlayıcı sınırına MUST yerleştirsin; SDK resource+çağrı birleşik deseni korunur.
- **FR-005**: Geçiş DAVRANIŞ İÇERMEMELİ: canlı iyzico ödeme/taksit/kart akışı, yeni iş-kuralı MUST NOT
  eklensin. Yalnız yapı/taşıma.
- **FR-006**: Geçiş, mevcut derlemeyi MUST kırmasın: çözüm derlenir; tüm mevcut testler (Merchant 30 +
  Commission 20) yeşil kalır.
- **FR-007**: Geçiş sonrası `Payment.Api/Domains/` altında `BaseRequestV2`/`ProviderResourceV2` türeyen
  tip = 0 (Domains TAMAMEN boşalır); yapısal doğrulama kuralları geçer.

### Key Entities *(include if feature involves data)*

- **Payment/Installment/StoredCard wire tipleri** (sağlayıcı sınırı, 40 tip): iyzico ödeme/taksit/kart
  API'sinin istek/yanıt şekilleri + PKI imza + canlı HTTP + nested DTO + enum. Domain DEĞİL; sağlayıcı
  tarafına taşınır, gelecekte charge akışı domain'ine handler sınırında çevrilir (davranış işi).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: `Payment.Api/Domains/` altında `BaseRequestV2`/`ProviderResourceV2` türeyen tip = 0
  (grep — Domains tamamen boşalır).
- **SC-002**: `Provider/Payments/` 28, `Provider/Installments/` 6, `Provider/StoredCards/` 6 dosya;
  ilgili `Domains/` klasörleri yok.
- **SC-003**: `dotnet build` 0 hata; tüm çözüm testleri (Merchant 30 + Commission 20) yeşil.
- **SC-004**: Yeni uç/handler/iş-kuralı EKLENMEZ (davranış yok); yeni endpoint = 0.

## Assumptions

- Sağlayıcı tipleri mevcut `Provider/` yapısına üç alt-klasörle taşınır; ayrı proje açılmaz. Klasör
  adları origin'le aynı (çoğul) — tip adıyla çakışmaz.
- Material davranışsal olarak uyuyor; dış referans yalnız 3 `GlobalUsings.cs` satırı. Program.cs/Agent/
  Admin kullanmaz → güvenli.
- iyzico'ya gerçek ödeme/taksit/kart çağrıları + charge akışı domain'i AYRI spec(ler)de.
- Payment.Api test projesi yok; doğrulama derleme + diğer BC testlerinin yeşilliği + grep.
- Bu, iyzico SDK yapısal uyarlama roadmap'inin Payment.Api ayağı (025 Merchant + 026/027 Commission'dan
  sonra).

## Dependencies

- Payment.Api `Provider/` çekirdeği (`BaseRequestV2`, `ProviderResourceV2`, `RestHttpClientV2`,
  `RequestStringConvertible`, PKI/hash) — wire tiplerinin taşınacağı sınır.
- Ortak zemin: [[decisions_iyzico_sdk_ddd_adaptation]]. Önceki geçişler: 025/026/027 (birebir desen).
