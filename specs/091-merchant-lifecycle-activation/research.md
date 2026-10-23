# Phase 0 Research: Store Lifecycle / Activation

Teknik bağlamda `NEEDS CLARIFICATION` kalmadı; kararlar brainstorm kiliti + mevcut kod taraması
(089/090 PaymentLink/BankCharge/StoreResultDelivery/InDoubtReconciliation + 045 hosted-form precedent
+ Common auth) + anayasa İlke VI/VII'den türetildi. Konsolide kararlar:

## D1 — Onboarding portu charge portundan AYRI (İlke VII)

- **Decision**: Yeni `IBankOnboardingAdapter` portu; mevcut `IBankPaymentAdapter` (charge) ile paylaşılmaz. Metotlar: `SubmitReferralAsync(OnboardingReferral)` (canonical→wire, banka-özel eksik alan → `Rejected`), `QueryApplicationStatusAsync(bankApplicationRef)` (reconcile), `NormalizeCallback(rawBody)` (banka webhook→`OnboardingOutcome`, anti-corruption).
- **Rationale**: Onboarding ≠ charge (farklı domain operasyonu, farklı wire, farklı yaşam döngüsü). İlke VII "her banka adapter arkasında, wire nested, paylaşılan SDK yok". Charge adapter'ı (InterVpos/Turuncu) zaten var — simetri.
- **Alternatives considered**: Charge portunu genişletmek → iki operasyonu birbirine bağlar, tek-sorumluluk ihlali. Paylaşılan onboarding SDK → İlke VII reddi.

## D2 — (B) Yalın çekirdek + adapter submit-öncesi reddeder

- **Decision**: Canonical `OnboardingReferral` yalnız ortak çekirdeği taşır (ünvan/vergi no/IBAN/MCC/URL/yetkili). Banka-özel zorunlu alan çekirdekte yoksa adapter `SubmitReferralAsync` içinde `OnboardingOutcome.Rejected(BANK_FIELD_MISSING, reason)` döner; sabit form BÜYÜMEZ, store o bankada açılmaz, ötekilerde devam.
- **Rationale**: Brainstorm (B) kararı. Domain 3 bankanın kaprisiyle şişmez; divergence adapter'da. Tek sabit hosted form (UX + tekil bakım).
- **Alternatives considered**: Şişman superset form (A) → domain şişer. Banka-başına dinamik ek form → kullanıcı reddetti ("form sabit").

## D3 — Runtime credential at-rest şifreli (İlke VI, user-secrets değil)

- **Decision**: Per-(store,banka) banka credential'ı (ShopCode/UserCode/UserPass vb.) RUNTIME webhook'la gelir → `IBankCredentialStore` ardında AES-şifreli ayrı Marten dokümanında saklanır; DEK (`CredentialEncryptionOptions.Key`) secret-store'da. Aggregate yalnız opak `CredentialRef` (Guid) tutar. Charge yolu `Get(ref)` ile çözer. Sır ASLA log/trace/query DTO/webhook-dönüşünde.
- **Rationale**: İlke VI "sır yalnız secret-store" statik config içindir; runtime-teslim sır user-secrets'a yazılamaz. Standart çözüm: ciphertext DB'de, DEK secret-store'da. İlke VI'nın asıl niyeti (sır render/sızıntı yok + dar PCI yüzeyi) korunur.
- **Alternatives considered**: Credential'ı aggregate/düz DB'de tutmak → İlke VI ihlali. Harici Vault/KeyVault → dev/sim YAGNI (ileride impl değişir, abstraction sabit kalır). Credential'ı hiç saklamayıp her charge'da bankadan istemek → banka API'si yok, anlamsız.

## D4 — Fan-out: form submit'te banka başına BankApplication + senkron referral

- **Decision**: `SubmitOnboardingProfile` handler'ı `StoreOnboardingOptions.BankCodes` üzerinden döner; her banka adapter'ına `SubmitReferralAsync` çağırır; sonuç `Submitted`→`BankApplication(Referred)`, `Rejected`→`BankApplication(Rejected+reason)`. Aggregate `SubmitProfile(profile, bankCodes)` ile per-banka entry'leri kurar, `Status=Submitted`.
- **Rationale**: Referral senkron (anlık ack / submit-öncesi red); asıl onay async webhook'la sonra gelir. Per-banka durum aggregate'te (adapter stateless, İlke VII + İlke II).
- **Alternatives considered**: Async publish fan-out → referral ack'i anında alınamaz, submit-öncesi red UX'i kaybolur. Tek toplu başvuru → banka-başına durum izlenemez.

## D5 — Banka→PG sonuç: HMAC-imzalı, token-kapılı, dayanıklı webhook

