# Tasks: Onboarding Aggregate Sadeleştirme (5 → 2)

**Feature**: `015-onboarding-aggregate-slim` | **Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

**Girdi**: plan.md, spec.md (US1–US3), data-model.md, contracts/onboarding-surface.md, research.md, quickstart.md

**Not**: Davranış-korumalı refactor. Testler saf domain birim testi (mevcut konvansiyon) — challenge +
aktivasyon davranışı yeni sahiplerine taşındığından test dosyaları da taşınır (silme değil, uyarlama).

**Tüm yollar** `src/services/Merchant.Api/` (kod) ve `tests/Merchant.Api.Tests/` (test) köküne görelidir.

## Dokümantasyon kuralı (çapraz-kesen — HER kod görevinde geçerli)

Eklenen/taşınan **her** aggregate metodu ve yeniden yazılan **her** handler adımı, ne işe yaradığını
anlatan **Türkçe açıklama** taşır (anayasa: yorumlar/XML dokümanları Türkçe; mevcut stil):

- Public aggregate metotları + fabrikalar → XML `<summary>` (mevcut `RegisterRequest`/`Merchant`
  dokümanlarıyla aynı ton; invariant + statü geçişi + neden açıklanır).
- Handler içi mantık adımları → mevcut numaralı satır-içi yorum deseni korunur/güncellenir
  (ör. `// 1) Descriptor'ı çek + doğrula`, `// 2) Mükerrer koruma ...`).
- Taşınan davranışta eski açıklama körü körüne kopyalanmaz; yeni sahibe/akışa göre güncellenir
  (ör. "RegisterRequest'ten ÖNCE yaşar" ifadesi artık geçersiz — challenge talebin ALANI).

Bu kural her `[US*]` kod görevinin tamamlanma koşuludur; ayrıca T021 ile bütünsel doğrulanır.

---

## Phase 1: Setup

- [X] T001 Baseline doğrula: `dotnet build` + `dotnet test tests/Merchant.Api.Tests` şu an yeşil mi (refactor öncesi referans); değilse durup nedeni not et.

---

## Phase 2: Foundational (bloklayan ön koşul)

Yok. Konsolidasyon story-bazlı ilerler; ortak bloklayan altyapı değişimi gerekmez. (US3, US1+US2'nin dokunduğu handler'ları düzenler → sıra Dependencies bölümünde.)

---

## Phase 3: US1 — Challenge RegisterRequest'e gömülür (Priority: P1) 🎯 MVP

**Story hedefi**: `DomainControlChallenge` aggregate'i silinir; challenge alan/davranışı `RegisterRequest`'e iner. Talep `AwaitingDomainControl` ile doğar, `Passed`'de `Pending`'e geçer. Dış sözleşme eklemeli (RequestId + Message + AwaitingDomainControl).

**Independent Test**: Başvuru gönder → `RegisterRequest` `AwaitingDomainControl` (token+expected taşır); değeri yayınla+tekrar → aynı talep `Pending`; `DomainControlChallenge` dokümanı yok. (quickstart S1–S4)

- [X] T002 [P] [US1] `ChallengeOutcome` enum'unu `Domains/DomainControlChallenges/DomainControlChallenge.cs`'ten yeni dosyaya taşı: `Domains/RegisterRequests/ChallengeOutcome.cs` (namespace `Merchant.Api.Domains.RegisterRequests`); `ChallengeStatus` enum'unu TAŞIMA (silinecek). Enum değerlerine kısa Türkçe açıklama yorumu koru.
- [X] T003 [US1] `Domains/RegisterRequests/RegisterRequest.cs`: `RegisterRequestStatus`'a `AwaitingDomainControl = 0` ekle (yeni ilk durum — Türkçe yorumla açıkla); challenge alanlarını ekle (`ChallengeToken`, `ChallengeExpectedValue`, `ChallengeExpiresAtUtc`), her birine ne olduğunu belirten XML/satır yorumu.
- [X] T004 [US1] `Domains/RegisterRequests/RegisterRequest.cs`: challenge davranışlarını ekle — `CreateAwaiting(string domain, MerchantDescriptor descriptor, string? externalRef)` (AwaitingDomainControl + ilk bilet), `ResultDomain IssueChallenge(DateTime nowUtc)` (süre dolmuşsa yeni token/expected/expiry), `ResultDomain<ChallengeOutcome> VerifyChallenge(string? fetchedValue, DateTime nowUtc)` (eski `DomainControlChallenge.Verify` mantığı; `Passed` → `Status=Pending`). Eski `Create(descriptor, outcome, ...)` fabrikasını kaldır/uyarla. Her metoda XML `<summary>` yaz (invariant + statü geçişi + neden; taşınan açıklamayı yeni akışa göre güncelle).
- [X] T005 [US1] `Domains/RegisterRequests/Features/Agent/SubmitRegistration.cs`: challenge'ı `RegisterRequest` üstünden yürüt — descriptor fetch (aynen) → dup kontrol (`AwaitingDomainControl`+`Pending`+`Approved`) → aynı domain için AwaitingDomainControl talebini BUL veya `CreateAwaiting` ile OLUŞTUR → süre dolmuşsa `IssueChallenge` → challenge değerini fetch et → `VerifyChallenge` → `Passed` değilse `ChallengeRequired` yanıtı (artık `RequestId` DOLU) → `Passed`'de talep zaten `Pending`. `DomainControlChallenge` `session.Store` çağrılarını kaldır. Numaralı satır-içi yorumları yeni akışa göre güncelle; sınıf `<summary>`'sini "challenge artık request alanı" olacak şekilde düzelt.
- [X] T006 [US1] `Domains/RegisterRequests/Features/Agent/RegistrationStatus.cs`: `RegistrationStatusResponse`'a `Message` (Türkçe metin) alanı ekle; `AwaitingDomainControl` dâhil güncel durumu + sıradaki adımı metinle döndür (on-demand "sürecim ne oldu?"). Yeni davranışı `<summary>` ile açıkla.
- [X] T007 [P] [US1] `McpTools/MerchantOnboardingMcpTools.cs`: `submit_registration` + `registration_status` `[Description]` metinlerini güncelle (AwaitingDomainControl + RequestId + on-demand metin; `get_merchant` değişmez).
- [X] T008 [US1] `Domains/DomainControlChallenges/` klasörünü tümüyle SİL (aggregate + `ChallengeStatus`/`ChallengeOutcome` eski konumu). Ölü `using`/referansları temizle (özellikle SubmitRegistration + RegisterRequest).
- [X] T009 [P] [US1] `tests/Merchant.Api.Tests/DomainControlChallengeTests.cs` içeriğini `RegisterRequestTests.cs`'e uyarla (Verify Passed/Failed/Expired + AwaitingDomainControl→Pending geçişi + IssueChallenge yeniden-üretim); eski test dosyasını sil.

