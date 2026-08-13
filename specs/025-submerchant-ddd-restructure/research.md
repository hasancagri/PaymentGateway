# Phase 0 Research: SubMerchants Yapısal DDD Geçişi

Kararların hepsi yapısal/konvansiyon; iş-modeli clarify'ları zeminle çözüldü
([[decisions_iyzico_sdk_ddd_adaptation]]). Her biri: Decision / Rationale / Alternatives.

## R1 — Wire/istemci tipleri nereye taşınır

- **Decision**: Beş tip (`SubMerchant`, `CreateSubMerchantRequest`, `UpdateSubMerchantRequest`,
  `RetrieveSubMerchantRequest`, `SubMerchantType`) → `Merchant.Api/Provider/Onboarding/`, namespace
  `Merchant.Api.Provider.Onboarding`. `Domains/SubMerchants/` klasörü silinir.
- **Rationale**: Bunlar iyzico API wire/istemci malzemesi (`BaseRequestV2`/`ProviderResourceV2`
  türevi, PKI imza, canlı HTTP). CP.VPOS-sınırı: sağlayıcı tipleri `Domains/` sınırını geçmez.
  Sağlayıcı sınırı = mevcut `Provider/`. iyzico ucu `/onboarding/submerchant` → alt-klasör adı
  `Onboarding` (iyzico'nun kendi gruplaması; klasör adı `SubMerchant` yapılırsa tip adıyla çakışır).
- **Alternatives**: `Domains/` içinde tutmak (ihlalin kaynağı); ayrı proje/paket (BC-içi malzeme,
  aşırı); klasör adı `SubMerchant` (tip-adı çakışması).

## R2 — `Domains/SubMerchants/` klasörünün kaderi

- **Decision**: Klasör tamamen DAĞITILIR (silinir). İçindekiler R1 ile Provider'a gider; geride
  domain aggregate/tip kalmaz.
- **Rationale**: Aggregate-klasör kuralı: `Domains/<X>/` bir `: AggregateRoot` içerir. Bu klasörde
  aggregate YOK (yalnız wire tipleri + kullanılmayan enum). Wire tipleri çıkınca boş/aykırı klasör
  kalır — dağıtmak kuralı geri getirir (SC-002).
- **Alternatives**: Klasörü bırakıp içine aggregate koymak (davranış = kapsam dışı, YAGNI);
  boş klasör bırakmak (aggregate-klasör kuralı ihlali).

## R3 — Domain-tarafı sub-merchant temsili (US2): yeni VO/aggregate mı?

- **Decision**: YENİ domain tipi ÜRETİLMEZ. Sub-merchant bağının domain temsili 023 `Merchant`
  aggregate'inde ZATEN var ve konvansiyona uygun: `SubMerchantKey` (nullable, private setter) alanı
  + `MerchantType` tip matrisi. Bu iş onu KORUR; wire vocab'ı ayırır.
- **Rationale**: (1) FR-006/SC-003 guardrail: `Merchant` davranışı/yüzeyi değişmez, mevcut testler
  (`Assert.Null(merchant.SubMerchantKey)` ×2) yeşil kalmalı. `SubMerchantKey` string'ini bir VO'ya
  sarmak bu asserl'leri + GetMerchant/UpdateMerchant yanıt alanını + Marten şeklini kırar — davranış
  spec'inin işi. (2) YAGNI (anayasa): kimsenin tüketmediği spekülatif `SubMerchantRegistration` VO
  eklemek, tam da anayasanın uyardığı erken-yapı. Richer VO, kaydı DOLDURAN davranış geldiğinde
  (o alanı gerçekten tükettiğinde) anlamlı doğar.
- **Alternatives**: `SubMerchantRegistration` VO'yu `Merchants/ValueObjects/` altında iskelet olarak
  eklemek (kullanılmayan tip — YAGNI; ve Merchant'a wire etmek testleri kırar); `SubMerchant`'ı ayrı
  aggregate yapmak (1 merchant = 1 sub-merchant flat → ayrı kimlik/lifecycle yok, aggregate aşırı;
  davranış da yok).

## R4 — `SubMerchantType` enum: domain mı sağlayıcı mı, korunur mu

- **Decision**: `SubMerchantType` (PERSONAL/PRIVATE_COMPANY/LIMITED_OR_JOINT_STOCK_COMPANY) KORUNUR
  (FR-005) ve sağlayıcı sınırına (`Provider/Onboarding/`) **wire vocab** olarak taşınır. Silinmez.
- **Rationale**: iyzico'nun UPPER wire kelime-dağarı; şu an KULLANILMIYOR bile (wire request'ler
  `string SubMerchantType` taşıyor). Domain kavramı `MerchantType` (023) tarafından zaten karşılanıyor.
  Enum, gelecekteki `MerchantType`→iyzico `subMerchantType` çevirisinin (davranış spec'i) sağlayıcı-
  tarafı sözlüğü. Sınıra koymak bu rolü netleştirir; korumak FR-005'i karşılar (tip düşmez, matris
  hizası `MerchantType` ile sürer).
- **Alternatives**: Silmek (FR-005 "koru" der; çeviri sözlüğü olarak lazım olacak — dev-fazı
  "kullanılmayanı sil" kuralı burada FR-005 ile eziliyor); domain'e koymak (`MerchantType` zaten
  domain; ikisi domain'de tekrar olur — wire vocab sağlayıcıya ait).

## R5 — `SubMerchant : ProviderResourceV2` içinde DTO + canlı çağrı karışımı (FR-002 edge)

- **Decision**: SDK'nın "resource = wire DTO + static HTTP çağrısı" desenini AYNEN sağlayıcı
  tarafında koru; DTO ile istemci-çağrısını ZORLA ayırma. FR-002 sınıra-taşıma ile karşılanır.
- **Rationale**: Bu, iyzico SDK'sının idiomatik deseni ve Payment.Api/Commission.Api Provider
  malzemesiyle TUTARLI (`InstallmentInfo.Retrieve`, `TransactionReport` … hepsi resource+call). Her
  iyzico resource'unu DTO+client'a bölmek büyük, tutarsız bir konvansiyon değişikliği olur (gold-
  plating). FR-002'nin özü: bu sorumluluk bir `Domains/` DOMAIN tipine karışmasın — Provider'a
  taşıyınca zaten domain tipi değil.
- **Alternatives**: Resource'u `SubMerchantResource` (DTO) + `SubMerchantClient` (çağrı) diye bölmek
  (diğer Provider malzemesiyle tutarsız, YAGNI, davranış-öncesi gereksiz); şimdi hiç dokunmamak
  (taşıma zaten gerekli).

## R6 — Referans/derleme güvenliği

- **Decision**: Taşıma güvenli — SubMerchant TİPLERİ hiçbir yerde referanslı değil. Dış "SubMerchant"
  geçişleri yalnız `Merchant.SubMerchantKey` (string alan) + yorumlar + `GlobalUsings.cs`'teki
  `global using Merchant.Api.Domains.SubMerchants;` satırı. GlobalUsings satırı güncellenir/kaldırılır.
- **Rationale**: `grep -rln SubMerchant` (Domains/SubMerchants dışı): Merchant.cs/UpdateMerchant/
  GetMerchant hepsi `SubMerchantKey` alanı; ui/others'ta hiç yok; testler yalnız `SubMerchantKey`
  null assert eder. Tipleri taşımak/namespace değiştirmek derlemeyi kırmaz.
- **Alternatives**: Yok — olgusal durum.

## Çözülmemiş NEEDS CLARIFICATION

Yok. Spec 0 marker; R1–R6 yapısal kararları sabitledi.
