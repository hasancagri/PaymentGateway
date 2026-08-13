# Research: Iyzipay SDK Migration (020)

**Date**: 2026-08-13 | **Spec**: [spec.md](spec.md)

## R1 — Mevcut durum envanteri

**Bulgular** (kod incelemesi, 2026-08-13):

- `src/otherProjects/` `.gitignore` satır 21 ile **tamamen dışlanmış** — Iyzipay kaynağı
  bugün sürüm kontrolünde YOK.
- İçerik: `Iyzipay/` (kütüphane, 21 dosya), `Iyzipay.Tests/` (55 .cs), `Iyzipay.Samples/`
  (~30 .cs + `Webhooks/`), `README.md` (SDK README'si), `build.sh` (Mono derleme betiği).
- Hedef çatılar: kütüphane `net45;netstandard2.1`, test + samples yalnız `net45` (+ Mono
  `FrameworkPathOverride` geçici çözümleri).
- Kaynakta **hiç `#if` koşullu derleme yok**, **`ConfigurationManager` kullanımı yok**;
  `App.config` yalnız `<supportedRuntime v4.5>` içerir (modern hedefte anlamsız → silinir).
- Test projesi paketlerde MSTest + NUnit birlikte listeler ama kaynakta **MSTest kullanımı
  SIFIR** (hiç `[TestMethod]`/`Microsoft.VisualStudio.TestTools` yok); tüm testler NUnit.
- Test yapısı: kökte 4 deterministik test dosyası (`HashGeneratorV2Test`,
  `RequestFormatterTest`, `ToStringRequestBuilderTests`, `ToStringRequestStyleTest`);
  `Functional/` altında ~25 canlı-sandbox fixture + bunların kullandığı `Builder/` ve
  `Util/` yardımcıları.
- Samples: her örnek NUnit `[Test]` metodu; `Sample` taban sınıfı `[SetUp]`'ta placeholder
  API anahtarıyla `https://sandbox-api.iyzipay.com`'a bağlanır → **tamamı canlı-API**,
  deterministik örnek yok.
- CP.VPOS emsal mekanizması: `src/services/CP.VPOS/CP.VPOS.csproj` içinde
  `<ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>` + sürümlü
  `PackageReference` (Newtonsoft 13.0.1). CLAUDE.md'deki `src/otherProjects/CP.VPOS` yolu
  ESKİMİŞ — gerçek konum `src/services/CP.VPOS` (slnx doğruluyor).
- Merkezî paketler: `Directory.Packages.props` Newtonsoft.Json 13.0.4 tanımlı; NUnit ailesi
  tanımlı değil (çözüm testleri xUnit).

## R2 — Hedef yerleşim

**Decision**: Kütüphane `src/services/Iyzipay`, örnekler `src/services/Iyzipay.Samples`,
testler `tests/Iyzipay.Tests` (deterministik) + `tests/Iyzipay.Tests.Functional` (canlı).
`src/otherProjects/` klasörü ve `.gitignore`'daki `src/otherProjects/` satırı tamamen
kaldırılır; SDK README'si `src/services/Iyzipay/README.md` olarak taşınır, `build.sh` silinir.

**Rationale**: CP.VPOS emsali birebir (`src/services/CP.VPOS`); test projeleri çözümün
`tests/` konvansiyonunu izler. Çift kopya yasağı (FR-006) klasörün kökten silinmesini
gerektirir; gitignore satırı kalırsa gelecekte aynı tuzak tekrar kurulur.

**Alternatives considered**: (a) `src/otherProjects`'te bırakıp gitignore'dan çıkarmak —
"otherProjects = versiyonlanmayan referans" anlamını bozar, CP.VPOS emsaline aykırı.
(b) `src/others/` — orası altyapı servisleri (Common, Shared, Identity) için; harici POS/SDK
kütüphaneleri `src/services`'te (CP.VPOS emsali).

## R3 — Hedef çatı ve derleme ayarları

**Decision**: Üç projede tek hedef `net10.0`. `net45`/`netstandard2.1`, Mono
`FrameworkPathOverride` blokları, eski `<Reference Include="System.*">` listeleri ve
`App.config` kaldırılır. `Nullable` ve `ImplicitUsings` KAPALI kalır (CP.VPOS emsali,
olduğu-gibi harici kod). Paket temizliği: MSTest.* ve kullanılmayan adapter kaldırılır.

**Rationale**: Spec FR-003 modern hizalama ister; çözüm standardı net10.0.
`ImplicitUsings` özellikle riskli: kütüphanenin kendi `Iyzipay.HttpClient` tipi var —
implicit `System.Net.Http` using'i ad çakışması üretir. Nullable açmak ~300 dosyalık
harici kodda uyarı seli yaratır; davranışsal koruma (FR-004) sözleşmesine aykırı müdahale
gerektirir.

**Alternatives considered**: `net10.0` + `netstandard2.1` çift hedef — çözümde tüketici
tek (ileride Payment.Api, net10.0); NuGet'e paket yayınlamıyoruz, çift hedef ölü yük.

## R4 — Paket yönetimi (CPM)

**Decision**: Üç Iyzipay projesi de CP.VPOS gibi CPM DIŞI:
`<ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>` + kendi sürümleri.
Sürümler: Newtonsoft.Json 13.0.2 (SDK'nın kendi sürümü korunur), NUnit 3.14.0 (3.x'in son
sürümü), NUnit3TestAdapter 4.6.0, Microsoft.NET.Test.Sdk 17.12.0.

**Rationale**: CP.VPOS emsali — harici, olduğu-gibi taşınan kütüphane kendi sürüm adasını
tutar; `Directory.Packages.props`'a legacy-pinli NUnit 3.x ailesi sızdırılmaz (çözüm
testleri xUnit). **NUnit 4'e YÜKSELTİLMEZ**: NUnit 4 klasik assert'leri
(`Assert.AreEqual`) kaldırdı — mevcut test kaynağı derlenmez, FR-004 (kaynak olduğu gibi)
ihlal olur. NUnit 3.14 modern TFM'lerde çalışır.

