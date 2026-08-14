# Implementation Plan: Kart Vault Dirilişi

**Branch**: `031-card-vault-revival` | **Date**: 2026-08-14 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/031-card-vault-revival/spec.md`

## Summary

022'de sökülen 017 kart kasası, ECommerce'in CANLI `GatewayCardTokenizer` sözleşmesine birebir
hizalanıp Payment.Api'de diriltilir: `StoredCard` aggregate (Luhn + MM/yy expiry doğrulama, PAN
yalnız AES-korumalı, BIN/Last4/Brand türetimi, opak `card_` token, idempotent soft revoke) +
`CardVault/` altyapısı (`IPanProtector`/`DevPanProtector`/`PanTools`) + Tokenize/Revoke slice'ları
(`/api/v1.0/merchants/{merchantId}/vault/cards`, `cards.write` + `MerchantScoped`). Sapmalar:
BC-içi `CardBrand` enum'u (SharedKernel silindi), `UpdateCard` YAGNI kırpıldı, PAN normalize
eklendi. iyzico çağrısı YOK; ECommerce'e SIFIR dokunuş. Kararlar: [research.md](research.md) R1-R8.

## Technical Context

**Language/Version**: C# / .NET 10 (net10.0)

**Primary Dependencies**: Mevcut Payment.Api yığını — Marten (paymentDb, Newtonsoft
NonPublicSetters), Wolverine, Minimal API; `System.Security.Cryptography` (AES — BCL, paket yok).
Yeni paket YOK.

**Storage**: paymentDb — YENİ `StoredCard` document'ı (identity = `Token` string); Schema.For
kaydı gerekmez (mevcut Program.cs stili); migration yok (temiz başlangıç)

**Testing**: xUnit — YENİ `tests/Payment.Api.Tests` projesi (022'de silinmişti; Merchant/Commission
csproj deseni + slnx kaydı). Eski StoredCardCreate/Revoke testleri uyarlanır + Luhn/brand/normalize
matrisi (R8). Merchant (47) + Commission (31) regresyon.

**Target Platform**: Aspire AppHost; Payment.Api http://localhost:5201 (launchSettings'ten
doğrulanacak — T görevi); tüketici ECommerce Customer.Api (`DropShopVault` config'i mevcut)

**Project Type**: Mevcut BC içinde yeni aggregate + 2 uç; Payment.Api'nin 022 sonrası İLK canlı
endpoint'leri. Tek repo (ECommerce dokunuşu YOK — FR-009)

**Performance Goals**: Yok (dev)

**Constraints**: Dış sözleşme SABİT (contracts/vault-api.md — canlı istemciden doğrulandı, R4);
CVV sözleşmede yok; yanıt yalnız `{token}`; PAN log/yanıt/sorguda açık geçmez (SC-002);
`Provider/StoredCards` (iyzico wire tipleri) AYRI namespace — dokunulmaz; iyzico çağrısı yok

**Scale/Scope**: 1 aggregate + 2 enum + 3 altyapı dosyası + 2 slice + endpoint extension +
Program.cs tek satır + yeni test projesi (~10 dosya)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| İlke | Değerlendirme | Durum |
|---|---|---|
| I. BC İzolasyonu | StoredCard Payment BC içinde; MerchantId yalnız referans (cross-BC doğrulama yok — 024 emsali). ECommerce ayrı sistem, iletişim yalnız HTTP sözleşmesi + OAuth. | ✅ |
| II. Zengin Domain | Anemik değil: statik `Create` fabrikası (Luhn/expiry/türetim invariant'ları içeride), `Revoke` davranışı; private setter; `CardVault/` altyapı seam'i aggregate DEĞİL (protector DI ile fabrikaya parametre — 017 emsali). | ✅ |
| III. Vertical Slice + CQRS | 2 slice `Features/Commands/`; static class + record + Response + Handler + endpoint-extension; `[Transactional]`; repository yok. | ✅ |
| IV. Result Pattern | `Create`/`Revoke` → `ResultDomain`; handler'lar `FeatureObjectResultModel<T>`; kodlar resource sabitleri. | ✅ |
| V. Kimlik + Açık Yetki | Her uç açıkça `cards.write` + `MerchantScoped` beyan eder; kiracı sınırı çift kapı (policy + handler'da `card.MerchantId != cmd.MerchantId` → RECORD_NOT_FOUND, sahiplik sızdırmaz); `cards.write` yalnız Active merchant'ta (mevcut zincir, değişmez). PAN write-only + enc-at-rest. | ✅ |

**Gate sonucu**: GEÇTİ — ihlal yok. (Dev-sabit AES anahtarı anayasal ihlal değil; spec Assumption
+ kod yorumu prod-KMS notuyla kayıtlı — 017'de aynı kabulle geçmişti.)

## Project Structure

### Documentation (this feature)

```text
specs/031-card-vault-revival/
├── plan.md              # Bu dosya
├── research.md          # R1-R8
├── data-model.md        # StoredCard + CardBrand + CardVault altyapısı + statü makinesi
├── quickstart.md        # S1-S3 (curl + ECommerce sıfır-dokunuş kanıtı + PAN sızma kontrolü)
├── contracts/
│   └── vault-api.md     # SABİT dış sözleşme (canlı istemciden)
└── tasks.md             # /speckit-tasks üretecek
```

### Source Code (repository root)

```text
src/services/Payment.Api/
├── CardVault/                              # YENİ (altyapı — R6; 017 emsali)
│   ├── IPanProtector.cs
│   ├── DevPanProtector.cs                  # ISingletonDependency → AddAllDependencies otomatik kayıt
│   └── PanTools.cs                         # LuhnValidator, BinExtractor, Last4Extractor, BrandDetector
├── Domains/StoredCards/                    # YENİ (Provider/StoredCards'tan AYRI namespace)
│   ├── StoredCard.cs
│   ├── CardBrand.cs                        # R2 — BC-içi enum
│   ├── StoredCardStatus.cs
│   ├── StoredCardEndpointExtension.cs      # merchants/{merchantId}/vault/cards grubu
│   └── Features/Commands/
│       ├── TokenizeCard.cs
│       └── RevokeCard.cs
└── Program.cs                              # +app.AddStoredCardGroupEndpointExtension(apiVersionSet)

tests/Payment.Api.Tests/                    # YENİ proje (csproj + slnx kaydı)
├── StoredCardCreateTests.cs                # Luhn/expiry/türetim/normalize matrisi
└── StoredCardRevokeTests.cs                # idempotent revoke + statü
```

**Structure Decision**: 017'nin dosya düzeni aynen (kanıtlanmış); tek yeni ad `CardBrand.cs`.
ECommerce reposuna dokunulmaz.

## Complexity Tracking

> İhlal yok — tablo boş.
