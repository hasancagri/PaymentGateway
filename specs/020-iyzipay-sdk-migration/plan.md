# Implementation Plan: Iyzipay SDK Migration

**Branch**: `020-iyzipay-sdk-migration` | **Date**: 2026-08-13 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/020-iyzipay-sdk-migration/spec.md`

## Summary

Gitignore'lu `src/otherProjects/` altındaki Iyzipay SDK'sı (kütüphane + testler +
örnekler) sürüm kontrolündeki kaynak ağacına taşınır ve PaymentGateway.slnx'e eklenir.
Hedef çatılar `net45`/`netstandard2.1`'den `net10.0`'a çekilir; Mono/net45 kalıntıları
temizlenir. Test projesi deterministik (`tests/Iyzipay.Tests`, koşar) ve canlı-sandbox
(`tests/Iyzipay.Tests.Functional`, yalnız derlenir) olarak ikiye bölünür; Samples yalnız
derlenir. Kaynak kod CP.VPOS emsaliyle OLDUĞU GİBİ korunur; hiçbir mevcut projeye Iyzipay
referansı eklenmez (ödeme akışı entegrasyonu sonraki spec).

## Technical Context

**Language/Version**: C# / .NET 10 (`net10.0`; Iyzipay projelerinde `Nullable` ve
`ImplicitUsings` KAPALI — CP.VPOS emsali, bkz. research R3)

**Primary Dependencies**: Newtonsoft.Json 13.0.2 (SDK'nın kendi sürümü); test tarafında
NUnit 3.14.0 + NUnit3TestAdapter 4.6.0 + Microsoft.NET.Test.Sdk 17.12.0 (NUnit 4 BİLİNÇLİ
değil — klasik assert'ler kalkar, kaynak derlenmez; research R4)

**Storage**: N/A (kütüphane; kalıcılık yok)

**Testing**: NUnit (SDK'nın kendi test mirası). Deterministik testler `dotnet test`'te;
canlı-sandbox testleri `IsTestProject=false` ile keşif dışı, elle
`-p:IsTestProject=true` ile koşulabilir (research R5)

**Target Platform**: Çözümün geri kalanıyla aynı (macOS/Linux/Windows dev, .NET 10 SDK)

**Project Type**: Harici istemci kütüphanesi taşıması (3 proje: library + 2 test/örnek →
4 proje olarak yerleşir, bkz. R5 bölünmesi)

**Performance Goals**: N/A (derleme-zamanı taşıma; çalışma zamanı davranışı kapsam dışı)

**Constraints**: Kaynak koda davranışsal müdahale YOK (FR-004); mevcut projelere referans
eklenmez (FR-007); `dotnet test` kimlik bilgisiz yeşil (FR-005); çift kopya yasak (FR-006)

**Scale/Scope**: ~290 .cs dosyası, 3→4 csproj, slnx + .gitignore + CLAUDE.md güncellemesi

## Constitution Check

*GATE: Anayasa v1.4.0'a göre değerlendirildi (Phase 0 öncesi + Phase 1 sonrası yeniden).*

| İlke | Değerlendirme | Sonuç |
|------|---------------|-------|
| I. BC İzolasyonu | Iyzipay BC değil, harici kütüphane; hiçbir BC'ye referans eklenmiyor, DB/model paylaşımı yok | PASS |
| II. Zengin Domain | N/A — olduğu-gibi harici kod; CP.VPOS sınır kuralının aynısı ileride geçerli olacak (tipler slice sınırını geçmeyecek — bu spec'te tüketici yok) | PASS |
| III. Vertical Slice + CQRS | N/A — feature/endpoint eklenmiyor | PASS |
| IV. Result Pattern | N/A — handler yok | PASS |
| V. Merkezi Kimlik/Yetki | N/A — HTTP yüzeyi/endpoint eklenmiyor | PASS |
| VI. Spec-Driven | Tam akış izleniyor (spec → plan → tasks → implement) | PASS |
| Teknoloji: .NET 10 | Üç proje net10.0'a çekiliyor | PASS |
| Teknoloji: CPM | İSTİSNA GENİŞLİYOR — aşağıda Complexity Tracking'de gerekçeli | JUSTIFIED |
| Teknoloji: Nullable/ImplicitUsings açık | İSTİSNA — aşağıda Complexity Tracking'de gerekçeli (CP.VPOS emsali) | JUSTIFIED |
| Test konvansiyonu (saf birim, dış HTTP test edilmez) | Varsayılan koşuya yalnız deterministik testler girer; canlı-API testleri keşif dışı | PASS |

**Post-Phase-1 yeniden değerlendirme**: Tasarım artefaktları yeni ihlal getirmedi;
iki JUSTIFIED istisna Complexity Tracking'de. GEÇTİ.

## Project Structure

### Documentation (this feature)

```text
specs/020-iyzipay-sdk-migration/
├── plan.md              # Bu dosya
├── research.md          # Phase 0 çıktısı (R1-R7 kararları)
├── data-model.md        # Phase 1 çıktısı (proje envanteri — domain verisi yok)
├── quickstart.md        # Phase 1 çıktısı (doğrulama senaryoları)
└── tasks.md             # Phase 2 çıktısı (/speckit-tasks — bu komut üretmez)
```

`contracts/` ÜRETİLMEDİ: özellik dışa yeni arayüz (endpoint, tool, event) açmıyor;
taşınan kütüphanenin kendi API yüzeyi olduğu gibi korunuyor (FR-004).

### Source Code (repository root)

```text
# KAYNAK (taşıma öncesi — gitignore'lu, silinecek):
src/otherProjects/
├── Iyzipay/               → src/services/Iyzipay/
├── Iyzipay.Tests/         → ikiye bölünür (aşağıda)
├── Iyzipay.Samples/       → src/services/Iyzipay.Samples/
├── README.md              → src/services/Iyzipay/README.md
└── build.sh               → SİLİNİR (Mono derleme betiği, anlamsız)