- **Decision**: `POST /internal/onboarding/callback/{callbackToken}` (anonim, per-session `CallbackToken` path-gated + `X-Signature` HMAC-SHA256 doğrulama). Gövde `{bankCode, outcome, reason?, credential?}`. Handler: token eşleşmezse/imza geçersizse reddet (durum değişmez); geçerliyse adapter `NormalizeCallback` → `RecordBankOutcome`; credential varsa `IBankCredentialStore.Save`→`CredentialRef`; ilk onayda `PaymentActivated` publish + `OnboardingResultDelivery`. Banka outbox-retry ile gönderir.
- **Rationale**: 090 `/pay` callback (token) + `StoreResultDelivery` (HMAC) desenlerinin aynası, ters yön. İlke VI makine-handoff: credential yalnız imzalı S2S webhook gövdesinde. Idempotent (tekrar gelen onay no-op).
- **Alternatives considered**: PG→banka poll (yalnız) → gerçek-zaman kaybı, savurgan. Kimlikli callback → banka PG'de IdP istemcisi değil (anonim+token İlke V deseni). İmzasız → sahte sonuç riski.

## D6 — Reconcile watchdog: Process/ (InDoubtReconciliation aynası)

- **Decision**: `Process/OnboardingReconciliation` — bekleyen (`Referred`) başvurular için periyodik `adapter.QueryApplicationStatusAsync`; `Approved`→`RecordBankOutcome`(+credential), hâlâ sürüyor→durum değişmez (erken terminal YOK), terminal red→kaydet. `ScheduleAsync` tick.
- **Rationale**: Kaçan/geç webhook'a karşı kesinlik (FR-012). "Kullanıcı mı tetikledi, süreç mi?" → BC'nin kendi dayanıklı süreci = `Process/`. InDoubtReconciliation birebir desen.
- **Alternatives considered**: Yalnız webhook → kayıp bildirim store'u sonsuz beklemede bırakır. Yalnız poll → gerçek-zaman kaybı. İkisi (webhook ana + watchdog yedek) = 090 deseni.

## D7 — Hosted form + TTL link + anonim token (045 fikri, mekanizma webhook)

- **Decision**: `CreateStoreOnboarding` (store-api-key) tahmin-edilemez `OpenToken` + TTL üretir, opak handle + form link döner. `GET /store-onboarding/{openToken}` (anonim, fail-closed) tek sabit formu render eder (`StoreOnboardingPages.BuildOnboardingFormPage`). `POST .../submit` (anonim) profili bağlar. 045'in mail+MCP+MerchantKey teslimi KULLANILMAZ (088'de sökülü).
- **Rationale**: 089 `OpenToken` fail-closed deseni + `PaymentLinkPages` birebir. İlke V anonim+per-session token. İlke VI: credential form dönüşünde değil, webhook'la makine-handoff.
- **Alternatives considered**: EC gövdede tüm profili S2S gönderir (form yok) → kullanıcı "form kalsın" dedi. Mail teslim → Mail.Worker sökülü.

## D8 — İlk-onayda Live + CreatePaymentLink Live-guard (089/090 köprüsü)

- **Decision**: `StoreOnboarding.RecordBankOutcome` ilk `Approved`'da `Status=Live` (sonraki onaylar Live tutar, çift-aktivasyon no-op). `CreatePaymentLink` handler'ı `MerchantId` için `Live` StoreOnboarding arar; yoksa `STORE_NOT_LIVE`.
- **Rationale**: FR-009/FR-010. Aynı BC içi cross-aggregate okuma (query, İlke I-içi serbest). "Store satışa açık mı" kapısı charge'ın önüne.
- **Alternatives considered**: Tüm bankalar onay (threshold=hepsi) → kullanıcı "ilk onayda" dedi. Live-guard'ı Pay'e koymak → link üretimi zaten boşa gider, Create'te kesmek daha erken.

## D9 — Per-store credential ile charge: SEAM (bu dilimde kapsam notu)

- **Decision**: 091 credential'ı güvenli yakalar + `IBankCredentialStore` lookup seam'ini açar. Charge yolunun (`BankChargeRouter`/adapter) per-store credential KULLANMASI (bugün statik `InterVposBankProfile` yerine store'un `CredentialRef`'i) açık integration kenarı — bu dilimde Live-guard + seam kurulur; charge'ın credential'ı fiilen kullanması, gerçek ikinci store + BankSimulator onboarding geldiğinde bağlanır (tasks'ta ayrı, işaretli).
- **Rationale**: BankSimulator onboarding yüzeyi YOK → uçtan-uca credential-charge doğrulanamaz; MVP = aktivasyon döngüsü + güvenli credential + Live-gate. YAGNI + dürüst sınır.
- **Alternatives considered**: Charge'ı hemen per-store credential'a bağlamak → test edilemez (sim yok), 090'ı erken kırar. Credential'ı hiç yakalamamak → aktivasyonun anlamı kalmaz.
