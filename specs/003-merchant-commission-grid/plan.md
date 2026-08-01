# Implementation Plan: Merchant Komisyon Grid

**Branch**: `feat/merchant-commission-grid` | **Date**: 2026-08-01 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/003-merchant-commission-grid/spec.md`

## Summary

Commission BC'deki mevcut `MerchantCommission` aggregate'i **kombinasyon-bazlı** modele dönüştürür
(002'nin banka grid'inin merchant karşılığı). Merchant komisyonu artık tek bir `BankCommission`'a
değil, kombinasyona `(MerchantId, Criteria)` bağlanır; `Criteria` = mevcut değer nesnesi aynen
(kart markası × tip × bölge × **taksit**). Eski `BankCommissionId` bağı, `BankCode` snapshot'ı ve
`rate > banka oranı` hard invariant'ı kaldırılır; aggregate banka-bağımsız olur (`rate > 0` sanity).

Banka oranı ile ilişki **okuma anında** kurulur: `GetMerchantCommissions` handler'ı, her kombinasyonu
servisleyen `BankCommission` oranlarından en-düşük/en-yüksek'i hesaplar ve `rate <= bankMax` ise
`belowBankCeiling` işaretler (saklanmaz → banka oranı değişince otomatik tazelenir). Admin'e 002'nin
`BankCommissions/Create` grid'ine simetrik bir merchant grid'i eklenir: merchant seç → tüm
kombinasyonlar, eksikler işaretli, satır içi banka aralığı + tavan-altı kırmızı işaret, eksen filtreleri
+ boşları-doldur + 20'li sayfalama, tek atomik toplu upsert. Amaç: bir merchant'ın komisyon
oranlarının tek ekranda, banka maliyeti görünür halde yönetilmesi.

## Technical Context

**Language/Version**: C# / .NET 10 (Nullable + ImplicitUsings açık)

**Primary Dependencies**: Marten (Postgres document store), Wolverine (in-process bus), Asp.Versioning,
Scrutor; Admin tarafı ASP.NET Core Razor Pages + typed HttpClient. Yeni paket YOK (CPM korunur).

**Storage**: Marten/Postgres — `commission` şeması. Mevcut belge `MerchantCommission` **yeniden
şekillenir** (alan silme/ekleme). Seed yok; taşınacak veri yok (pre-release). `BankCommission` salt-okunur
referanstır.

**Testing**: xUnit saf domain birim testleri (`tests/Commission.Api.Tests`) + `GetMerchantCommissions`
tavan-altı/aralık hesabı için bir handler/projeksiyon testi. Banka/dış HTTP ve Admin UI test edilmez.

**Target Platform**: Aspire ile orkestre edilen Linux/masaüstü sunucu servisleri (Postgres + RabbitMQ).

**Project Type**: Web (mikroservis backend `Commission.Api` + Razor Pages Admin UI + YARP gateway).

**Performance Goals**: Etkileşimli admin; belirli SLA yok. Grid en fazla 4×2×2×15 = 240 satır/merchant;
banka aralığı hesabı için `BankCommission` kümesi tek istekte belleğe alınıp kombinasyona göre gruplanır.

**Constraints**: Merchant.Api'ye senkron cross-call YOK (`MerchantId` opak Guid; merchant listesi Admin'de
ayrı `IMerchantApiClient.GetAllAsync` ile alınır, backend handler'da değil). CP.VPOS runtime bağı yok.
Yetki bu dilimde uygulanmaz (proje geneli erteleme). Yalnız TL (oran yüzdedir). Banka kodu filtresi YOK.

**Scale/Scope**: Operatörün gireceği kadar merchant; merchant başına ≤240 komisyon kombinasyonu. Tek
operatör rolü.

## Constitution Check

*GATE: Phase 0 öncesi geçmeli; Phase 1 sonrası yeniden bakılır.*

| İlke | Durum | Not |
|------|-------|-----|
| I. BC İzolasyonu | ✅ | `MerchantCommission` Commission.Api içinde. Merchant.Api DB'sine erişim yok; `MerchantId` opak Guid, senkron çağrı yok. Banka aralığı aynı BC'deki `BankCommission`'dan okunur. |
| II. Zengin Domain | ✅ | `MerchantCommission`: private setter + statik `Create(merchantId, criteria, rate)` + `UpdateRate(rate)`; `rate > 0` invariant'ı aggregate içinde. Tavan-altı işareti domain invariant'ı DEĞİL (cross-aggregate read projeksiyonu) → query handler'da. |
| III. Vertical Slice + CQRS | ✅ | `Domains/MerchantCommissions/Features/{Commands,Queries}`; `[Transactional]` command handler'lar; `IDocumentSession`; endpoint-extension. Yeni `BulkUpsertMerchantCommissions` slice. |
| IV. Result Pattern | ✅ | `ResultDomain`/`FeatureObjectResultModel`; `MessageItem.Code` resource sabiti. Kullanılmayan `MERCHANT_RATE_MUST_EXCEED_BANK_RATE` sabiti kaldırılır. |
| V. Merkezi Kimlik & Açık Yetki | ⚠️ Ertelendi | Endpoint'ler korumasız — mevcut slice'larla tutarlı, proje geneli AUTHZ_MODEL ertelemesi (constitution TODO). Yeni açık sınır getirmez. Bkz. Complexity Tracking. |
| VI. Spec-Driven | ✅ | specify→plan→tasks→implement akışı izleniyor. |
| Tekno — CP.VPOS sınırı | ✅ | CP.VPOS'a runtime bağımlılık/tip geçişi yok. |
| Tekno — CPM | ✅ | Yeni paket yok. |
| Tekno — yalnız TL | ✅ | Oran yüzde; para birimi alanı yok. |
| Akış — Türkçe | ✅ | Yorumlar/mesajlar/commit Türkçe. |

**Sonuç**: Geçer. Tek uyarı yetki ertelemesi — mevcut kod tabanıyla tutarlı, gerekçe Complexity Tracking'de.

## Project Structure

### Documentation (this feature)

```text
specs/003-merchant-commission-grid/
├── plan.md              # Bu dosya
├── research.md          # Phase 0
├── data-model.md        # Phase 1
├── quickstart.md        # Phase 1
├── contracts/           # Phase 1 (merchant-commissions HTTP kontratı: tek + bulk + enriched GET)
├── checklists/
│   └── requirements.md  # /speckit-specify çıktısı
└── tasks.md             # /speckit-tasks çıktısı (bu komut üretmez)
```

### Source Code (repository root)

```text
src/services/Commission.Api/
├── Domains/
│   ├── MerchantCommissions/
│   │   ├── MerchantCommission.cs                 # REFACTOR: (MerchantId, Criteria, Rate); banka bağı+invariant kaldır
│   │   ├── MerchantCommissionEndpointExtension.cs # + BulkUpsert grup metodu
│   │   └── Features/
│   │       ├── Commands/
│   │       │   ├── CreateMerchantCommission.cs   # REFACTOR: CriteriaDto girer, banka yüklemez, (MerchantId,Criteria) upsert
│   │       │   ├── UpdateMerchantCommission.cs   # REFACTOR: UpdateRate(rate) yalnız
│   │       │   └── BulkUpsertMerchantCommissions.cs # YENİ toplu upsert (BulkUpsertBankCommissions pattern'i)
│   │       └── Queries/
│   │           └── GetMerchantCommissions.cs     # REFACTOR: enriched (bankMin/bankMax/belowBankCeiling/isMissing)
│   └── SharedKernel/CommissionResourceConstants.cs # MERCHANT_RATE_MUST_EXCEED_BANK_RATE sabitini kaldır
├── Program.cs                                    # MerchantCommission schema zaten kayıtlı; bulk endpoint grup metoduyla eklenir

