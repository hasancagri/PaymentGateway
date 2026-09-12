---
description: "Task list — Hosted Kart Saklama (040, store 075 hizalama)"
---

# Tasks: Hosted Kart Saklama — Store 075 Hizalama

**Input**: `specs/040-hosted-card-storage-075/` (plan/spec/research/data-model/contracts/quickstart)

**Tests**: İlke VI (Domain-TDD) → yalnız saf domain (`CardSession` davranışı) test-first ZORUNLU;
handler/endpoint/iyzico-wire test-sonrası/canlı sandbox doğrulama.

**Kapsam:** Payment BC. iyzico çağrıları mevcut V2 motoru (`Utils/*V2`, JSON+HMAC) uzatılarak yapılır
(SDK yok). Dış yüzey `contracts/pg-endpoints.md` (store 075 `pg-card-contract.md` ile birebir).

---

## Phase 1: Setup

- [X] T001 iyzico Checkout Form + Card Storage uç sabitleri + resource alanları ekle `src/services/Payment.Api/Utils/ProviderConstants.cs` + `Utils/ProviderResourceV2.cs` (checkoutform initialize/retrieve, cardstorage card list/delete yolları)
- [X] T002 [P] Kart hata kodu sabitleri (CARD_SESSION_FAILED, CARD_NOT_FOUND, PROVIDER_UNAVAILABLE, CARD_SESSION_EXPIRED) ekle `src/services/Payment.Api/Constants/PaymentResourceConstants.cs`
- [X] T003 `CardSession` Marten şema kaydı + paymentDb index (Id) `src/services/Payment.Api/Program.cs`

---

## Phase 2: Foundational (Blocking)

**⚠️ Bu faz bitmeden US başlayamaz.**

