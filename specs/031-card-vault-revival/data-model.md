# Data Model: Kart Vault Dirilişi (031)

**Date**: 2026-08-14 | **Spec**: [spec.md](spec.md) | **Research**: [research.md](research.md)

## StoredCard (YENİ aggregate — Payment.Api, `Domains/StoredCards/`)

`StoredCard : AggregateRoot` — 017 kodunun R2/R3 sapmalı dirilişi. Marten identity = `Token`
(string; `Id` Guid alanı AggregateRoot'tan gelir ama kimlik Token'dır — eski desen).

| Alan | Tip | Not |
|---|---|---|
| `Token` | `string` | `"card_" + Guid("N")` — opak, immutable, Marten identity |
| `MerchantId` | `Guid` | kiracı sınırı; immutable |
| `EncryptedPan` | `string` | `IPanProtector.Protect` çıktısı; hiç okunmaz/dönmez |
| `Bin` | `string` | ilk 6 hane; immutable |
| `Last4` | `string` | son 4 hane; immutable |
| `Brand` | `CardBrand` | BC-içi enum (R2); immutable |
| `Expiry` | `string` | `MM/yy` |
| `HolderName` | `string` | |
| `Status` | `StoredCardStatus` | `Active=0, Revoked=1` |

### Davranışlar (ResultDomain — 014; handler notu zorunlu)

| Metot | Kural | Handler |
|---|---|---|
| `static Create(merchantId, pan, expiry, holderName, IPanProtector)` → `ResultDomain<StoredCard>` | zorunlu alanlar; PAN normalize (boşluk/tire temizle) + Luhn + 12-19 hane; expiry `MM/yy` + ay sonu ≥ bugün; türetimler (BIN/Last4/Brand); `Protect`; Active doğar. Aynı PAN → hep YENİ token (FR-006). | `TokenizeCardCommandHandler` |
| `Revoke()` → `ResultDomain` | Active → Revoked; **zaten Revoked → Ok (idempotent)**; fiziksel silme yok. | `RevokeCardCommandHandler` |

**R3 kırpması**: `UpdateDetails` YOK. **Normalize farkı** (eskiye ek): PAN `Trim` yerine
rakam-dışı ayıklama (boşluk/tire) — spec edge case; Luhn zaten rakam-dışında false döndürüyordu,
normalize bunu kullanıcı dostu yapar.

## CardBrand (YENİ enum — `Domains/StoredCards/CardBrand.cs`)

`Unknown=0, Visa=1, MasterCard=2, Amex=3, Troy=4` — BrandDetector prefix kuralları (R2).

## CardVault altyapısı (`CardVault/` — R6)

| Tip | Görev |
|---|---|
| `IPanProtector` | `string Protect(string pan)` — enc-at-rest seam (Reveal ödeme spec'inde) |
| `DevPanProtector : IPanProtector, ISingletonDependency` | AES-CBC, dev-sabit anahtar, IV prepend (eski kod aynen) |
| `PanTools` (`LuhnValidator`, `BinExtractor`, `Last4Extractor`, `BrandDetector`) | saf statik yardımcılar (altyapı — helper serbest) |

## Statü makinesi

```
Create ──► Active ──Revoke()──► Revoked  (terminal; Revoke tekrar → Ok, Reactivate YOK — yeni kart = yeni tokenize)
```

## Kalıcılık

paymentDb, mevcut şema (`PaymentSchemaName`), Schema.For kaydı gerekmez (mevcut Program.cs stili);
`LoadAsync<StoredCard>(token)` string identity ile. Migration yok (temiz başlangıç).

## Slice haritası

```
src/services/Payment.Api/
├── CardVault/
│   ├── IPanProtector.cs
│   ├── DevPanProtector.cs
│   └── PanTools.cs
└── Domains/StoredCards/            # Provider/StoredCards (iyzico wire tipleri) AYRI — dokunulmaz
    ├── StoredCard.cs
    ├── CardBrand.cs
    ├── StoredCardStatus.cs
    ├── StoredCardEndpointExtension.cs
    └── Features/Commands/
        ├── TokenizeCard.cs
        └── RevokeCard.cs
```

**Ad çakışması notu**: `Provider/StoredCards/` (iyzico wire tipleri, namespace
`Payment.Api.Provider.StoredCards`) ile `Domains/StoredCards/` (namespace
`Payment.Api.Domains.StoredCards`) ayrı namespace'lerde — çakışma yok; `Card` tipi yalnız
Provider'da, domain'de `StoredCard`.