# HEDEF (taşıma sonrası):
src/services/
├── CP.VPOS/                     # mevcut, dokunulmaz (emsal)
├── Iyzipay/                     # kütüphane; net10.0, CPM dışı, Nullable/ImplicitUsings kapalı
│   └── README.md                # SDK README'si (otherProjects kökünden)
└── Iyzipay.Samples/             # örnekler; IsTestProject=false, yalnız derlenir

tests/
├── Iyzipay.Tests/               # kökteki 4 deterministik NUnit dosyası; dotnet test'te koşar
└── Iyzipay.Tests.Functional/    # Functional/ ağacı (fixture+Builder+Util); IsTestProject=false

# GÜNCELLENEN mevcut dosyalar:
PaymentGateway.slnx              # 4 yeni proje (src/services + tests klasörleri)
.gitignore                       # 'src/otherProjects/' satırı kaldırılır
CLAUDE.md                        # otherProjects bölümü + eskimiş CP.VPOS yolu düzeltilir
```

**Structure Decision**: CP.VPOS emsali — harici POS/ödeme SDK'ları `src/services/` altında
CPM-dışı ada olarak durur; test projeleri çözümün `tests/` konvansiyonunu izler.
Deterministik/canlı bölünmesi FR-005'in sıfır kaynak-değişiklikli çözümüdür (research R5).
`bin/`, `obj/` taşınmaz (FR-008; kök `.gitignore` zaten dışlıyor).

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| CPM istisnası 2. projeye genişliyor (Iyzipay ailesi `ManagePackageVersionsCentrally=false`) | Harici, olduğu-gibi taşınan SDK kendi sürüm adasını tutar; legacy-pinli NUnit 3.x ailesi merkezî props'a sızmaz | CPM'e dahil etmek: NUnit 3.x sürümlerini `Directory.Packages.props`'a ekler, xUnit standardını bulandırır; SDK'nın Newtonsoft pini merkezî sürümle çakışma riski taşır. Anayasa metnindeki "bilinçli istisna: CP.VPOS" deseni aynen uygulanıyor — CLAUDE.md implement'te güncellenir |
| `Nullable`/`ImplicitUsings` bu projelerde kapalı (anayasa "her projede açık" der) | ~290 dosyalık harici kod nullable-oblivious; ImplicitUsings, kütüphanenin kendi `Iyzipay.HttpClient` tipiyle `System.Net.Http.HttpClient` ad çakışması üretir | Açmak: ya uyarı seli ya kaynak müdahalesi — FR-004 (olduğu-gibi koruma) ihlali. CP.VPOS aynı gerekçeyle bugün de kapalı (yerleşik emsal) |