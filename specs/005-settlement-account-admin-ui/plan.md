# Implementation Plan: Settlement Hesabı Yönetim Ekranları (Admin)

**Branch**: `005-settlement-account-admin-ui` | **Date**: 2026-08-01 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/005-settlement-account-admin-ui/spec.md`

## Summary

Mevcut Admin BFF'e (`src/ui/Admin`, Razor Pages) merchant settlement hesaplarını yöneten ekranlar
eklenir: liste (merchant-scoped), ekleme formu (banka dropdown + IBAN + sahip + hesap no + açıklama),
düzenleme + aktif/pasif. Tüm veri/doğrulama 004 `Merchant.Api` settlement-accounts API'sinden gelir;
**hiçbir backend değişmez** (FR-011). Yeni bir typed `HttpClient` (`ISettlementAccountApiClient`) 5
endpoint'i sarar; ekranlar mevcut `MerchantCommissions`/`Banks` sayfa desenini birebir izler.

Banka seçim listesi Merchant BC'de endpoint olarak açık olmadığından (backend'e dokunulamaz), dropdown
kaynağı olarak mevcut `Commission.Api` `GET /banks/catalog` (aynı kanonik liste) yeniden kullanılır —
BFF zaten iki API'yi çağırıyor (bkz. research D1).

## Technical Context

**Language/Version**: C# / .NET 10 (Nullable + ImplicitUsings açık)

**Primary Dependencies**: ASP.NET Core Razor Pages (Admin BFF), typed `HttpClient` (Aspire service
discovery `http://merchant-api`), `System.Net.Http.Json`. Yeni NuGet paketi YOK (CPM korunur).

**Storage**: Yok — bu katman durum saklamaz. Tüm kalıcılık 004 Merchant.Api (Marten) üzerinden.

**Testing**: UI için otomatik test yok (proje deseni: saf domain birim testi; Razor Pages/HTTP
entegrasyonu quickstart senaryolarıyla elle doğrulanır). Backend zaten 004'te test edildi.

**Target Platform**: Aspire üzerinden ayağa kalkan Admin web (`admin-web`), masaüstü tarayıcı.

**Project Type**: Web UI (BFF) — mevcut `src/ui/Admin`.

**Performance Goals**: Standart Admin panel beklentileri; ekran başına birkaç API çağrısı. Özel hedef yok.

**Constraints**: Backend (Merchant.Api) DEĞİŞMEZ (FR-011); tenant sınırı rota `{merchantId}` ile;
yetki yok (ertelendi); ondalık yok (settlement'ta oran alanı yok — komisyon grid'inden farklı).

**Scale/Scope**: 3 ekran (Index/Create/Edit) + 1 typed client + DTO'lar + merchant detay navigasyon
bağlantısı. Merchant başına birkaç hesap.

## Constitution Check

*GATE: Phase 0 öncesi geçmeli; Phase 1 sonrası tekrar bakılır.*

| İlke | Durum | Not |
|------|-------|-----|
| I. Bounded Context İzolasyonu | PASS | Admin BFF bir kompozisyon katmanı; DB/aggregate'e değil yalnız HTTP API'lere erişir. Merchant.Api + Commission.Api zaten ayrı; cross-BC DB yok. Banka katalogu Commission API'sinden okunur (veri kopyalama değil, canlı okuma). |
| II. Zengin Domain Modeli | N/A | Bu katmanda domain/aggregate yok; sunum + API çağrısı. Invariant'lar 004 aggregate'inde. |
| III. Vertical Slice + CQRS | N/A (UI) | Backend deseni; UI tarafında Razor Pages sayfa-başına-özellik deseni (mevcut Admin konvansiyonu) izlenir. |
| IV. Result Pattern | PASS | `ApiResult<T>` + `ApiMessage` ile API hata kodları yüzeye taşınır; `MessageText` Türkçe metne çevirir. Exception yok (transport hatası dostça `SERVER_ERROR`). |
| V. Merkezi Kimlik & Açık Yetki | ERTELENDİ | Yetki yok (001–004 ile tutarlı). Tenant izolasyonu: tüm sorgular `merchantId` rota parametresiyle; başka merchant'ın hesabı UI'de asla gösterilmez (SC-004). |
| VI. Spec-Driven | PASS | specify→plan→tasks→implement. |

**Teknoloji kısıtları**: .NET 10 + Aspire service discovery (mevcut). Yeni paket yok, CPM korunur.
Ondalık kültür sorunu bu feature'ı etkilemez (settlement'ta ondalık alan yok).

Sonuç: **Gate PASS.** UI katmanı; anayasa backend ilkelerini ihlal etmez. Yetki ertelemesi tanınmış.

## Project Structure

### Documentation (this feature)

```text
specs/005-settlement-account-admin-ui/
├── plan.md              # bu dosya
├── research.md          # Phase 0
├── data-model.md        # Phase 1 (view/DTO modelleri)
├── quickstart.md        # Phase 1
├── contracts/           # Phase 1 (UI ekran + tüketilen API kontratları)
│   └── screens.md
└── tasks.md             # /speckit-tasks (bu komut üretmez)
```

### Source Code (repository root)

```text
src/ui/Admin/
├── Program.cs                          # DEĞİŞİR: AddHttpClient<ISettlementAccountApiClient,...>
├── Clients/
│   ├── ApiModels.cs                    # DEĞİŞİR: settlement DTO'ları eklenir
│   └── SettlementAccountApiClient.cs   # YENİ: 5 endpoint sarmalı (merchant-scoped rota)
└── Pages/
    ├── Merchants/
    │   └── Details.cshtml              # DEĞİŞİR: "Settlement Hesapları" navigasyon butonu
    └── SettlementAccounts/             # YENİ
        ├── Index.cshtml(.cs)           # US1: merchant seç + hesap listesi
        ├── Create.cshtml(.cs)          # US2: ekleme formu (banka dropdown)
        └── Edit.cshtml(.cs)            # US3: düzenleme + aktif/pasif
```

**Structure Decision**: Mevcut `MerchantCommissions` (merchant seç → merchant-scoped alt kaynak) ve
`Banks` (katalog dropdown'lı Create) desenleri birleştirilir. Yeni `Pages/SettlementAccounts/` klasörü;
tek yeni typed client; `ApiModels.cs` ve `Program.cs` genişler; `Merchants/Details.cshtml` bir buton
kazanır. Başka dosya değişmez. **Backend hiç değişmez.**

## Complexity Tracking

Anayasa ihlali yok. Tek dikkat: banka dropdown'ının Commission.Api katalogundan okunması (Merchant BC
kendi katalog endpoint'ini açmadığından). Alternatifleri (Merchant.Api'ye endpoint ekle = FR-011 ihlali;
UI'de statik liste = üçüncü kopya) research D1'de tartıldı; canlı okuma seçildi.