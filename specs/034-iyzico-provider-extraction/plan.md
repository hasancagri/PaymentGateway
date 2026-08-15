# Implementation Plan: Iyzico.Provider Çekirdek Çıkarımı

**Branch**: `034-iyzico-provider-extraction` | **Date**: 2026-08-15 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/034-iyzico-provider-extraction/spec.md`

## Summary

Üç serviste (Payment.Api, Merchant.Api, Commission.Api) birebir aynı (md5-özdeş) 14 iyzico
transport dosyasını yeni paylaşılan `src/others/Iyzico.Provider` class lib'ine taşı; kopyaları
sil; üç BC'ye ProjectReference ekle. BC-özel istek/yanıt alt klasörleri, secret'lı config ve
runtime wiring BC'de kalır. Davranış-korumalı mekanik taşıma — çalışma-anı davranışı bit
düzeyinde değişmez.

## Technical Context

**Language/Version**: C# / .NET 10 (net10.0)

**Primary Dependencies**: Newtonsoft.Json (CPM'de 13.0.4 kayıtlı; çekirdeğin tek paket bağımlılığı)

**Storage**: N/A (çekirdek transport, kalıcılık yok)

**Testing**: `dotnet test` (mevcut saf domain testleri); davranış-koruma = üretilen istek gövde/başlık/imza karşılaştırması

**Target Platform**: sunucu (Aspire orkestrasyonu); Iyzico.Provider yalnız kütüphane, host değil

**Project Type**: paylaşılan altyapı class kütüphanesi (BC değil; Common/Shared hizasında)

**Performance Goals**: N/A (davranış değişmez, performans nötr)

**Constraints**: davranış bit-düzeyinde korunur; CPM kuralı korunur (proje dosyasında sürüm yok); sınır kuralı korunur (transport tipi domain'e sızmaz)

**Scale/Scope**: 14 çekirdek dosya taşınır; 3 BC dokunulur (csproj + GlobalUsings + namespace using). Yeni domain kodu yok.

## Constitution Check

*GATE: Phase 0 öncesi geçmeli; Phase 1 sonrası yeniden.*

- **İlke I (BC İzolasyonu) — PASS.** Iyzico.Provider paylaşılan **domain modeli DEĞİL**; paylaşılan
  *transport altyapısı* (HTTP/hash/JSON) — `Common`/`Shared` gibi anayasa-onaylı paylaşımlar
  kategorisinde. BC-özel istek/yanıt tipleri (Payments/Onboarding/Payout…) BC'de kalır; bir BC
  başka BC'nin tipini görmez. CP.VPOS sınır kuralının (madde "CP.VPOS sınırı") analoğu: transport
  tipi slice/domain sınırını geçmez, handler sınırında domain temsiline çevrilir.
- **CPM (Teknoloji Kısıtları) — PASS.** Yeni lib sürümsüz `PackageReference` (Newtonsoft.Json)
  kullanır; sürüm `Directory.Packages.props`'tan. CP.VPOS istisnası DEĞİL.
- **İlke VI (Spec-Driven) — PASS.** Bu akış spec-kit'ten geçiyor.
- **İlke II/III/IV/V — N/A.** Refactor domain/feature/result/auth yüzeyine dokunmaz; davranış aynı.

**Sonuç: gate geçti; gerekçelendirilmiş ihlal yok. Complexity Tracking boş.**

## Project Structure

### Documentation (this feature)

```text
specs/034-iyzico-provider-extraction/
├── plan.md              # bu dosya
├── research.md          # Phase 0 — kararlar (konum, namespace, görünürlük, using, CPM)
├── quickstart.md        # Phase 1 — doğrulama rehberi (build + test + md5 + istek karşılaştırma)
├── checklists/
│   └── requirements.md  # spec kalite checklist'i (16/16 geçti)
└── tasks.md             # /speckit-tasks çıktısı (bu komutta ÜRETİLMEZ)
```

data-model.md ve contracts/ **üretilmedi** — bilinçli: refactor'ün domain entity'si yok (kod
taşıma) ve dış kontrat değişmez (davranış aynı; iyzico wire kontratı sabit). Anayasa "if
applicable" — uygulanmıyor.

### Source Code (repository root)

```text
src/others/Iyzico.Provider/                 # YENİ class lib
├── Iyzico.Provider.csproj                   # net10.0, Nullable+ImplicitUsings, PackageReference Newtonsoft.Json (sürümsüz)
├── BaseRequestV2.cs                          # taşınan 14 çekirdek (namespace Iyzico.Provider)
├── DigestHelper.cs
├── HashGeneratorV2.cs
├── HttpClient.cs
├── JsonBuilder.cs
├── PagingRequest.cs
├── ProviderConstants.cs
├── ProviderOptions.cs                        # transport-config tipi (BC secret'lı settings buna map'lenir)
├── ProviderResourceV2.cs                     # protected GetHttpHeaders* — miras yoluyla cross-assembly
├── RequestFormatter.cs
├── RequestStringConvertible.cs
├── RestHttpClientV2.cs                        # internal → PUBLIC (alt klasörler ayrı assembly'den çağırır)
├── StringHelper.cs                            # internal KALIR (yalnız RequestFormatter çekirdek-içi kullanır)
└── ToStringRequestBuilder.cs

src/services/Payment.Api/Provider/            # çekirdek SİLİNİR; alt klasörler KALIR
├── Payments/ Installments/ StoredCards/      # BC-özel istek/yanıt (namespace <BC>.Provider.<Sub> aynı)
src/services/Merchant.Api/Provider/Onboarding/
src/services/Commission.Api/Provider/{Payout,Reporting}/

PaymentGateway.slnx                           # <Project Path="src/others/Iyzico.Provider/Iyzico.Provider.csproj" /> eklenir
```

**Structure Decision**: yeni proje `src/others/` altında (paylaşılan altyapı hizası — Common,
Shared, Identity.Server yanında). BC referans verir; ters bağımlılık yok. Her BC'de yalnız 3 tür
dokunuş: (1) `.csproj` ProjectReference, (2) GlobalUsings çekirdek-using değişimi/ekleme,
(3) çekirdek dosya silme. Payment ayrıca Program.cs'te `new Payment.Api.Provider.ProviderOptions`
→ `new Iyzico.Provider.ProviderOptions` (tek satır; Merchant/Commission'da provider runtime-wiring
yok — 022 ara durum).

## Complexity Tracking

> Constitution Check ihlali yok — boş.
