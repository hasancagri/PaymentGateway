---
description: "Task list — 093 Taksitli Ödeme (per-taksit en avantajlı banka)"
---

# Tasks: Taksitli Ödeme

**Input**: `/specs/093-installment-payment/spec.md` + plan `~/.claude/plans/dreamy-gathering-crab.md`

**Tests**: Domain-TDD — saf hesap (router per-taksit seçim, vade farkı, RateFor) test-first; endpoint/page/adapter test-sonra.

**Organization**: US1 (quote+göster) + US2 (seçilen taksitle öde) = MVP. 092 cost-routing absorbe (yarım kırık onarılır).

## Path Conventions

Tek Payment BC: `src/services/Payment.Api/`, testler `tests/Payment.Api.Tests/`.

---

## Phase 1: Setup / Foundational

- [X] T001 `BankCommissionRate` read-model'i kart-ailesi boyutuna reshape: `List<CommissionRateRow{Scheme,CardType,Installment,Rate}>` + `DefaultCommissionRate` + `RateFor(scheme,cardType,installment)` (en spesifik→gevşek→default) `src/services/Payment.Api/Domains/AcquirerRates/BankCommissionRate.cs`
- [X] T002 [P] `bank-commission-seed.json` kart-ailesi satırlarıyla doldur (MAVI/SARI/TURUNCU × şema × tip × {1,2,3,6,9}; per-taksit avantaj farklı bankada olacak şekilde) `src/services/Payment.Api/Domains/AcquirerRates/bank-commission-seed.json`
- [X] T003 [P] `PaymentLinkOptions`'a `InstallmentCounts` (ör. [1,2,3,6,9]) ekle `src/services/Payment.Api/Options/PaymentLinkOptions.cs` + appsettings.json
- [X] T004 `PosAccountOptions` CostPriority çıkışını tamamla (092 yarım); appsettings.json'dan CostPriority kaldır (capability kalır) `src/services/Payment.Api/Options/PosAccountOptions.cs` + appsettings.json

---

## Phase 2: US1 — Taksit seçeneklerini + en avantajlı bankayı hesapla/göster (P1) 🎯 MVP

- [X] T005 [US1] `BankChargeRouterTests` (test-first): per-taksit min-oran seçimi (X 2-takside, Y 4-takside); on-us yalnız eşit oranda; yetenek eleme; tanımsız oran→default `tests/Payment.Api.Tests/BankChargeRouterTests.cs`
- [X] T006 [US1] `BankChargeRouter`: `RoutingCandidate.EffectiveCost` (decimal); `SelectCandidates(pan, binCards, amount, installment, rates)` → aile+taksit oranı×tutar, `OrderBy(EffectiveCost).ThenByDescending(IsOnUs).ThenBy(FallbackOrder).ThenBy(BankCode)`; `RouteAsync(request, binCards, rates, ct)` (092 imza onarımı); `SelectThreeDAdapter` fallback sırası `src/services/Payment.Api/Domains/PaymentLinks/BankCharge/BankChargeRouter.cs`
- [X] T007 [US1] Quote saf kısımları test edildi: per-taksit en-avantajlı banka + EffectiveRate `BankChargeRouterTests` (Select_PerTaksit_*), vade farkı `FinalAmount=Amount×(1+oran)` `PaymentLinkTests` (ApplyInstallment). QuoteInstallments handler = glue (Marten session) → test-sonra/canlı (ayrı dosya YOK).
- [X] T008 [US1] `QuoteInstallments` query+handler: openToken+bin → link(Amount) + BinCard aile → her taksit için router aday seç (min) → `{installment, acquirerBankCode, acquirerBankName, totalAmount, monthlyAmount}` listesi `src/services/Payment.Api/Domains/PaymentLinks/Features/Queries/QuoteInstallments.cs`
- [X] T009 [US1] Endpoint `GET /payment-links/{openToken}/installments?bin=` [anon] + Discovery.IncludeType `PaymentLinkEndpointExtension.cs` + `Program.cs`
- [X] T010 [US1] Hosted sayfa: kart ilk-6 `onblur` → installments fetch → radio "N taksit · {Banka} · aylık X · toplam Y"; seçili `installmentCount` pay gövdesine `PaymentLinkPages.cs`

**Checkpoint**: BIN girilince per-taksit en-avantajlı banka + taksitli tutar ekranda.

---

## Phase 3: US2 — Seçilen taksitle o bankadan öde (P1) 🎯 MVP

- [X] T011 [US2] `PaymentLink`: `InstallmentCount` + `FinalAmount` alanları; Pay anında set; `RecordAttempt`/çekim FinalAmount üzerinden (Create imzası korunur) `src/services/Payment.Api/Domains/PaymentLinks/PaymentLink.cs`
- [X] T012 [US2] `BankChargeRequest`'e `InstallmentCount` ekle `src/services/Payment.Api/Domains/PaymentLinks/BankCharge/IBankPaymentAdapter.cs`
- [X] T013 [US2] `PayCardBody` + `PayPaymentLink.Command`'a `InstallmentCount`; handler: seçilen taksitin acquirer+FinalAmount'unu SUNUCUDA yeniden hesapla (quote mantığı) → `RouteAsync(request, binCards, rates, ct)`; **failover YOK** (tek acquirer; red→başarısız) `src/services/Payment.Api/Domains/PaymentLinks/Features/Commands/PayPaymentLink.cs` + `PaymentLinkEndpointExtension.cs`
- [X] T014 [US2] `InterVposBankAdapter.BuildAuthForm` gerçek `InstallmentCount` (tek-çekim boş, >1 sayı). `Build3DForm` taksit SEAM (3DS taksit sonraki iş). `TuruncuBankAdapter` wire taksit alanı EKLENMEDİ (demo kartı MAVIBANK/InterVpos'a gider; Turuncu taksit = CodeRefactor kuyruğu) `src/services/Payment.Api/Domains/PaymentLinks/BankCharge/InterVposBankAdapter.cs`
- [X] T015 [US2] `PayPaymentLinkTests`/`PaymentLinkTests` (test-sonra kabul): seçilen taksit → doğru banka + FinalAmount; gösterilen=çekilen; failover yok `tests/Payment.Api.Tests/`

**Checkpoint**: Seçilen taksitle, gösterilen bankadan, taksitli tutarla çekim.

---

## Phase 4: Polish

- [X] T016 FLOW.md: taksit akışı (BIN→quote→seçim→çekim) + routing kuralı (per-taksit maliyet, failover yok) güncelle (İlke IX) `src/services/Payment.Api/FLOW.md`
- [X] T017 `dotnet build` 0 + `dotnet test` (yeni + 089/090/091/092 bozulmadan) + `check-flow-links.sh` yeşil; 092 derleme kırığı giderildi
- [X] T018 092 kapanış: `specs/092-cost-based-routing/spec.md` başına "ABSORBE EDİLDİ → 093" notu eklendi
- [ ] T019 Commit (093) + canlı E2E (Aspire+BankSim+dev-seed: BIN→taksit→4 taksit→Öde→Paid, FinalAmount; gün-sonu komisyon)

---

## Dependencies

- Setup (T001-T004) → US1 (T005-T010) → US2 (T011-T015) → Polish.
- `BankChargeRouter.cs` (T006), `PayPaymentLink.cs` (T013) SIRALI. `PaymentLink.cs`/`IBankPaymentAdapter.cs` additive.
- MVP = US1+US2.
