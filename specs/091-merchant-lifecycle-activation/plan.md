# Implementation Plan: Store Lifecycle / Activation

**Branch**: `pivot/bank-adapter-skeleton` (spec dir `091-merchant-lifecycle-activation`) | **Date**: 2026-10-22 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/091-merchant-lifecycle-activation/spec.md`

## Summary

Bir store'un gerçek kart ödemesi kabul edebilmesi için bankalarda kendi merchant hesabını açtığı
aktivasyon döngüsü. Super-admin → EC (X-Api-Key S2S) → PG `StoreOnboarding` aggregate'i (Draft) +
tek-kullanımlık TTL link üretir. Store, PG'nin tek sabit hosted formunu doldurur (ortak çekirdek
profil: ünvan/vergi no/IBAN/MCC/URL/yetkili). Submit → PG yapılandırılmış her bankaya **onboarding
port/adapter** (İlke VII, charge portundan AYRI) ile referral fan-out eder; adapter banka-özel zorunlu
alanı eksikse o bankayı **submit-öncesi reddeder** (B kararı, sabit form büyümez). Banka async
çalışanıyla inceler (banka operasyon MCP'si AgentPlatform'da — 091 DIŞI); sonuç PG'ye **banka→PG
HMAC-imzalı, secret-token kapılı, dayanıklı webhook** ile iner (approve+credential / reject+reason).
**İlk onayda store `Live`** → `CreatePaymentLink` artık Live store şartı arar (089/090 gerçek charge
açılır). Per-(store,banka) credential RUNTIME geldiğinden user-secrets'a konamaz → **at-rest şifreli
saklanır (DEK secret-store'da)**, aggregate yalnız opak `CredentialRef` tutar; credential hiçbir
dönüş/log/DTO'da görünmez (İlke VI). Kaçan sonuç `Process/OnboardingReconciliation` watchdog ile
yakalanır. Komisyon YOK; PG yalnız durumu yansıtır (banka otorite).

## Technical Context

**Language/Version**: C# / .NET 10 (`Nullable`+`ImplicitUsings` açık)

**Primary Dependencies**: Wolverine (bus + RabbitMQ fanout + `[Transactional]` outbox + durable local queue retry), Marten (Postgres doküman), Scrutor (DI marker), Minimal API, `System.Security.Cryptography` (HMACSHA256 webhook imza + AES at-rest credential)

**Storage**: Marten/Postgres (`PaymentSchemaName`). `StoreOnboarding` yeni düz doküman (aynı Payment BC/DB); `BankApplication` onun altında nested child koleksiyon. Per-store banka credential AYRI şifreli doküman (ciphertext DB'de, DEK secret-store'da) — aggregate'te değil, opak `CredentialRef`. PAN yok (İlke VI, bu feature kart işlemez).

**Testing**: xUnit, saf domain birim testleri (host/entegrasyon harness yok). Domain-TDD: `StoreOnboarding` davranışı (SubmitProfile/RecordBankOutcome/ilk-onay→Live/resubmit) + `StoreProfile` VO + reconcile karar mantığı test-first; adapter/webhook/endpoint/hosted-form test-sonra/canlı.

**Target Platform**: Linux/container, .NET Aspire AppHost (Postgres + RabbitMQ); tek servis izole çalışmaz.

**Project Type**: Tek Payment BC web-service (`src/services/Payment.Api`) + test projesi. `StoreOnboarding` yeni aggregate, AYNI serviste/DB'de (ayrı servis/BC DEĞİL — gerekçe Structure Decision).

**Performance Goals**: Aktivasyon async (insan-hızı); form→submit anlık algı; banka onayı dakikalar-saatler (banka tarafı). Ölçek: bugün tek store (ECommerceAgent), mimari çok-store.

**Constraints**: Yalnız TRY. Credential + callback sırları yalnız secret-store / at-rest şifreli; ASLA log/trace/DTO/dönüş. Banka = otorite (onay+credential bankadan); PG öngörü/yansıma. Belirsiz/sürüyor sonuç terminal sayılmaz (reconcile bekler). BankSimulator onboarding yüzeyi YOK → PG port+stub'a karşı yazılır.

**Scale/Scope**: 1 yeni aggregate (`StoreOnboarding`) + 1 child entity (`BankApplication`) + 2 VO (`StoreProfile`, `BankMerchantCredential`) + onboarding port + stub/simulator onboarding adapter (banka başına) + credential at-rest store (abstraction + şifreli impl) + 5 slice (create/open-form/submit/callback-receive/status) + inbound webhook endpoint + outbound "activated" webhook mesaj/handler + reconcile Process + hosted form sayfası + `CreatePaymentLink` Live-guard (089/090 köprüsü) + ~4 Options + resource sabitleri + testler + FLOW.md.

## Constitution Check

*GATE: Phase 0 öncesi geçmeli; Phase 1 sonrası yeniden denetlenir.*

- **I. BC İzolasyonu** ✅ `StoreOnboarding` AYNI Payment BC içinde yeni aggregate (ayrı DB/servis değil); store-Live durumu charge yetkilendirmesinin parçası, aynı tutarlılık sınırı. Banka = BC değil, onboarding adapter arkası dış sistem; wire domain sınırını geçmez. Banka operasyon MCP'si ayrı repo (AgentPlatform). Store'a bildirim `Shared` event + imzalı webhook (DB erişimi değil). **GEÇER.**
- **II. Zengin Domain** ✅ `StoreOnboarding : AggregateRoot`; `BankApplication` child entity (base almaz); koleksiyon private + `IReadOnlyList`, mutasyon yalnız `SubmitProfile`/`RecordBankOutcome`/`Resubmit`. `StoreProfile`/`BankMerchantCredential` VO (record + private ctor + static `Create`). `OnboardingStatus`/`BankApplicationStatus` enum aggregate dosyasında. İlk-onay→Live invariant'ı aggregate'te. **GEÇER.**
- **III. VSA + CQRS** ✅ create/open-form/submit/callback-receive = `Features/Commands` (`[Transactional]`, `IDocumentSession`+`IMessageBus`); status = `Features/Queries`. Watchdog = `Process/OnboardingReconciliation` (BC'nin kendi dayanıklı süreci, kullanıcı-tetikli değil). Repository yok. Onboarding adapter = İlke VII, kullanan slice'ın `BankOnboarding/` klasöründe nested. **GEÇER.**
- **IV. Result** ✅ Handler `FeatureResultModel`/`FeatureObjectResultModel<T>`, aggregate `ResultDomain`; red `MessageItem.Code` resource sabiti. Banka onboarding sonucu outcome-enum (`OnboardingOutcome`: Submitted/Approved/Rejected + reason) — retry-able/red teknik `Error`'a eşlenmez. **GEÇER.**
- **V. Yetki** ✅ `POST /store-onboarding` + `/status` = `store-api-key` (X-Api-Key, mevcut handler). Form açılış/submit = anonim per-session `OpenToken` (fail-closed). Banka→PG callback = anonim per-session `CallbackToken` (path-gated) + HMAC doğrulama. PG MCP yüzeyi YOK. *(Önceki İlke V metin-drift'i — "store→gateway = platform IdP bearer" — anayasa v3.0.0 amendment'iyle KAPANDI: artık store→gateway = X-Api-Key, tek OAuth otoritesi AgentPlatform.)* **GEÇER.**
- **VI. Kart/Sır Minimizasyonu (NON-NEGOTIABLE)** ✅ Bu feature kart işlemez (PAN yok). Banka credential = sır: aggregate'e YAZILMAZ (yalnız opak `CredentialRef`); at-rest AES şifreli ayrı dokümanda, DEK secret-store'da (`CredentialEncryptionOptions.Key`). Credential + callback imza sırları ASLA log/trace/query DTO/webhook-dönüşünde. Makine-handoff: banka→PG HMAC-imzalı dayanıklı webhook (publisher ham gövde imzalar, PG aynı body'yi doğrular). Status/handle dönüşü credential İÇERMEZ. **GEÇER.** *(Runtime-secret at-rest deseni anayasa v3.1.0 amendment'iyle İlke VI'da AÇIKÇA meşru: at-rest AES + DEK secret-store + opak CredentialRef. Önceki metin-boşluğu kapandı.)*
- **VII. Port/Adapter + Anti-Corruption** ✅ Yeni ortak port `IBankOnboardingAdapter` (charge portundan AYRI; referral-submit + application-status-query + callback-normalize). Her banka adapter'ı wire'ını private nested tutar; canonical `OnboardingReferral`→wire, wire-callback→normalize `OnboardingOutcome` (anti-corruption, iki yön). Paylaşılan SDK yok. (B) banka-özel zorunlu alan eksik → adapter submit-öncesi `Rejected(reason)` döner. Adapter STATELESS; per-banka durum aggregate'te. Banka OTORİTE. **GEÇER.**
- **VIII. Spec-Driven** ✅ Tam akış, domain-TDD. **GEÇER.**
- **IX. Domain Legibility** ✅ `StoreOnboarding` yeni domain süreci → FLOW.md aynı PR'da genişler (yeni adımlar: onboard-başlat/form/fan-out/banka-sonuç/Live/reconcile; yeni edge-anchor tipler). Charge FLOW'una "Live store şartı" notu. **GEÇER.**

**Sonuç:** İlke V metin-drift'i dışında ihlal yok; o da belgeli amendment adayı (Complexity Tracking). Diğer tüm kapılar geçer.

## Project Structure

### Documentation (this feature)

```text
specs/091-merchant-lifecycle-activation/
├── spec.md
├── plan.md              # bu dosya
├── research.md          # Phase 0
├── data-model.md        # Phase 1
├── quickstart.md        # Phase 1
├── contracts/           # Phase 1 (onboarding port + webhook'lar + REST)
└── checklists/requirements.md
```

### Source Code (repository root)

```text
src/services/Payment.Api/
├── Domains/StoreOnboarding/                       # YENİ aggregate klasörü
│   ├── StoreOnboarding.cs                          # YENİ: aggregate root + OnboardingStatus/BankApplicationStatus enum + SubmitProfile/RecordBankOutcome/Resubmit
│   ├── BankApplication.cs                          # YENİ: child entity (BankCode, status, reason?, credentialRef?)
│   ├── ValueObjects/
│   │   ├── StoreProfile.cs                          # YENİ: canonical çekirdek profil VO (ünvan/vergi no/IBAN/MCC/URL/yetkili) + IBAN mod-97
│   │   └── BankMerchantCredential.cs               # YENİ: banka credential VO (transient; aggregate'e girmez, credential-store'a gider)
│   ├── BankOnboarding/                             # İlke VII — onboarding port + adapter (charge'dan AYRI, wire nested)
│   │   ├── IBankOnboardingAdapter.cs               # YENİ: port (SubmitReferralAsync + QueryApplicationStatusAsync + NormalizeCallback) + OnboardingReferral/OnboardingOutcome
│   │   └── SimulatorBankOnboardingAdapter.cs       # YENİ: stub (alan doğrula → Submitted ack; gerçek banka/BankSimulator sonra), private nested wire + normalize
│   ├── Credentials/
│   │   ├── IBankCredentialStore.cs                 # YENİ: Save(storeId,bankCode,cred)→CredentialRef / Get(ref)→cred (yalnız charge yolu)
│   │   └── EncryptedBankCredentialStore.cs         # YENİ: AES at-rest (DEK secret-store), şifreli Marten doküman; dönüşte sır yok (İlke VI)
│   ├── Features/Commands/
│   │   ├── CreateStoreOnboarding.cs                # YENİ: EC S2S başlatır → Draft + OpenToken + opak handle döner
│   │   ├── OpenOnboardingForm.cs                   # YENİ: anonim fail-closed form açılışı (OpenToken)
│   │   ├── SubmitOnboardingProfile.cs              # YENİ: profil → fan-out her banka adapter.SubmitReferral → BankApplication(Referred)/Rejected
│   │   ├── ReceiveBankOnboardingOutcome.cs         # YENİ: banka→PG webhook; token+HMAC doğrula → adapter normalize → RecordBankOutcome + credential-store + ilk-onay publish
│   │   └── OnboardingResultDelivery.cs             # YENİ: store'a "Live/sonuç" HMAC-imzalı dayanıklı webhook (StoreResultDelivery aynası)
│   ├── Features/Queries/
│   │   └── QueryOnboardingStatus.cs                # YENİ: opak handle + özet durum (credential/sır YOK)
│   ├── Process/
│   │   └── OnboardingReconciliation.cs             # YENİ: watchdog (bekleyen başvuru durum-sorgu; InDoubtReconciliation aynası)
│   ├── StoreOnboardingEndpointExtension.cs         # YENİ: 2 store [store-api-key] + 2 anonim(form) + 1 callback(token+HMAC) uç
│   └── StoreOnboardingPages.cs                     # YENİ: BuildOnboardingFormPage (tek sabit hosted form, PaymentLinkPages aynası)
├── Domains/PaymentLinks/Features/Commands/
│   └── CreatePaymentLink.cs                        # DEĞİŞİR: Live StoreOnboarding şartı guard'ı (FR-010 köprüsü; yoksa STORE_NOT_LIVE)
├── Options/
│   ├── StoreOnboardingOptions.cs                   # YENİ: Ttl + FormBaseUrl + form metinleri + BankCodes[] (fan-out hedefi)
│   ├── OnboardingStoreCallbackOptions.cs           # YENİ: EC'ye "Live" bildirim CallbackUrl(appsettings) + CallbackSecret(secret-store)
│   └── CredentialEncryptionOptions.cs              # YENİ: Key (SIR, secret-store) — at-rest AES DEK
│   # Banka→PG inbound webhook imza sırrı: InterVposBankOptions.InterVposBankProfile'a InboundCallbackSecret(SIR) alanı EKLENİR (banka başına)
├── Constants/StoreOnboardingResourceConstants.cs   # YENİ: ONBOARDING_NOT_FOUND/LINK_EXPIRED/INVALID_PROFILE/BANK_FIELD_MISSING/ALREADY_SUBMITTED/DUPLICATE_ONBOARDING/CALLBACK_REJECTED/STORE_NOT_LIVE
├── Program.cs                                       # DEĞİŞİR: 3 Options + onboarding adapter fan-out (banka başına) + credential-store DI + Discovery.IncludeType (yeni *Handler'lar) + Marten index (OpenToken/CallbackToken/MerchantId unique)
├── FLOW.md                                          # DEĞİŞİR: onboarding süreci + Live-gate notu
└── appsettings.json                                # DEĞİŞİR: StoreOnboarding/OnboardingStoreCallback section + BankCodes

