# Implementation Plan: Bank Referansı + Komisyon Grid

**Branch**: `feat/bank-reference-commission-grid` | **Date**: 2026-07-31 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/002-bank-reference-commission-grid/spec.md`

## Summary

Commission BC'ye `Bank` referans aggregate'i (Code, Name, IsActive, SupportedInstallments) + tam
CRUD eklenir; bankalar seed edilmez (DB boş başlar) ama operatör serbest metin yerine kanonik bir
**banka katalogundan** (CP.VPOS `BankService.AllBanks`'ten statik kopyalanan Code+Name; runtime
bağımlılık yok) seçerek ekler. Ad ve kod katalogdan gelir, ikisi de immutable; operatör yalnız taksit
setini + aktifliği yönetir. Admin UI'a banka yönetim sayfaları (katalog selectbox + taksit 1..15
checkbox grid) ve bir banka seçildiğinde o bankanın desteklediği taksitlere göre kart markası × tip ×
bölge × taksit tüm kombinasyonlarını gösteren, eksikleri işaretleyen ve toplu kaydeden bir komisyon
grid'i eklenir. Amaç: bir bankanın hiçbir komisyon kombinasyonunun eksik kalmaması.

## Technical Context

**Language/Version**: C# / .NET 10 (Nullable + ImplicitUsings açık)

**Primary Dependencies**: Marten (Postgres document store), Wolverine (in-process bus), Asp.Versioning,
Scrutor; Admin tarafı ASP.NET Core Razor Pages + typed HttpClient. Yeni paket YOK (CPM korunur).

**Storage**: Marten/Postgres — `commission` şeması. Yeni belge: `Bank` (seed yok, boş başlar).
Mevcut: `BankCommission`, `MerchantCommission`.

**Testing**: xUnit saf domain birim testleri (`tests/Commission.Api.Tests`); banka/dış HTTP test edilmez.

**Target Platform**: Aspire ile orkestre edilen Linux/masaüstü sunucu servisleri (Postgres + RabbitMQ).

**Project Type**: Web (mikroservis backend `Commission.Api` + Razor Pages Admin UI + YARP gateway).

**Performance Goals**: Etkileşimli admin; belirli bir SLA yok. Grid en fazla 4×3×2×15 = 360 satır/banka
(tipik 144 @ 6 taksit) — tek istekte döner.

**Constraints**: CP.VPOS tipleri slice sınırını geçmez; banka katalogu (Code+Name) CP.VPOS
`BankService.AllBanks`'ten statik veri olarak Commission.Api içine kopyalanır (runtime CP.VPOS
bağımlılığı/tip geçişi yok — `AllBanks` zaten `internal`, erişilemez). Yetki bu dilimde uygulanmaz
(proje geneli erteleme). Yalnız TL (para birimi modeli yok; oran yüzdedir).

**Scale/Scope**: Operatörün gireceği kadar banka (elle), banka başına ≤360 komisyon kombinasyonu.
Tek operatör rolü.

## Constitution Check

*GATE: Phase 0 öncesi geçmeli; Phase 1 sonrası yeniden bakılır.*

| İlke | Durum | Not |
|------|-------|-----|
| I. BC İzolasyonu | ✅ | `Bank` Commission.Api içinde, kendi şeması. Başka BC DB'sine erişim yok. Banka POS entegrasyonu (CP.VPOS) ayrı ve dokunulmuyor. |
| II. Zengin Domain | ✅ | `Bank`: private setter + statik `Create`/`Update`/`SoftDelete`; taksit koleksiyonu private, validasyon aggregate içinde. |
| III. Vertical Slice + CQRS | ✅ | `Domains/Banks/Features/{Commands,Queries}`; `[Transactional]` command handler'lar; `IDocumentSession`; endpoint-extension. |
| IV. Result Pattern | ✅ | `ResultDomain`/`FeatureObjectResultModel`; `MessageItem.Code` resource sabiti (yeni: `BANK_HAS_COMMISSIONS`). |
| V. Merkezi Kimlik & Açık Yetki | ⚠️ Ertelendi | Endpoint'ler bu dilimde korumasız — mevcut Commission/Merchant slice'larıyla tutarlı, proje geneli AUTHZ_MODEL ertelemesi (constitution TODO). Yeni açık sınır ilkesi getirmez. |
| VI. Spec-Driven | ✅ | Bu akış (specify→plan→tasks→implement) izleniyor. |
| Tekno kısıtı — CP.VPOS sınırı | ✅ | Banka listesi seed edilmez; CP.VPOS'a runtime bağımlılık/tip geçişi yok. |
| Tekno kısıtı — CPM | ✅ | Yeni paket yok. |
| Tekno kısıtı — yalnız TL | ✅ | Oran yüzde; para birimi alanı yok. |
| Akış — Türkçe | ✅ | Yorumlar/mesajlar/commit Türkçe. |

**Sonuç**: Geçer. Tek uyarı yetki ertelemesi — mevcut kod tabanıyla tutarlı, gerekçesi
Complexity Tracking'de.

## Project Structure

### Documentation (this feature)

```text
specs/002-bank-reference-commission-grid/
├── plan.md              # Bu dosya
├── research.md          # Phase 0
├── data-model.md        # Phase 1
├── quickstart.md        # Phase 1
├── contracts/           # Phase 1 (banks + bulk bank-commissions HTTP kontratı)
├── checklists/
│   └── requirements.md  # /speckit-specify çıktısı
└── tasks.md             # /speckit-tasks çıktısı (bu komut üretmez)
```

### Source Code (repository root)

```text
src/services/Commission.Api/
├── Domains/
│   ├── Banks/                              # YENİ aggregate + slice
│   │   ├── Bank.cs                         # Name katalogdan türer; Create(code,installments)/Update(isActive,installments)
│   │   ├── BankCatalog.cs                  # YENİ statik kanonik katalog (CP.VPOS'tan kopya Code+Name)
│   │   ├── BankEndpointExtension.cs
│   │   └── Features/
│   │       ├── Commands/{CreateBank,UpdateBank,DeleteBank}.cs
│   │       └── Queries/{GetBanks,GetBank,GetBankCatalog}.cs   # GetBankCatalog YENİ
│   ├── BankCommissions/
│   │   └── Features/Commands/BulkUpsertBankCommissions.cs   # YENİ toplu endpoint
│   └── SharedKernel/CommissionResourceConstants.cs          # + BANK_HAS_COMMISSIONS
├── Program.cs                              # Schema.For<Bank>, AddBankGroup...

