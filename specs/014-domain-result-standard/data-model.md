# Phase 1 Data Model: Hedef Metot Envanteri (PaymentGateway)

Kaynak: `ResultDomain` mevcut — `src/others/Common/Results/ResultDomain.cs`
(`Ok()`, `Ok(T)`, `Error(List<MessageItem>)`, `Error(MessageItem)`; alanlar `IsSuccess`, `Messages`, `Data?`).

Kapsam: **handler'dan çağrılan** ham-dönen aggregate davranış/fabrika metotları (FR-000).

## Refactor hedefleri (5 metot)

| # | Aggregate.Method | Def (file:line) | Şu an döner | Hedef | Not |
|---|------------------|-----------------|-------------|-------|-----|
| 1 | `Merchant.TryActivate()` | Merchants/Merchant.cs:181 | `bool` | `ResultDomain` | idempotent 3-koşul; false→`Error(msg)`, true→`Ok()` |
| 2 | `DomainControlChallenge.Issue()` | DomainControlChallenges/DomainControlChallenge.cs:28 | aggregate | `ResultDomain<DomainControlChallenge>` | fabrika, `Ok(data)` |
| 3 | `DomainControlChallenge.Verify()` | DomainControlChallenges/DomainControlChallenge.cs:45 | `ChallengeOutcome` | `ResultDomain<ChallengeOutcome>` | outcome-enum → `Ok(outcome)` (Karar 2) |
| 4 | `ActivationTicket.Issue()` | ActivationTickets/ActivationTicket.cs:23 | aggregate | `ResultDomain<ActivationTicket>` | fabrika, `Ok(data)` |
| 5 | `OnboardingNotification.Create()` | OnboardingNotifications/OnboardingNotification.cs:22 | aggregate | `ResultDomain<OnboardingNotification>` | fabrika, `Ok(data)` |

## Çağıran güncellemeleri (13 call-site)

| Metot | Handler (file:line) |
|-------|---------------------|
| TryActivate | Merchants/Features/Commands/SetReturnUrl.cs:37 |
| TryActivate | SettlementAccounts/Features/Commands/CreateSettlementAccount.cs:69 |
| TryActivate | ReadModels/MerchantCommissionGridReadyHandler.cs:33 |
| Challenge.Issue | RegisterRequests/Features/Agent/SubmitRegistration.cs:85 |
| Challenge.Verify | RegisterRequests/Features/Agent/SubmitRegistration.cs:93 |
| ActivationTicket.Issue | RegisterRequests/Features/Commands/ApproveRegisterRequest.cs:58 |
| OnboardingNotification.Create | RegisterRequests/Features/Agent/SubmitRegistration.cs:138 |
| OnboardingNotification.Create | RegisterRequests/Features/Commands/ApproveRegisterRequest.cs:81 |

Her call-site: sonucu `IsSuccess` kontrolü + `.Data!` açımıyla günceller. Fabrikalar başarısız
olmadığından `Ok`-yolu tek satır (`.Data!`), ama imza tek-tip.

## Test güncellemeleri (3 dosya)

| Test | Etkilenen satırlar | Metot |
|------|--------------------|-------|
| tests/Merchant.Api.Tests/MerchantOnboardingTests.cs | 54,66,81,87 | TryActivate assertion → `IsSuccess` |
| tests/Merchant.Api.Tests/DomainControlChallengeTests.cs | 9,20,22,31,33,42,44,50,52,61,63 | Issue/Verify → `.Data` açımı |
| tests/Merchant.Api.Tests/ActivationTicketTests.cs | 9,18,32,44 | Issue → `.Data` açımı |

`OnboardingNotification.Create` için test yok — davranış değişmediğinden yeni test zorunlu değil
(mevcut kapsam korunur).

## Muaf (dokunulmaz)

- `PosAccount.GetCommissionRate(int) : decimal?` — saf lookup getter (Karar 3).
- Reference.Api aggregate'leri — event-only, handler'dan çağrılan ham davranış metodu yok.
- Commission.Api aggregate'leri — envanterde handler-çağrılı ham metot çıkmadı (yalnız
  `ResultDomain`/`void` dönenler). Doğrulama: build sonrası derleyici + testler.
- Domain service (`BankRouter`), MCP tool, seeder, read-model projeksiyonları.
