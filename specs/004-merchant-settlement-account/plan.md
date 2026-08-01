# Implementation Plan: Merchant Settlement Hesabı

**Branch**: `004-merchant-settlement-account` | **Date**: 2026-08-01 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/004-merchant-settlement-account/spec.md`

## Summary

Merchant'a settlement/payout amacıyla para yatırılacak banka hesabı yönetimi. Payment sistemi
içinde **Merchant BC** (`Merchant.Api`) altında yeni bir vertical slice: `MerchantSettlementAccount`
aggregate + CRUD (create/update/list/get) + hesap-bazlı aktif/pasif durum. Mevcut `Merchant`
aggregate'ine dokunulmaz; bağ MerchantId ile kurulur.

Teknik yaklaşım: IBAN saf format + mod-97 doğrulaması aggregate içinde (Result pattern, test
edilebilir saf domain). Banka referansı 4-hane banka koduyla tutulur ve Merchant BC'nin kendi
tuttuğu **yerel `BankCatalog` kopyasına** (statik referans, gömülü singleton lookup) karşı
doğrulanır — Commission BC'ye runtime çağrı yok (BC izolasyonu). Merchant varlığı ve merchant-içi
IBAN mükerrerliği handler'da Marten sorgusuyla kontrol edilir. Persistence Marten document
(`MerchantSchemaName`), her komut handler'ı `[Transactional]`.

## Technical Context

**Language/Version**: C# / .NET 10 (Nullable + ImplicitUsings açık)

**Primary Dependencies**: Marten (Postgres document store), Wolverine (in-proc bus + endpoint
discovery), ASP.NET Minimal API + API Versioning, Scrutor (marker-based DI). Aspire orchestration.

**Storage**: Postgres (Marten), schema `SchemaConstants.MerchantSchemaName`. Yeni document tipi:
`MerchantSettlementAccount`.

**Testing**: Saf domain birim testleri (host/entegrasyon harness'ı yok). Öncelik:
`MerchantSettlementAccount` aggregate (IBAN mod-97, TR kısıtı, durum geçişleri) + `BankCodeLookup`.
Test projesi henüz yok; eklenirse `Merchant.Api` için ayrı saf-domain test projesi.

**Target Platform**: Linux server (Aspire üzerinden), tek düğüm dev (Wolverine Solo).

**Project Type**: Web-service (bounded context mikroservisi) — mevcut `Merchant.Api`.

**Performance Goals**: Standart API beklentileri; IBAN doğrulaması ve katalog lookup bellek-içi.
Özel hedef yok.

**Constraints**: BC izolasyonu (cross-BC DB/aggregate erişimi yok); yalnız TL/TR IBAN; Result
pattern (beklenen hata exception değil); repository yok (doğrudan `IDocumentSession`).

**Scale/Scope**: 1 aggregate, 3 command slice (Create/Update/SetStatus) + 2 query slice (List/Get),
1 gömülü lookup (BankCode), 1 endpoint extension. Merchant başına birkaç hesap.

## Constitution Check

*GATE: Phase 0 öncesi geçmeli; Phase 1 sonrası tekrar bakılır.*

| İlke | Durum | Not |
|------|-------|-----|
| I. Bounded Context İzolasyonu | PASS | Banka referansı yerel katalog kopyasıyla doğrulanır; Commission BC'ye çağrı yok, cross-BC DB erişimi yok. Banka Merchant BC'de yalın kod referansı. |
| II. Zengin Domain Modeli | PASS | `MerchantSettlementAccount` = private setter + statik `Create` + davranış (`UpdateDetails`, `Activate`/`Deactivate`). Invariant'lar (IBAN, zorunlu alan) aggregate içinde. Düz enum status (mevcut `MerchantStatus` konvansiyonu). |
| III. Vertical Slice + CQRS | PASS | `Domains/MerchantSettlementAccounts/Features/{Commands,Queries}`; feature = tek static class (record + Response + Handler + endpoint). Command'lar `[Transactional]`. Repository yok. |
| IV. Result Pattern | PASS | `FeatureObjectResultModel<T>`/`ResultDomain`; `MessageItem.Code` resource sabiti. Bulunamadı/format/iş kuralı Result ile. |
| V. Merkezi Kimlik & Açık Yetki | ERTELENDİ | AUTHZ_MODEL ertelemesi gereği endpoint korumasız (bilinçli, 001-003 ile tutarlı). Tenant filtre (merchant-bazlı liste) baştan uygulanır. |
| VI. Spec-Driven | PASS | Akış: specify→plan→tasks→implement. |

**Teknoloji kısıtları**: yalnız TL → currency alanı yok, yalnız TR IBAN. CP.VPOS tipleri sınırı
geçmez (bu feature CP.VPOS'a dokunmaz). Marker DI (`ISingletonDependency` lookup için).

Sonuç: **Gate PASS.** Yetki ertelemesi anayasada tanınmış (TODO(AUTHZ_MODEL)); yeni ihlal yok.

## Project Structure

### Documentation (this feature)

```text
specs/004-merchant-settlement-account/
├── plan.md              # bu dosya
├── research.md          # Phase 0
├── data-model.md        # Phase 1
├── quickstart.md        # Phase 1
├── contracts/           # Phase 1 (HTTP endpoint kontratları)
│   └── settlement-accounts.http.md
└── tasks.md             # /speckit-tasks (bu komut üretmez)
```

### Source Code (repository root)

```text
src/services/Merchant.Api/
├── Program.cs                         # DEĞİŞİR: opts.Schema.For<MerchantSettlementAccount>() +
│                                      #          app.AddMerchantSettlementAccountGroupEndpointExtension(...)
└── Domains/
    ├── Merchants/                     # DOKUNULMAZ (mevcut)
    └── MerchantSettlementAccounts/    # YENİ slice
        ├── MerchantSettlementAccount.cs           # aggregate (Create/UpdateDetails/Activate/Deactivate + IBAN)
        ├── SettlementAccountStatus.cs             # düz enum (Active/Passive)
        ├── MerchantSettlementAccountEndpointExtension.cs
        ├── Lookups/
        │   ├── BankCodeLookup.cs                  # IBankCodeLookup : ISingletonDependency + impl
        │   └── BankCatalog.cs                     # yerel statik kopya (Commission.Api'dekiyle aynı liste)
        └── Features/
            ├── Commands/
            │   ├── CreateSettlementAccount.cs
            │   ├── UpdateSettlementAccount.cs
            │   └── SetSettlementAccountStatus.cs
            └── Queries/
                ├── GetSettlementAccount.cs
                └── GetMerchantSettlementAccounts.cs
```

**Structure Decision**: Mevcut `Merchant.Api` mikroservisine yeni bir `Domains/<Aggregate>`
slice'ı eklenir; `Merchants` slice'ıyla birebir aynı desen (aggregate + Features/{Commands,Queries}
+ EndpointExtension + gömülü Lookups). `Program.cs` iki satır genişler (Marten schema kaydı +
endpoint map). Başka dosya değişmez.

## Complexity Tracking

Anayasa ihlali / gerekçelendirilmiş ek karmaşıklık yok. Banka doğrulaması için katalog kopyası
bilinçli YAGNI kararı (bkz. research.md); alternatifleri (gRPC / Reference BC) daha ağır ve
şu an gereksiz.