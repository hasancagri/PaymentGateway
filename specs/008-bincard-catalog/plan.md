# Implementation Plan: BinCard Referans Kataloğu

**Branch**: `008-bincard-catalog` | **Date**: 2026-08-02 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/008-bincard-catalog/spec.md`

## Summary

CP.VPOS'a gömülü BIN tablosunu (~9900 kayıt, 6 hane) Payment BC'nin kendi Postgres/Marten
deposuna bir **referans lookup kataloğu** olarak taşı. Payment.Api'nin BIN okuması donmuş
kütüphaneden bu katalog sorgusuna geçer. Katalog seed + idempotent toplu import ile güncellenebilir.

**Teknik yaklaşım:**
- Yeni domain `Payment.Api/Domains/BinCards/`. `BinCard` = Marten document (davranış-zengin
  aggregate DEĞİL — referans veri), kimlik = `BinNumber` (string). Tip-güvenli domain enum'ları
  (`CardType`/`CardBrand`/`CardProgram`), CP.VPOS enum'undan **seed/import sınırında** çevrilir.
- **Çözümleme** `Queries/ResolveBinCard`: `binNumber → CardInfo?` (null = bulunamadı, sahte-default
  yok), 8→6 truncate fallback, `InstallmentBankCodes` çözüm anında türetilir.
- **Seed** `BinCardSeeder : IInitialData`: katalog boşsa `VPOSClient.AllCreditCardBinList()`'ten
  bir kez doldur (Marten `InitialData` startup hook'u).
- **Import** `Commands/ImportBinCards`: yayınlanan liste → idempotent upsert (`session.Store`,
  kimlik BinNumber). Endpoint ile tetiklenir.
- **Okuma yolu switch**: `ProcessPayment.LoadCardInfo` + `GetInstallmentOptions` artık
  `ResolveBinCard` çağırır; `VPOSClient.CreditCardBinQuery` bırakılır; null bilinçli ele alınır.
  CP.VPOS derlemede kalır, değiştirilmez.

## Technical Context

**Language/Version**: C# / .NET 10 (`Nullable` + `ImplicitUsings` açık)

**Primary Dependencies**: Mevcut — Marten 9.5 (document store + `IInitialData` seed), WolverineFx 6.4
(command/query bus), CP.VPOS (yalnız seed kaynağı `VPOSClient.AllCreditCardBinList()` + enum legend
`CreditCardType/Brand/Program`). **Yeni paket gerekmez.**

**Storage**: Marten (Postgres, `paymentDb`, Payment şeması). `BinCard` document, `BinNumber` kimlik,
`CardProgram` üzerinde index (taksit-banka türetme sorgusu için).

**Testing**: `tests/Payment.Api.Tests` (yeni, xUnit) — saf domain: CP.VPOS enum/int → domain enum
eşlemesi, 8→6 fallback, `InstallmentBankCodes` türetme, import idempotency (upsert), bilinmeyen BIN → null.
Seed/import DB/HTTP birim testi yok — quickstart ile elle (anayasa test kuralı).

**Target Platform**: Linux/container, Aspire orchestrated (AppHost — mevcut `payment-api` node).

**Project Type**: Mevcut Payment mikroservisine domain ekleme (yeni proje yok).

**Performance Goals**: Çözümleme sıcak yol; `BinNumber` exact-match indexli. Taksit-banka türetme
`CardProgram` indexli sorgu — ~9900 kayıt için kabul edilebilir; gerekirse cache (HybridCache mevcut).

**Constraints**: Yalnız TL/yurt-içi. CP.VPOS tipleri domain sınırını geçmez. Bilinmeyen BIN → null
(istisna/sahte-default yok).

**Scale/Scope**: ~9900 seed kaydı. 1 domain (BinCards), 1 query + 1 command + 1 seeder + mapping +
endpoint; 2 mevcut call-site switch (LoadCardInfo, GetInstallmentOptions).

## Constitution Check

*GATE: Phase 0 öncesi geçti. Phase 1 sonrası tekrar bakıldı — ihlal yok (Complexity Tracking'e bakınız).*

| İlke | Durum | Not |
|------|-------|-----|
| I. Bounded Context İzolasyonu | ✅ | BinCard Payment BC içinde, kendi deposunda. `bankCode` yerel string referans (mevcut BankCatalog kopyası deseni); cross-BC çağrı/DB erişimi yok. |
| II. Zengin Domain Modeli | ⚠️ gerekçeli | BinCard davranış-zengin aggregate DEĞİL — referans/lookup verisi (invariant/davranış yok). Anayasa II anemik **aggregate**'i yasaklar; bu aggregate değil, okuma modeli. Bkz. Complexity Tracking. Domain mantığı (enum eşleme, 8→6, türetme) saf query/mapping'de. |
| III. Vertical Slice + CQRS | ✅ | `Queries/ResolveBinCard` + `Commands/ImportBinCards` (static class, record+Response+Handler+Endpoint). Repository yok, `IDocumentSession`. |
| IV. Result Pattern | ✅ | Import `FeatureObjectResultModel<T>` (atlanan kayıt raporu). Çözümleme `CardInfo?` (null = bulunamadı; beklenen "yok" durumu — istisna yok, anayasa IV ruhu). |
| V. Merkezi Kimlik & Açık Yetki | ⚠️ ertelenmiş | Import state-değiştiren + hassas; anayasa V açık yetki ister ama proje-geneli AUTHZ ertelemesi. Endpoint şimdilik korumasız — **risk olarak işaretlendi** (Identity BC'de kapanır). |
| VI. Spec-Driven | ✅ | spec→plan→tasks→implement. |

**Teknoloji kısıtları:** .NET 10 + Aspire ✅ · Marten (`IInitialData` seed) ✅ · Wolverine ✅ ·
CPM (yeni paket yok) ✅ · yalnız TL ✅ · CP.VPOS tipleri slice'ı geçmez (sınırda enum çevrilir) ✅.

## Project Structure

### Documentation (this feature)

```text
specs/008-bincard-catalog/
├── spec.md
├── plan.md              # bu dosya
├── research.md          # Phase 0
├── data-model.md        # Phase 1
├── quickstart.md        # Phase 1
├── contracts/           # Phase 1 (resolve + import sözleşmeleri)
└── tasks.md             # /speckit-tasks (bu komut üretmez)
```

### Source Code (repository root)

```text
src/services/Payment.Api/
├── Domains/BinCards/                        # YENİ domain
│   ├── BinCard.cs                            # Marten document (BinNumber kimlik) + alanlar
│   ├── CardType.cs / CardBrand.cs / CardProgram.cs   # domain enum'ları (düz C#, PaymentStatus stili)
│   ├── BinCardMapping.cs                     # CP.VPOS enum/int → domain enum (SINIR çevirisi)
│   ├── BinCardSeeder.cs                      # IInitialData — boşsa VPOSClient.AllCreditCardBinList()'ten seed
│   ├── Features/
│   │   ├── Queries/ResolveBinCard.cs         # binNumber → CardInfo? (8→6 fallback, taksit-banka türetme)
│   │   └── Commands/ImportBinCards.cs        # idempotent bulk upsert + endpoint
│   └── BinCardEndpointExtension.cs           # api/v1/bin-cards (import; opsiyonel resolve/debug)
├── Domains/Payments/
│   ├── Features/Commands/ProcessPayment.cs   # LoadCardInfo → ResolveBinCard'a geçer, null ele alınır
│   └── Features/Queries/GetInstallmentOptions.cs  # aynı switch
└── Program.cs                                # opts.Schema.For<BinCard>() + Index; opts.InitialData.Add(seeder)