src/ui/Admin/
├── Clients/
│   ├── CommissionApiClient.cs                    # + BulkUpsertMerchantCommissionsAsync; GetMerchantCommissions enriched döner
│   └── ApiModels.cs                              # + bulk request + enriched item modelleri (bankMin/bankMax/belowBankCeiling/isMissing)
├── Pages/
│   ├── MerchantCommissions/
│   │   ├── Index.cshtml(.cs)                     # mevcut liste — enriched kolonlar (banka aralığı, tavan-altı işaret)
│   │   └── Create.cshtml(.cs)                    # YENİ grid: merchant seç → kombinasyon matrisi + toplu upsert
│   └── Shared/_Layout.cshtml                     # nav'a "Merchant Komisyon Grid"
├── MessageText.cs                                # gerekli yeni mesaj metinleri
└── wwwroot/
    ├── css/site.css                              # tavan-altı (.below-ceiling) + "banka yok" stilleri (mevcut .missing yeniden kullanılır)
    └── js/                                        # mevcut commission-grid.js / filterable-table.js yeniden kullanılır (banka aralığı + tavan-altı işaretini render eder)

tests/Commission.Api.Tests/
└── MerchantCommissionTests.cs                    # REFACTOR/GENİŞLET: Create/UpdateRate sanity + tavan-altı hesabı
```

**Structure Decision**: Yeni servis/proje yok. Mevcut `MerchantCommissions` slice'ı kombinasyon-bazlı
modele refactor edilir; 002'de kurulan grid altyapısı (client JS, filtre, boş-doldur, sayfalama, `.missing`
stili) yeniden kullanılır. Grid'in banka aralığı + tavan-altı işareti backend `GetMerchantCommissions`
handler'ında hesaplanır (test edilebilir, read-time).

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Endpoint'ler yetkisiz (İlke V ertelemesi) | Proje geneli AUTHZ_MODEL kararı Identity dilimine ertelendi; tüm mevcut slice'lar aynı durumda | Bu dilimde yetki getirmek henüz kararlaşmamış modeli tek feature'a uydurmak olur — tutarsızlık yaratır. Erteleme constitution TODO'sunda takipli. |
| `MerchantCommission` yıkıcı refactor (alan silme) | Mevcut model routing gerçeğiyle uyumsuz (tek bankaya bağlı); kombinasyon-bazlı model zorunlu | Alan ekleyip eskiyi korumak iki-model karmaşası + ölü invariant bırakır. Pre-release, veri yok → temiz refactor risksiz. |