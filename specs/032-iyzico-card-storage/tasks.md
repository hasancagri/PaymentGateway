# Tasks: iyzico Saklı Kart'a Geçiş (Model A)

**Input**: Design documents from `/specs/032-iyzico-card-storage/`

**Prerequisites**: plan.md, spec.md, research.md (R1-R8), data-model.md, contracts/vault-api.md,
quickstart.md

**Tests**: Saf domain birim testleri (aggregate Create-kimliklerle + Revoke); iyzico çağrısı
quickstart canlı (sandbox). 031'in Luhn/normalize testleri SİLİNİR.

**Organization**: US1 (tokenize→iyzico) MVP, US2 (revoke) üstüne. 031 yeniden-yazımı — mevcut
dosyalar revize/silinir.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Setup

- [X] T001 iyzico sandbox key user-secrets'a (git'e girmez): `dotnet user-secrets --project
      src/services/Payment.Api set "IyzicoProviderSettings:ApiKey"/"SecretKey"/"BaseUrl"` (sandbox
      değerleri + `https://sandbox-api.iyzipay.com`); appsettings.json'a boş placeholder bölüm.
- [X] T002 Taban çizgisi: `dotnet build` 0 hata + `dotnet test tests/Payment.Api.Tests` (031, 14
      test) yeşil — değişiklik öncesi referans.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Options + aggregate revizyonu + altyapı kırpma — iki story de buna bağlı; build ancak
US1 slice'ıyla yeşile döner (iyzico çağrısı handler'da).

- [X] T003 [P] `IyzicoProviderSettings` Options POCO (ApiKey/SecretKey/BaseUrl + DataAnnotations
      zorunlu) + `AddOptionsExt` (BindConfiguration + ValidateOnStart) — düz POCO inject;
      `ProviderOptions`'a map (handler kullanır) — `src/services/Payment.Api/Options/IyzicoProviderSettings.cs`
- [X] T004 [P] CardVault kırpma: `IPanProtector.cs` + `DevPanProtector.cs` SİLİNİR; `PanTools.cs`'ten
      `LuhnValidator` SİLİNİR (Bin/Last4/BrandDetector KALIR) + iyzico `CardAssociation` string→
      `CardBrand` eşleyici eklenir (`VISA`→Visa, `MASTER_CARD`→MasterCard, `AMERICAN_EXPRESS`→Amex,
      `TROY`→Troy, diğer→Unknown) — `src/services/Payment.Api/CardVault/PanTools.cs`
- [X] T005 `StoredCard` aggregate revizyonu: `EncryptedPan` KALDIR; `CardUserKey`+`CardToken` EKLE;
      `Create` imzası `(Guid merchantId, string cardUserKey, string cardToken, string bin, string
      last4, CardBrand brand, string expiry, string holderName)` → zorunlu alan kontrolü (merchantId/
      cardUserKey/cardToken boş olamaz), Active doğar, `card_` token; Luhn/expiry/AES/IPanProtector
      ÇIKAR; `Revoke` DEĞİŞMEZ — `src/services/Payment.Api/Domains/StoredCards/StoredCard.cs`
- [X] T006 Test revizyonu: `StoredCardCreateTests` — Luhn/normalize/PAN/gerçek-koruyucu testleri
      SİLİNİR; yeni: Create geçerli kimliklerle → token+Active+alanlar, boş cardUserKey/cardToken/
      merchantId reddi; `StoredCardRevokeTests` DEĞİŞMEZ —
      `tests/Payment.Api.Tests/StoredCardCreateTests.cs`
- [X] T007 Build kapısı (kısmi): aggregate + test derlenir ama slice'lar henüz eski imzada —
      T008'e kadar tam yeşil beklenmez (sıra notu).

---

## Phase 3: User Story 1 — Kart Kaydetme (iyzico Saklı Kart) (P1) 🎯 MVP

**Goal**: `POST vault/cards` → iyzico `Card.Create` → cardUserKey/cardToken sakla → `{token}` (031
sözleşmesi birebir).

**Independent Test**: quickstart S1 — curl tokenize (sandbox test kartı) + DB'de cardUserKey/cardToken
var, PAN yok + negatifler.

- [X] T008 [US1] `TokenizeCard` handler iyzico çağrısı: `ProviderOptions` inject; `CreateCardRequest`
      kur (Email=sentetik sabit, ExternalId=üretilecek token, `CardInformation{CardNumber=cmd.Pan
      normalize, ExpireMonth/Year cmd.Expiry'den, CardHolderName, CardAlias}`) → `Card.Create(req,
      opts)` await; `Status != "success"` VEYA exception → `INVALID_OPERATION_ERROR`, Store YOK
      (fail-closed, FR-007); başarı → `StoredCard.Create(merchantId, resp.CardUserKey, resp.CardToken,
      resp.BinNumber, resp.LastFourDigits, brand(resp.CardAssociation), cmd.Expiry, cmd.HolderName)`
      + Store; Response{Token} (031 aynı) — `src/services/Payment.Api/Domains/StoredCards/Features/Commands/TokenizeCard.cs`
