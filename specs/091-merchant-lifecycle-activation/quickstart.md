# Quickstart / Doğrulama: Store Lifecycle / Activation

Uçtan-uca doğrulama senaryoları. Canlı adımlar tüm geliştirme bitince topluca koşulur. BankSimulator
onboarding yüzeyi henüz YOK → gerçek banka onayı webhook'u elle/test-hook ile simüle edilir (D9).

## Önkoşul

- Repo kökü; `.NET 10` + Docker (Aspire Postgres + RabbitMQ).
- `StoreOnboardingOptions` (Ttl + FormBaseUrl + form metinleri + `BankCodes[]`) appsettings'te.
- `OnboardingStoreCallbackOptions.CallbackUrl` appsettings; `CallbackSecret` user-secrets.
- `CredentialEncryptionOptions.Key` user-secrets (at-rest AES DEK).
- `InterVposBankProfile.InboundCallbackSecret` (banka başına) user-secrets.
- `StoreApiKeyOptions.ExpectedKey` user-secrets (mevcut).

## Birim test (domain-TDD, önce)

```bash
dotnet test
```
Beklenen (yeni yeşil):
- `StoreOnboardingTests`: `SubmitProfile` → per-banka `Referred`; banka-özel eksik → o banka `Rejected`, diğerleri `Referred`; `RecordBankOutcome(ilk Approved)` → `Live`; ikinci banka Approved → `Live` kalır (çift-aktivasyon no-op); aynı banka tekrar Approved → no-op; `RecordBankOutcome(Rejected)` → o banka Rejected, store Live'lığı (≥1 Approved) + diğerleri değişmez; `Resubmit(reddedilen)` → `Referred`; Draft değilken submit → `ALREADY_SUBMITTED`; expired → `LINK_EXPIRED`.
- `StoreProfileTests`: zorunlu alan boş → `INVALID_PROFILE`; IBAN mod-97 geçersiz → red; MCC 4-hane dışı → red.
- `OnboardingReconciliationTests`: bekleyen başvuru Approved dönerse `RecordBankOutcome`; hâlâ sürüyor → durum DEĞİŞMEZ (erken terminal yok).
- Mevcut 089/090 testleri bozulmaz (`PaymentLink` şeması değişmez; yalnız `CreatePaymentLink` handler guard'ı → `STORE_NOT_LIVE` testi eklenir).

## Build + guard

```bash
dotnet build
scripts/check-flow-links.sh
scripts/check-claude-spec-links.sh
```
Beklenen: 0 hata; FLOW.md yeni edge-anchor tipler kodda var (`StoreOnboarding.SubmitProfile`,
`StoreOnboarding.RecordBankOutcome`, `IBankOnboardingAdapter`, `OnboardingReconciliation`,
`OnboardingResultDelivery`).

## Canlı E2E (Aspire) — toplu, en sonda

```bash
dotnet run --project src/aspire/AppHost/AppHost.csproj
```

1. **Başlat (S1):** `POST /store-onboarding` (X-Api-Key) → `pgStoreRef` + `formUrl` + `expiresAt`. Yanıtta credential/sır YOK.
2. **Form (S2):** `GET formUrl` → hosted form render. Bilinmeyen token → 404.
3. **Submit (S3):** `POST .../submit` profil → `status: Submitted`, her banka `Referred` (ya da eksik alan bankası `Rejected`).
4. **Banka onayı (S4, simüle):** `POST /internal/onboarding/callback/{callbackToken}` imzalı gövde (`Approved` + credential) → store `Live`; credential at-rest şifreli yazıldı (DB'de ciphertext, düz değil); `OnboardingResultDelivery` store CallbackUrl'e POST (imzalı, credential'sız).
5. **Red izolasyonu (S5):** ikinci banka `Rejected` callback → o banka Rejected, store `Live` kalır, status sorgusu red sebebini gösterir.
6. **Live-gate (S6):** `Live` öncesi `POST /payment-links` → `STORE_NOT_LIVE`; `Live` sonrası → link üretilir (089/090 akışı).
7. **Sır sızıntısı (S7):** tüm yanıt/log/trace taranır → banka credential + imza sırrı %0 görünür (İlke VI).
8. **Reconcile (S8):** webhook'u atlayıp banka onayı varken watchdog tick → store `Live` (kaçan sonuç toparlandı).

## Sınır (bu dilimde doğrulanmaz)

- Gerçek banka intake + çalışan-onay (BankSimulator + AgentPlatform MCP) — ayrı repo.
- Charge'ın per-store credential'ı fiilen kullanması (D9 seam) — gerçek 2. store + sim gelince.
