---
description: "Task list — Card Vault / Tokenization (017)"
---

# Tasks: Card Vault / Tokenization (Kart Saklama)

**Input**: `/specs/017-card-vault-tokenization/` (plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md)

**Tests**: Saf domain birim testleri DAHİL (anayasa: davranışlı aggregate test önceliği). Host/HTTP/
entegrasyon testi YOK — quickstart ile elle doğrulanır.

**Organization**: User story bazlı fazlar; her story bağımsız test edilebilir artış.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Paralel çalışabilir (farklı dosya, bağımlılık yok)
- **[Story]**: US1/US2/US3

## Path Conventions

Payment.Api vertical slice: `src/services/Payment.Api/Domains/<Aggregate>/Features/{Commands,Queries}`.
Vault altyapısı `src/services/Payment.Api/CardVault/`. Testler `tests/Payment.Api.Tests/`.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Proje iskeleti + paylaşılan enum/tipler

- [ ] T001 `tests/Payment.Api.Tests` xUnit projesi oluştur (yoksa), `PaymentGateway.slnx`'e ekle, Payment.Api referansı ver (mevcut `tests/Merchant.Api.Tests` şablonu)
- [ ] T002 [P] `CardBrand` enum'u oluştur (`src/services/Payment.Api/Domains/StoredCards/CardBrand.cs`): Visa, Mastercard, Amex, Troy, Unknown
- [ ] T003 [P] `StoredCardStatus` enum'u oluştur (`src/services/Payment.Api/Domains/StoredCards/StoredCardStatus.cs`): Active, Revoked

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Aggregate + kalıcılık + auth düzlemi — TÜM story'lerden önce bitmeli

**⚠️ CRITICAL**: Bu faz bitmeden hiçbir user story başlayamaz

- [ ] T004 [P] `PanTools` saf yardımcıları (`src/services/Payment.Api/CardVault/PanTools.cs`): `LuhnValidator.IsValid`, `BinExtractor.Extract`, `Last4Extractor`, `BrandDetector.Detect(pan) : CardBrand` (private helper serbest — altyapı, aggregate değil)
- [ ] T005 [P] `IPanProtector` + `DevPanProtector` (`src/services/Payment.Api/CardVault/IPanProtector.cs`, `DevPanProtector.cs`): reversible dev enc-at-rest, `ISingletonDependency`; `string Protect(string pan)` (Reveal ileride)
- [ ] T006 `StoredCard` aggregate + `Create` fabrikası (`src/services/Payment.Api/Domains/StoredCards/StoredCard.cs`): AggregateRoot; private setter; `Create(Guid merchantId, string pan, string expiry, string holderName, IPanProtector protector) : ResultDomain<StoredCard>` — Luhn RET, expiry geçmiş RET, boş RET; Token üret (`card_`+Guid N), Bin/Last4/Brand türet (PanTools), PAN protect→EncryptedPan, Status=Active; `MessageItem` inline; `<summary>`+`<remarks>Handler: TokenizeCard</remarks>` (bağımlı: T002–T005)
- [ ] T007 Marten kaydı: `opts.Schema.For<StoredCard>().Identity(x => x.Token).Index(x => x.MerchantId)` (`src/services/Payment.Api/Program.cs`)
- [ ] T008a Yeni capability scope sabiti: `AuthorizationScopes.PaymentVault = "payment.vault"` (`src/others/Common/Utils/Constants/AuthorizationScopes.cs`)
- [ ] T008 Identity.Server: `payment.vault` scope'unu kayıt listesine ekle + **Active** merchant demetine ver (statü-kapılı; Provisioning ALMAZ). `payment.write` merchant'a VERİLMEZ (mcp/pos kapalı kalır) — merchant client provisioning/scope seed'i (`src/others/Identity.Server/` seed + `Clients` config); charge fail-closed korunur (bağımlı: T008a)
- [ ] T009 Payment.Api auth: merchant token'ının `payment.vault` scope'unu kabulü (audience/scope) + `MerchantScoped`/`AdminPlaneOnly` policy + `IHttpContextAccessor` kaydı (`src/services/Payment.Api/Program.cs` / auth extension). `/mcp` + `/pos-accounts` `payment.write`'ta kalır — Payment.Api'nin ilk merchant-scoped düzlemi
- [ ] T010 `StoredCardEndpointExtension` iskeleti + route group `api/v{version:apiVersion}/merchants/{merchantId:guid}/vault/cards` + version set; `Program.cs`'e `AddStoredCardGroupEndpointExtension(apiVersionSet)` (`src/services/Payment.Api/Domains/StoredCards/StoredCardEndpointExtension.cs`)
- [ ] T011 [P] Domain testleri — `Create`: Luhn RET, expiry geçmiş RET, Ok + türetilmiş Bin/Last4/Brand doğru, EncryptedPan ham PAN değil, **aynı PAN iki kez Create → farklı Token (FR-014 non-idempotent)** (`tests/Payment.Api.Tests/StoredCardCreateTests.cs`)

