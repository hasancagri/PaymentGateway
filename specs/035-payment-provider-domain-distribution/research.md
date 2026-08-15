# Phase 0 Research: Payment Provider Domain Dağıtımı

Sınıflandırma + mimari brainstorming'de kilitlendi; burada gerekçe + kritik düzeltmeler. NEEDS CLARIFICATION yok.

## R1: Saf-wire tipleri Iyzico.Provider SDK'ya

- **Decision**: 42 saf-wire tipi (12 BaseRequestV2 istek + 12 ProviderResourceV2 yanıt/yürütücü +
  response alt-nesne ConvertedPayout/InstallmentDetail/InstallmentPrice + wire enum/sabit Currency/
  Locale/Status/PaymentChannel/PaymentGroup/BasketItemType + uyuyan LoyaltyReward/RefundReason +
  PaymentCard) → `Iyzico.Provider.{Payments,Installments,StoredCards}`.
- **Rationale**: bunlar iyzico API wire kontratı — BC-bağımsız, davranışsız, iyzico serileştirme taşır.
  SDK = tam iyzico istemcisi (resmi Iyzipay SDK gibi). 034 çekirdeğinin doğal devamı.
- **Alternatives**: BC'de Provider/ olarak bırakmak — REDDEDİLDİ (kullanıcı: çorba, Domains'ten okunmalı).

## R2: Domain-uygun 4 tip → VO

- **Decision**: `Buyer`, `Address`, `BasketItem` → `Domains/Payments/ValueObjects/`; `CardInformation`
  → `Domains/StoredCards/ValueObjects/`. Her VO: private ctor + statik `Create` (→ `ResultDomain<T>`) +
  ince yapısal doğrulama. iyzico `ToPKIRequestString` VO'ya GİRMEZ (SDK wire tipinde kalır).
- **Rationale**: domain kavramları (alıcı/adres/sepet/kart-bilgisi); anti-anemik VO kuralı. Yapısal
  şimdi, zengin kural sonra.
- **Alternatives**: Kova-3 (PaymentCard/PaymentChannel/PaymentGroup) da VO — kullanıcı şimdilik SDK dedi.

## R3: VO araya-girme — HTTP Input DTO KALIR (kritik düzeltme)

- **Decision**: `ChargePayment.BuyerInput`/`BasketItemInput` ve `TokenizeCard` command alanları
  (`Pan/Expiry/HolderName`) **HTTP-sınır DTO'su olarak kalır** (JSON deserialize için — VO private-ctor
  deserialize edilemez). Domain VO handler'da araya girer: `Input DTO → VO.Create (doğrulama) → SDK wire DTO`.
- **Rationale**: Endpoint minimal-API body'yi record'a bind eder; VO immutable/private-ctor olduğundan
  doğrudan bind olmaz. Input record = slice-local wire-in şekli (domain değil, endpoint DTO'su —
  anayasa buna izin verir). VO = domain doğrulama + iyzico izolasyon noktası.
- **Sonuç**: spec FR-004 "anemik record kaldır" → **yeniden ifade**: record HTTP DTO'ya indirgenir
  (domain rolü kalmaz), domain Buyer/BasketItem VO'su eklenir; handler Input→VO→SDK zincirini kurar.
  Görünür kazanç: doğrulama VO.Create'te merkezî + wire tipleri BC dışına çıkar + Provider/ silinir.
- **Alternatives**: VO'yu doğrudan command/body'ye koyup özel JsonConverter — REDDEDİLDİ (VO immutability
  ile savaşır, fazla ceremonisi).

## R4: Address VO Buyer'dan türetilir

- **Decision**: `Address` bağımsız input DEĞİL — bugün `BuildAddress(buyer)` ile Buyer alanlarından
  türetiliyor (contactName=name+surname, city/country/description=buyer). Domain VO'su da Buyer'dan
  üretilir (shipping=billing, tek türetme); ayrı command input eklenmez.
- **Rationale**: davranış-koruma — bugünkü türetme aynen; Address domain'de VO ama kaynağı Buyer.
- **Alternatives**: Address'i ayrı input yapmak — REDDEDİLDİ (yeni API alanı = davranış/kontrat değişir).

## R5: CardInformation = tokenize-anı ham kart (Model A)

- **Decision**: `CardInformation` VO = tokenize-anı ham kart (`TokenizeCard` command'ın Pan/Expiry/
  HolderName'i). VO `Create` ince doğrulama (Luhn + expiry format); handler VO → SDK `CardInformation`
  wire → `Card.Create` (iyzico'ya bir kez gider, Model A — sonra yalnız token saklanır).
- **Rationale**: kart-bilgisi StoredCard domain kavramı (kart saklama eylemi). Ham PAN transient
  (gateway'de kalıcı değil — Model A, `iyzico_card_storage_032`).
- **Not**: VO kalıcı StoredCard alanı DEĞİL — token/CardUserKey saklanır (mevcut), CardInformation transient.

## R6: Çalışma-anı davranış-koruma

- **Decision**: üretilen iyzico istekleri (charge/tokenize) bit-korunur — VO→SDK-wire map bugünkü
  `BuildRequest` alan eşlemesini aynen üretir. Payment kalıcı şema değişmez.
- **Rationale**: refactor; iş değişmez. Doğrulama: mevcut testler + (ops.) canlı charge.
- **Alternatives**: yok.

## R7: data-model VAR, contracts YOK

- **Decision**: data-model.md üretilir (4 VO — alan/Create/doğrulama). contracts/ YOK.
- **Rationale**: domain entity (VO) var → data-model anlamlı (034'ten farklı). Dış kontrat/endpoint
  değişmez (HTTP body şekli korunur) → contract yok.
