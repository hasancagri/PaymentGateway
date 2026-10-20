# Feature Specification: Phase 0 Teardown — Ölü Servis Sökümü

**Feature Branch**: `pivot/bank-adapter-skeleton`

**Created**: 2026-10-20

**Status**: Draft

**Input**: Banka-adaptör pivotunun ilk adımı: iyzico + Merchant BC + Commission BC + Mail.Worker + Admin ölür; Aspire/test/Identity/gateway/Payment.Api altyapısı korunur. Yeni aggregate/tablo/event YOK (küçük ölçek: silme + unwire).

## Bağlam

Constitution v2.0.0 ile domain yeniden tanımlandı: tek Payment BC, banka-adaptör, pay-by-link, kart saklama yok. Eski yapı (iyzico ödeme kanalı, Merchant/Commission BC'leri, müşteriyle konuşan Mail.Worker, Merchant/Commission hassas-veri Admin BFF'i) artık işlevsiz. Bu söküm, yeni iskeletin (Phase 1) üstüne inşa edileceği temiz zemini açar. Çalışan 041 hosted-CF akışı SİLİNMEZ — Phase 1'de PaymentLink'e evrilecek.

## User Scenarios & Testing *(mandatory)*

Aktör: **geliştirme ekibi** (bu bir altyapı sökümüdür; son-kullanıcı davranışı değişmez — ödeme zaten EC tarafında PAYMENT_DISABLED).

### User Story 1 — Ölü servisleri sil (Priority: P1)

Merchant.Api, Commission.Api, Mail.Worker ve Admin projeleri (+ Merchant/Commission test projeleri) çözümden ve diskten kaldırılır; slnx girdileri, GHCR push hedefleri ve CD/k8s manifest'leri temizlenir.

**Why this priority**: Pivotun özü bu servislerin ölümü; gerisi (unwire) bunun zorunlu sonucu. Tek başına uygulanınca derleme kırılır → US2 ile birlikte anlam kazanır ama sökümün çekirdeğidir.

**Independent Test**: `PaymentGateway.slnx` artık bu projeleri listelemez; `dotnet sln list` çıktısında yoklar; diskte klasörleri yok.

**Acceptance Scenarios**:

1. **Given** çözümde Merchant.Api/Commission.Api/Mail.Worker/Admin var, **When** söküm yapılır, **Then** dört proje + iki test projesi slnx'ten ve diskten kalkar.
2. **Given** CD manifest'leri 6 servis deploy ediyor, **When** söküm yapılır, **Then** yalnız yaşayan servisler (payment-api, identity-server, gateway) manifest'te kalır.

---

### User Story 2 — Yaşayan altyapıdan ölü bağları unwire et (Priority: P1)

Silinen servislere bağlı tüm referanslar yaşayan projelerden (Aspire AppHost, Identity.Server, Shared, Common, gateway) temizlenir; `dotnet build` 0 hata verir.

**Why this priority**: Silme tek başına derlemeyi kırar; unwire olmadan sistem ayağa kalkmaz. US1 ile birlikte "yeşil derleme" eşiğini oluşturur.

**Independent Test**: `dotnet build` 0 hata; Aspire AppHost açılır, dashboard'da yalnız payment-api/identity-server/gateway görünür; ölü event kontratına/`WithReference`'a kalan atıf yok.

**Acceptance Scenarios**:

1. **Given** AppHost merchant-api/commission-api/mail-worker/admin-web + merchantDb/commissionDb + mailpit wire ediyor, **When** unwire yapılır, **Then** bunlar kalkar; postgres(paymentDb/identityDb)/rabbit/payment-api/identity-server/gateway kalır.
2. **Given** Identity.Server MerchantLifecycle event'lerini tüketiyor + MerchantActivationClient çağırıyor, **When** unwire yapılır, **Then** bu consumer'lar + client + merchant-api client-secret config kalkar, Identity.Server derlenir ve ayakta kalır.
3. **Given** Shared/IntegrationEvents.cs Merchant*/Commission*/SendEmailRequested kontratları taşıyor, **When** unwire yapılır, **Then** bu kontratlar silinir; PaymentCompleted/PaymentFailed KALIR.
4. **Given** gateway appsettings bayat EC rotaları (basket/catalog/order/storefront/customer-api) taşıyor, **When** unwire yapılır, **Then** yalnız payment-route kalır.

---

### User Story 3 — Payment.Api'den iyzico'yu strip et (Priority: P2)

Payment.Api içindeki iyzico transport engine, iyzico Options'ları ve MerchantKey/MerchantStatus auth zinciri kaldırılır; çalışan 041 HostedPayments akışı korunur.

**Why this priority**: Payment.Api yaşayan servis; iyzico kalıntısı Phase 1 iskeletini kirletir. US1/US2'den ayrı test edilebilir (yalnız Payment.Api + testleri). P2 çünkü 041 akışının iyzico'ya bağımlılığı var — strip ederken denge gerekir.

**Independent Test**: `Utils/*V2` + iyzico Options + MerchantApiConsumers/MerchantStatusReference/MerchantApiKeyReference/ApiKeyAuthenticationHandler diskte yok; `dotnet test tests/Payment.Api.Tests` yeşil.

**Acceptance Scenarios**:

1. **Given** Payment.Api iyzico V2 engine + IyzicoProviderSettings/IyzicoRequestOptions taşıyor, **When** strip yapılır, **Then** bunlar silinir.
2. **Given** X-Api-Key auth MerchantApiKeyReference'a bakıyor, **When** strip yapılır, **Then** ApiKeyAuthenticationHandler + iki reference doküman + MerchantApiConsumers kalkar.
3. **Given** 041 HostedPayments akışı iyzico engine'e bağlı, **When** strip yapılır, **Then** akış ya minimal korunur ya da iyzico'ya bağlı kısmı Phase 1'e devredilir; Payment.Api derlenir ve domain testleri yeşil kalır.

