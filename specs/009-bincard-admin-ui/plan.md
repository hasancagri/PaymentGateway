# Implementation Plan: BinCard Katalog Görüntüleme Ekranları (Admin)

**Branch**: `009-bincard-admin-ui` | **Date**: 2026-08-02 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/009-bincard-admin-ui/spec.md`

## Summary

008 ile Payment BC DB'sine alınan BinCard kataloğunu (~9957 kayıt) Admin Razor Pages BFF'te
**salt-okuma** göster. İki ekran: (US1) BIN gir → tekil detay (banka/tip/marka/program/ticari +
taksit-banka listesi); (US2) sayfalı + çok-alanlı filtreli liste. Admin backend'e kural sızdırmaz;
yalnız Payment.Api sonucunu Türkçe gösterir.

**Teknik yaklaşım:**
- **Payment.Api'ye iki yeni okuma ucu** (BinCards domain, mevcut vertical slice deseni):
  - `GetBinCardDetail`: `GET api/v1/bin-cards/{bin}` — mevcut debug ucunu **zenginleştirir**; ham
    BinCard alanları + türetilmiş taksit-banka listesi döner (US1). İç çözümleme (`ResolveBinCard`
    static, ProcessPayment/GetInstallmentOptions tüketicileri) **değişmez**.
  - `ListBinCards`: `GET api/v1/bin-cards?bankCode=&cardProgram=&cardType=&cardBrand=&commercial=&page=&pageSize=`
    — çok-alanlı AND filtre + sayfalı ham katalog listesi (US2). Marten `ToPagedList`.
  - Yanıt DTO'larında enum'lar **string ad** olarak döner (Admin, Payment.Api enum tipine bağımlı
    kalmasın); filtreler string/parse ile alınır.
- **Admin'e payment-api tüketimi**: yeni typed `IBinCardApiClient` (BaseAddress `http://payment-api`),
  `ApiModels`'e detail/list/filter modelleri, `Program.cs` kaydı, AppHost'ta Admin→payment-api referansı.
- **İki Razor sayfası** `Pages/BinCards/`: `Resolve` (tekil arama/detay) + `Index` (filtreli sayfalı
  liste). Enum→Türkçe etiket eşlemesi Admin'de (sunum), _Layout'a menü bağlantısı.

## Technical Context

**Language/Version**: C# / .NET 10 (`Nullable` + `ImplicitUsings` açık)