**Checkpoint US1**: `dotnet build` 0 hata; challenge testleri RegisterRequest üstünde yeşil; quickstart S1–S4 canlı geçer.

---

## Phase 4: US2 — ActivationTicket Merchant'a gömülür (Priority: P2)

**Story hedefi**: `ActivationTicket` aggregate'i silinir; aktivasyon key/süre/kullanıldı `Merchant`'a iner; redeem Merchant üstünden yürür. Tek-kullanım + TTL invariant'ı korunur.

**Independent Test**: Talebi onayla → merchant Provisioning + `ActivationToken`/`ExpiresAt` dolu; redeem → key bir kez döner, ikinci redeem RET; `ActivationTicket` dokümanı yok. (quickstart S5–S6)

- [X] T010 [US2] `Domains/Merchants/Merchant.cs`: aktivasyon alanlarını ekle (`ActivationToken`, `ActivationExpiresAtUtc`, `ActivationRedeemedAtUtc` — her birine Türkçe alan yorumu); `void IssueActivation(DateTime nowUtc)` (token Guid"N" + 24h) ve `ResultDomain RedeemActivation(DateTime nowUtc)` (tek-kullanım + TTL; başarıda redeemed işaretle + `Provision()` etkisi). `Provision()`'ı redeem'e katla veya private yardımcıya indir. Her metoda XML `<summary>` (invariant + neden).
- [X] T011 [US2] `RedeemActivation.cs`'i `Domains/ActivationTickets/Features/Commands/` konumundan `Domains/Merchants/Features/Commands/RedeemActivation.cs`'e taşı; handler `ActivationTicket` sorgusu yerine `Merchant`'ı `ActivationToken` ile bulsun → `merchant.RedeemActivation(now)` → başarıda `MerchantProvisioned` yayınla, `MerchantKey` bir kez döndür. Endpoint (`/activation/redeem`, `AdminPlaneOnly`+`merchant.write`) + `Produces` aynen korunur. Sınıf `<summary>` + satır-içi yorumları yeni akışa (Merchant üstünden redeem) göre güncelle.
- [X] T012 [US2] `Domains/RegisterRequests/Features/Commands/ApproveRegisterRequest.cs`: `ActivationTicket.Issue(merchant.Id)` + `session.Store(ticket)` yerine `merchant.IssueActivation(DateTime.UtcNow)`; aktivasyon mailindeki `activationToken` artık `merchant.ActivationToken`'dan gelir. `ActivationTicket` `using`/referanslarını kaldır. İlgili numaralı yorumu ("3) Aktivasyon bileti + link") güncelle.
- [X] T013 [US2] `Domains/ActivationTickets/` klasörünü tümüyle SİL (aggregate + `ActivationTicketStatus` + eski Features konumu). Ölü referansları temizle.
- [X] T014 [P] [US2] `tests/Merchant.Api.Tests/ActivationTicketTests.cs` içeriğini `MerchantTests.cs`'e uyarla (IssueActivation + RedeemActivation tek-kullanım/TTL/başarı → Provision); eski test dosyasını sil.