**Checkpoint**: Aggregate + persistence + auth hazır; story'ler başlayabilir

---

## Phase 3: User Story 1 - Kart tokenize et ve ödemede kullan (Priority: P1) 🎯 MVP

**Goal**: Merchant PAN gönderir → yalnız token alır; token ödeme akışında gerçek karta çözülür.

**Independent Test**: Active merchant token'ıyla tokenize → yalnız `{token}` döner; aynı token 007 quote akışında doğru BIN'e çözülür; PAN hiçbir yerde görünmez.

- [ ] T012 [US1] `TokenizeCard` slice: command `(Guid MerchantId, string Pan, string Expiry, string HolderName)` + `Response{ string Token }` + `[Transactional]` Handler (`IDocumentSession` + `IPanProtector`; `StoredCard.Create` → `session.Store`; yanıt yalnız Token) + endpoint `MapPost("/")` `RequireAuthorization(AuthorizationScopes.PaymentVault, AuthorizationPolicies.MerchantScoped)`, route'tan `Guid merchantId` (`src/services/Payment.Api/Domains/StoredCards/Features/Commands/TokenizeCard.cs`)
- [ ] T013 [US1] `SimulatedCardVault` → gerçek çözüm: fixture map kaldır; `LoadAsync<StoredCard>(token)` null→Error, `ResolveBinCard.Resolve(session, card.Bin, ct)` null→Error, değilse `Ok(cardInfo)` (`src/services/Payment.Api/CardVault/SimulatedCardVault.cs`)
- [ ] T014 [US1] quickstart S1–S3 canlı doğrulama (tokenize yalnız-token, round-trip quote, Luhn/expiry RET) — Aspire

**Checkpoint**: MVP — merchant kart saklayıp token'la ödeme çözebilir; PAN Payment BC'de kalır

---

## Phase 4: User Story 2 - Kartı sil (soft revoke) (Priority: P2)

**Goal**: Merchant kartı iptal eder (soft); iptal edilmiş token ödeme/çözümde reddedilir.

**Independent Test**: Token'ı revoke et → kayıt Revoked (fiziksel durur); sonra resolve → RET; tekrar revoke → idempotent Ok.

- [ ] T015 [US2] `StoredCard.Revoke() : ResultDomain` ekle — idempotent (zaten Revoked→Ok), aksi Status=Revoked; `<remarks>Handler: RevokeCard</remarks>` (`src/services/Payment.Api/Domains/StoredCards/StoredCard.cs`)
- [ ] T016 [US2] `RevokeCard` slice: command `(Guid MerchantId, string Token)` + `[Transactional]` Handler (`LoadAsync`, sahiplik `MerchantId` eşleşme kontrolü, `Revoke()`, `session.Update`) + endpoint `MapDelete("/{token}")` `RequireAuthorization(PaymentVault, MerchantScoped)` (`src/services/Payment.Api/Domains/StoredCards/Features/Commands/RevokeCard.cs`)
- [ ] T017 [US2] Resolve Revoked-guard: `SimulatedCardVault` Status==Revoked→Error (`src/services/Payment.Api/CardVault/SimulatedCardVault.cs`) (bağımlı: T013 — aynı dosya)
- [ ] T018 [P] [US2] Domain test: Revoke idempotent + Revoked kart Update RET (`tests/Payment.Api.Tests/StoredCardRevokeTests.cs`)
- [ ] T019 [US2] quickstart S4 canlı doğrulama (soft revoke + resolve RET + idempotent)

**Checkpoint**: US1 + US2 bağımsız çalışır

---

## Phase 5: User Story 3 - Kartı güncelle (expiry + holder) (Priority: P3)

**Goal**: Merchant expiry/kart-sahibi günceller; PAN değişmez, token sabit.

**Independent Test**: Update → aynı token, expiry/holder güncel; PAN alanı reddedilir; Revoked token Update RET.