tests/
└── Payment.Api.Tests/                        # YENİ — saf domain birim testleri
```

**Structure Decision**: Yeni proje yok — mevcut Payment.Api'ye `Domains/BinCards/` eklenir. Marten
şema kaydı + `IInitialData` seed Program.cs'te. Okuma yolu iki mevcut call-site'ta katalog sorgusuna
çevrilir; CP.VPOS'a dokunulmaz.

## Okuma yolu geçişi (parite + null)

- `ProcessPayment.LoadCardInfo(cardNumber)` bugün `VPOSClient.CreditCardBinQuery` çağırıp bulunamazsa
  `CardInfo(null,false,[])` (sahte-default) dönüyor. Yeni: `ResolveBinCard` çağırır, **null döner**
  bulunamazsa. İmza `CardInfo?` olur.
- Çağıranlar null'ı ele alır: `ProcessPayment` null kart → Result reddi (uydurma default ile ilerlemez);
  `GetInstallmentOptions` null kart → boş/uygun sonuç. (Derin ProcessPayment yeniden kurgusu ayrı feature;
  008 yalnız read-path swap + null ele alma yapar.)
- **Parite**: bilinen BIN için dönen banka/tip/marka/program/ticari + taksit-banka listesi CP.VPOS ile
  birebir aynı (SC-001). Türetme mantığı CP.VPOS ile aynı (aynı `cardProgram` → bankalar, kart bankası başta).

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| BinCard davranış-zengin aggregate değil (Marten document/read-model) | Referans/lookup verisi: invariant yok, davranış yok, okuma-ağırlıklı/yazma-toplu. Aggregate yapmak sahte davranış + gereksiz ağırlık getirir. | Zengin aggregate: anayasa II'nin amacı iş kuralını modelde toplamak; BinCard'ın iş kuralı yok. Aggregate zorlamak YAGNI ihlali olurdu. |
| Import endpoint yetkisiz (state-değiştiren) | Anayasa V açık yetki ister; proje-geneli AUTHZ ertelemesi (Identity BC yok). Diğer BC endpoint'leriyle tutarlı. | Şimdi ad-hoc yetki eklemek: proje-geneli erteleme kararına aykırı, tutarsızlık yaratır. Identity BC'de topluca kapanır. |