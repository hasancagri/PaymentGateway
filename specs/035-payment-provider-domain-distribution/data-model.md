# Phase 1 Data Model: Payment Domain VO'ları

4 yeni Value Object. Ortak kural (anayasa II + CLAUDE.md): `class`/`record`, **AggregateRoot DEĞİL**,
private ctor + statik `Create` fabrikası (→ `ResultDomain<T>`), immutable (init-only/get), iyzico
BİLMEZ (serileştirme yok). Konum: `<Aggregate>/ValueObjects/`. **Kalıcı değil** — charge/tokenize-anı
transient değerler; aggregate alanı olarak Marten'e yazılmaz. Davranış: **yapısal + ince doğrulama
şimdi**, zengin invariant sonra.

## Buyer (Domains/Payments/ValueObjects/)

Alıcı değer nesnesi. Kaynak: `ChargePayment.BuyerInput` HTTP DTO → `Buyer.Create(...)`.

| Alan | Tip | Not |
|---|---|---|
| Name, Surname | string | boş olamaz |
| Email | string | inline format doğrulama (@ + domain; Merchant IBAN/e-posta deseni referans) |
| GsmNumber | string | boş olamaz |
| IdentityNumber | string | TR kimlik 11 hane (ince) |
| RegistrationAddress, City, Country | string | boş olamaz |
| Ip | string | boş olamaz |

- `Create(name, surname, email, gsmNumber, identityNumber, registrationAddress, city, country, ip)
  : ResultDomain<Buyer>` — geçersizse `Error(MessageItem)`.
- Handler: `Buyer` VO → SDK `Iyzico.Provider.Payments.Buyer` wire DTO (Id="BY-…" ekli).

## Address (Domains/Payments/ValueObjects/)

Adres değer nesnesi. **Buyer'dan türetilir** (bağımsız input yok — research R4). shipping=billing.

| Alan | Tip | Kaynak (Buyer'dan) |
|---|---|---|
| ContactName | string | `Name + " " + Surname` |
| City, Country | string | Buyer.City / Buyer.Country |
| Description | string | Buyer.RegistrationAddress |

- `Create(contactName, city, country, description) : ResultDomain<Address>` (veya `FromBuyer(Buyer)`
  fabrikası — türetmeyi kapsül içinde tutar; boş kontrol).
- Handler: `Address` VO → SDK `Iyzico.Provider.Payments.Address` wire DTO (shipping + billing).

## BasketItem (Domains/Payments/ValueObjects/)

Sepet kalemi. Kaynak: `ChargePayment.BasketItemInput` HTTP DTO (Id, Name, Category1, Price) → `Create`.

| Alan | Tip | Not |
|---|---|---|
| Id, Name, Category1 | string | boş olamaz |
| Price | decimal | > 0 |

- `Create(id, name, category1, price) : ResultDomain<BasketItem>`.
- Handler: `BasketItem` VO → SDK `Iyzico.Provider.Payments.BasketItem` (ItemType="PHYSICAL", Price
  invariant-culture string). ItemType wire tarafında sabit (domain'e enum girmez — Kova-1 karar).

## CardInformation (Domains/StoredCards/ValueObjects/)

Tokenize-anı ham kart (Model A — transient, iyzico'ya bir kez). Kaynak: `TokenizeCard` command
(Pan, Expiry, HolderName) → `Create`.

| Alan | Tip | Not |
|---|---|---|
| Pan | string | Luhn (ince) — kalıcı DEĞİL |
| ExpireMonth, ExpireYear | string | expiry format (Expiry ayrıştırma) |
| CardHolderName | string | boş olamaz |

- `Create(pan, expiry, holderName) : ResultDomain<CardInformation>` — Luhn + expiry ayrıştırma.
- Handler: `CardInformation` VO → SDK `Iyzico.Provider.StoredCards.CardInformation` wire → `Card.Create`.
- `CardBrand` domain enum'u (mevcut) ile hizalı; VO markayı taşımaz (SDK yanıtından türetilen ayrı alan).

## İlişkiler / akış

```
HTTP body DTO (BuyerInput/BasketItemInput | Pan/Expiry/Holder)
   → [handler] VO.Create (doğrulama, ResultDomain)          ← domain (Payment.Api)
   → [handler] VO → SDK wire DTO map                         ← anti-corruption sınır
   → Iyzico.Provider çağrı-yürütücü (Payment.Create/Card.Create)
   → SDK yanıt → domain aggregate (Payment.Succeeded / StoredCard.Create)
```

VO'lar aggregate'e yazılmaz; yalnız akışta doğrulama + izolasyon taşır.