- [ ] T020 [US3] `StoredCard.UpdateDetails(string expiry, string holderName) : ResultDomain` ekle — Status==Active değil→RET, expiry geçmiş→RET; yalnız Expiry+HolderName değişir; `<remarks>Handler: UpdateCard</remarks>` (`src/services/Payment.Api/Domains/StoredCards/StoredCard.cs`) (bağımlı: T015 — aynı dosya)
- [ ] T021 [US3] `UpdateCard` slice: command `(Guid MerchantId, string Token, string Expiry, string HolderName)` (PAN YOK) + `[Transactional]` Handler (sahiplik eşleşme, `UpdateDetails`, `session.Update`) + endpoint `MapPut("/{token}")` `RequireAuthorization(PaymentVault, MerchantScoped)`; yanıt `{Token}` (`src/services/Payment.Api/Domains/StoredCards/Features/Commands/UpdateCard.cs`)
- [ ] T022 [P] [US3] Domain test: UpdateDetails yalnız expiry/holder değiştirir, PAN/token/bin/last4/brand sabit, Revoked→RET (`tests/Payment.Api.Tests/StoredCardUpdateTests.cs`)
- [ ] T023 [US3] quickstart S5 canlı doğrulama

**Checkpoint**: Üç story bağımsız işler

---

## Phase 6: Polish & Cross-Cutting Concerns

- [ ] T024 [P] 007 quickstart güncelle: sabit fixture token'ları kalktı; akış önce tokenize edip dönen token'ı kullanır (`specs/007-a2a-payment-session/quickstart.md`)
- [ ] T025 [P] (Opsiyonel) `SimulatedCardVault` → `StoredCardVault` yeniden adlandır + DI/kullanım (`src/services/Payment.Api/CardVault/`)
- [ ] T026 PAN sızıntı denetimi: log/HTTP yanıtı/RabbitMQ event'inde tam PAN yok (en fazla last4) — tokenize/resolve yolları gözden geçir (S7)
- [ ] T027 quickstart S6 (tenant izolasyon fail-closed: cross-merchant 403, Provisioning 401/403) + S1–S7 uçtan uca koşum — Aspire

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (P1)**: bağımsız, hemen başlar
- **Foundational (P2)**: Setup sonrası; TÜM story'leri bloklar
- **User Stories (P3–P5)**: Foundational sonrası; öncelik sırası P1→P2→P3 (aynı `StoredCard.cs` + `SimulatedCardVault.cs` paylaşımı nedeniyle sıralı önerilir)
- **Polish (P6)**: istenen story'ler bitince

### User Story Dependencies

- **US1 (P1)**: Foundational sonrası; başka story'ye bağlı değil (MVP)
- **US2 (P2)**: Foundational sonrası; T017 T013'ün üstüne yazar (aynı dosya) → US1 sonrası
- **US3 (P3)**: Foundational sonrası; T020 T015'in üstüne yazar (aynı dosya) → US2 sonrası

### Within Each Story

- Domain testleri davranışla birlikte; aggregate metodu → slice → endpoint; resolve değişimi ilgili story'de

### Parallel Opportunities

- T002/T003 (Setup) paralel
- T004/T005/T011 (Foundational, farklı dosya) paralel; T006 T002–T005'e bağlı; T007–T010 çekirdekten sonra
- Domain test task'ları [P] (farklı test dosyaları)
- **Dikkat**: `StoredCard.cs` (T006/T015/T020) ve `SimulatedCardVault.cs` (T013/T017) aynı dosya → bu task'lar arası [P] YOK, sıralı

---

## Implementation Strategy

### MVP First (US1)

1. Phase 1 Setup → 2. Phase 2 Foundational (KRİTİK) → 3. Phase 3 US1 → **DUR & DOĞRULA** (S1–S3) → demo.

### Incremental Delivery

Setup+Foundational → US1 (MVP, tokenize+round-trip) → US2 (revoke) → US3 (update) → Polish. Her story
öncekini bozmadan değer ekler.

---

## Notes

- [P] = farklı dosya, bağımlılık yok. Aynı dosyayı düzenleyen task'lar sıralı.
- Aggregate kuralları: private helper yok (inline; VO muaf), metot yalnız handler'dan, her public metotta `<summary>`+`<remarks>Handler:>`.
- Auth (T008a/T008/T009): yeni **`payment.vault`** capability scope (analyze C1 düzeltmesi) — `payment.write` merchant'a verilmez, `/mcp`+`/pos-accounts` kapalı kalır. Anayasa V "Active tam demet"in dar capability scope'la ödeme düzleminde ilk gerçekleşmesi (kullanıcı onaylı); charge fail-closed korunur.
- Resolve-anı cross-merchant eşleşmesi bilinçli ertelendi (research R3) — charge feature'ında (007 devamı); bu feature yazım tarafında tam korur + Revoked kontrolü.
- Her task/mantıksal grup sonrası commit; checkpoint'te story bağımsız doğrula.
