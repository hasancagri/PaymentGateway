# Phase 1 Data Model: NonSecure Banka Çekim Dilimi

Yeni aggregate YOK. Mevcut `PaymentLink` (089) genişler; `PaymentAttempt` onun altında child entity. `BinCard` = BC-içi salt-okur referans doküman (aggregate değil). PosAccount = Options öngörü (kalıcı değil).

## PaymentLink (aggregate — genişler)

**Yeni alanlar**
- `PaymentModel PaymentModel { get; private set; }` — `enum PaymentModel { NonSecure = 0 }` (3DS değeri rezerve, bu versiyonda eklenmez). `Create`'te `NonSecure` set edilir (ctor imzası değişmez → 089 testleri bozulmaz).
- `private readonly List<PaymentAttempt> _attempts = new();` + `public IReadOnlyList<PaymentAttempt> Attempts => _attempts;`

**Yeni/değişen davranış**
- `ResultDomain RecordAttempt(BankChargeOutcome outcome, DateTime now)`
  - Guard (private `PayableGuard(now, grace)` — mevcut `IsPayable` gövdesinden çıkarılır, 3. kopya olmaz):
    - `IsTerminal` → `TERMINAL_VIOLATION` (tek-yön; Paid link ikinci çekim yapamaz = çift-çekim guard).
    - Pending/Opened değilse ya da `now > ExpiresAt + grace` → ilgili hata (lazy Expired flip).
  - `outcome.IsApproved` → attempt ekle, `Status = Paid`, `UpdatedTime` güncelle, `Ok`.
  - declined → attempt ekle (DeclineClass ile), durum DEĞİŞMEZ (Opened kalır), `Ok` (red outcome'da taşınır, `Error` değil).
- Grace `PaymentLinkOptions.GraceWindow`'dan (handler verir). `IsPayable` stateless ön-kontrol olarak kalır.

**Değişmezler**
- Terminal tek-yön (FR-002/FR-008 089). Paid yalnız onaylı attempt'le erişilir.
- PAN/CVV YOK (İlke VI) — aggregate kart verisi taşımaz.

**Durum geçişi (bu dilimde erişilen)**
```
Pending ─Open→ Opened ─RecordAttempt(approved)→ Paid (terminal)
                 │
                 └─RecordAttempt(declined)→ Opened (kalır; yeni deneme mümkün)
Pending/Opened ─(lazy TTL)→ Expired | ─Cancel→ Cancelled   (089, değişmez)
```

## PaymentAttempt (child entity — YENİ)

AggregateRoot DEĞİL, base almaz; düz class (Marten NonPublicSetters uyumlu parametresiz ctor). Yalnız `PaymentLink.RecordAttempt` içinde üretilir.

| Alan | Tip | Not |
|---|---|---|
| `BankCode` | string | Denemeyi işleyen (yönlendirilen) banka |
| `IsApproved` | bool | `Outcome` özetinden |
| `DeclineClass` | `DeclineClass?` | onayda null |
| `ProviderRef` | string? | opak banka işlem referansı (gelecekte durum-sorgu/mutabakat); sır değil |
| `AuthCode` | string? | onay kodu (onayda) |
| `RawCode` | string | banka ham kodu (denetim) |
| `AttemptedAt` | DateTime | |

**Değişmez**: Kart verisi İÇERMEZ (İlke VI). Red'ler denetim/mutabakat için saklanır (silinmez).

## BankChargeOutcome (Value Object — YENİ)

`Domains/PaymentLinks/ValueObjects/BankChargeOutcome.cs`. `record` + private ctor + static factory. Anti-corruption normalize şekli (pos-master TX_APPROVED/TX_DECLINED + 4-sınıf).

- Factory: `Approved(string providerRef, string authCode, string rawCode)` · `Declined(DeclineClass declineClass, string rawCode, string? providerRef = null)`
- Alanlar: `bool IsApproved`, `DeclineClass? DeclineClass`, `string? ProviderRef`, `string? AuthCode`, `string RawCode`

## DeclineClass (enum — YENİ, PaymentLink.cs içinde)

```
enum DeclineClass { IssuerDecline, TechnicalFailure, ConfigError, FraudDecline }
```
Failover kararını yöneten domain taksonomisi: Issuer/Fraud → failover YOK; Technical/Config → failover.

## BinCard (BC-içi referans doküman — YENİ)

`Domains/BinCards/BinCard.cs`. Aggregate DEĞİL — salt-okur düz Marten dokümanı (BIN tablosu). Startup'ta embedded JSON'dan seed (`BinCardSeeder`). Güncelleme = reseed (dev).

| Alan | Tip | Not |
|---|---|---|
| `Bin` | string | BIN prefix (6-8 hane), id |
| `IssuerBankCode` | string | on-us eşlemesi için (== PosAccount.BankCode olabilir) |
| `Scheme` | string | Visa/Mastercard/Troy |
| `CardType` | string | credit/debit (yetenek elemede kullanılır) |

Lookup: `ResolveByPan(pan)` → ilk eşleşen `BinCard?` (prefix); bulunamazsa `null` → routing fail-open (on-us atlanır).

## PosAccountOptions / BankProfile (yönlendirme öngörü — YENİ, Options)

`Options/PosAccountOptions.cs`. Acquirer başına öngörü kaydı (banka otorite, PG kopyası — İlke VII). 3 giriş.

| Alan | Tip | Not |
|---|---|---|
| `BankCode` | string | adapter `BankCode` ile eşleşir; on-us için issuer ile kıyas |
| `SupportedCardTypes` | string[] | yetenek eleme (credit/debit) |
| `SupportsNonSecure` | bool | bu versiyonda hep true (3DS seam) |
| `CostPriority` | int | düşük = önce (maliyet sıralaması) |

## RoutingCandidate (router iç VO — YENİ, ops.)

Router'ın aday sıralama çıktısı: `BankCode`, `IsOnUs`, `CostPriority`. Sıralama: on-us önce, sonra CostPriority. Saf hesap (test-first).

## BankChargeRequest + CardData (transient input VO'lar — YENİ, saklanmaz)

`Domains/PaymentLinks/BankCharge/`. Yalnız çekim anında yaşar (endpoint body → command → request → wire). ASLA saklanmaz/loglanmaz/aggregate'e girmez (İlke VI).

- `CardData`: `Pan`, `HolderName`, `ExpiryMonth`, `ExpiryYear`, `Cvv`
- `BankChargeRequest`: `CardData Card`, `decimal Amount`, `string Currency`, `string OrderRef`, `Guid PaymentLinkId`

## İlişkiler

```
PaymentLink 1 ──* PaymentAttempt   (nested, consistency aynı aggregate)
PaymentAttempt *── 1 BankChargeOutcome özeti (alanlara düzleştirilir)
BankChargeRouter:
  1) BinCard.ResolveByPan(pan) → issuerBankCode (ya da null)
  2) PosAccountOptions[] → yetenek eleme → aday
  3) aday sıralama: on-us (issuer==PosAccount.BankCode) → CostPriority → RoutingCandidate[]
  4) sırayla IBankPaymentAdapter.ChargeAsync → BankChargeOutcome
     Approved/Issuer/Fraud → dur; Technical/Config → sıradaki aday
BankCode: BinCard.IssuerBankCode ⟷ PosAccountOptions.BankCode ⟷ IBankPaymentAdapter.BankCode (on-us zinciri)
```

## Marten notu

`PaymentAttempt` koleksiyonu `PaymentLink` dokümanının parçası olarak serialize edilir (ayrı tablo/stream yok). 089 partial-unique index (aktif-tekil) + OpenToken unique korunur. `BinCard` ayrı düz doküman tablosu (Bin = id); startup seed (`ApplyAllDatabaseChangesOnStartup` zaten açık). Yeni defansif migration yok (dev aşaması).
