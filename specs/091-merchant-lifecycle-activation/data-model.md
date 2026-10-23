# Phase 1 Data Model: Store Lifecycle / Activation

Yeni aggregate `StoreOnboarding` (aynı Payment BC/DB). `BankApplication` onun altında child entity.
`StoreProfile`/`BankMerchantCredential` VO. Banka credential aggregate'e GİRMEZ — at-rest şifreli
ayrı dokümanda (opak `CredentialRef`). PAN/kart verisi YOK (bu feature kart işlemez).

## StoreOnboarding (aggregate — YENİ)

`Domains/StoreOnboarding/StoreOnboarding.cs`, `: AggregateRoot` (Id + denetim; Marten NonPublicSetters
parametresiz ctor).

| Alan | Tip | Not |
|---|---|---|
| `MerchantId` | Guid | Store kimliği (EC'nin verdiği; `CreatePaymentLink.MerchantId` ile eşleşir) |
| `CorrelationId` | string | EC isteğinin correlation'ı (opak handle tabanı) |
| `OpenToken` | string | Form açılışının tahmin-edilemez gizli token'ı (unique index, fail-closed) |
| `CallbackToken` | string | Banka→PG webhook path kapısı (tahmin-edilemez, unique) |
| `ExpiresAt` | DateTime | Form linki TTL (lazy kontrol) |
| `Status` | `OnboardingStatus` | Draft / Submitted / Live |
| `Profile` | `StoreProfile?` | Submit'e dek null |
| `_applications` | `List<BankApplication>` | private; okuma `IReadOnlyList<BankApplication> Applications` |

**Enum'lar (aggregate dosyasında)**
```
enum OnboardingStatus { Draft = 0, Submitted = 1, Live = 2 }
enum BankApplicationStatus { Referred = 0, Approved = 1, Rejected = 2 }
```

**Davranış**
- `static ResultDomain<StoreOnboarding> Create(Guid merchantId, string correlationId, string openToken, string callbackToken, DateTime expiresAt)`
  - Zorunlu alan + `expiresAt > CreatedTime`; `Status = Draft`.
- `ResultDomain SubmitProfile(StoreProfile profile, IReadOnlyList<(string bankCode, OnboardingOutcome refResult)> fanout, DateTime now)`
  - Guard: `Status == Draft` değilse `ALREADY_SUBMITTED`; `now > ExpiresAt` → `LINK_EXPIRED` (lazy).
  - `Profile = profile`; her fanout için `BankApplication` ekle (Submitted→`Referred`, Rejected→`Rejected`+reason); `Status = Submitted`. (Handler adapter'ları çağırır, sonucu verir — aggregate wire görmez.)
- `ResultDomain RecordBankOutcome(string bankCode, OnboardingOutcome outcome, Guid? credentialRef, DateTime now)`
  - İlgili `BankApplication` yoksa `ONBOARDING_NOT_FOUND`.
  - `Approved` → app.Status=Approved + credentialRef set; **ilk Approved'da `Status=Live`** (zaten Live ise no-op, çift-aktivasyon yok).
  - `Rejected` → app.Status=Rejected + reason; diğer app'ler + store Live'lığı etkilenmez (izolasyon).
  - Idempotent: aynı banka zaten Approved ise tekrar gelen Approved no-op (`Ok`).
- `ResultDomain Resubmit(string bankCode, OnboardingOutcome refResult, DateTime now)`
  - `Rejected` bir app'i tekrar `Referred`'a çeker (yeni referral); diğerleri değişmez.

**Değişmezler**
- İlk banka onayı → `Live` (tek-yön; Live'dan Draft/Submitted'a dönülmez bu dilimde).
- Per-banka durum izole: bir bankanın red/onayı ötekini + store Live'lığını (≥1 Approved varsa) etkilemez.
- Credential aggregate'te DEĞİL — yalnız `CredentialRef` (Guid). Sır taşınmaz (İlke VI).
- Koleksiyon private; mutasyon yalnız yukarıdaki metotlardan.

**Durum geçişi**
```
Draft ─SubmitProfile→ Submitted ─RecordBankOutcome(ilk Approved)→ Live
                         │                                          │
                         ├─RecordBankOutcome(Rejected)→ Submitted   ├─RecordBankOutcome(Approved, n.banka)→ Live (kalır)
                         └─Resubmit(reddedilen banka)→ Submitted    └─RecordBankOutcome(Rejected)→ Live (kalır; o banka Rejected)
```

## BankApplication (child entity — YENİ)

`Domains/StoreOnboarding/BankApplication.cs`. AggregateRoot DEĞİL; düz class (parametresiz ctor).
Yalnız `StoreOnboarding` metotlarında üretilir/değişir.

| Alan | Tip | Not |
|---|---|---|
| `BankCode` | string | Başvurulan banka |
| `Status` | `BankApplicationStatus` | Referred / Approved / Rejected |
| `ReasonCode` | string? | Red sebebi (onayda null); resource sabiti / banka normalize kodu |
| `CredentialRef` | Guid? | Onayda credential-store opak işareti; aksi null |
| `BankApplicationRef` | string? | Banka tarafı başvuru referansı (reconcile `QueryApplicationStatusAsync` için); sır değil |
| `UpdatedAt` | DateTime | |

**Değişmez**: Credential/sır İÇERMEZ (yalnız opak `CredentialRef`). Red'ler silinmez (denetim).

## StoreProfile (Value Object — YENİ)

`Domains/StoreOnboarding/ValueObjects/StoreProfile.cs`. `record` + private ctor + static `Create`.
Canonical ortak çekirdek (D2 yalın set).

- Alanlar: `LegalName`, `TaxNumber`, `Iban`, `Mcc`, `WebsiteUrl`, `ContactName`, `ContactEmail`
- `static ResultDomain<StoreProfile> Create(...)`: tüm alanlar zorunlu (boş değil); `Iban` mod-97 (memory 004 deseni); `Mcc` 4 hane; `WebsiteUrl`/`ContactEmail` basit biçim. Hata → `INVALID_PROFILE`.

## BankMerchantCredential (Value Object — YENİ)

`Domains/StoreOnboarding/ValueObjects/BankMerchantCredential.cs`. `record` + private ctor + static
`Create`. **Transient + sır** — aggregate'e girmez; webhook gövdesinden üretilir, `IBankCredentialStore`'a
yazılır (şifreli), oradan yalnız charge yolu okur.

- Alanlar (banka-genel): `ShopCode`, `UserCode`, `UserPass`, `SecretKey?`, `SubscriptionKey?` (banka başına doluluk değişir — adapter normalize eder).
- ASLA log/trace/DTO/webhook-dönüşünde. `ToString()` maskeli/override.

## Credential at-rest doküman (altyapı — YENİ, aggregate değil)

`EncryptedBankCredentialStore` tarafından yönetilen şifreli Marten dokümanı:

| Alan | Tip | Not |
|---|---|---|
| `Id` (CredentialRef) | Guid | aggregate `BankApplication.CredentialRef` buna işaret eder |
| `MerchantId` | Guid | lookup (charge: store+banka) |
| `BankCode` | string | lookup |
| `Ciphertext` | string | AES(DEK, BankMerchantCredential JSON); DEK `CredentialEncryptionOptions.Key` (secret-store) |
| `Nonce`/`Tag` | string | AES-GCM |

**Değişmez**: plaintext credential HİÇBİR sorgu/dönüş DTO'sunda materyalize olmaz; yalnız charge yolu `Get(ref)` ile deşifre eder.

## Port tipleri (İlke VII — adapter'da nested kalır; domain-görünür canonical)

- `OnboardingReferral(StoreProfile Profile, string BankCode, string CallbackUrl, string CallbackToken)` — canonical referral (banka wire adapter içinde türetilir).
- `OnboardingOutcome` — normalize sonuç VO: `Kind ∈ {Submitted, Approved, Rejected}`, `ReasonCode?`, `BankApplicationRef?`, `BankMerchantCredential?` (yalnız Approved + yalnız callback yolunda dolu). Factory: `Submitted(ref)` · `Approved(cred, ref)` · `Rejected(reasonCode)`.

## PaymentLink (DEĞİŞİR — köprü)

`CreatePaymentLink` handler'ı: `MerchantId` için `StoreOnboarding.Status == Live` sorgusu; yoksa
`FeatureResultModel` `STORE_NOT_LIVE`. Aggregate `PaymentLink` ŞEMASI değişmez (yalnız handler guard'ı).