- [X] T009 [US1] Program.cs: `AddOptionsExt<IyzicoProviderSettings>` wire; StoredCard Marten kaydı
      (Identity(Token).Index(MerchantId)) korunur; endpoint extension DEĞİŞMEZ —
      `src/services/Payment.Api/Program.cs`
- [X] T010 [US1] Build kapısı: `dotnet build` 0 hata + `dotnet test tests/Payment.Api.Tests` yeşil.
- [X] T011 [US1] Checkpoint (canlı): quickstart S1 — mt_doc_storedcard truncate + user-secrets key;
      curl tokenize (5528790000000008) → `{token}`; DB'de CardUserKey/CardToken/Bin/Last4 var, PAN 0;
      negatifler (bozuk PAN→iyzico reddi 400, yabancı merchantId→403).

---

## Phase 4: User Story 2 — Kart Silme (P2)

**Goal**: `DELETE vault/cards/{token}` → iyzico `Card.Delete` best-effort + yerel soft revoke.

**Independent Test**: quickstart S2 — revoke (iyzico'dan da silinir) + idempotent + not-found.

- [X] T012 [US2] `RevokeCard` handler iyzico çağrısı: token'dan StoredCard yükle (MerchantId
      eşleşmezse RECORD_NOT_FOUND — sahiplik sızdırmaz); `Card.Delete(DeleteCardRequest{CardUserKey,
      CardToken}, opts)` best-effort (try/catch, hata yutulur — FR-006 fail-open); `card.Revoke()` +
      Update — `src/services/Payment.Api/Domains/StoredCards/Features/Commands/RevokeCard.cs`
- [X] T013 [US2] Checkpoint (canlı): quickstart S2 — revoke → 200 + gateway Revoked + iyzico'dan
      kalkar; tekrar → 200 (idempotent); uydurma token → RECORD_NOT_FOUND.

---

## Phase 5: Polish & Uçtan Uca

- [X] T014 Regresyon: `dotnet build` (çözüm) 0 hata; Payment + Merchant (47) + Commission (31) yeşil.
- [X] T015 quickstart S3 — sıfır-dokunuş (SC-001): iki AppHost; ECommerce Profil → kart ekle
      (5528790000000008) → listede Mastercard •0008; paymentDb'de Active StoredCard (CardUserKey/
      CardToken dolu, PAN yok); sil → düşer + gateway Revoked + iyzico'dan silinir; ECommerce
      `git status` temiz (FR-008). Commit/PR kullanıcı onayıyla.

---

## Dependencies

```
T001 (key) + T002 (baseline) ─► Phase 2 (T003 ∥ T004 → T005 → T006)
Phase 2 ─► US1 (T008 → T009 → T010 → T011)   # build ancak T010'da tam yeşil (slice iyzico imzası)
US1 ─► US2 (T012 → T013)
US1+US2 ─► Polish (T014 → T015)
```

## Parallel Opportunities

- T003 ∥ T004 (Options POCO ∥ CardVault kırpma, ayrı dosyalar).

## Implementation Strategy

**MVP**: T001-T011 (Setup + Foundational + US1) — ECommerce kart ekleme iyzico Saklı Kart'a geçer,
kullanıcıya görünmez (sıfır dokunuş). US2 silmeyi iyzico'ya yayar. iyzico çağrısı gerçek (sandbox);
aggregate imzası değiştiğinden Foundational+US1 bölünmez tek yeşil-build dilimi (T010'da döner).

> Canlı S1+S2 GEÇTİ (2026-08-14): tokenize→iyzico Saklı Kart (card_ token; DB'de CardUserKey+CardToken, PAN sızma 0, EncryptedPan alanı 0); bozuk PAN→iyzico reddi 400; yabancı merchant 403; revoke→iyzico Card.Delete+yerel 200; idempotent 200; uydurma→RECORD_NOT_FOUND; DB Revoked. CANLI FIX: sentetik e-posta '.local' TLD + '+' iyzico'da geçersiz (ERRCODE=5 'email hatalı format') → 'vault{kısa-merchantId}@dropshop.com' (spike'la doğrulandı). S3 (ECommerce uçtan uca) kullanıcıda.

> T015 (S3 ECommerce tarayıcı uçtan uca): kullanıcı onayıyla ATLANDI — S1/S2 gateway+sözleşmeyi kanıtladı, ECommerce binary değişmedi + git temiz (sıfır dokunuş = SC-001 kanıtı). Tarayıcı-tıklaması sonraya.