src/ui/Admin/
├── Clients/
│   ├── CommissionApiClient.cs              # + bank & bulk metotları
│   └── ApiModels.cs                        # + Bank request/response modelleri
├── Pages/
│   ├── Banks/{Index,Create,Edit,Delete}.cshtml(.cs)         # Create: katalog selectbox; Edit: Code+Name salt-görünüm; taksit 1..15 checkbox grid
│   ├── BankCommissions/Create.cshtml(.cs)  # grid; eksen filtre + "boşları doldur" + 20'li sayfalama
│   └── BankCommissions/Index.cshtml(.cs)   # "Banka" kolonu kod→ad; + eksen filtre + 20'li sayfalama (salt-görünüm)
├── Pages/Shared/_Layout.cshtml             # nav'a "Bankalar" + "Komisyon Grid"
├── MessageText.cs                          # + BANK_HAS_COMMISSIONS metni
└── wwwroot/
    ├── css/site.css                        # grid + .missing + filtre/doldur + sayfalama stili
    └── js/filterable-table.js              # JENERIK client eksen filtresi + 20'li sayfalama + opsiyonel doldur (grid ve liste ortak; commission-grid.js'ten refactor)

tests/Commission.Api.Tests/
└── BankTests.cs                            # YENİ saf domain testleri
```

**Structure Decision**: Mevcut Commission.Api vertical-slice düzeni ve Admin Razor Pages yapısı
korunur. `Bank` yeni bir Domains alt-klasörü; komisyon grid'i mevcut BankCommission slice'ına toplu
endpoint ekler. Yeni servis/proje yok.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Endpoint'ler yetkisiz (İlke V ertelemesi) | Proje geneli AUTHZ_MODEL kararı Identity dilimine ertelendi; mevcut tüm slice'lar aynı durumda | Bu dilimde yetki getirmek, henüz kararlaşmamış modeli tek feature'a özgü uydurmak olur — tutarsızlık yaratır. Erteleme constitution TODO'sunda takipli. |