### Edge Cases

- 041 HostedPayments akışı iyzico transport'a bağlıysa ve strip derlemeyi kırıyorsa: iyzico'ya bağlı command handler'lar Phase 1'e devredilecek biçimde minimal bırakılır (akış kavramı silinmez, iyzico çağrısı stub'lanır/çıkarılır) — kararı US3 implement sırasında ölç.
- 041 akışı yalnız iyzico'ya değil, silinen merchant-auth zincirine de bağlı: InitiateHostedPayment `MerchantStatusReference` Active-gate'i (İlke V fail-closed) yükler, endpoint tenant `MerchantId`'yi X-Api-Key `merchant_id` claim'inden türetir. Reference + ApiKey handler silinince bunlar da kalkar: Active-gate'in statü kaynağı (merchant.lifecycle fanout) öldüğü için gate + tenant kaynağı Phase 1'e devredilir (stub), akış kavramı korunur.
- Payment.Api.Tests iyzico'ya bağlı testler içeriyorsa: bu testler 041'in domain davranışını mı yoksa iyzico wire'ı mı sınıyor ayırt et; wire testi varsa kaldır, domain testi kalır.
- RabbitMQ'da MerchantLifecycle/MailDelivery exchange'leri artık publisher'sız: binding'ler tüketici ölünce kendiliğinden işlevsiz; dev-reset şema/exchange temizler, migration gerekmez.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Çözüm Merchant.Api, Commission.Api, Mail.Worker, Admin projelerini ve Merchant.Api.Tests/Commission.Api.Tests projelerini İÇERMEMELİ (slnx + disk).
- **FR-002**: Aspire AppHost silinen servisleri + yalnız onların kullandığı kaynakları (merchantDb, commissionDb, mailpit) wire ETMEMELİ; paymentDb/identityDb/rabbit/postgres korunmalı.
- **FR-003**: Identity.Server derlenmeli ve ayakta kalmalı; Merchant'a bağlı tüm parçalar (MerchantActivationClient, MerchantLifecycle event handler'ları, merchant-api client config) kaldırılmalı.
- **FR-004**: Shared/IntegrationEvents.cs yalnız yaşayan kontratları (PaymentCompleted/PaymentFailed) içermeli; Merchant*/Commission*/SendEmailRequested kontratları kaldırılmalı.
- **FR-005**: Common'dan `AuthorizationPolicies.MerchantApiKey` ve ona bağlı ölü yardımcılar kaldırılmalı.
- **FR-006**: gateway route config yalnız payment-route içermeli; PG'de karşılığı olmayan EC rotaları kaldırılmalı.
- **FR-007**: Payment.Api iyzico transport engine'ini (Utils/*V2), iyzico Options'larını ve MerchantKey/MerchantStatus auth zincirini İÇERMEMELİ.
- **FR-008**: 041 HostedPayments domain akışı korunmalı (Phase 1 PaymentLink şablonu); iyzico'ya bağımlı kısımları Phase 1'e devredilecek biçimde minimal tutulmalı.
- **FR-009**: `dotnet build` 0 hata vermeli; kırık referans / çözülmeyen using / ölü event publish/consume kalmamalı.
- **FR-010**: CD/k8s deploy manifest'leri + GHCR push hedefleri yalnız yaşayan servisleri (payment-api, identity-server, gateway) içermeli.
- **FR-011**: CLAUDE.md + docs/conventions.md bu PR'da güncellenmeli (bayat BC haritası/servis listesi/iyzico+Merchant+Commission atıfları pivota göre); `scripts/check-claude-spec-links.sh` + `scripts/check-flow-links.sh` guard'ları geçmeli.

### Key Entities

Yok (yeni veri modeli üretilmez; yalnızca mevcut yapı kaldırılır).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: `dotnet build` 0 hata, 0 kırık referans.
- **SC-002**: `dotnet test` yeşil (Payment.Api.Tests; silinen servislerin test projeleri artık yok).
- **SC-003**: Aspire AppHost açılınca dashboard'da yalnız 3 servis (payment-api, identity-server, gateway) + postgres/rabbit; merchant-api/commission-api/mail-worker/admin-web/mailpit yok.
- **SC-004**: `scripts/check-claude-spec-links.sh` + `scripts/check-flow-links.sh` guard'ları geçer.
- **SC-005**: Repo genelinde "Merchant.Api"/"Commission.Api"/"Mail.Worker"/"iyzico"/"Iyzico" sembol/using/atfı yaşayan kodda kalmaz (docs tarihsel referans hariç).

## Assumptions

- 041 HostedPayments akışı diskte korunur ama iyzico'ya bağlı olduğu için Phase 1'e kadar "canlı ödeme" beklenmiyor (EC zaten PAYMENT_DISABLED); strip sonrası akışın iyzico bağı Phase 1'de PaymentLink + stub adaptörle değişecek.
- Identity.Server silinmez (constitution: şirket SSO'suna evrilecek); bu söküm yalnız Merchant bağlarını keser.
- Dev aşaması: geriye-uyum/veri migration üretilmez; DB şema/exchange reset ile temizlenir (feedback: dev-phase-no-defensive-migrations).
- Branch `pivot/bank-adapter-skeleton` (master'dan); canlı uçtan-uca doğrulama pivot sonuna ertelendi (yalnız build/test/Aspire-açılış doğrulanır).
- Admin silinince hiçbir yaşayan iş yüzeyi kaybolmaz (044'ten beri Admin yalnız Merchant/Commission hassas-veri BFF'iydi).