src/others/Common/                                   # mevcut auth yeniden kullanılır (yeni dosya yok)
└── Extensions/ApiKeyAuthentication.cs               # store-api-key policy (mevcut) — yeni uçlara uygulanır

tests/Payment.Api.Tests/
├── StoreOnboardingTests.cs                          # YENİ: SubmitProfile fan-out + RecordBankOutcome + ilk-onay→Live + çift-onay no-op + resubmit + red izolasyonu
├── StoreProfileTests.cs                             # YENİ: VO doğrulama (zorunlu alan + IBAN mod-97)
└── OnboardingReconciliationTests.cs                 # YENİ: bekleyen başvuru yoklama + "sürüyor"da erken-terminal YOK
```

**Structure Decision**: `StoreOnboarding` = AYNI Payment BC içinde yeni aggregate (`src/services/Payment.Api/Domains/StoreOnboarding/`), ayrı servis/DB DEĞİL. Gerekçe: (a) constitution "tek Payment BC"; (b) store-Live durumu charge yetkilendirmesinin parçası — ayrı servis `CreatePaymentLink` Live-guard'ı için cross-BC senkron/eventing gerektirirdi (gereksiz İlke I yükü); (c) 088'de sökülen ayrı Merchant BC'ye dönmek regresyon. Onboarding port+adapter İlke VII gereği kullanan slice içinde (`Domains/StoreOnboarding/BankOnboarding/`), charge portundan (`PaymentLinks/BankCharge/`) ayrı — iki farklı domain operasyonu, paylaşılan SDK yok. Credential at-rest store `Domains/StoreOnboarding/Credentials/` (aggregate değil altyapı; sır yalnız charge yolu okur).

## Complexity Tracking

| İhlal / sapma | Neden gerekli | Reddedilen basit alternatif |
|---|---|---|
| ~~İlke V metin-drift~~ **ÇÖZÜLDÜ** (anayasa v3.0.0) | 2026-10-22 kimlik birleşimi PG IdP'yi söktü, EC→PG = paylaşılan API key. Anayasa İlke V v3.0.0'da güncellendi (store→gateway = X-Api-Key, tek OAuth = AgentPlatform). Artık sapma yok. | — (kapandı) |
| ~~Runtime credential at-rest şifreli~~ **ÇÖZÜLDÜ** (anayasa v3.1.0) | Per-(store,banka) credential RUNTIME webhook'la gelir; statik secret-store'a yazılamaz. İlke VI v3.1.0'da "runtime-teslim sır = at-rest AES + DEK secret-store + opak CredentialRef" açıkça meşrulaştırıldı. Artık sapma yok. | — (kapandı) |