**Alternatives considered**: CPM'e dahil etmek — Newtonsoft 13.0.4 merkezi sürümüyle uyum
kolay ama NUnit ailesini merkezî props'a eklemek çözüm-geneli konvansiyonu (xUnit)
bulandırır; iki istisnayı tek desende (CP.VPOS modeli) tutmak daha okunur. Anayasadaki
"bilinçli istisna: CP.VPOS" cümlesi bu plana göre "harici olduğu-gibi kütüphaneler
(CP.VPOS, Iyzipay)" olarak genişler — Complexity Tracking'de gerekçeli; CLAUDE.md
güncellemesi implement kapsamında.

## R5 — Testlerin ayrıştırılması (deterministik vs canlı)

**Decision**: Test projesi İKİYE bölünür:

- `tests/Iyzipay.Tests` — kökteki 4 deterministik dosya. Normal test projesi
  (`Microsoft.NET.Test.Sdk` + NUnit + adapter); `dotnet test`'te koşar.
- `tests/Iyzipay.Tests.Functional` — `Functional/` ağacının tamamı (fixture + Builder +
  Util). Aynı paketler AMA `<IsTestProject>false</IsTestProject>` → `dotnet test`
  varsayılan koşusu keşfetmez, proje yine derlenir. Elle koşu (anahtar sağlanınca):
  `dotnet test tests/Iyzipay.Tests.Functional -p:IsTestProject=true`.

Samples projesi test projesi OLMAZ: `Microsoft.NET.Test.Sdk`/adapter kaldırılır, NUnit
yalnız `[Test]`/`[SetUp]` atributlarının derlenmesi için kalır; `<IsTestProject>false`
açıkça set edilir (NUnit referansı sdk'nın otomatik işaretlemesini tetiklemesin).

**Rationale**: FR-005 — kimlik bilgisi olmadan yeşil koşu + canlıların derlenmeye devamı.
Proje bölme sıfır kaynak-dosya değişikliğiyle bunu sağlar; alternatiflerin hepsi kaynak
düzenlemesi ister. `Sample` taban sınıfı `[SetUp]`'ta canlı URL'ye hazırlandığından
Samples'da koşturulabilir deterministik alt küme yok.

**Alternatives considered**: (a) Tek projede ~25 fixture'a `[Explicit]` eklemek — 25
dosyaya kaynak müdahalesi, FR-004 ruhuna aykırı ve unutulan fixture koşuyu kırar.
(b) `--filter Category!=...` — varsayılan `dotnet test` çağrısını korumaz, disiplin ister.
(c) Tümünü `IsTestProject=false` yapmak — deterministik testler de koşamaz, SC-002 boşa düşer.

## R6 — Kaynak-kod müdahale sınırı

**Decision**: Kaynak dosyalara dokunulmaz; yalnız derleme kırılırsa asgari düzeltme yapılır
ve plan-dışı her düzeltme quickstart'ta not edilir. Bilinen riskler: `Sample.cs`'te çift
`using` (BOM kalıntılı) — uyarı üretir, derlemeyi kırmaz; `ConsoleTraceListener` modern
.NET'te mevcut (3.0+); Newtonsoft davranış farkı (README'deki net45 ondalık kırpması) tek
modern hedefte tek davranışa iner, deterministik 4 test bundan etkilenmez (hash/format
testleri).

**Rationale**: FR-004 (CP.VPOS emsali — API yüzeyi ve iş mantığı yeniden yazılmaz).

## R7 — Kapsam dışı teyitleri

- Hiçbir mevcut projeye Iyzipay `ProjectReference`'ı eklenmez (FR-007); AppHost/Aspire
  değişikliği yok (kütüphane servis değil).
- Ödeme kanalı entegrasyonu (PosAccount-adayı, BankRouter katılımı — bkz. memory
  `project_iyzico_integration_future`) sonraki spec döngüsü; bu taşıma ön koşul.
- CLAUDE.md güncellemesi (otherProjects bölümü + eskimiş CP.VPOS yolu) implement'te yapılır.