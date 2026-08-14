# Tasks: Kart Vault Dirilişi

**Input**: Design documents from `/specs/031-card-vault-revival/`

**Prerequisites**: plan.md, spec.md, research.md (R1-R8), data-model.md, contracts/vault-api.md,
quickstart.md

**Tests**: Saf domain birim testleri DAHİL (R8 — eski 017 testleri şablon); handler/HTTP
entegrasyonu quickstart ile elle.

**Organization**: User story bazlı; US1 (tokenize) MVP, US2 (revoke) üstüne gelir. Referans kod:
`git show 9c393ad^:<eski yol>` — birebir temel, sapmalar R2/R3 + PAN normalize.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Setup

- [X] T001 Yeni test projesi: `tests/Payment.Api.Tests/Payment.Api.Tests.csproj`
      (Merchant.Api.Tests csproj deseni — xUnit, Payment.Api proje referansı) + PaymentGateway.slnx
      kaydı + boş `GlobalUsings.cs`; `dotnet build` 0 hata taban çizgisi.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Altyapı + aggregate — iki story de buna bağlı.

- [X] T002 [P] `CardVault/` altyapısı (eski kod `9c393ad^` aynen): `IPanProtector.cs` (yalnız
      `Protect`; Reveal ödeme spec'ine — yorumda not), `DevPanProtector.cs` (AES-CBC, dev-sabit
      SHA256 anahtar, IV prepend, `ISingletonDependency`), `PanTools.cs` (LuhnValidator 12-19 hane,
      BinExtractor, Last4Extractor, BrandDetector — SharedKernel yerine BC-içi CardBrand'e döner) —
      `src/services/Payment.Api/CardVault/`
- [X] T003 [P] Enum'lar: `CardBrand` (`Unknown=0, Visa, MasterCard, Amex, Troy` — R2) +
      `StoredCardStatus` (`Active=0, Revoked=1`, eski yorumlarla) —
      `src/services/Payment.Api/Domains/StoredCards/CardBrand.cs`,
      `src/services/Payment.Api/Domains/StoredCards/StoredCardStatus.cs`
- [X] T004 `StoredCard` aggregate (eski kod temel; R3: `UpdateDetails` YOK; EK: PAN normalize —
      rakam-dışı karakterler ayıklanır, doğrulama/türetim normalize edilmiş haneyle): `Create(
      merchantId, pan, expiry, holderName, IPanProtector)` → `ResultDomain<StoredCard>` (zorunlu
      alanlar, Luhn, MM/yy + ay-sonu ≥ bugün, `card_`+Guid("N") token, Protect, BIN/Last4/Brand)
      + idempotent `Revoke()`; `<summary>` + `<remarks>Handler:</remarks>` notları —
      `src/services/Payment.Api/Domains/StoredCards/StoredCard.cs`
- [X] T005 Domain birim testleri (eski StoredCardCreate/RevokeTests uyarlaması + ekler): geçerli
      kart → token `card_` önekli + türetimler doğru + Status Active; Luhn matrisi (geçersiz hane,
      12'den kısa, 19'dan uzun); boşluklu/tireli PAN normalize edilip geçer; geçmiş expiry / bozuk
      biçim reddi; eksik alanlar; aynı PAN → farklı token; Revoke: Active→Revoked,
      tekrar→Ok(idempotent) — `tests/Payment.Api.Tests/StoredCardCreateTests.cs`,
      `tests/Payment.Api.Tests/StoredCardRevokeTests.cs`
- [X] T006 Checkpoint: `dotnet build` 0 hata + `dotnet test tests/Payment.Api.Tests` yeşil.

---

## Phase 3: User Story 1 — Kart Kaydetme / Tokenize (P1) 🎯 MVP

**Goal**: `POST /api/v1.0/merchants/{merchantId}/vault/cards` → yalnız `{token}` (sözleşme SABİT).

**Independent Test**: quickstart S1 — curl tokenize + 4 negatif + PAN sızma kontrolü (DB).

- [X] T007 [US1] `TokenizeCard` slice'ı (eski kod aynen): Command(MerchantId, Pan, Expiry,
      HolderName) + Request gövdesi (PAN yalnız burada) + Response{Token} + `[Transactional]`
      Handler (`StoredCard.Create` + Store) + endpoint `MapPost("/")` (`CardsWrite` +
      `MerchantScoped`) —
      `src/services/Payment.Api/Domains/StoredCards/Features/Commands/TokenizeCard.cs`
