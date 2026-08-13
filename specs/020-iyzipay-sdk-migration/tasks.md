# Tasks: Iyzipay SDK Migration

**Input**: Design documents from `/specs/020-iyzipay-sdk-migration/`

**Prerequisites**: plan.md, spec.md, research.md (R1-R7), data-model.md, quickstart.md

**Tests**: Yeni test YAZILMAZ — özellik, SDK'nın MEVCUT testlerini taşıyıp koşturmaktır
(spec US2). Görevler taşınan testlerin koşusunu doğrular.

**Organization**: Görevler user story bazlı; her story bağımsız teslim edilebilir artış.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Paralel koşabilir (farklı dosyalar, bekleyen bağımlılık yok)
- **[Story]**: US1 (kütüphane derlenir), US2 (deterministik testler koşar), US3 (samples derlenir)

## Path Conventions

Plan'daki yerleşim: kütüphane + samples `src/services/`, testler `tests/`.
Kaynak: `src/otherProjects/` (gitignore'lu; taşıma sonrası kökten silinir).
`bin/`/`obj/` klasörleri HİÇBİR görevde taşınmaz (FR-008).

---

## Phase 1: Setup

**Purpose**: Çalışma dalı ve taşıma öncesi durum tespiti

- [X] T001 `master`'dan `020-iyzipay-sdk-migration` dalını oluştur (`git checkout -b 020-iyzipay-sdk-migration`)
- [X] T002 Taşıma öncesi taban çizgisi: `dotnet build` + mevcut test projelerinin (`tests/Payment.Api.Tests`, `tests/Merchant.Api.Tests`, `tests/Commission.Api.Tests`, `tests/Reference.Api.Tests`) yeşil olduğunu doğrula — SC-004 kıyas noktası

**Checkpoint**: Dal açık, mevcut durum yeşil kanıtlı

---

## Phase 2: Foundational

**Purpose**: Yok — bu taşımada tüm story'lerin ortak ön koşulu Phase 1 ile sınırlı.
Story'ler yalnız kütüphane projesine (US1) bağımlıdır; o bağımlılık US1 içinde çözülür.

*(Görev yok — US2 ve US3, US1'in T003-T005 görevlerine bağımlıdır; aşağıda Dependencies bölümünde açık.)*

---

## Phase 3: User Story 1 - Iyzipay kütüphanesi çözümün derlenen parçası olur (Priority: P1) 🎯 MVP

**Goal**: Kütüphane `src/services/Iyzipay`'de, net10.0 hedefli, slnx'e ekli, sıfır hatayla
derlenir ve git tarafından izlenir.

**Independent Test**: `dotnet build` sıfır hata; `git check-ignore src/services/Iyzipay/Iyzipay.csproj`
eşleşme bulmaz (quickstart S1 + S4'ün kütüphane kısmı).

### Implementation for User Story 1

- [X] T003 Kütüphane kaynağını taşı: `src/otherProjects/Iyzipay/` içeriğini (tüm .cs + `App.config` HARİÇ, `bin/`/`obj/` HARİÇ) `src/services/Iyzipay/` altına kopyala; `src/otherProjects/README.md`'yi `src/services/Iyzipay/README.md` olarak taşı
- [X] T004 `src/services/Iyzipay/Iyzipay.csproj`'u yeniden yaz: tek `<TargetFramework>net10.0</TargetFramework>`; `<ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>`; `Newtonsoft.Json 13.0.2` sürümlü referans; net45 `<Reference>` blokları, Mono/`FrameworkPathOverride` kalıntıları ve NuGet-paketleme metadata'sı kaldırılır; `Nullable`/`ImplicitUsings` EKLENMEZ (kapalı kalır — plan Complexity Tracking)
- [X] T005 `PaymentGateway.slnx` `/src/services/` klasörüne `src/services/Iyzipay/Iyzipay.csproj` girdisini ekle
- [X] T006 Derle ve asgari-müdahale kuralını uygula: `dotnet build src/services/Iyzipay` sıfır hata olana dek YALNIZ derlemeyi kıran satırlara asgari düzeltme (research R6); yapılan her düzeltmeyi `specs/020-iyzipay-sdk-migration/quickstart.md` Notlar bölümüne listele
- [X] T007 [US1] Kaynak kopyayı sil: `src/otherProjects/Iyzipay/` klasörünü kaldır (FR-006'nın kütüphane ayağı); `git status` ile `src/services/Iyzipay` dosyalarının izlendiğini doğrula

**Checkpoint**: Kütüphane tek başına derlenir, izlenir; MVP hazır

---

## Phase 4: User Story 2 - Deterministik testler koşar (Priority: P2)

**Goal**: Kök 4 deterministik test dosyası `tests/Iyzipay.Tests`'te `dotnet test` ile koşar;
`Functional/` ağacı `tests/Iyzipay.Tests.Functional`'da yalnız derlenir (`IsTestProject=false`).

**Independent Test**: Kimlik bilgisi tanımsızken `dotnet test tests/Iyzipay.Tests` yeşil;
çözüm-geneli `dotnet test` Functional'ı listelemez (quickstart S2 + S3).

### Implementation for User Story 2

- [X] T008 [P] [US2] `tests/Iyzipay.Tests/` oluştur: `src/otherProjects/Iyzipay.Tests` kökündeki 4 dosyayı (`HashGeneratorV2Test.cs`, `RequestFormatterTest.cs`, `ToStringRequestBuilderTests.cs`, `ToStringRequestStyleTest.cs`) taşı; yeni `Iyzipay.Tests.csproj`: net10.0, CPM dışı, `NUnit 3.14.0` + `NUnit3TestAdapter 4.6.0` + `Microsoft.NET.Test.Sdk 17.12.0`, `ProjectReference ../../src/services/Iyzipay/Iyzipay.csproj`; MSTest paketleri ve Mono kalıntıları ALINMAZ (research R1/R4)
- [X] T009 [P] [US2] `tests/Iyzipay.Tests.Functional/` oluştur: `Functional/` ağacının tamamını (fixture'lar + `Builder/` + `Util/` + `BaseTest.cs`) taşı; csproj T008 ile aynı paket seti + `<IsTestProject>false</IsTestProject>` (research R5); `ProjectReference` aynı
- [X] T010 [US2] `PaymentGateway.slnx` `/tests/` klasörüne `tests/Iyzipay.Tests/Iyzipay.Tests.csproj` ve `tests/Iyzipay.Tests.Functional/Iyzipay.Tests.Functional.csproj` girdilerini ekle
- [X] T011 [US2] Doğrula: `dotnet test tests/Iyzipay.Tests` yeşil (dış servis çağrısı yok); `dotnet test` (çözüm-geneli) Functional'ı koşuya ALMAZ ve yeşil biter; derleme kıran satır olursa asgari düzeltme + quickstart notu (research R6)
- [X] T012 [US2] Kaynak kopyayı sil: `src/otherProjects/Iyzipay.Tests/` klasörünü kaldır

**Checkpoint**: US1 + US2 bağımsız doğrulanabilir; testli taşıma tamam

---

## Phase 5: User Story 3 - Örnek kullanım kodu derlenir referans olarak korunur (Priority: P3)

**Goal**: Samples `src/services/Iyzipay.Samples`'ta derlenir; hiçbir otomatik koşuya girmez.

**Independent Test**: `dotnet build` Samples dahil sıfır hata; çözüm-geneli `dotnet test`
Samples'ı listelemez (quickstart S1 + S3).

### Implementation for User Story 3

- [X] T013 [US3] `src/services/Iyzipay.Samples/` oluştur: `src/otherProjects/Iyzipay.Samples`'ın tüm .cs dosyalarını + `Webhooks/` klasörünü taşı (`bin`/`obj` hariç); yeni `Iyzipay.Samples.csproj`: net10.0, CPM dışı, YALNIZ `NUnit 3.14.0` (atribut derlemesi) + `Newtonsoft.Json 13.0.2`, `<IsTestProject>false</IsTestProject>`, `ProjectReference ../Iyzipay/Iyzipay.csproj`; `Microsoft.NET.Test.Sdk`/adapter/MSTest ALINMAZ (research R5)
- [X] T014 [US3] `PaymentGateway.slnx` `/src/services/` klasörüne `src/services/Iyzipay.Samples/Iyzipay.Samples.csproj` girdisini ekle; `dotnet build` sıfır hata (asgari düzeltme kuralı + quickstart notu — `Sample.cs` çift-using uyarısı beklenir, research R6); `dotnet test` Samples'ı koşuya almadığını doğrula
- [X] T015 [US3] Kaynak kopyayı sil: `src/otherProjects/Iyzipay.Samples/` klasörünü kaldır

**Checkpoint**: Üç story de bağımsız işlevsel

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Kalıntı temizliği, dokümantasyon, uçtan uca doğrulama

- [X] T016 `src/otherProjects/` kalanını temizle: `build.sh`'ı sil, boşalan klasörü kaldır; `.gitignore`'dan `src/otherProjects/` satırını çıkar (FR-006 tam kapanış); `git status`'ta beklenmedik dosya (bin/obj, .DS_Store vb.) girmediğini doğrula (FR-008)
- [X] T017 [P] `CLAUDE.md` güncelle: `src/otherProjects/CP.VPOS` eskimiş yolu `src/services/CP.VPOS` yap; otherProjects bölümünü kaldır/yeniden yaz; `src/services/Iyzipay` (+ Samples) yerleşimini, CPM istisnasının "harici olduğu-gibi kütüphaneler (CP.VPOS, Iyzipay)" olarak genişlediğini ve `dotnet test tests/Iyzipay.Tests` komutunu işle
- [X] T018 Quickstart S1-S5 senaryolarını uçtan uca koş (`specs/020-iyzipay-sdk-migration/quickstart.md`): S1 build, S2 deterministik test, S3 varsayılan koşu dışılık, S4 git izleme + çift-kopya-yok, S5 referans sızmadı; sonuçları quickstart Notlar'a işle (SC-001..SC-004 kanıtı)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: Bağımsız — hemen başlar
- **Foundational (Phase 2)**: Görev yok
- **US1 (Phase 3)**: T001 sonrası; T003→T004→T005→T006→T007 sıralı
- **US2 (Phase 4)**: T006 sonrası başlar (kütüphane derlenir olmalı; T007'yi beklemez)
- **US3 (Phase 5)**: T006 sonrası başlar; US2'den bağımsız
- **Polish (Phase 6)**: T016 tüm story'lerin silme görevlerini (T007, T012, T015) bekler; T017 paralel; T018 en son

### User Story Dependencies

- **US1 (P1)**: Başka story'ye bağımlı değil
- **US2 (P2)**: Yalnız US1'in kütüphane derlemesine (T006) bağımlı; US3'ten bağımsız
- **US3 (P3)**: Yalnız T006'ya bağımlı; US2'den bağımsız

### Parallel Opportunities

- T008 ‖ T009 (farklı proje klasörleri; ikisi de T006 sonrası)
- US2 (T008-T012) ‖ US3 (T013-T015) — farklı dosya kümeleri; yalnız slnx düzenlemeleri (T010 vs T014) aynı dosyaya dokunur, ardışık uygulanır
- T017 ‖ T016 (farklı dosyalar)

---

## Parallel Example: US2 + US3 birlikte

```bash
# T006 (kütüphane derlendi) sonrası aynı anda:
Task: "T008 tests/Iyzipay.Tests oluştur (4 deterministik dosya + csproj)"
Task: "T009 tests/Iyzipay.Tests.Functional oluştur (Functional ağacı + IsTestProject=false)"
Task: "T013 src/services/Iyzipay.Samples oluştur (samples + Webhooks + csproj)"
# Ardından sıralı: T010 → T014 (ikisi de PaymentGateway.slnx'i düzenler)
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Phase 1 (dal + taban çizgisi)
2. Phase 3: T003-T007 — kütüphane taşındı, derlendi, izleniyor
3. **DUR ve DOĞRULA**: quickstart S1 + S4 (kütüphane kısmı)

### Incremental Delivery

1. US1 → build yeşil → MVP
2. US2 → `dotnet test tests/Iyzipay.Tests` yeşil, Functional koşu dışı
3. US3 → Samples derlenir, koşu dışı
4. Polish → otherProjects/gitignore/CLAUDE.md temizliği + S1-S5 uçtan uca

---

## Notes

- Kaynak .cs dosyalarına müdahale YASAK; yalnız derleme kıran satıra asgari düzeltme, her
  düzeltme quickstart Notlar'a yazılır (FR-004, research R6)
- NUnit 4'e YÜKSELTME YOK (klasik assert'ler derlenmez — research R4)
- `dotnet build`/`dotnet test` çıktıları görev kapanış kanıtıdır; kırmızıyken görev kapanmaz
- Commit'ler görev veya mantıksal grup başına; mesajlar Türkçe (anayasa Geliştirme Akışı)