# Data Model: Iyzipay SDK Migration (020)

**Date**: 2026-08-13 | **Plan**: [plan.md](plan.md)

Bu özellik domain verisi TAŞIMAZ: yeni aggregate, tablo, event veya kalıcı durum yok.
"Veri modeli" bu bağlamda taşınan proje envanteri ve dosya eşlemesidir.

## Proje envanteri

### 1. Iyzipay (kütüphane) → `src/services/Iyzipay`

- **İçerik**: iyzico .NET istemcisi — `Model/`, `Request/`, HTTP iletişim katmanı
  (`HttpClient`, `IyzipayResourceV2`, `HashGeneratorV2`, `DigestHelper`, `JsonBuilder` vb.).
- **csproj değişimi**: `TargetFrameworks net45;netstandard2.1` → `TargetFramework net10.0`;
  net45 `<Reference>` blokları ve NuGet-paketleme metadata'sı sadeleşir;
  `ManagePackageVersionsCentrally=false` + `Newtonsoft.Json 13.0.2`.
- **Silinen**: `App.config` (yalnız net45 supportedRuntime), `bin/`, `obj/`.
- **Eklenen**: `README.md` (otherProjects kökünden taşınır).
- **Değişmez**: tüm .cs kaynakları (FR-004).

### 2. Iyzipay.Tests (deterministik) → `tests/Iyzipay.Tests`

- **İçerik**: kök 4 dosya — `HashGeneratorV2Test.cs`, `RequestFormatterTest.cs`,
  `ToStringRequestBuilderTests.cs`, `ToStringRequestStyleTest.cs`. Dış servis yok, saf.
- **csproj**: net10.0; NUnit 3.14.0 + NUnit3TestAdapter 4.6.0 + Microsoft.NET.Test.Sdk
  17.12.0; CPM dışı. MSTest paketleri ATILIR (kaynakta kullanım sıfır — research R1).
- **Referans**: `src/services/Iyzipay`.

### 3. Iyzipay.Tests.Functional (canlı sandbox) → `tests/Iyzipay.Tests.Functional`

- **İçerik**: `Functional/` ağacının tamamı — ~25 fixture + `Builder/` (istek kurucuları)
  + `Util/` (`DecimalHelper`, `RandomGenerator`) + `BaseTest.cs`.
- **csproj**: net10.0; aynı NUnit ailesi; **`<IsTestProject>false</IsTestProject>`** —
  varsayılan `dotnet test` keşfetmez, derleme güvencesi sürer. Elle koşu:
  `dotnet test tests/Iyzipay.Tests.Functional -p:IsTestProject=true` (anahtar gerekir).
- **Referans**: `src/services/Iyzipay`.

### 4. Iyzipay.Samples → `src/services/Iyzipay.Samples`

- **İçerik**: ~30 örnek sınıfı (NUnit `[Test]` metodlu) + `Webhooks/` imza doğrulama
  örnekleri + `Sample.cs` taban sınıfı (canlı sandbox URL, placeholder anahtar).
- **csproj**: net10.0; yalnız NUnit 3.14.0 (atribut derlemesi için) + Newtonsoft 13.0.2;
  `Microsoft.NET.Test.Sdk`/adapter ATILIR; `<IsTestProject>false</IsTestProject>`.
- **Referans**: `src/services/Iyzipay`.

## Çözüm/kök dosya değişimleri

| Dosya | Değişim |
|-------|---------|
| `PaymentGateway.slnx` | `/src/services/` klasörüne Iyzipay + Iyzipay.Samples, `/tests/` klasörüne Iyzipay.Tests + Iyzipay.Tests.Functional eklenir |
| `.gitignore` | Satır `src/otherProjects/` kaldırılır |
| `src/otherProjects/` | Klasör KÖKTEN silinir (README taşındıktan, build.sh silindikten sonra) — FR-006 |
| `CLAUDE.md` | otherProjects bölümü güncellenir; eskimiş `src/otherProjects/CP.VPOS` yolu `src/services/CP.VPOS` olarak düzeltilir; Iyzipay yerleşimi ve CPM-istisna genişlemesi not edilir |

## Durum geçişleri / doğrulama kuralları

Yok — kalıcı durum ve iş kuralı taşınmıyor. Tek "geçiş" derleme-zamanı:
gitignore'lu kopya → izlenen, derlenen, test edilen 4 proje.