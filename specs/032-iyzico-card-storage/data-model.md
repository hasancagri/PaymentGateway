# Data Model: iyzico Saklı Kart'a Geçiş (Model A) — 032

**Date**: 2026-08-14 | **Spec**: [spec.md](spec.md) | **Research**: [research.md](research.md)

## StoredCard (031'den DEĞİŞİR — Payment.Api, `Domains/StoredCards/`)

`StoredCard : AggregateRoot`. Marten identity = `Token` (string; 031 fix korunur).

| Alan | 031 | 032 | Not |
|---|---|---|---|
| `Token` | ✓ | ✓ | opak `card_`+Guid("N"); Marten identity; ECommerce'e dönen |
| `MerchantId` | ✓ | ✓ | kiracı sınırı |
| `EncryptedPan` | ✓ | **KALDIRILIR** | PAN artık iyzico'da |
| `CardUserKey` | — | **YENİ** | iyzico kullanıcı-kimliği (per-kart, R2) |
| `CardToken` | — | **YENİ** | iyzico kart-kimliği |
| `Bin` | ✓ | ✓ | iyzico `BinNumber`'dan (fallback: türet) |
| `Last4` | ✓ | ✓ | iyzico `LastFourDigits`'ten |
| `Brand` | ✓ | ✓ | iyzico `CardAssociation`'dan eşlenir (fallback: BrandDetector) |
| `Expiry` | ✓ | ✓ | `MM/yy` |
| `HolderName` | ✓ | ✓ | |
| `Status` | ✓ | ✓ | Active/Revoked |

### Davranışlar

| Metot | 032 imza | Kural | Handler |
|---|---|---|---|
| `Create` | `static ResultDomain<StoredCard> Create(Guid merchantId, string cardUserKey, string cardToken, string bin, string last4, CardBrand brand, string expiry, string holderName)` | zorunlu alanlar (merchantId, cardUserKey, cardToken boş olamaz); Active doğar. **Luhn/expiry/AES YOK** (iyzico doğruladı). | `TokenizeCardCommandHandler` |
| `Revoke` | `ResultDomain Revoke()` | Active→Revoked; idempotent; soft. DEĞİŞMEZ (031). | `RevokeCardCommandHandler` |

**031'den kalkanlar**: `Create`'in Luhn/expiry doğrulaması, `IPanProtector` parametresi, PAN
normalize. Doğrulama artık iyzico'da (handler çağrısı).

## CardBrand (KORUNUR — `Domains/StoredCards/CardBrand.cs`)

`Unknown, Visa, MasterCard, Amex, Troy`. iyzico `CardAssociation` string'i buna eşlenir
(`VISA`→Visa, `MASTER_CARD`→MasterCard, `AMERICAN_EXPRESS`→Amex, `TROY`→Troy, diğer→Unknown).
`BrandDetector`/`BinExtractor`/`Last4Extractor` (PanTools) fallback için KALIR; `LuhnValidator`
SİLİNİR.

## CardVault altyapısı (`CardVault/` — küçülür)

| Tip | 032 |
|---|---|
| `IPanProtector` | **SİLİNİR** (PAN saklanmıyor) |
| `DevPanProtector` | **SİLİNİR** |
| `PanTools`: LuhnValidator | **SİLİNİR** |
| `PanTools`: BinExtractor/Last4Extractor/BrandDetector | **KALIR** (fallback gösterim) + CardAssociation eşleyici eklenir |

## iyzico çağrı akışı (handler içi)

**Tokenize** (`TokenizeCardCommandHandler`):
```
CreateCardRequest{ Email=sentetik, ExternalId=yeni-token, Card=CardInformation{CardNumber, ExpireMonth, ExpireYear, CardHolderName, CardAlias} }
  → Card.Create(req, providerOptions)   [POST /cardstorage/card]
  → Status != "success" → INVALID_OPERATION_ERROR, Store YOK (fail-closed, FR-007)
  → başarı → StoredCard.Create(merchantId, resp.CardUserKey, resp.CardToken, resp.BinNumber, resp.LastFourDigits, brand, expiry, holder) + session.Store
```
**Revoke** (`RevokeCardCommandHandler`):
```
Load StoredCard by token; MerchantId eşleşmezse RECORD_NOT_FOUND (sahiplik sızdırmaz)
  → Card.Delete(DeleteCardRequest{CardUserKey, CardToken}, providerOptions)  [DELETE /cardstorage/card]  best-effort (hata yutulur, FR-006 fail-open)
  → card.Revoke() + session.Update
```

## ProviderOptions bağlama (R6)

`Options/IyzicoProviderSettings` (ApiKey/SecretKey/BaseUrl POCO) → `AddOptionsExt` (BindConfiguration
+ ValidateOnStart) → düz POCO inject. Handler bir `ProviderOptions` map'ler (mevcut Provider tipi).
Sandbox key user-secrets'ta.

## Statü makinesi (DEĞİŞMEZ)

```
Create ──► Active ──Revoke()──► Revoked (terminal; tekrar Revoke → Ok)
```

## Kalıcılık

paymentDb, `mt_doc_storedcard` — 031 kayıtları truncate (R7). `Schema.For<StoredCard>().Identity(
Token).Index(MerchantId)` korunur.

## Slice haritası (031'den revizyon)

```
src/services/Payment.Api/
├── Options/IyzicoProviderSettings.cs         # YENİ (Options pattern)
├── CardVault/PanTools.cs                     # LuhnValidator çıkar; Bin/Last4/Brand kalır + CardAssociation eşleyici
│   (IPanProtector.cs, DevPanProtector.cs → SİLİNİR)
├── Domains/StoredCards/
│   ├── StoredCard.cs                         # EncryptedPan çıkar, CardUserKey/CardToken girer; Create imza değişir
│   ├── CardBrand.cs / StoredCardStatus.cs    # DEĞİŞMEZ
│   ├── StoredCardEndpointExtension.cs        # DEĞİŞMEZ (rotalar sabit)
│   └── Features/Commands/{TokenizeCard,RevokeCard}.cs   # iyzico çağrısı eklenir
└── Program.cs                                # +AddOptionsExt (IyzicoProviderSettings)

tests/Payment.Api.Tests/                      # Luhn/normalize testleri çıkar; Create(kimliklerle)+Revoke kalır
```
