# Phase 1 Data Model: Card Vault / Tokenization

## Aggregate: StoredCard

Payment BC document (Marten). Kartın kayıt-otoritesi. Kimlik = opak `Token`.

| Alan | Tip | Notlar |
|------|-----|--------|
| `Token` | string | **Marten identity**; opak, tahmin-edilemez (ör. `card_` + Guid "N"); PAN'dan türetilmez; immutable |
| `MerchantId` | Guid | Sahip merchant; **index**; tenant sınırı (immutable) |
| `EncryptedPan` | string | `IPanProtector` ile korunmuş PAN (dev simüle enc-at-rest); **hiç okunmaz/dönmez** bu feature'da; immutable |
| `Bin` | string | PAN'dan türetilen ilk 6–8 hane; resolve girdisi; immutable |
| `Last4` | string | PAN son 4 hane; denetim/gösterim; immutable |
| `Brand` | CardBrand | PAN prefix'inden türetilir; immutable |
| `Expiry` | string (`MM/yy`) | Son kullanma; `UpdateDetails` ile değişebilir |
| `HolderName` | string | Kart sahibi; `UpdateDetails` ile değişebilir |
| `Status` | StoredCardStatus | `Active` \| `Revoked` |
| `Id` / audit | Guid + BaseUserTrackModel | AggregateRoot tabanından (CreatedTime/UpdatedTime vb.) |

### Enums

- **StoredCardStatus**: `Active`, `Revoked`
- **CardBrand**: `Visa`, `Mastercard`, `Amex`, `Troy`, `Unknown` (Enumeration veya enum — mevcut
  Payment.Api enum konvansiyonuna uyar)

### Fabrika / Davranışlar (hepsi handler'dan çağrılır; `<remarks>Handler: X</remarks>`)

- `static ResultDomain<StoredCard> Create(Guid merchantId, string pan, string expiry, string holderName, IPanProtector protector)`
  → **Handler: TokenizeCard**
  - Invariant: PAN Luhn'dan geçer (aksi `Error`), expiry gelecekte (aksi `Error`), pan/expiry/holder
    boş değil. Token üretilir, `Bin`/`Last4`/`Brand` türetilir, PAN protector ile şifrelenir,
    `Status = Active`. `ResultDomain<StoredCard>.Ok(card)`.
- `ResultDomain UpdateDetails(string expiry, string holderName)` → **Handler: UpdateCard**
  - Invariant: `Status == Active` (Revoked → `Error`), expiry gelecekte. Yalnız `Expiry`+`HolderName`
    değişir. PAN/token/bin/last4/brand DOKUNULMAZ.
- `ResultDomain Revoke()` → **Handler: RevokeCard**
  - Idempotent: zaten `Revoked` → `Ok` (hata değil). Aksi `Status = Revoked`. Kayıt fiziksel durur.

> Aggregate kuralları: private setter, private helper YOK (inline), aggregate metodu yalnız
> handler'dan çağrılır, her public metotta `<summary>` + `<remarks>Handler: ...>`. `MessageItem`
> her metotta inline (referans: `SettlementAccount.UpdateDetails`).

### State geçişleri

```
(yok) --Create--> Active
Active --UpdateDetails--> Active
Active --Revoke--> Revoked
Revoked --Revoke--> Revoked   (idempotent no-op Ok)
Revoked --UpdateDetails--> RET (Error)
```

## Resolve akışı (query-side, mevcut sözleşme)

`ICardVault.ResolveCardInfoAsync(token)` (implementasyon `SimulatedCardVault`→gerçek):
1. `LoadAsync<StoredCard>(token)` — null → `Error` (bulunamadı).
2. `Status == Revoked` → `Error`.
3. `ResolveBinCard.Resolve(session, card.Bin, ct)` → `CardInfo?`; null → `Error`; değilse
   `ResultDomain<CardInfo>.Ok(cardInfo)`.

> Not (R3): Bu feature'da resolve merchant eşleşmesi yapmaz (PaymentSession merchantId taşımıyor);
> ödeme-anı cross-merchant kapısı charge feature'ında (007 devamı). Yazım tarafı tam korunur.

## Altyapı tipleri

- **IPanProtector** (`CardVault/`): `string Protect(string pan)` (+ ileride `string Reveal(string enc)`
  — bu feature'da kullanılmaz). Dev impl `DevPanProtector` reversible, `ISingletonDependency`.
- **PanTools** (saf, `CardVault/` veya `SharedKernel`): `LuhnValidator.IsValid(pan)`,
  `BinExtractor.Extract(pan)`, `Last4Extractor`, `BrandDetector.Detect(pan) : CardBrand`.

## Marten kaydı (Program.cs)

```
opts.Schema.For<Payment.Api.Domains.StoredCards.StoredCard>()
    .Identity(x => x.Token)
    .Index(x => x.MerchantId);
```

## İlişkiler

- StoredCard `MerchantId` → Merchant (yalnız referans, Payment BC'de zengin model değil — anayasa I).
- StoredCard `Bin` → BinCard katalog (resolve anında lookup; FK değil, lookup).
- Token → ECommerce tarafı `(UserId, token, last4, brand, isDefault)` (dış sistem, bu repo dışı).