**Primary Dependencies**: Mevcut — ASP.NET Core Razor Pages (Admin BFF), typed `HttpClient` + Aspire
service discovery, Marten 9.5 (`ToPagedList` sayfalama), WolverineFx 6.4 (query bus). **Yeni paket
gerekmez** (PagedList.Core zaten Common'da `FeaturePagedResultModel` ile mevcut).

**Storage**: Marten (Postgres, `paymentDb`) — 008'in `BinCard` document'i. Filtre/sayfalama sorgu
zamanında; `CardProgram` zaten indexli (008). Ek index gerekirse plan'da işaretli (bkz. research R3).

**Testing**: Saf domain birim testi kapsamı sınırlı — filtre/sayfalama Marten LINQ (DB), enum
parse/etiket saf. Razor Pages/HTTP/BFF entegrasyonu test edilmez (proje kuralı) → quickstart ile elle
(005 deseniyle aynı: BFF smoke).

**Target Platform**: Linux/container, Aspire orchestrated (`admin-web` + `payment-api` node'ları).

**Project Type**: Web application — mevcut Admin (frontend BFF) + Payment.Api (backend) eklemesi.

**Performance Goals**: Liste sorgusu her zaman sayfalı (sabit `pageSize`, sunucu tarafı sınır) —
~9957 kaydın tamamı hiçbir görünümde çekilmez (SC-002). Detay: `BinNumber` exact-match indexli.

**Constraints**: Admin backend'e kural sızdırmaz (FR-011, anayasa I). CP.VPOS'a dokunulmaz. Yalnız
TL/yurt-içi. Yetki yok (proje-geneli erteleme).

**Scale/Scope**: ~9957 kayıt. 2 yeni backend query + endpoint; 1 Admin client; 2 Razor sayfası +
enum etiket yardımcısı; AppHost + Admin Program.cs kaydı. Per-kayıt CRUD yok.

## Constitution Check

*GATE: Phase 0 öncesi geçti. Phase 1 sonrası tekrar bakıldı — ihlal yok (Complexity Tracking'e bakınız).*

| İlke | Durum | Not |
|------|-------|-----|
| I. Bounded Context İzolasyonu | ✅ | Admin, payment-api'yi HTTP + service discovery ile tüketir (DB erişimi yok). Yeni sorgular Payment BC içinde. `bankCode` ham string gösterilir (banka adı için cross-BC çağrı YOK — bilinçli, spec assumption). |
| II. Zengin Domain Modeli | ✅ (N/A) | Yeni aggregate yok; detay/liste 008 `BinCard` lookup document'i üzerinde okuma sorgusu. 008'in read-model gerekçesi geçerli. |
| III. Vertical Slice + CQRS | ✅ | `Queries/GetBinCardDetail` + `Queries/ListBinCards` (static class, record+Response+Handler+Endpoint). Repository yok, `IQuerySession`. |
| IV. Result Pattern | ✅ | Liste `FeaturePagedResultModel<T>`; detay `FeatureObjectResultModel<T>` (bulunamazsa NotFound). Admin `ApiResult` zarfı. |
| V. Merkezi Kimlik & Açık Yetki | ⚠️ ertelenmiş | Yeni okuma uçları + Admin ekranları korumasız — proje-geneli AUTHZ ertelemesi. Salt-okuma (state değişmez) → risk düşük; **işaretli** (Identity BC'de kapanır). |
| VI. Spec-Driven | ✅ | spec→plan→tasks→implement. |

**Teknoloji kısıtları:** .NET 10 + Aspire ✅ · Marten (`ToPagedList`) ✅ · Wolverine ✅ · CPM (yeni
paket yok) ✅ · yalnız TL ✅ · Admin kural sızdırmaz (yalnız API sonucu + Türkçe etiket) ✅.

## Project Structure

### Documentation (this feature)

```text
specs/009-bincard-admin-ui/
├── spec.md
├── plan.md              # bu dosya
├── research.md          # Phase 0
├── data-model.md        # Phase 1
├── quickstart.md        # Phase 1
├── contracts/           # Phase 1 (detail + list sözleşmeleri + Admin UI kontratı)
└── tasks.md             # /speckit-tasks (bu komut üretmez)
```

### Source Code (repository root)

```text
src/services/Payment.Api/Domains/BinCards/
├── Features/Queries/
│   ├── GetBinCardDetail.cs        # YENİ — bin → BinCardDetailResponse? (ham alanlar + taksit-banka)
│   └── ListBinCards.cs            # YENİ — filtre + sayfalı liste (Marten ToPagedList)
├── BinCardEndpointExtension.cs    # GÜNCELLE — GET {bin} zenginleşir (detail), GET / (list) eklenir
└── (008 dosyaları değişmez: BinCard, ResolveBinCard, ImportBinCards, Seeder, Mapping, enum'lar)

src/ui/Admin/
├── Clients/
│   ├── BinCardApiClient.cs        # YENİ — IBinCardApiClient (payment-api): GetDetail, List
│   └── ApiModels.cs               # GÜNCELLE — BinCardDetail, BinCardListItem, BinCardListResponse, BinCardListFilter
├── Pages/BinCards/                # YENİ
│   ├── Resolve.cshtml(.cs)        # US1 — BIN ara → detay
│   └── Index.cshtml(.cs)          # US2 — filtreli sayfalı liste
├── PageModels/ (BasePageModel mevcut) + enum→TR etiket yardımcısı (yeni, Admin sunum)
├── Pages/Shared/_Layout.cshtml    # GÜNCELLE — "BIN Kataloğu" menü bağlantısı
└── Program.cs                     # GÜNCELLE — AddHttpClient<IBinCardApiClient> → http://payment-api

src/aspire/AppHost/AppHost.cs      # GÜNCELLE — admin-web .WithReference(payment-api).WaitFor(...)
```

**Structure Decision**: Yeni proje yok. Payment.Api'ye iki okuma slice'ı (mevcut BinCards domain
içinde) eklenir; Admin'e bir typed client + iki Razor sayfası + payment-api referansı. 008 kodu ve
CP.VPOS'a dokunulmaz. US1 zenginleştirilen `GET {bin}` ucunu, US2 yeni `GET /` liste ucunu tüketir.

## Okuma yolu / parite notu

- `GET api/v1/bin-cards/{bin}` bugün (008) `CardInfo(BankCode, IsCreditCard, InstallmentBankCodes)`
  dönüyor — marka/program/ticari yok. US1 bunları ister → endpoint yanıtı `BinCardDetailResponse`'a
  zenginleşir (ham BinCard alanları + türetilmiş taksit-banka). Bu ucun **iç HTTP tüketicisi yok**
  (ProcessPayment/GetInstallmentOptions `ResolveBinCard` static'ini çağırır) → kırılma yok.
- **Parite**: detaydaki taksit-banka listesi `ResolveBinCard.DeriveInstallmentBankCodes` ile üretilir
  (008 ile birebir, SC-003). Admin türetmeyi yeniden yapmaz.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Salt-UI feature'a **backend eklemesi** (005 tam salt-UI'ydi) | US2 filtre+sayfalama ~9957 kayıtta client-side yapılamaz; Payment BC kendi verisini sayfalı/filtreli sunmalı (anayasa I: veri sahibi BC sorgular). US1 detay için mevcut uç marka/program taşımıyor. | Admin'in tüm kataloğu çekip client-side filtrelemesi: ~9957 kayıt her istekte transfer + anayasa I ihlali (kural/işleme Admin'e sızar). Reddedildi. |
| Yeni okuma uçları yetkisiz | Anayasa V açık yetki ister; proje-geneli AUTHZ ertelemesi. Salt-okuma → state değişmez. | Şimdi ad-hoc yetki: proje-geneli erteleme kararına aykırı. Identity BC'de topluca kapanır. |