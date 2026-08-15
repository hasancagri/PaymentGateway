# Phase 0 Research: Iyzico.Provider Çekirdek Çıkarımı

Kararlar brainstorming'de kilitlendi; burada gerekçe + alternatif kaydı. NEEDS CLARIFICATION yok.

## R1: Proje konumu

- **Decision**: `src/others/Iyzico.Provider` (paylaşılan altyapı hizası — Common/Shared/Identity.Server yanında).
- **Rationale**: BC değil, paylaşılan transport. `src/others/` bu kategori için mevcut konvansiyon. BC'ler tek yönlü referans verir.
- **Alternatives**: (a) `src/services/Iyzico.Provider` — 020'de `src/services/Iyzipay` böyleydi ama o host/SDK adasıydı; bu düz lib, `others` daha doğru. (b) `Common`'a koymak — REDDEDİLDİ: Common'ı her proje kullanır, transport tipi tüm sisteme açılır + Common'ın domain-base/result semantiği kirlenir.

## R2: Namespace

- **Decision**: taşınan 14 dosya `namespace Iyzico.Provider`. BC alt klasörleri `<BC>.Provider.<Sub>` (Payments/Onboarding/Payout…) **aynı kalır**.
- **Rationale**: çekirdek BC-bağımsız → BC-adı taşımaz. Alt klasörler BC-özel → BC namespace'inde kalır. Alt klasörler `using Iyzico.Provider;` ile çekirdeği görür (bugün namespace-nesting ile parent `<BC>.Provider`'ı görüyorlar).
- **Alternatives**: alt klasörleri de `Iyzico.Provider.<Sub>`'a taşımak — REDDEDİLDİ (Approach B): BC-özel tipleri paylaşılan projeye alır, sınır kuralını çiğner.

## R3: Görünürlük (internal → public)

- **Decision**: `RestHttpClientV2` `internal`→`public`. `StringHelper` `internal` KALIR. Gerisi zaten public; `ProviderResourceV2.GetHttpHeaders*` `protected` kalır.
- **Rationale**: BC alt klasörleri (ayrı assembly) `RestHttpClientV2.Create()` doğrudan çağırıyor → public şart. `StringHelper` yalnız `RequestFormatter` (çekirdek-içi) tarafından kullanılıyor → gizli kalır (en dar yüzey). `protected` üyeler miras yoluyla (`Payment : PaymentResource : ProviderResourceV2`) cross-assembly erişilir → değişiklik gerekmez.
- **Alternatives**: `InternalsVisibleTo` ile internal tutmak — REDDEDİLDİ: 3 tüketici için attribute gürültüsü; `Create()` zaten mantıksal olarak public fabrika.
- **Doğrulama**: `grep RestHttpClientV2` alt klasörlerde 24 çağrı (Payment/Merchant/Commission); `StringHelper` yalnız RequestFormatter'da (çekirdek).

## R4: CPM (Central Package Management)

- **Decision**: yeni lib sürümsüz `<PackageReference Include="Newtonsoft.Json" />`. Sürüm `Directory.Packages.props` (13.0.4 zaten var).
- **Rationale**: anayasa CPM istisnasız (yalnız CP.VPOS muaftı, o da silindi). Yeni ada açmak yasak.
- **Alternatives**: lokal sürüm — REDDEDİLDİ (CPM ihlali).

## R5: ProviderOptions ve BC config

- **Decision**: `ProviderOptions` (transport-config POCO) çekirdeğe taşınır. BC-özel `IyzicoProviderSettings` (secret'lı) BC'de kalır ve `Iyzico.Provider.ProviderOptions`'a map'lenmeye devam eder.
- **Rationale**: ProviderOptions BC-bağımsız transport alanları (ApiKey/SecretKey/BaseUrl taşıyıcı) — davranışsız POCO, üç kopyası özdeş. Secret kaynağı (user-secrets) + Options-pattern binding BC'de kalır → secret çekirdeğe sızmaz.
- **Nüans (runtime wiring)**: yalnız **Payment** provider'ı runtime kullanıyor (Program.cs `AddSingleton<ProviderOptions>` map). **Merchant/Commission** provider uyuyor (022 ara durum — alt klasör tipleri `ProviderOptions`'ı yalnız parametre alır, DI wiring yok). Payment'ta tek satır tip-adı değişir: `new Payment.Api.Provider.ProviderOptions` → `new Iyzico.Provider.ProviderOptions`.

## R6: GlobalUsings (BC-başına farklı)

- **Decision**:
  - **Payment**: `global using Payment.Api.Provider;` → `global using Iyzico.Provider;` (alt-namespace satırları `.StoredCards/.Payments/.Installments` KALIR).
  - **Commission**: `global using Commission.Api.Provider;` → `global using Iyzico.Provider;` (`.Payout/.Reporting` KALIR).
  - **Merchant**: Provider global using'i YOK → `global using Iyzico.Provider;` EKLE (Onboarding bugün namespace-nesting ile parent'ı görüyor; taşımadan sonra explicit using şart).
- **Rationale**: çekirdek namespace değişince alt klasörlerin çekirdeği görmesi gerekir. Anayasa "her projede tek GlobalUsings.cs" — oraya eklenir.
- **Doğrulama**: Merchant GlobalUsings'te bugün hiç Provider satırı yok → nesting bağı; en yüksek regresyon riski burada.

## R7: data-model / contracts üretilmedi

- **Decision**: Phase 1'de data-model.md ve contracts/ YOK.
- **Rationale**: refactor'ün domain entity'si yok (kod taşıma); dış kontrat değişmez (iyzico wire davranışı bit-düzeyinde korunur). Anayasa/şablon "if applicable" → uygulanmıyor. Doğrulama quickstart.md'de.
