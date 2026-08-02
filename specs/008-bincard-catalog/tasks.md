---
description: "Task list for BinCard Referans Kataloğu (008)"
---

# Tasks: BinCard Referans Kataloğu

**Input**: Design documents from `/specs/008-bincard-catalog/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/bincard-api.md, quickstart.md

**Tests**: Saf domain birim testleri dahildir (proje/anayasa kuralı + spec talebi). HTTP/DB
round-trip / dış çağrı birim testi YOK — quickstart ile elle doğrulanır.

**Organization**: Görevler user story'ye göre gruplu. US1 (çözümle) = MVP; US2 (seed) US1'i
uçtan-uca anlamlı kılar; US3 (import) güncellenebilirliği tamamlar.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: paralel çalışabilir (farklı dosya, bağımlılık yok)
- Yol: `src/services/Payment.Api/...` ve `tests/Payment.Api.Tests/...`

---

## Phase 1: Setup

**Purpose**: Test projesi + domain klasörü hazır.

- [X] T001 [P] `tests/Payment.Api.Tests` xUnit projesi oluştur (`tests/Merchant.Api.Tests` desenini
  birebir izle: csproj `Payment.Api` + `CP.VPOS` referansı, xunit/xunit.runner.visualstudio/
  Microsoft.NET.Test.Sdk CPM'den); `PaymentGateway.slnx`'e ekle.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: BinCard document + enum + mapping + Marten kaydı — tüm story'ler buna dayanır.

**⚠️ CRITICAL**: Bu faz bitmeden hiçbir user story başlamaz.

- [X] T002 [P] `CardType` enum'u `src/services/Payment.Api/Domains/BinCards/CardType.cs` (Debit=0,
  Credit=1 — CP.VPOS değerleriyle birebir, PaymentStatus stili düz enum).
- [X] T003 [P] `CardBrand` enum'u `.../Domains/BinCards/CardBrand.cs` (Unknown=-1, Visa=0,
  MasterCard=1, Troy=2, Amex=3, Discover=4, Unionpay=5, JCB=6).
- [X] T004 [P] `CardProgram` enum'u `.../Domains/BinCards/CardProgram.cs` (Unknown=-1 … World=11,
  Advantage=12, SaglamKart=13 — data-model.md legend).
- [X] T005 `BinCard` document `.../Domains/BinCards/BinCard.cs` (BinNumber kimlik, BankCode, CardType,
  CardBrand, CardProgram, Commercial; data-model.md).
- [X] T006 `BinCardMapping` `.../Domains/BinCards/BinCardMapping.cs` — CP.VPOS `CreditCardBinQuery
  Response` (ve int değerler) → `BinCard` + domain enum; tanınmayan marka/program → `Unknown`
  (çökmez). CP.VPOS tipi domain sınırını GEÇMEZ (yalnız burada tüketilir). (T005'e bağlı)
- [X] T007 Marten kaydı `src/services/Payment.Api/Program.cs`: `opts.Schema.For<BinCard>()
  .Identity(x => x.BinNumber)` + `.Index(x => x.CardProgram)`. (T005'e bağlı)
- [X] T008 [P] Birim test `tests/Payment.Api.Tests/BinCardMappingTests.cs` — tüm enum değer eşlemesi
  + tanınmayan değer → Unknown. (T006'ya bağlı)

**Checkpoint**: Katalog tipi + eşleme hazır; story'ler başlayabilir.

---

## Phase 3: User Story 1 - BIN'den kart bilgisini katalogdan çöz (P1) 🎯 MVP

**Goal**: `binNumber → CardInfo?` katalogdan; bulunamazsa null; 8→6 fallback; taksit-banka türetme.
Ödeme/taksit okuma yolu donmuş kütüphaneden katalog sorgusuna geçer.

**Independent Test**: Bilinen BIN → CP.VPOS ile birebir aynı kart bilgisi + taksit-banka listesi;
banka kartı → boş liste; bilinmeyen BIN → null; 8 hane → ilk 6 ile çözülür.

### Implementation

- [X] T009 [US1] `ResolveBinCard` query `.../Domains/BinCards/Features/Queries/ResolveBinCard.cs`
  (record + `CardInfo?` çıktı + Handler). **Saf çözüm mantığını** ayrı static olarak yaz (hedef
  BinCard + aynı CardProgram adayları → `CardInfo`: kredi+geçerli program ise BankCode distinct
  destek-azalan, kart bankası başa; banka kartı/Unknown program → boş). Handler `IQuerySession` ile
  BinNumber exact-match (8 hane → tam eşleşme yoksa `binNumber[..6]`), yoksa **null** döner. (T005,
  T006, T007'ye bağlı)
- [X] T010 [US1] `BinCardEndpointExtension` debug ucu `.../Domains/BinCards/BinCardEndpointExtension.cs`
  (`GET api/v1/bin-cards/{bin}` → CardInfo/404) ve `Program.cs`'te map. (T009'a bağlı)
- [X] T011 [US1] Okuma yolu switch — `ProcessPayment.LoadCardInfo` `.../Domains/Payments/Features/
  Commands/ProcessPayment.cs`: `VPOSClient.CreditCardBinQuery` yerine `ResolveBinCard`; imza
  `CardInfo?`; null kart → Result reddi (sahte-default üretme). (T009'a bağlı)
- [X] T012 [US1] Okuma yolu switch — `GetInstallmentOptions` `.../Domains/Payments/Features/Queries/
  GetInstallmentOptions.cs`: `ResolveBinCard` kullan; null kart → boş/uygun sonuç. (Model B tutar
  davranışına DOKUNMA — o 007'nin işi.) (T009'a bağlı)
- [X] T013 [P] [US1] Birim testler `tests/Payment.Api.Tests/ResolveBinCardTests.cs` (saf çözüm
  static'ine): 8→6 seçim; taksit-banka türetme (kart bankası başta; banka kartı → boş; Unknown
  program → boş); bilinmeyen BIN → null. (T009'a bağlı)

**Checkpoint**: Çözümleme çalışır ve okuma yolu katalogtan besleniyor (veri seed ile — US2).

---

## Phase 4: User Story 2 - Katalogu ilk kez doldur (seed) (P1)

**Goal**: Boş katalog başlangıçta CP.VPOS gömülü kaynağından bir kez dolar; doluysa atlanır.

**Independent Test**: Boş DB ile başlat → katalog ~9900 kayıt; yeniden başlat → sayı değişmez.

### Implementation

- [X] T014 [US2] `BinCardSeeder : Marten.Schema.IInitialData` `.../Domains/BinCards/BinCardSeeder.cs`:
  `Populate` içinde katalog boşsa gömülü `Domains/BinCards/Data/bincards.json` (EmbeddedResource,
  ~9957 kayıt, CP.VPOS BinService'ten çıkarıldı) → `BinCardMapping.FromCodes` → toplu `session.Store`;
  doluysa hiçbir şey yapma (idempotent). **Kullanıcı kararı: seed kaynağı VPOSClient değil JSON —
  CP.VPOS'a seed bağımlılığı yok.** (T005, T006'ya bağlı)
- [X] T015 [US2] `Program.cs`'te seeder'ı kaydet: `.InitializeWith(new BinCardSeeder())` (Marten 9
  fluent API; `opts.InitialData` değil) — `ApplyAllDatabaseChangesOnStartup` ile birlikte. (T014, T007'ye bağlı)

**Checkpoint**: US1 çözümü artık gerçek veriyle uçtan uca çalışır (MVP tam).

---

## Phase 5: User Story 3 - Yayınlanan BIN listesiyle güncelle (idempotent import) (P2)

**Goal**: Operatör bir BIN listesini toplu upsert eder; idempotent; geçersiz kayıt atlanır+raporlanır.

**Independent Test**: Karışık liste (var+yeni) → var olan güncellenir, yeni eklenir; aynı liste
ikinci kez → içerik/sayı değişmez; geçersiz kayıt → skipped, batch bozulmaz.

### Implementation

- [X] T016 [US3] `ImportBinCards` command `.../Domains/BinCards/Features/Commands/ImportBinCards.cs`
  (`ImportBinCardsCommand{ items }` + `ImportBinCardsResponse{ imported, updated, skipped,
  skippedReasons }` + `[Transactional]` Handler): her item `BinCardMapping` ile domain'e; geçersiz/
  eksik (ör. binNumber boş) → skip+say; geçerli → `session.Store` upsert (kimlik BinNumber). (T005,
  T006'ya bağlı)
- [X] T017 [US3] Import endpoint `POST api/v1/bin-cards/import` `.../Domains/BinCards/
  BinCardEndpointExtension.cs` (`IMessageBus.InvokeAsync`) + Program.cs map. (T016, T010'a bağlı)
- [X] T018 [P] [US3] Birim testler `tests/Payment.Api.Tests/ImportBinCardsTests.cs` — geçersiz kayıt
  atlama + rapor sayıları + mapping doğruluğu. (Upsert idempotency Marten Store semantiğidir → DB;
  quickstart'ta doğrulanır.) (T016'ya bağlı)

**Checkpoint**: Üç story de bağımsız çalışır; katalog güncellenebilir.

---

## Phase 6: Polish & Cross-Cutting

- [X] T019 [P] `VPOSClient.CreditCardBinQuery`'nin BIN çözümü için artık çağrılmadığını doğrula
  (grep `src/services/Payment.Api`); CP.VPOS'a dokunulmadığını teyit et.
- [X] T020 `dotnet build` + `dotnet test tests/Payment.Api.Tests` yeşil.
- [ ] T021 `quickstart.md` senaryolarını elle koştur (Aspire: seed sayısı, çözüm paritesi,
  idempotent import).

---

## Dependencies & Execution Order

- **Setup (T001)**: bağımsız.
- **Foundational (T002-T008)**: Setup sonrası; TÜM story'leri bloklar. T002/T003/T004/T008 [P];
  T005→T006→T007 sıralı (T005 doc, sonra mapping/kayıt).
- **US1 (T009-T013)**: Foundational sonrası. T009 çekirdek; T010/T011/T012 T009'a bağlı (T011/T012
  farklı dosya → aralarında [P]); T013 [P].
- **US2 (T014-T015)**: Foundational sonrası (US1'den bağımsız kodlanır; ama MVP değeri için US1+US2
  birlikte). 
- **US3 (T016-T018)**: Foundational sonrası; US1/US2'den bağımsız. T017 T010'un endpoint extension
  dosyasını paylaşır (sıralı). T018 [P].
- **Polish (T019-T021)**: istenen story'ler bitince.

### Story bağımsızlığı
- US1 birim testleri saf (in-memory) — bağımsız. Uçtan uca US2 verisine ihtiyaç duyar.
- US3 tamamen bağımsız (kendi command + endpoint).

---

## Parallel Example

```bash
# Foundational enum'ları birlikte:
T002 CardType.cs · T003 CardBrand.cs · T004 CardProgram.cs

# US1 read-path switch iki farklı dosya:
T011 ProcessPayment.cs · T012 GetInstallmentOptions.cs
```

---

## Implementation Strategy

**MVP** = Phase 1 + 2 + US1 + US2 (çözümleme gerçek veriyle çalışır). US3 (import) ikinci artımdır.

1. Setup + Foundational → tip/eşleme/şema hazır.
2. US1 (çözümle) + US2 (seed) → katalog uçtan uca çalışır, okuma yolu geçti → **MVP**.
3. US3 (import) → güncellenebilirlik.
4. Polish → parite/idempotency elle doğrula, build+test yeşil.

---

## Notes

- [P] = farklı dosya, bağımlılık yok.
- Test = yalnız saf domain (mapping, çözüm static'i, import validation). DB/HTTP/seed round-trip
  quickstart ile elle.
- Her task/mantıksal grup sonrası commit.
- Bilinmeyen BIN → **null** (istisna/sahte-default yok) — kritik karar, T009/T011/T013'te korunur.
- CP.VPOS değiştirilmez; yalnız seed kaynağı + enum legend olarak okunur.