- [X] T004 [P] [Domain-TDD] `CardSession` davranış testleri (ÖNCE, FAIL etmeli): Start→Pending; Complete boş-cardUserKey RED + Pending→Completed tek-sefer; Fail→Failed; IsUsable(ttl) `tests/Payment.Api.Tests/CardVault/CardSessionTests.cs`
- [X] T005 `CardSession.cs` aggregate (Start/Complete/Fail/IsUsable; Id=conversationId, MerchantId, CheckoutFormToken, CardUserKey?, Status, CreatedAt) `src/services/Payment.Api/Domains/StoredCards/CardSession.cs` (T004 yeşile)
- [X] T006 [P] iyzico Checkout Form **initialize** wire (V2 HMAC): kart-kaydet + nominal + callbackUrl → paymentPageUrl+token `src/services/Payment.Api/Utils/` (RestHttpClientV2/HashGeneratorV2 yeniden kullan)
- [X] T007 [P] iyzico Checkout Form **retrieve** wire: token → cardUserKey + cardToken(lar) + gösterim `src/services/Payment.Api/Utils/`
- [X] T008 [P] iyzico **Card list** (cardUserKey) + **Card delete** (cardUserKey, cardToken) wire `src/services/Payment.Api/Utils/`
- [X] T009 `StoredCard.cs` uyarlama: `CardUserKey` per-USER (R2 gruplama) + handler `CompleteCardSession`'dan yazılır (Create remarks güncelle) `src/services/Payment.Api/Domains/StoredCards/StoredCard.cs`
- [X] T010 075 uç grubu iskeleti: `.../vault/card-sessions` + `.../vault/cards` grup, OpenIddict **CardsWrite scope + merchant_id claim** koruması (merchant path YOK; claim'den çözülür — store Bearer taşır). Çözülen merchantId sahiplik kontrolünün (FR-012) temeli: userHandle→`StoredCard.MerchantId` = claim merchant_id `src/services/Payment.Api/Domains/StoredCards/StoredCardEndpointExtension.cs`

**Checkpoint**: CardSession + iyzico wire + uç grubu hazır; derlenir.

---

## Phase 3: User Story 1 - Hosted kart ekleme (P1) 🎯 MVP

**Goal**: Kullanıcı iyzico hosted formunda kart kaydeder; cardUserKey (pgUserHandle) döner.
**Independent Test**: quickstart Senaryo 1 (add-session→addUrl→form→complete→pgUserHandle; PAN yok).

- [X] T011 [US1] `StartCardSession` slice: iyzico CF initialize → `{addUrl, conversationId}`; opsiyonel `userHandle` (R2) iletilir; `CardSession(Pending)` yaz `src/services/Payment.Api/Domains/StoredCards/Features/Commands/StartCardSession.cs`
- [X] T012 [US1] `POST /vault/card-sessions` ucu (X-Api-Key) → StartCardSession `src/services/Payment.Api/Domains/StoredCards/StoredCardEndpointExtension.cs`
- [X] T013 [US1] `CompleteCardSession` slice: conversationId→CardSession çöz→CF retrieve→cardUserKey; `StoredCard`(lar) yaz + `CardSession.Complete`; iptal/hata→`Fail` kayıt yok (FR-002) `.../Features/Commands/CompleteCardSession.cs`
- [X] T014 [US1] `GET /vault/card-sessions/{conversationId}` (JWT'siz, tek-kullanımlık) → CompleteCardSession → `{status, pgUserHandle}` `.../StoredCardEndpointExtension.cs`

**Checkpoint**: Kart ekleme uçtan uca (görünürlük US2).

---

## Phase 4: User Story 2 - Kartları listele (P1)

**Goal**: userHandle ile canlı kart listesi (marka+son4+skt+alias+cardHandle).
**Independent Test**: quickstart Senaryo 2.

- [X] T015 [US2] `ListCards` query: userHandle → iyzico Card list → `CardView`; handle yoksa boş; erişilemez→PROVIDER_UNAVAILABLE. **Tenant (FR-012): userHandle çağıran merchant'a ait olmalı** (fail-closed) `.../Features/Queries/ListCards.cs`
- [X] T016 [US2] `GET /vault/cards?userHandle=` ucu (X-Api-Key) → ListCards `.../StoredCardEndpointExtension.cs`

**Checkpoint**: Ekle+listele MVP.

---

## Phase 5: User Story 3 - NON-3D taksitsiz çekim (P2)

**Goal**: userHandle+cardHandle ile NON-3D tek çekim; idempotent.
**Independent Test**: quickstart Senaryo 3.

- [X] T017 [US3] `ChargePayment` girdisini vault-token→`userHandle`+`cardHandle`'a çevir; `installment` kaldır; NON-3D (iyzico paymentCard{cardUserKey,cardToken}); idempotency correlationKey KORUNUR. **Tenant (FR-012): userHandle çağıran merchant'a ait olmalı.** **Geçiş (I1): eski vault-token/taksitli çağıranı ara (grep) — kalmadıysa temiz kaldır, kaldıysa hizala** `src/services/Payment.Api/Domains/Payments/Features/Commands/ChargePayment.cs`
- [X] T018 [US3] `POST /merchants/{merchantId}/payments` gövdesini handle'a hizala (039 yüzeyi evrilir); yanıt `{status, pgPaymentId}` `src/services/Payment.Api/Domains/Payments/PaymentEndpointExtension.cs`

**Checkpoint**: Saklı kartla NON-3D çekim.

---

## Phase 6: User Story 4 - Kart sil (P2)

**Goal**: userHandle+cardHandle ile silme; handle-scoped.
**Independent Test**: quickstart Senaryo 4.

- [X] T019 [US4] `DeleteCard` slice: sahiplik (cardHandle userHandle listesinde) → iyzico Card delete → `{deleted}`; yerel StoredCard soft-revoke. **Tenant (FR-012): userHandle çağıran merchant'a ait olmalı** (fail-closed) `.../Features/Commands/DeleteCard.cs`
- [X] T020 [US4] `DELETE /vault/cards` ({userHandle, cardHandle}) ucu (X-Api-Key) → DeleteCard `.../StoredCardEndpointExtension.cs`

**Checkpoint**: Ekle/listele/çek/sil tam.

---

## Phase 7: User Story 5 - Eski PAN-POST söküm (P3)

**Goal**: TokenizeCard (PAN) + token-RevokeCard kaldırılır (SC-006).
**Independent Test**: quickstart Senaryo 6 (grep TokenizeCard = 0).

- [X] T021 [US5] `TokenizeCard.cs` (PAN-POST) SÖK + `POST /vault/cards` uç kaydını kaldır `src/services/Payment.Api/Domains/StoredCards/Features/Commands/TokenizeCard.cs`
- [X] T022 [US5] token-bazlı `RevokeCard.cs` (`DELETE /vault/cards/{token}`) SÖK (silme handle ile DeleteCard'a taşındı) `src/services/Payment.Api/Domains/StoredCards/Features/Commands/RevokeCard.cs`
- [X] T023 [US5] Kalan referansları temizle (GlobalUsings/EndpointExtension/testler) + `dotnet build` yeşil

---

## Phase 8: Polish & Cross-Cutting

- [X] T024 [P] `check-claude-spec-links.sh` için BC haritasına 040 spec yolu (gerekiyorsa) + CLAUDE.md kart yüzeyi notu güncelle `CLAUDE.md`
- [X] T025 [P] İzolasyon/uyum denetimi: `grep -rn "TokenizeCard\|vaultToken\|installment" src/services/Payment.Api --include=*.cs` beklenen artık yok/handle; PAN kabul eden uç 0
- [X] T026 `dotnet build` + `dotnet test` (CardSession domain testleri dahil) yeşil
- [ ] T027 quickstart 6 senaryo iyzico sandbox + store 075 ile canlı doğrula (BLOKE: iyzico sandbox creds + Aspire + store birlikte)
- [ ] T028 (BLOKE: Marten integration/E2E harness YOK — constitution E2E harness henüz kurulmadı; izolasyon 4 handler'da MerchantId-filtreli, yapısal garanti + T027 canlı) [İzolasyon — FR-012/SC-005] Multitenant/kullanıcı izolasyon doğrulaması: merchant A'nın X-Api-Key'iyle merchant B'ye (veya başka kullanıcıya) ait userHandle/cardHandle ile list/delete/charge → fail-closed red + B etkilenmez. Handler-seviyesi (userHandle→StoredCard.MerchantId eşleşmesi) `tests/Payment.Api.Tests/CardVault/CardVaultIsolationTests.cs`

---

## Dependencies & Execution Order

- **Setup (P1)** → **Foundational (P2, BLOKLAR)** → US1..US5 → **Polish**.
- US1 (add) + US2 (list) = MVP. US3 (charge) US1'e bağlı (kayıtlı kart). US4 (delete) US1+US2. US5 (söküm) US1-US4 yeni yol çalışınca.
- T028 (izolasyon, FR-012/SC-005) list/delete/charge (T015/T017/T019) bittikten sonra; tenant sahiplik kontrolü onlarda gömülü.
- İlke VI: `CardSession` testleri (T004) implementasyondan (T005) ÖNCE, FAIL.

### Parallel Opportunities

- Setup: T002 [P]. Foundational: T004 [P] (test), T006/T007/T008 [P] (ayrı wire dosyaları) — T005 T004 sonrası, T009/T010 wire sonrası.
- Polish: T024, T025 [P].

## Implementation Strategy

**MVP (US1+US2):** Setup → Foundational → add + list → DUR & doğrula (kart ekle/gör, PAN yok) → demo.
**Incremental:** MVP → US3 (çekim) → US4 (sil) → US5 (söküm).

## Notes

- iyzico bu repoda impl edilir (PG'nin İÇİ); mağaza yalnız sözleşmeyi tüketir.
- Kart migrasyonu yok (sandbox/demo). cardUserKey per-user gruplama R2 kararı.
- Her task/grup sonrası commit; checkpoint'te story bağımsız doğrula.