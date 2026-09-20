# Tasks: Merchant Self-Servis MerchantKey Yenileme

**Feature**: 046-merchant-key-reissue | **Branch**: `046-merchant-key-reissue`
**Girdi**: plan.md, spec.md, data-model.md, contracts/, research.md, quickstart.md

## Format: `[ID] [P?] [Story] Açıklama (dosya yolu)`

- **[P]** = paralel çalışabilir (farklı dosya, tamamlanmamış task'a bağlı değil)
- **[US1/US2/US3]** = kullanıcı hikâyesi; Setup/Foundational/Polish etiketi yok
- Domain-TDD (İLKE VI): saf domain davranışı (aggregate metodu) test-FIRST — test task'ı impl'den önce.
  Handler/endpoint/consumer/altyapı test-sonra ya da canlı doğrulama (quickstart).

## Path Conventions

PG mono-repo, servis-başına proje. Merchant.Api = otorite; Identity.Server + Payment.Api = tüketici;
Shared = event kontratı. Testler `tests/Merchant.Api.Tests/` (mevcut xUnit + Shouldly).

---

## Phase 1: Setup (Paylaşılan Altyapı)

- [X] T001 Branch `046-merchant-key-reissue` üzerinde çalıştığını doğrula; `dotnet build` temel yeşil
  (başlangıç durumu) — regresyon karşılaştırma tabanı.

---

## Phase 2: Foundational (Tüm hikâyeleri bloke eden ön koşullar)

**Amaç**: reissue zincirinin ortak sözleşmeleri — hiçbir hikâye bunlar olmadan tamamlanamaz.

- [X] T002 Yeni integration event ekle: `MerchantKeyReissued(Guid MerchantId, string MerchantKey)` —
  `src/others/Shared/IntegrationEvents.cs`. Yorumda: yeni key taşır, iç event, `MerchantLifecycle`
  fanout (MerchantCreated ile aynı exchange).
- [X] T003 Yeni salt-append doc: `MerchantKeyReissueLog` (Id, MerchantId, ReissuedAt, InitiatedBy,
  Reason?) — `src/services/Merchant.Api/Domains/MerchantKeyReissueLogs/MerchantKeyReissueLog.cs`.
  Yalnız insert; update/delete metodu YOK.
- [X] T004 Marten şema kaydı: `MerchantKeyReissueLog` için Merchant.Api `Program.cs`'te `Schema.For<>`
  + `MerchantId` index (geçmiş sorgusu için) — `src/services/Merchant.Api/Program.cs`.

**Checkpoint**: event + audit doc derleniyor; tüketiciler henüz yok.

---

## Phase 3: User Story 1 - Kaybolan/unutulan key'i yenileme (Priority: P1) 🎯 MVP

**Amaç**: merchant kimliğiyle yeni key alır; eski key ANINDA her temsilde ölür; yeni key tek-
kullanımlık reveal'dan bir kez okunur.

**Bağımsız test**: bilinen bir Active merchant için reissue tetikle → eski key ödeme yüzeyinde 401,
yeni key geçer, reveal URL bir kez açılır. (quickstart adım 1-4.)

### Tests for User Story 1 (Domain-TDD — İLKE VI, test-FIRST) ⚠️

- [X] T005 [P] [US1] `Merchant.ReissueKey()` domain testi (test-first): Active merchant'ta yeni
  `mk_`-prefiksli key üretir + eski key'den farklı; Active-olmayan merchant'ta Result error, key
  değişmez — `tests/Merchant.Api.Tests/MerchantReissueKeyTests.cs`.
  ↳ senaryo: kaybını bildiren aktif merchant taze key alır; pasif merchant'ın key'i yanlışlıkla değişmez.
- [X] T006 [P] [US1] `CredentialRevealLink` reuse davranış testi: `Create` yeni key ile + eski
  linkler `Kill` sonrası nötr; `Consume` tek-kullanımlık — `tests/Merchant.Api.Tests/CredentialRevealLinkReissueTests.cs`.
  ↳ senaryo: yeni key yalnız bir kez gösterilir; eski teslim linkleri artık açılmaz.

### Implementation for User Story 1

- [X] T007 [US1] `Merchant.ReissueKey() : ResultDomain` metodu — yeni `MerchantKey = "mk_" + Guid`;
  yalnız Active guard; `src/services/Merchant.Api/Domains/Merchants/Merchant.cs`. (T005 yeşil yapar.)
  ↳ senaryo: merchant "yeni key ver" der, taze bir key doğar.
- [X] T008 [US1] Reissue command + handler: merchant yükle → `ReissueKey()` → `MerchantKeyReissued`
  publish ([Transactional] outbox) → yeni `CredentialRevealLink.Create` + önceki linkleri `Kill` →
  reveal URL + expiry döndür. Merchant yok/Active değil → Result error (FR-009). Eşzamanlıda tek key
  (FR-010) — `src/services/Merchant.Api/Domains/Merchants/Features/Commands/ReissueMerchantKey.cs`.
  ↳ senaryo: tek istekle yeni key doğar, eski öldürülür, merchant'a gösterim linki döner.
- [X] T009 [US1] S2S REST endpoint `POST api/v1/onboarding/reissue` — ecommerce-onboarding m2m auth
  (045 onboarding S2S grubu deseni); body `{merchantId, reason?}`; yanıt `{revealUrl, expiresAt}`
  (key İÇERMEZ, FR-005). Endpoint grubuna ekle — `src/services/Merchant.Api/Program.cs` +
  ilgili EndpointExtension.
  ↳ senaryo: merchant kendi admin ekranından tetikler; kaybettiği key'i sunmak zorunda kalmaz.
- [X] T010 [US1] Identity.Server tüketici: `MerchantClientEventHandler.Handle(MerchantKeyReissued)`
  → OpenIddict client_secret = yeni key (mevcut BuildDescriptor/Update yolu) —
  `src/others/Identity.Server/EventHandlers/MerchantClientEventHandler.cs`. Binding'i tüketici kurar.
  ↳ senaryo: yeni key'le token alınır, eski key'le alınamaz.
- [X] T011 [US1] Payment.Api tüketici: `MerchantApiConsumers.Handle(MerchantKeyReissued)` → mevcut
  `MerchantApiKeyReference`'i merchantId ile bul, KeyHash'i REPLACE (sil+store ya da merchantId-
  anahtarlı upsert) — eski hash KALMAMALI (FR-003 kritik, research Karar 2) —
  `src/services/Payment.Api/MerchantApiConsumers.cs`. Binding'i tüketici kurar.
  ↳ senaryo: eski key'le ödeme başlatma anında 401; yeni key'le geçer.
- [X] T012 [US1] Wolverine keşif kontrolü: yeni handler/consumer sınıfları `*Handler`/`*Consumers`
  son-ekiyle bitmiyor ya da taramaya girmiyorsa `opts.Discovery.IncludeType(...)` ekle (Identity +
  Payment + Merchant Program.cs) — sessiz mesaj yutulmasını önle.
  ↳ senaryo: reissue mesajı sessizce yutulmaz — yeni key gerçekten her yere yayılır.
- [X] T013 [US1] `dotnet build` (tüm çözüm) + T005/T006 testleri yeşil.
  ↳ senaryo: MVP uçtan uca çalışır durumda.

**Checkpoint**: US1 tek başına canlı doğrulanabilir (quickstart 1-4). MVP burada biter.

---

## Phase 4: User Story 2 - İzlenebilir yenileme geçmişi (Priority: P2)

**Amaç**: her reissue salt-append kayıt üretir; merchant/operatör geçmişi okur.

**Bağımsız test**: reissue yap → geçmiş sorgusu 1 kayıt (zaman + tetikleyen + neden); kayıt
düzenlenemez/silinemez. (quickstart adım 5.)

- [X] T014 [US2] Reissue handler'a audit yazımı ekle: başarılı reissue'de `MerchantKeyReissueLog`
  insert (MerchantId, ReissuedAt, InitiatedBy = m2m/store kimliği, Reason = istekten) — aynı
  [Transactional] commit — `src/services/Merchant.Api/Domains/Merchants/Features/Commands/ReissueMerchantKey.cs`.
  ↳ senaryo: her key değişimi iz bırakır — "kim, ne zaman, neden."
- [X] T015 [US2] Geçmiş sorgu slice: merchantId ile `MerchantKeyReissueLog` listesi (salt-okuma) —
  `src/services/Merchant.Api/Domains/MerchantKeyReissueLogs/Features/Queries/GetMerchantKeyReissueHistory.cs`.
  Erişim yüzeyi: admin MCP tool ya da MerchantScoped S2S (plan'a göre; v1 = MerchantScoped okuma).
  ↳ senaryo: merchant/operatör "key'im ne zaman değişti" sorusuna yanıt alır.
- [X] T016 [US2] Kayıt eşzamanlı/tekrarlı reissue'de her seferinde ayrı satır; edit/delete yolu
  olmadığını doğrula (kod incelemesi + T017 canlı).
  ↳ senaryo: merchant "siz habersiz değiştirdiniz" diyemez — kayıt silinemez/değişmez.
- [ ] T017 [US2] Canlı doğrulama: 2 ardışık reissue → geçmişte 2 kayıt, sırayla.
  ↳ senaryo: iki kez yenileyen merchant geçmişte iki satır görür.

**Checkpoint**: US1 + US2 bağımsız çalışır.

---

## Phase 5: User Story 3 - Adla merchant çözümleme (Priority: P3)

**Amaç**: çıplak GUID yerine merchant adı/e-postasıyla tetik (D5 eşi).

**Bağımsız test**: tek eşleşen ad → doğru merchant reissue; sıfır/çok eşleşme → belirsizlik/bulunamadı,
key değişmez. (spec US3 kabul.)

- [X] T018 [P] [US3] Merchant ad/e-posta → MerchantId çözümleme sorgusu (tam-eşleşme; belirsiz/yok
  reddi) — `src/services/Merchant.Api/Domains/Merchants/Features/Queries/ResolveMerchantByName.cs`.
  (D5 `admin_find_merchant` ile paylaşılabilir; conventions'a göre bilinçli tekrar kabul.)
  ↳ senaryo: operatör merchant'ı adıyla bulur, GUID ezberlemez.
- [X] T019 [US3] Reissue endpoint/handler'a opsiyonel ad-bazlı girdi: `{merchantId?}` ya da
  `{merchantName?/email?}`; ad verilirse T018 ile çöz, belirsiz/yok → Result error, key değişmez
  (FR-011) — `ReissueMerchantKey.cs` + endpoint.
  ↳ senaryo: "Ayşe Kitap için key yenile" GUID'siz çalışır; belirsiz adda yanlış merchant'a dokunulmaz.
- [ ] T020 [US3] Canlı doğrulama: adla reissue (tek eşleşme geçer, belirsiz reddedilir).
  ↳ senaryo: tek eşleşen ad geçer, iki eşleşen ad reddedilir.

**Checkpoint**: 3 hikâye de bağımsız.

---

## Phase 6: Polish & Cross-Cutting

- [X] T021 [P] FLOW.md güncelle: Merchant.Api `FLOW.md`'ye reissue süreç adımı + kenar-anchor
  (`Merchant.ReissueKey → MerchantKeyReissued`, `CredentialRevealLink.Create/Kill`) — İLKE VII, AYNI
  PR. `scripts/check-flow-links.sh` yeşil.
- [X] T022 [P] Rate-limit iz-bırakma: reissue endpoint FR-012 için S1 (genel rate-limiting borcu)
  kapsamına alınacak yeri işaretle (yorum/TODO); S1 gelince policy takılır.
- [ ] T023 Canlı E2E (quickstart tümü): reissue → eski 401 → yeni geçer → reveal tek-kullanımlık →
  geçmiş kaydı → in-flight ödeme tamamlanır (SC-001..006).
- [X] T024 `dotnet build` + `dotnet test` (Merchant.Api.Tests dahil bağımlı test projeleri) tam yeşil.

---

## Bağımlılıklar & Sıra

- **Setup (T001)** → **Foundational (T002-T004)** → hikâyeler.
- **US1 (T005-T013)** MVP; Foundational'a bağlı. Consumer'lar (T010/T011) event'e (T002) bağlı.
- **US2 (T014-T017)** US1 handler'ına ekleme; audit doc'a (T003) bağlı. US1'den sonra.
- **US3 (T018-T020)** US1 endpoint'ine ekleme; bağımsız çözümleme sorgusu. US1'den sonra.
- **Polish (T021-T024)** en son; T021 FLOW.md süreç değiştiği için zorunlu.

## Paralel Fırsatlar

- Foundational: T002 ∥ T003 (farklı dosya).
- US1 testleri: T005 ∥ T006 (test-first, farklı dosya).
- US1 consumer'ları: T010 ∥ T011 (Identity vs Payment, farklı repo-alanı) — event (T002) hazırsa.
- Polish: T021 ∥ T022.

## MVP Kapsamı

**US1 (T001-T013)** = MVP: kaybolan key yerine yeni key, eski anında ölür, tek-kullanımlık teslim.
US2 (audit) + US3 (ad çözümleme) artımlı.

## Toplam

24 task — Setup 1 · Foundational 3 · US1 9 · US2 4 · US3 3 · Polish 4.