**Checkpoint US2**: `dotnet build` 0 hata; aktivasyon testleri Merchant üstünde yeşil; quickstart S5–S7 canlı geçer.

---

## Phase 5: US3 — OnboardingNotification silinir (Priority: P3)

**Story hedefi**: `OnboardingNotification` aggregate'i silinir; onboarding mailleri gönderilir + loglanır, ayrı durum kaydı yazılmaz.

**Independent Test**: Başvuru/onay mail tetikler → mail Mailpit'e düşer, sonuç loglanır, `OnboardingNotification` dokümanı OLUŞMAZ. (quickstart S9)

**Bağımlılık**: US1 (SubmitRegistration) + US2 (ApproveRegisterRequest) aynı handler'lara dokunur → US3 onlardan SONRA.

- [X] T015 [US3] `Domains/RegisterRequests/Features/Agent/SubmitRegistration.cs` `NotifyAdminAsync`: `OnboardingNotification.Create/MarkSent/MarkFailed` + `session.Store(notification)` kaldır; yalnız `mail.SendAsync` + sonuç `ILogger` (başarısızlıkta `LogWarning`). Mail best-effort — bunu yorumla belirt (akış kesilmez).
- [X] T016 [US3] `Domains/RegisterRequests/Features/Commands/ApproveRegisterRequest.cs` `SendActivationMailAsync`: aynı şekilde `OnboardingNotification`'ı kaldır, yalnız gönder + logla; yorumu güncelle.
- [X] T017 [US3] `Domains/OnboardingNotifications/` klasörünü tümüyle SİL (aggregate + `NotificationKind`/`NotificationStatus`). Ölü `using Merchant.Api.Domains.OnboardingNotifications;` referanslarını (Submit + Approve) temizle.

**Checkpoint US3**: `dotnet build` 0 hata; `OnboardingNotification` referansı kalmadı.

---

## Phase 6: Polish & Cross-Cutting

- [X] T018 Yapısal doğrulama: `grep -rlE "class .*: AggregateRoot" src/services/Merchant.Api/Domains` yalnız `RegisterRequest.cs` + `Merchant.cs` + `MerchantSettlementAccount.cs` döner; silinen 3 klasör yok (SC-001).
- [X] T019 `dotnet build` 0 hata + `dotnet test tests/Merchant.Api.Tests` tümü yeşil (ölü kod/referans yok) (SC-004).
- [ ] T020 Aspire ile canlı quickstart S1–S9 (özellikle S3 aynı-talep Pending, S6 ikinci-redeem RET, S7 TryActivate→Active, S9 notification yok) elle doğrula (SC-003); `MerchantProvisioned` + `MerchantStatusChanged(Active)` consumer log'unda "Successfully processed", "No known handler" yok.
- [X] T021 Dokümantasyon geçişi: eklenen/taşınan her aggregate metodu (`CreateAwaiting`/`IssueChallenge`/`VerifyChallenge`/`IssueActivation`/`RedeemActivation`) XML `<summary>` taşıyor; yeniden yazılan handler adımları (SubmitRegistration/RedeemActivation/ApproveRegisterRequest) güncel Türkçe yorumlu; taşınan eski açıklamalarda artık geçersiz ifade ("RegisterRequest'ten ÖNCE yaşar", "ayrı bilet" vb.) kalmadı.

---

## Dependencies

- **Setup (T001)** → her şeyden önce (baseline).
- **US1 (T002–T009)** → MVP; bağımsız başlar. Sıra: T002 → T003 → T004 → T005/T006 → T007/T008/T009.
- **US2 (T010–T014)** → US1'den bağımsız (farklı aggregate/dosyalar) ama T012 `ApproveRegisterRequest`'i düzenler; US1 ile çakışmaz (US1 Approve'a dokunmaz). Sıra: T010 → T011 → T012 → T013 → T014.
- **US3 (T015–T017)** → US1 (SubmitRegistration) + US2 (ApproveRegisterRequest) SONRASI (aynı handler'lar, çakışma önleme).
- **Polish (T018–T021)** → tüm story'ler sonrası.

## Parallel opportunities

- US1 içi `[P]`: T002 (enum taşı), T007 (MCP metin), T009 (test) — farklı dosyalar.
- US2 içi `[P]`: T014 (test) diğer US2 kod tamamlanınca.
- US1 ve US2 büyük ölçüde paralel yürütülebilir (ayrı aggregate/handler); tek kesişim `ApproveRegisterRequest` yalnız US2'de düzenlenir. US3 ikisinin ardından tek pass.

## Implementation strategy

- **MVP = US1** (challenge → RegisterRequest): en büyük "envai çeşit aggregate" azalışı; süreç tek statü enum'undan okunur. Tek başına teslim edilebilir + test edilebilir.
- **Artımlı**: US1 → US2 → US3 sırayla; her checkpoint'te build+test yeşil tutulur. Dev DB migration yok — şema değişiminde sıfırlanır.
- **Yorum kuralı** (yukarıdaki Dokümantasyon kuralı) her kod görevinin parçasıdır; T021 bütünsel kapanış.