- [X] T008 [US1] `StoredCardEndpointExtension` (grup `api/v{version:apiVersion}/merchants/
      {merchantId:guid}/vault/cards`, Tokenize map'i) + Program.cs'e
      `app.AddStoredCardGroupEndpointExtension(apiVersionSet);` —
      `src/services/Payment.Api/Domains/StoredCards/StoredCardEndpointExtension.cs`,
      `src/services/Payment.Api/Program.cs`
- [X] T009 [US1] Checkpoint: build 0 hata; quickstart S1 canlı — merchant token'ıyla (029'un
      ECommerce Demo merchant'ı, scope `cards.write`) tokenize + negatifler (Luhn, geçmiş expiry,
      eksik alan, yabancı merchantId → 403) + DB'de açık PAN 0 (SC-002 sorgusu).

---

## Phase 4: User Story 2 — Kart Silme / Revoke (P2)

**Goal**: `DELETE .../vault/cards/{token}` — idempotent soft revoke; sahiplik sızdırmaz.

**Independent Test**: quickstart S2 — revoke + tekrar-revoke 200 + bilinmeyen token
RECORD_NOT_FOUND.

- [X] T010 [US2] `RevokeCard` slice'ı (eski kod aynen): Load by token; `card is null || MerchantId
      != cmd.MerchantId` → RECORD_NOT_FOUND (sahiplik sızdırmaz); `card.Revoke()` + Update;
      endpoint `MapDelete("/{token}")` (`CardsWrite` + `MerchantScoped`); extension'a map eklenir —
      `src/services/Payment.Api/Domains/StoredCards/Features/Commands/RevokeCard.cs`,
      `src/services/Payment.Api/Domains/StoredCards/StoredCardEndpointExtension.cs`
- [X] T011 [US2] Checkpoint: build 0 hata; quickstart S2 canlı (revoke → 200; tekrar → 200;
      uydurma token → RECORD_NOT_FOUND).

---

## Phase 5: Polish & Uçtan Uca

- [X] T012 Regresyon: `dotnet build` (çözüm) 0 hata; Payment + Merchant (47) + Commission (31)
      testleri yeşil.
- [X] T013 quickstart S3 — sıfır-dokunuş kanıtı (SC-001): iki AppHost; ECommerce Profil →
      Kartlarım → kart ekle (`4111 1111 1111 1111`, 12/29) → listede Visa •1111; paymentDb'de
      Active StoredCard; sil → listeden düşer + gateway kaydı Revoked; ECommerce logunda "Vault
      tokenize başarısız" YOK. ECommerce reposuna HİÇBİR dokunuş olmadığı `git status` ile
      doğrulanır. Commit/PR kullanıcı onayıyla.

---

## Dependencies

```
T001 ─► Phase 2 (T002 ∥ T003 → T004 → T005 → T006)
Phase 2 ─► US1 (T007 → T008 → T009)
US1 ─► US2 (T010 → T011)      # extension dosyası T008'de doğar
US1+US2 ─► Polish (T012 → T013)
```

## Parallel Opportunities

- T002 ∥ T003 (ayrı dosyalar); T005'in iki test dosyası paralel yazılabilir.

## Implementation Strategy

**MVP**: T001-T009 (Setup + Foundational + US1) — ECommerce'in kart EKLEME akışı canlanır (silme
henüz gateway'de yoksa ECommerce fail-open davranışıyla yerel silmeye devam eder — kırılmaz).
US2 döngüyü kapatır. Dış sözleşme sabit olduğundan hiçbir aşamada ECommerce'e dokunulmaz;
son kanıt T013 `git status` (ECommerce repo temiz).

> Canlı doğrulama (2026-08-14): S1 (tokenize + negatifler: Luhn/expiry 400, yabancı merchant 403, boşluklu PAN normalize) + S2 (revoke idempotent 200, uydurma→RECORD_NOT_FOUND) + S3 (ECommerce profilden kart eklendi, gateway'de card_ token+bin/last4/brand, PAN sızma 0, EncryptedPan base64) GEÇTİ. ECommerce git temiz (sıfır dokunuş).
> CANLI FIX: Program.cs'e Schema.For<StoredCard>().Identity(Token).Index(MerchantId) eklendi (R7 hatalıydı — Token string identity bu kayıt olmadan çalışmaz, revoke LoadAsync(token) kırılırdı); bayat 017 tablosu (mt_doc_storedcard, Türkçe-ı index'li) düşürüldü — MartenSchemaException çözüldü.
