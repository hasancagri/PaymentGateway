# Phase 1 Data Model: Onboarding Aggregate Sadeleştirme

Onboarding sonrası **iki** aggregate kalır. Aşağıda son hâlleri; kaldırılan tipler ayrıca listelenir.

## RegisterRequest (aggregate root — GENİŞLER)

Başvuru sürecinin tamamı. Challenge artık ayrı aggregate değil; alanları buraya gömülür.

| Alan | Tip | Not |
|------|-----|-----|
| Id | Guid | AggregateRoot |
| Domain | string | normalize/lower; mükerrer anahtarı |
| LegalName | string | descriptor'dan |
| TaxId | string | descriptor'dan |
| ContactEmail | string | descriptor'dan |
| WebhookUrl | string | descriptor'dan |
| **ChallengeToken** | string | **YENİ** (eski DomainControlChallenge.Token) |
| **ChallengeExpectedValue** | string | **YENİ** (adayın yayınlayacağı değer) |
| **ChallengeExpiresAtUtc** | DateTime | **YENİ** (~1h TTL) |
| ChallengeResult | ChallengeOutcome | mevcut; Verify sonucu |
| Status | RegisterRequestStatus | **`AwaitingDomainControl` EKLENİR** |
| ReviewedAtUtc | DateTime? | onay/ret anı |
| ReviewNote | string? | |
| CreatedMerchantId | Guid? | Approved'da doğan merchant |
| ExternalRef | string? | opak (FR-018) |

**Status enum** (`RegisterRequestStatus`):

```
AwaitingDomainControl = 0   # YENİ — talep doğdu, challenge henüz geçmedi
Pending               = 1   # challenge geçti, admin onayı bekleniyor
Approved              = 2
Rejected              = 3
```

> Not: `AwaitingDomainControl` yeni ilk durum. Mevcut değerler (Pending=1, Approved=2, Rejected=3)
> KORUNUR; yeni değer 0 ile eklenir (dev: DB sıfırlanır, çakışma yok).

**Davranışlar**:
- `static ResultDomain<RegisterRequest> CreateAwaiting(string domain, MerchantDescriptor descriptor, string? externalRef)`
  — AwaitingDomainControl talebi; descriptor alanları kopyalanır; ilk challenge bileti üretilir.
- `ResultDomain IssueChallenge(DateTime nowUtc)` — süre dolmuşsa yeni token/expected/expiry.
- `ResultDomain<ChallengeOutcome> VerifyChallenge(string? fetchedValue, DateTime nowUtc)` — eski
  `DomainControlChallenge.Verify` mantığı; `Passed` → `Status = Pending`.
- `ResultDomain Approve(Guid merchantId, string? note)` — yalnız `Pending` (değişmez).
- `ResultDomain Reject(string? note)` — yalnız `Pending` (değişmez).

**Invariant'lar**: `Approve`/`Reject` yalnız Pending; `AwaitingDomainControl` karar alamaz.
Challenge Verify tam-byte eşleşme + TTL; `Passed` idempotent (tekrar Verify Passed döner).

**ValueObject**: `RegisterRequests/ValueObjects/MerchantDescriptor.cs` (değişmez).

**Enum taşıma**: `ChallengeOutcome` (Pending/Passed/Failed/Expired) → `RegisterRequests/` altına
taşınır (eski namespace `DomainControlChallenges` silinir).

---

## Merchant (aggregate root — GENİŞLER)

Kalıcı sonuç. Aktivasyon bileti alanları gömülür (mevcut alanların TAMAMI korunur; yalnız
aktivasyonla ilgili yeni alanlar/metotlar eklenir).

**Mevcut alanlar** (değişmez): MerchantKey, Name, Email, Phone, CountryCode, CityCode, Mcc,
WebhookUrl, Status, TaxId, ReturnUrl, ExternalRef, HasSettlementAccount, CommissionGridReady,
ActivatedAtUtc.

**Yeni alanlar** (aktivasyon bileti):

| Alan | Tip | Not |
|------|-----|-----|
| **ActivationToken** | string | tek-kullanım teslim bileti (Guid "N"); MerchantKey'den AYRI |
| **ActivationExpiresAtUtc** | DateTime? | ~24h TTL |
| **ActivationRedeemedAtUtc** | DateTime? | redeemed-flag (doluysa kullanılmış) |

**Yeni davranışlar**:
- `void IssueActivation(DateTime nowUtc)` — `ActivationToken` + 24h expiry üretir (onayda çağrılır).
- `ResultDomain RedeemActivation(DateTime nowUtc)` — tek-kullanım (`ActivationRedeemedAtUtc` doluysa
  RET) + TTL (`nowUtc > ActivationExpiresAtUtc` → RET). Başarıda: `ActivationRedeemedAtUtc = now`,
  `Provision()` etkisi uygulanır (Status Provisioning sabit + `ActivatedAtUtc`).

**Mevcut davranışlar** (değişmez): `Create`, `CreateForOnboarding`, `SetReturnUrl`,
`MarkSettlementAccountPresent`, `MarkCommissionGridReady`, `SetExternalRef`, `TryActivate`,
`UpdateProfile`, `Activate/Deactivate/Suspend`. `Provision()` — redeem'e katlanır; ayrı public
çağıran kalmazsa private/kaldırılır (implement kararı; davranış korunur).

**Invariant'lar**: aktivasyon tek-kullanım + TTL; `TryActivate` 3-koşul (settlement + gridReady +
ReturnUrl) DEĞİŞMEZ.

**Status enum** (`MerchantStatus`): değişmez (Active/Passive/Suspended/Provisioning).

---

## Kaldırılan tipler

| Tip | Nereye |
|-----|--------|
| `DomainControlChallenge` (aggregate) | Alanlar+davranış → RegisterRequest |
| `ChallengeStatus` (enum) | SİLİNİR — RegisterRequestStatus'a erir |
| `ChallengeOutcome` (enum) | TAŞINIR → RegisterRequests namespace |
| `ActivationTicket` (aggregate) | Alanlar+davranış → Merchant |
| `ActivationTicketStatus` (enum) | SİLİNİR — redeemed/expiry alanlarına erir |
| `OnboardingNotification` (aggregate) | SİLİNİR — mail loglanır |
| `NotificationKind` / `NotificationStatus` (enum) | SİLİNİR |

## Durum akış diyagramı (RegisterRequest + Merchant)

```
submit_registration (descriptor OK)
   │
   ▼
RegisterRequest: AwaitingDomainControl ──VerifyChallenge(Passed)──▶ Pending
   │  (Failed → aynı durum; Expired → IssueChallenge)                 │
   │                                                          admin Approve
   │                                                                  ▼
   │                                              Merchant doğar (Provisioning)
   │                                              + IssueActivation (token+24h)
   │                                              RegisterRequest: Approved
   │                                                                  │
   │                                          RedeemActivation(token) │
   │                                                                  ▼
   │                                    Merchant.Provision + MerchantProvisioned
   │                                    (MerchantKey bir kez döner)
   │                                                                  │
   │                          3 koşul (settlement+grid+returnUrl) → TryActivate
   │                                                                  ▼
   │                                                       Merchant: Active
   ▼
(Reject → RegisterRequest: Rejected; merchant doğmaz)
```