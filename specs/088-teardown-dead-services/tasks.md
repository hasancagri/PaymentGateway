---
description: "Task list — Phase 0 Teardown: Ölü Servis Sökümü"
---

# Tasks: Phase 0 Teardown — Ölü Servis Sökümü

**Input**: `specs/088-teardown-dead-services/spec.md`

**Prerequisites**: spec.md (küçük ölçek — plan atlandı, bkz. checklists/requirements.md L39 + conventions.md artefakt-ölçekleme)

**Tests**: Yeni test YAZILMAZ (teardown). Payment.Api.Tests domain testleri KORUNUR; iyzico-wire testi varsa kaldırılır.

**Branch**: `pivot/bank-adapter-skeleton` (master'dan)

## Format: `[ID] [P?] [Story] Description`

- **[P]**: paralel koşabilir (farklı dosya, bekleyen bağımlılık yok)
- **[Story]**: US1 / US2 / US3
- Her task tam dosya yolu taşır

---

## Phase 1: Setup

**Purpose**: Söküm öncesi temiz başlangıç referansı.

- [X] T001 `pivot/bank-adapter-skeleton` branch'inde olduğunu doğrula + söküm-öncesi referans `dotnet build` ve `dotnet test` çıktısını kaydet (hangi testler yeşildi — strip sonrası regresyon kıyası için)

---

## Phase 2: Foundational

**Purpose**: Yok. Bu bir söküm; bloklayan ortak prerequisite yok. Doğrudan US1'e geç.

---

## Phase 3: User Story 1 — Ölü servisleri sil (Priority: P1) 🎯 MVP

**Goal**: Merchant.Api, Commission.Api, Mail.Worker, Admin + iki test projesi çözümden/diskten/CD'den kalkar.

**Independent Test**: `dotnet sln list` bu projeleri listelemez; klasörleri diskte yok; CD manifest yalnız yaşayan servisleri deploy eder.

> Not: US1 tek başına derlemeyi kırar (yaşayan projeler bu servislere referans verir) — yeşil derleme US2 ile gelir. US1 sökümün çekirdeğidir, US2 zorunlu tamamlayıcısıdır.

- [X] T002 [P] [US1] `src/services/Merchant.Api/` klasörünü (+ `.csproj`) diskten sil
- [X] T003 [P] [US1] `src/services/Commission.Api/` klasörünü (+ `.csproj`) diskten sil
- [X] T004 [P] [US1] `src/others/Mail.Worker/` klasörünü (+ `.csproj`) diskten sil
- [X] T005 [P] [US1] `src/ui/Admin/` klasörünü (+ `.csproj`) diskten sil
- [X] T006 [P] [US1] `tests/Merchant.Api.Tests/` klasörünü (+ `.csproj`) diskten sil
- [X] T007 [P] [US1] `tests/Commission.Api.Tests/` klasörünü (+ `.csproj`) diskten sil
- [X] T008 [US1] `PaymentGateway.slnx`: Merchant.Api / Commission.Api / Mail.Worker / Admin / Merchant.Api.Tests / Commission.Api.Tests `<Project>` satırlarını (6 satır) kaldır; Payment.Api.Tests KALIR
- [X] T009 [US1] `.github/workflows/ci.yml` (~L55 servis döngüsü): `merchant-api commission-api mail-worker admin-web` hedeflerini çıkar, yalnız yaşayan servisler kalsın (identity-server, payment-api + gateway varsa); GHCR push hedeflerini aynı şekilde buda
- [X] T010 [US1] `deploy/k8s/paymentgateway.yaml`: merchant-api / commission-api / mail-worker / admin-web Deployment+Service bloklarını sil; yalnız payment-api / identity-server / gateway kalsın

**Checkpoint**: Dört servis + iki test projesi slnx/disk/CD'de yok (derleme henüz KIRIK — US2 düzeltir).

---

## Phase 4: User Story 2 — Yaşayan altyapıdan ölü bağları unwire et (Priority: P1)

**Goal**: Silinen servislere kalan tüm referanslar yaşayan projelerden (AppHost, Identity.Server, Shared, Common, gateway) temizlenir; `dotnet build` 0 hata.

**Independent Test**: `dotnet build` 0 hata; Aspire AppHost açılır, dashboard'da yalnız payment-api/identity-server/gateway; ölü event kontratına/`WithReference`'a atıf yok.

- [X] T011 [US2] `src/aspire/AppHost/AppHost.cs`: merchantDb + commissionDb `AddDatabase`, mailpit container, mailWorker/merchantApi/commissionApi/adminWeb `AddProject`+`WithReference`/`WaitFor`'larını kaldır; ghcr servis dizisini buda. KORU: postgres/rabbit/paymentDb/identityDb/identityServer/paymentApi
- [X] T012 [US2] `src/others/Identity.Server/Activation/MerchantActivationClient.cs` dosyasını sil
- [X] T013 [US2] `src/others/Identity.Server/EventHandlers/MerchantClientEventHandler.cs` dosyasını sil (MerchantCreated/Provisioned/KeyReissued/StatusChanged handler'ları — publisher öldü)
- [X] T014 [US2] `src/others/Identity.Server/Program.cs`: MerchantApi `AddHttpClient` kaydını + merchant.lifecycle exchange/queue/listener kurulumunu kaldır; MerchantClientEventHandler'a dair `Discovery.IncludeType` varsa sil
- [X] T015 [US2] `src/others/Identity.Server/Connect/SeedHostedService.cs`: `identity-activation` ClientSeed girdisini kaldır
- [X] T016 [US2] `src/others/Identity.Server/Config.cs`: `admin-ui` + `identity-activation` ClientSeed'lerini, merchant/commission API scope'larını (commission.read/write, merchant.admin vb.) kaldır; `RetiredClientIds`'i ölü client'larla güncelle
- [X] T017 [US2] `src/others/Shared/IntegrationEvents.cs`: `MerchantCreated`/`MerchantStatusChanged`/`MerchantKeyReissued`/`MerchantProvisioned`/`MerchantCommissionGridReady`/`SendEmailRequested`/`EmailAttachmentTable` record'larını sil. KORU: `PaymentCompletedEvent`/`PaymentFailedEvent`. `PaymentChargedEvent` (MerchantId taşır) → derlemede kullanılmıyorsa sil, kullanılıyorsa KORU (build ile doğrula)
- [X] T018 [US2] `src/others/Shared/RabbitMqConstants.cs`: `MerchantCommission` + `MailDelivery` + `MerchantLifecycle` sabit sınıflarını sil (Payment consumer'ı US3'te kalkıyor → MerchantLifecycle de ölür)
- [X] T019 [US2] `src/others/Common/Utils/Constants/AuthorizationPolicies.cs`: `MerchantApiKey` const'ını kaldır (FR-005). `MerchantScoped`/`AdminPlaneOnly` KORU (yalnız MerchantApiKey adlandı); dangling kullanım build'de doğrulanır
- [X] T020 [US2] gateway `appsettings.Development.json` (+ `appsettings.json` kontrol): EC route'ları (basket/catalog/file/file-images/order/storefront/customer-addresses/customer-cards) + cluster'ları (basket/catalog/file/order/stock/storefront/customer) sil; yalnız `payment-route` + `payment.cluster` kalsın
- [X] T021 [US2] **Checkpoint**: `dotnet build` → 0 hata, 0 kırık referans, çözülmeyen using yok (iyzico Payment.Api'de hâlâ duruyor — normal, US3 strip eder)

---

## Phase 5: User Story 3 — Payment.Api'den iyzico'yu strip et (Priority: P2)

**Goal**: Payment.Api iyzico transport engine + Options + MerchantKey/MerchantStatus auth zinciri kalkar; 041 HostedPayments domain akışı korunur (iyzico çağrısı Phase 1'e devredilecek biçimde stub).

**Independent Test**: `Utils/*V2` + iyzico Options + ApiKey auth zinciri diskte yok; `dotnet test tests/Payment.Api.Tests` yeşil; Payment.Api derlenir.

> Not: US1/US2'den bağımsız test edilebilir (yalnız Payment.Api + testleri). P2 çünkü 041 akışının iyzico'ya bağımlılığı var — strip ederken denge gerekir (edge case).

### Silme (iyzico engine + Options + auth zinciri)

- [X] T022 [P] [US3] `src/services/Payment.Api/Utils/` iyzico V2 motorunu sil: `RestHttpClientV2.cs`, `ProviderResourceV2.cs`, `HashGeneratorV2.cs`, `ProviderConstants.cs`, `ProviderOptions.cs`
- [X] T023 [P] [US3] `src/services/Payment.Api/Options/IyzicoProviderSettings.cs` + `Options/IyzicoRequestOptions.cs` sil
- [X] T024 [P] [US3] `src/services/Payment.Api/Auth/ApiKeyAuthenticationHandler.cs` + `Auth/ApiKeyHash.cs` sil
- [X] T025 [P] [US3] `src/services/Payment.Api/Domains/MerchantStatus/` klasörünü sil (`MerchantApiKeyReference.cs` + `MerchantStatusReference.cs`)
- [X] T026 [P] [US3] `src/services/Payment.Api/MerchantApiConsumers.cs` sil (merchant.lifecycle fanout tüketicisi)
- [X] T027 [P] [US3] `src/services/Payment.Api/Constants/HostedPaymentPolicies.cs` sil (yalnız silinen ApiKey şemasına bağlı `HostedPaymentApiKey` tanımlıyordu)

### Program.cs wiring strip

- [X] T028 [US3] `src/services/Payment.Api/Program.cs`: (a) `MerchantApiKeyReference` Marten şema index'i, (b) X-Api-Key `AddScheme` + `MerchantApiKey`/`HostedPaymentApiKey` policy'leri, (c) iyzico `IyzicoProviderSettings`/`ProviderOptions`/`IyzicoRequestOptions` Options blokları, (d) `Discovery.IncludeType(typeof(MerchantApiConsumers))`, (e) merchant.lifecycle RabbitMQ listener'ını kaldır. KORU: JWT bearer + scope policy'leri, HostedPaymentSession şeması

### 041 HostedPayments — iyzico bağını kes, akışı koru (Phase-1 defer)

- [X] T029 [US3] `src/services/Payment.Api/Domains/HostedPayments/Features/Commands/InitiateHostedPayment.cs`: (a) iyzico strip — `using Iyz = Payment.Api.Utils` alias, enjekte `ProviderOptions`/`IyzicoRequestOptions`, `ProviderResourceV2.GetHttpHeaders...` + `RestHttpClientV2...PostAsync<CheckoutFormInitializeResult>` çağrısı, `Iyz.ProviderResourceV2` base response tipi; (b) **merchant Active-gate strip** — `session.LoadAsync<MerchantStatusReference>` statü kapısını (L~111-114) + `ActiveStatus` const'ını (L~94) kaldır (T025 referansı siliyor → fail-closed statü kaynağı merchant.lifecycle fanout da ölüyor; **Active-gate Phase-1'e devrediliyor**, `// TODO Phase-1: merchant Active-gate`). Sağlayıcı etkileşimini `PROVIDER_UNAVAILABLE` (`HostedPaymentResourceConstants`) stub'la + `// TODO Phase-1 PaymentLink adapter`. Aggregate akışı/komut kavramı KORUNUR
- [X] T030 [US3] `src/services/Payment.Api/Domains/HostedPayments/Features/Commands/CompleteHostedPayment.cs`: aynı strip (alias, enjeksiyon, `RestHttpClientV2...PostAsync<RetrieveCheckoutFormResult>` retrieve çağrısı, `Iyz.ProviderResourceV2` base) + `PROVIDER_UNAVAILABLE` stub + TODO
- [X] T031 [US3] `src/services/Payment.Api/Domains/HostedPayments/HostedPaymentEndpointExtension.cs`: (a) silinen `HostedPaymentPolicies.HostedPaymentApiKey` policy referansını **GEÇİCİ Phase-0 compile placeholder** JWT `payment.write` scope policy ile değiştir — `// TODO Phase-1: gerçek auth = store-S2S AgentPlatform platform IdP + anonim per-session secret token (anayasa İlke V); payment.write placeholder, nihai yetki DEĞİL`; (b) `merchant_id` claim'den tenant `MerchantId` türetme (L~135) X-Api-Key handler ölünce kalktı → `MerchantId`'yi request body'den al / Phase-1'e kadar stub'la (`// TODO Phase-1 tenant kaynağı`); endpoint + komut dispatch kavramı KORUNUR. **Not: nihai yetki D1 — anayasa İlke V ile Phase-1'de hizalanacak**
- [X] T032 [US3] Dokunma-doğrula: `Domains/HostedPayments/HostedPaymentSession.cs`, `Features/Commands/StoreCallbackDelivery.cs`, `Constants/HostedPaymentResourceConstants.cs` iyzico'suz (değişmeden derlenmeli)

### Test triage

- [X] T033 [US3] `tests/Payment.Api.Tests/`: `HostedPaymentSessionTests.cs` (saf domain) KORU; iyzico wire'ı sınayan test dosyası varsa (ProviderResourceV2/RestHttpClientV2/HashGeneratorV2 atıflı) kaldır
- [X] T034 [US3] **Checkpoint**: `dotnet build` 0 hata + `dotnet test tests/Payment.Api.Tests` yeşil

---

## Phase 6: Polish & Cross-Cutting

**Purpose**: Doküman + guard + uçtan-uca doğrulama (FR-011, SC-004, SC-003, SC-005).

- [X] T035 [US3] Yaşayan projelerin FLOW.md'lerindeki BAYAT kenar-anchor'ları güncelle (yoksa `check-flow-links.sh` tüm `src/**/FLOW.md`'yi tarar → FAIL): (a) `src/services/Payment.Api/FLOW.md` — `MerchantApiKeyReference`/`MerchantStatusReference`/`MerchantApiConsumers`/`ApiKeyAuthenticationHandler` + iyzico V2 tipleri + `merchant.lifecycle`/`MerchantCreated`/`MerchantProvisioned`/`MerchantStatusChanged` anchor'larını çıkar, domain sürecini strip'e göre hizala; (b) `src/others/Identity.Server/FLOW.md` — teardown-sonrası M2M-IdP sürecine yeniden yaz (silinen `MerchantClientEventHandler`/`MerchantActivationClient`/`MerchantProvisioned`/`MerchantScopeRequirement`/`AddMerchantPermissions`/`merchant.*` scope anchor'ları gider — İlke IX: yaşayan BC bayat süreç belgesi taşıyamaz). Merchant/Commission/Mail.Worker FLOW.md'leri dizinleriyle silindi (T002–T004) — anchor'ları kendiliğinden gider
- [X] T036 `CLAUDE.md`: BC haritası + servis listesi + iyzico/Merchant/Commission/Mail.Worker/Admin atıflarını pivota göre güncelle (FR-011); 088 spec yolunu ekle
- [X] T037 `docs/conventions.md`: bayat iyzico/Merchant/Commission atıfları varsa pivota göre güncelle
- [X] T038 `scripts/check-claude-spec-links.sh` + `scripts/check-flow-links.sh` çalıştır → ikisi de geçer (SC-004)
- [X] T039 Final `dotnet build` (0 hata, SC-001) + `dotnet test` (Payment.Api.Tests yeşil, SC-002)
- [X] T040 `dotnet run --project src/aspire/AppHost/AppHost.csproj` → dashboard'da yalnız payment-api/identity-server/gateway + postgres/rabbit; merchant-api/commission-api/mail-worker/admin-web/mailpit YOK (SC-003)
- [X] T041 `grep -riE "Merchant\.Api|Commission\.Api|Mail\.Worker|iyzico|Iyzico" src` → yaşayan kodda sembol/using/atıf yok (docs tarihsel referans hariç) (SC-005)

---

## Dependencies

- **US1 → US2**: silme derlemeyi kırar, unwire düzeltir; US2 checkpoint (T021) ilk yeşil derleme eşiği. İkisi birlikte "yeşil derleme" MVP'si.
- **US3** US1/US2'den bağımsız (yalnız Payment.Api); US2'den sonra ya da paralel şeritte yapılabilir, ama tek branch'te sıralı önerilir.
- **Phase 6** tüm US'lerden sonra (final doğrulama).

## Parallel Execution

- **US1**: T002–T007 altı dir-silme tamamen paralel `[P]`; sonra T008–T010 sıralı (slnx/ci/k8s dosya düzenleri).
- **US3**: T022–T027 silmeleri paralel `[P]`; sonra T028 (Program.cs) → T029–T031 (handler/endpoint edit) → T032–T034 doğrulama sıralı.

## Implementation Strategy

- **MVP = US1 + US2** (yeşil derleme eşiği: dört ölü servis gitti, altyapı ayakta). Tek başına teslim edilebilir.
- **US3** ikinci artış (Payment.Api iyzico temizliği; 041 akışı Phase-1 PaymentLink şablonu olarak stub'lı korunur).
- **Polish** son artış (doküman + guard + Aspire-açılış doğrulama).
- Canlı uçtan-uca ödeme testi YOK — pivot sonuna ertelendi (EC zaten PAYMENT_DISABLED); yalnız build/test/Aspire-açılış doğrulanır.
