# Feature Specification: Payment Provider Domain Dağıtımı (Provider/ kaldırma)

**Feature Branch**: `035-payment-provider-domain-distribution`

**Created**: 2026-08-15

**Status**: Draft

**Input**: User description: "Payment.Api Provider/ klasörünü kaldır; iyzico wire tiplerini domain-uygunluğa göre dağıt — saf-wire → Iyzico.Provider SDK, domain-uygun 4 tip → Payment Domains'e VO. Yapısal uyarlama şimdi, davranış sonra; çalışma-anı bit-korunur. Kapsam yalnız Payment."

## User Scenarios & Testing *(mandatory)*

Aktör: **Payment BC geliştiricisi/bakımcısı**. Value = kodu `Domains/`'den okuyabilmek; iyzico
transport çorbası (`Provider/` klasörü) domain'e karışmasın. Bir ödeme feature'ına bakan
geliştirici tüm iş sürecini `Domains/<Aggregate>/` altında görür; wire tekniği SDK sınırının
arkasında kalır.

### User Story 1 - Provider/ klasörü kalkar, kod Domains'ten okunur (Priority: P1)

Bugün Payment.Api'de `Provider/` klasörü 46 iyzico wire dosyası taşıyor (istek/yanıt DTO,
çağrı-yürütücü, wire enum). Bir ödeme feature'ına bakan geliştirici hem `Domains/Payments`'e hem
`Provider/Payments`'e bakmak zorunda — teknik-katman çorbası. Bu klasör tamamen kalkar: saf-wire
tipleri paylaşılan `Iyzico.Provider` SDK'ya taşınır, domain-uygun tipler `Domains/`'e VO olur.

**Why this priority**: Asıl istek bu — `Provider/` klasörünün BC'den kalkması. Çözülmezse özellik yok.

**Independent Test**: `src/services/Payment.Api/Provider/` dizini yok; Payment.Api'deki her iyzico
iş süreci `Domains/<Aggregate>/` altından erişilir; çözüm derlenir.

**Acceptance Scenarios**:

1. **Given** Payment.Api'de 46 wire dosyalı `Provider/` klasörü, **When** dağıtım yapılır, **Then**
   `Provider/` klasörü silinir; saf-wire Iyzico.Provider SDK'da, domain-uygun tipler Domains/ VO'da.
2. **Given** bir geliştirici ChargePayment'a bakar, **When** `Domains/Payments`'i açar, **Then** buyer/
   basket domain VO'larını orada görür; wire dönüşümü handler sınırındadır.

### User Story 2 - Domain-uygun wire tipleri zengin VO olur (Priority: P1)

iyzico'nun anemik wire tipleri (`Buyer`, `Address`, `BasketItem` — public getter/setter + iyzico
serileştirme) domain kavramlarıdır. Bunlar Payment'ın domain'ine **yapısal olarak uyarlanır**:
`Domains/Payments/ValueObjects/`'a immutable VO (private ctor + statik `Create` + ince doğrulama);
`CardInformation` → `Domains/StoredCards/ValueObjects/`. iyzico serileştirme domain VO'suna sızmaz —
SDK'nın wire formunda kalır.

**Why this priority**: Domain-zenginleştirme yorumunun özü; anemik record'ları VO'ya yükseltir.

**Independent Test**: `Domains/Payments/ValueObjects/{Buyer,Address,BasketItem}` + `Domains/StoredCards/
ValueObjects/CardInformation` VO olarak var (private ctor + `Create`); anemik `BuyerInput`/
`BasketItemInput` record'ları kalkmış; VO doğrulaması (geçersiz email/kimlik) `Create`'te yakalanır.

**Acceptance Scenarios**:

1. **Given** `ChargePayment`'ta anemik `BuyerInput`/`BasketItemInput` record, **When** dağıtım yapılır,
   **Then** yerlerine `Buyer`/`BasketItem` VO gelir (private ctor + `Create`), handler VO kurar.
