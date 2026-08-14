# Implementation Plan: iyzico Saklı Kart'a Geçiş (Model A)

**Branch**: `032-iyzico-card-storage` | **Date**: 2026-08-14 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/032-iyzico-card-storage/spec.md`

## Summary

031'in kendi-kasa modeli (Model B, AES-korumalı PAN) iyzico Saklı Kart'a (Model A) geçirilir:
`StoredCard` `EncryptedPan` yerine iyzico `CardUserKey`+`CardToken` saklar; Tokenize handler iyzico
`Card.Create` (`/cardstorage/card`), Revoke `Card.Delete` çağırır; `DevPanProtector`/Luhn kalkar
(iyzico doğrular). Dış sözleşme (ECommerce vault uçları + token yanıtı) 031 ile birebir korunur
(sıfır dokunuş). Provider çekirdeği spike'la kanıtlı. Kritik karar (R2): sözleşme buyer kimliği
taşımadığından per-kart cardUserKey (gruplama ertelendi). Amaç: CVC-siz tekrar ödeme + recurring
altyapısı. Kararlar: [research.md](research.md) R1-R8.

## Technical Context

**Language/Version**: C# / .NET 10 (net10.0)

**Primary Dependencies**: Mevcut Payment.Api yığını — Marten (paymentDb), Wolverine, Minimal API;
**Provider/ çekirdeği** (RestHttpClientV2 + HashGeneratorV2 + StoredCards wire tipleri — 020'den,
uyuyor, spike'la kanıtlandı). Yeni paket YOK. Dış bağımlılık: **iyzico sandbox** (gerçek HTTP).

**Storage**: paymentDb — `StoredCard` document şekli değişir (EncryptedPan→CardUserKey/CardToken);
`mt_doc_storedcard` truncate (031 kayıtları uymaz); `Identity(Token).Index(MerchantId)` korunur.

**Testing**: xUnit `tests/Payment.Api.Tests` — aggregate saf testleri (Create-kimliklerle, Revoke);
031'in Luhn/normalize testleri SİLİNİR (mantık iyzico'ya geçti). iyzico çağrısı quickstart canlı
(sandbox) ile doğrulanır. Merchant (47) + Commission (31) regresyon.

**Target Platform**: Aspire AppHost; Payment.Api :5201; iyzico sandbox `sandbox-api.iyzipay.com`.
Tüketici ECommerce (değişmez).

**Project Type**: Mevcut BC içi revizyon (031 yeniden-yazımı) + ilk **canlı iyzico entegrasyonu**.
Tek repo (ECommerce dokunuşu YOK).

**Performance Goals**: Yok (dev); iyzico çağrısı senkron (RestHttpClientV2).

**Constraints**: Dış sözleşme SABİT (031, FR-008); CVC yok; PAN gateway'de hiç durmaz (FR-002);
sandbox key user-secrets (FR-009); iyzico çağrısı fail-closed (tokenize) / fail-open (revoke);
`Provider/` tipleri BC dışına sızmaz (sağlayıcı-sınır kuralı — handler map'ler, aggregate iyzico tipi görmez).

**Scale/Scope**: 1 aggregate revizyonu + 2 slice iyzico çağrısı + Options POCO + PanTools kırpma +
3 dosya silme (IPanProtector/DevPanProtector/Luhn) + test revizyonu (~8 dosya net).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| İlke | Değerlendirme | Durum |
|---|---|---|
| I. BC İzolasyonu | StoredCard Payment BC içinde; iyzico `Provider/` tipleri handler'da map'lenir, aggregate/domain iyzico tipi GÖRMEZ (sağlayıcı-sınır kuralı). ECommerce ayrı sistem, sözleşme HTTP+OAuth. | ✅ |
| II. Zengin Domain | `StoredCard` anemik değil: `Create` fabrikası (sağlayıcı kimlikleri invariant'ı) + `Revoke`; iyzico çağrısı HANDLER'da (yan etki), aggregate saf kalır. | ✅ |
| III. Vertical Slice + CQRS | 2 slice yerinde revize; `[Transactional]`; repository yok. iyzico çağrısı handler içinde (DB tx dışı yan etki, önce iyzico sonra Store). | ✅ |
| IV. Result Pattern | `Create`/`Revoke` → `ResultDomain`; handler `FeatureObjectResultModel<T>`; iyzico hatası → resource-kodlu MessageItem. | ✅ |
| V. Kimlik + Açık Yetki | Uç policy'leri DEĞİŞMEZ (`cards.write` + `MerchantScoped`); kiracı çift-kapı; PAN gateway'e girmez (Model A ile daha da güçlü). Sandbox secret user-secrets (FR-009). | ✅ |
| Config (Options pattern) | `ProviderOptions` → `IyzicoProviderSettings` POCO + `AddOptionsExt` (BindConfiguration+Validate); magic-string yok. | ✅ |

**Gate sonucu**: GEÇTİ. Not: iyzico'ya gerçek çağrı, 031 Model B kararının bilinçli tersine
çevrilmesi — spec Assumptions + memory'de kayıtlı (anayasal ihlal değil, ürün kararı).

## Project Structure

### Documentation (this feature)

```text
specs/032-iyzico-card-storage/
├── plan.md · research.md · data-model.md · quickstart.md
└── contracts/vault-api.md
```

### Source Code (repository root)

```text
src/services/Payment.Api/
├── Options/IyzicoProviderSettings.cs         # YENİ (ApiKey/SecretKey/BaseUrl POCO)
├── CardVault/PanTools.cs                     # LuhnValidator SİLİNİR; Bin/Last4/BrandDetector KALIR + CardAssociation eşleyici
│   IPanProtector.cs, DevPanProtector.cs      # SİLİNİR (PAN saklanmıyor)
├── Domains/StoredCards/
│   ├── StoredCard.cs                         # EncryptedPan→CardUserKey/CardToken; Create imza değişir; Luhn/AES çıkar
│   ├── CardBrand.cs, StoredCardStatus.cs     # DEĞİŞMEZ
│   ├── StoredCardEndpointExtension.cs        # DEĞİŞMEZ (rotalar sabit)
│   └── Features/Commands/
│       ├── TokenizeCard.cs                   # handler iyzico Card.Create çağırır (fail-closed)
│       └── RevokeCard.cs                     # handler iyzico Card.Delete best-effort (fail-open)
├── Program.cs                                # +AddOptionsExt(IyzicoProviderSettings); StoredCard Marten kaydı korunur
└── GlobalUsings.cs                           # gerekirse Provider.StoredCards using (zaten var)

tests/Payment.Api.Tests/
├── StoredCardCreateTests.cs                  # Luhn/normalize/PAN testleri SİLİNİR; Create-kimliklerle + zorunlu alan
└── StoredCardRevokeTests.cs                  # DEĞİŞMEZ (idempotent revoke)
```

**Structure Decision**: 031 dosya düzeni korunur; yerinde revizyon. `Provider/StoredCards` (iyzico
wire tipleri) ilk kez GERÇEKTEN kullanılır. ECommerce dokunulmaz.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| iyzico'ya gerçek dış çağrı (031 Model B "çağrı yok" kararı tersine) | CVC-siz tekrar ödeme + recurring YALNIZ sağlayıcı Saklı Kart'ıyla mümkün (kullanıcı isteği); kendi vault CVC re-entry'ye mahkûm | Model B'de kalıp CVC re-entry — recurring imkânsız, kullanıcı hedefi karşılanmaz |
| FR-004 gruplama karşılanmaz (per-kart cardUserKey) | Dış sözleşme buyer kimliği taşımıyor (FR-008 sıfır-dokunuş korunmalı) | Sözleşmeye buyer ekleme → ECommerce dokunuşu, FR-008 kırılır; ödeme için gruplama şart değil |