2. **Given** geçersiz alıcı verisi (bozuk email), **When** `Buyer.Create` çağrılır, **Then** domain
   sonucu hata döner (yapısal doğrulama; zengin kural sonra).

### User Story 3 - Çalışma-anı davranışı bit-korunur (Priority: P1)

Bu bir yeniden yapılandırma; ödeme çekimi, kart saklama, taksit sorgu akışları önce nasıl davranıyorsa
sonra da aynı davranır. Üretilen iyzico istek gövde/başlık/imzası değişmez; VO'lar kalıcı aggregate'e
EKLENMEZ (charge-anı transient değerler — bugünkü gibi).

**Why this priority**: Refactor güvenlik koşulu; canlı charge/kart akışı kayarsa iş başarısız.

**Independent Test**: mevcut testler yeşil; üretilen iyzico istekleri dağıtımdan önce ile aynı;
Payment kalıcı şeması değişmez (VO'lar Payment aggregate alanına dönüşmez).

**Acceptance Scenarios**:

1. **Given** dağıtım tamam, **When** kayıtlı kartla charge yapılır, **Then** iyzico'ya giden istek
   (buyer/basket/tutar/imza) dağıtımdan önce ile aynı; sonuç aynı.
2. **Given** dağıtım tamam, **When** `dotnet test` koşar, **Then** testler önceki gibi geçer.

### Edge Cases

- Bir wire tipi hem domain-uygun (VO) hem SDK-istek alanı ise: iki temsil bilinçli — domain VO (zengin,
  iyzico bilmez) + SDK wire DTO (imza-string). Handler çevirir. Aynı ada sahip iki tip karışmamalı.
- SDK'ya taşınan wire enum/sabit (Currency/Locale/Status) domain'e sızmamalı; domain kendi enum'unu
  (ör. PaymentStatus) kullanır.
- Uyuyan wire tipleri (LoyaltyReward/BasketItemType/RefundReason — 0 handler kullanımı) domain'e
  modellenmez; SDK'da uyur (YAGNI — kullanan feature çıkınca modellenir).
- VO doğrulaması başarısızsa charge akışı domain sonucuyla (exception'suz) reddedilmeli.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sistem `src/services/Payment.Api/Provider/` klasörünü tamamen kaldırmalı; içindeki
  iyzico wire tipleri iki hedefe dağıtılmalı (SDK veya Domains VO).
- **FR-002**: Saf-wire tipleri (istek/yanıt DTO, çağrı-yürütücü, response alt-nesne, wire enum/sabit
  Currency/Locale/Status, uyuyan LoyaltyReward/BasketItemType/RefundReason, PaymentCard/PaymentChannel/
  PaymentGroup) paylaşılan `Iyzico.Provider` SDK'ya taşınmalı; namespace `Iyzico.Provider.{Payments,
  Installments,StoredCards}`.
- **FR-003**: Domain-uygun 4 wire tipi Payment domain'ine VO olarak uyarlanmalı: `Buyer`, `Address`,
  `BasketItem` → `Domains/Payments/ValueObjects/`; `CardInformation` → `Domains/StoredCards/ValueObjects/`.
  Her VO: private ctor + statik `Create` fabrikası (anayasa VO kuralı) + ince yapısal doğrulama.
- **FR-004**: `ChargePayment`'taki anemik `BuyerInput`/`BasketItemInput` record'ları kaldırılıp
  yerlerine domain VO kullanılmalı; handler VO'dan SDK wire DTO'sunu kurmalı (anti-corruption sınır).
- **FR-005**: iyzico serileştirme (ToPKIRequestString / camelCase) domain VO'suna girmemeli; yalnız
  SDK wire tipinde kalmalı. Domain VO iyzico'yu bilmez.
- **FR-006**: Uyuyan wire tipleri (0 handler kullanımı) domain'e modellenmemeli; SDK'da kalmalı.
- **FR-007**: Payment kalıcı aggregate şeması değişmemeli — VO'lar charge-anı transient değerlerdir,
  Payment kaydına yeni alan olarak eklenmez.
- **FR-008**: Çözüm 0 hata derlenmeli; mevcut testler yeşil kalmalı; yeni VO'lar için saf domain
  birim testleri eklenmeli (anayasa: davranışlı domain önce test edilir — `Create` doğrulaması).
- **FR-009**: Çalışma-anı davranışı değişmemeli — üretilen iyzico istek gövde/başlık/imzası dağıtımdan
  önce ile aynı olmalı (charge/kart/taksit akışları bit-korunur).
- **FR-010**: Kapsam yalnız Payment.Api. Merchant/Commission `Provider/` klasörleri bu spec'te
  DOKUNULMAZ (kendi spec'lerinde). 034'ün `Iyzico.Provider` temeli üstüne genişler.

### Key Entities

- **Iyzico.Provider SDK (genişleyen)**: 034 transport çekirdeği + Payment'ın saf-wire istek/yanıt/
  yürütücü tipleri (`Iyzico.Provider.{Payments,Installments,StoredCards}`). BC-bağımsız; domain bilmez.
- **Buyer / Address / BasketItem (Payment domain VO)**: alıcı/adres/sepet-kalemi değer nesneleri;
  `Domains/Payments/ValueObjects/`. Immutable, `Create` fabrikalı, iyzico'dan habersiz. Kalıcı değil
  (charge-anı). Anemik `BuyerInput`/`BasketItemInput`'un yerini alır.
- **CardInformation (StoredCard domain VO)**: maskeli kart bilgisi (marka/son-haneler); `Domains/
  StoredCards/ValueObjects/`. `CardBrand` domain enum'u ile hizalı.
- **Handler (anti-corruption sınır)**: `ChargePayment`/`TokenizeCard`/`RevokeCard` — domain VO ↔ SDK
  wire DTO çevirir; wire tekniği domain'e sızmaz.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: `src/services/Payment.Api/Provider/` dizini yok (dosya sayısı 0); önceki 46 wire dosyası
  ya SDK'da ya Domains VO'da.
- **SC-002**: Payment.Api'deki iyzico iş süreçlerinin %100'ü `Domains/<Aggregate>/` altından erişilir
  (teknik-katman `Provider/` klasörü kalmadı).
- **SC-003**: 4 domain VO `Domains/.../ValueObjects/` altında, private ctor + `Create` ile; anemik
  `BuyerInput`/`BasketItemInput` record sayısı 0.
- **SC-004**: Çözüm 0 hata derlenir; mevcut test paketi + yeni VO birim testleri %100 geçer.
- **SC-005**: Üretilen iyzico charge isteği dağıtımdan önce ile aynı (davranış bit-korunur);
  Payment kalıcı şeması değişmez.

## Assumptions

- Kova sınıflandırması brainstorming'de kilitlendi: 42 saf-wire → SDK, 4 → domain VO; Kova-3
  (PaymentCard/PaymentChannel/PaymentGroup) şimdilik SDK (kullanıcı kararı).
- "Yapısal uyarlama şimdi, davranış sonra" (`iyzico_sdk_ddd_adaptation`): VO'lar `Create` + ince
  doğrulamayla kurulur; zengin iş kuralı ileride.
- VO'lar kalıcı Payment aggregate'ine eklenmez; dev aşaması olduğundan gerekirse DB sıfırlanır ama
  bu spec kalıcı şema değiştirmez (transient VO).
- 034 Iyzico.Provider mevcut ve Payment referans veriyor; bu spec onu genişletir (branch 034 üstüne).
- Canlı sandbox smoke opsiyonel; birincil doğrulama build + testler + üretilen istek karşılaştırması.
- Merchant/Commission Provider klasörleri ve uyuyan wire tipleri kapsam dışı.
