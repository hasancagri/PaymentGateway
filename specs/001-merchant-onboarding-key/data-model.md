# Phase 1 Data Model: Merchant.Api + Commission.Api

Tarih: 2026-07-31. Kaynak: spec.md Key Entities + research.md kararları. Aggregate'ler anayasa
II: private setter + statik `Create` fabrikası + davranış metotları + invariant içerde.
Kalıcılık Marten document (base tipler `Common.Domains`).

> **Enum/tip modelleme (implement-time detayı):** Kesin biçim kod anında netleşir; bu belge
> bağlamaz. Referans mimari status/type için düz `public enum` + `record` VO kullanıyor; anayasa
> `Enumeration`'ı öneriyor. Kart tipleri + MerchantStatus smart-enum/key-value adayı; kullanım
> gerektiğinde dönüştürülür.

> **Kapsam dışı (bu dilim):** Legacy `Merchant`'taki `IpList` (IP whitelist) eklenmez.
> **MerchantKey** Merchant.Api'de tutulmaz; key üretimi + hash saklama Identity dilimine ertelendi
> (`Identity.Server`/`ApiKey`, `identityDb`, yalnız SHA-256 hash).

---

## Merchant.Api

### Aggregate: `Merchant : AggregateRoot`

Global merchant registry — merchant kimliğinin **source of truth**'u. Key üretimi/alanı YOK.
Tenant YOK (global).

| Alan | Tip | Not |
|---|---|---|
| `Id` | `Guid` | BaseModel; dış dünyaya MerchantId |
| `Name` | `string` | boş olamaz |
| `Email` | `string` | boş olamaz + e-posta formatı |
| `Phone` | `string` | boş olamaz |
| `CountryCode` | `string` | **lookup kodu** (örn. "TR"); nesne değil |
| `CityCode` | `string` | **lookup kodu**; nesne değil |
| `Mcc` | `string` | **lookup kodu** (4 hane, örn. "5411") |
| `WebhookUrl` | `string` | mutlak `http`/`https` URL |
| `Status` | `MerchantStatus` | **smart-enum**; Create → Active |
| BaseModel alanları | | CreatedTime/UpdatedTime/IsActive/IsDeleted |

**Fabrika:** `static ResultDomain<Merchant> Create(name, email, phone, countryCode, cityCode, mcc, webhookUrl)`
— saf **format** doğrulamaları. Lookup **varlık** doğrulaması handler'da (bkz. bölünme).

**Davranışlar:**
- `ResultDomain UpdateProfile(...)` — aynı format doğrulaması.
- `Activate()` → Active, `Deactivate()` → Passive, `Suspend()` → Suspended.

### Smart-enum: `MerchantStatus`

| Kod | Ad |
|---|---|
| Active | Aktif |
| Passive | Pasif |
| Suspended | Askıda |

### Lookup referansları (kod-içi gömülü — DB'de DEĞİL)

`CountryCode`, `CityCode`, `Mcc` **ayrı aggregate/alt-entity DEĞİL** — sabit standart referans
veri. Merchant yalnız **kodu** saklar; nesne tutmaz, cascade yok.

- Yerleşim: **assembly'e gömülü** (static map veya embedded JSON), açılışta belleğe yüklenir.
  DB tablosu yok. Yönetim ihtiyacı çıkarsa ayrı Reference BC'ye terfi (Obsidian todo adayı).

```csharp
public record MccRef(string Code, string Name);        // "5411" -> "Grocery Stores"
public record CountryRef(string Code, string Name);    // "TR"   -> "Türkiye"
public record CityRef(string Code, string Name, string CountryCode);

public interface IMccLookup     : ISingletonDependency { bool Exists(string code); string? NameOf(string code); }
public interface ICountryLookup : ISingletonDependency { bool Exists(string code); string? NameOf(string code); }
public interface ICityLookup    : ISingletonDependency { bool Exists(string code); bool BelongsTo(string cityCode, string countryCode); }
```

### Doğrulama bölünmesi (kural)

| Kural | Nerede | Neden |
|---|---|---|
| isim/telefon/email boş değil | `Merchant.Create` (aggregate) | saf format |
| e-posta format | `Merchant.Create` | saf format |
| MCC `^\d{4}$`, webhook mutlak URL | `Merchant.Create` | saf format |
| MCC/Country/City **kayıtlı mı** | **handler** (`I*Lookup`) | referans veri okuması; aggregate saf kalır, lookup'a çağrı yapmaz |
| City ↔ Country tutarlı mı | handler (`ICityLookup.BelongsTo`) | çapraz lookup |

---

## Commission.Api

Tek servis, iki aggregate + ortak `Criteria` value object + üç kart tipi (`Domains/Shared`).

### Value Object: `Criteria`  *(record, değer eşitliği)*

```
Criteria(CardBrand Brand, CardType Type, TransactionRegion Region, int InstallmentCount)
```

- `InstallmentCount >= 1` (1 = peşin).
- Eşitlik: dört bileşen. Benzersizlik bu değere dayanır.
- Marten serileştirme: `PosAccount` deseni (Newtonsoft, non-public setter/ctor).

### Kart tipleri  *(smart-enum / key-value; biçim implement-time)*

`CardBrand`: `VISA→Visa`, `MASTERCARD→Mastercard`, `TROY→Troy`, `AMEX→Amex`
`CardType`: `CREDIT→Kredi`, `DEBIT→Banka`, `PREPAID→Ön Ödemeli`
`TransactionRegion`: `DOMESTIC→Yurtiçi`, `INTERNATIONAL→Yurtdışı`

> Değer setleri v1; genişletilebilir. Bölge = kartın çıkış menşei (≠ para birimi), TL kısıtıyla çelişmez.

---

### Aggregate: `BankCommission : AggregateRoot`

Gateway'in bankaya ödediği (maliyet). **Global** (tenant yok). Invariant'ın referans oranı.

| Alan | Tip | Not |
|---|---|---|
| `Id` | `Guid` | |
| `BankCode` | `string` | 4 hane (CP.VPOS/PosAccount `BankService` ile tutarlı) |
| `Criteria` | `Criteria` | marka×tip×bölge×taksit |
| `Rate` | `decimal` | yüzde (örn. 1.75); `>= 0` |
| BaseModel alanları | | |

**Benzersizlik:** `(BankCode, Criteria)` tek → handler duplicate kontrol (RECORD_DUPLICATE).
**Fabrika:** `Create(bankCode, criteria, rate)` — bankCode 4 hane; Installment ≥ 1; rate ≥ 0.
**Davranış:** `ResultDomain UpdateRate(decimal rate)` — rate ≥ 0.

> Veri bu dilimde `CreateBankCommission` ile girilir (quickstart seed). PosAccount uzlaştırma
> sonraki dilim (Obsidian todo).

---

### Aggregate: `MerchantCommission : AggregateRoot`

Merchant'ın gateway'e ödediği (gelir). Belirli bir `BankCommission`'a bağlı; invariant onun
oranına karşı. Tenant YOK (düz `MerchantId` filtresi).

| Alan | Tip | Not |
|---|---|---|
| `Id` | `Guid` | |
| `MerchantId` | `Guid` | Merchant.Api referansı — **çağrı YOK** (Karar 7) |
| `BankCommissionId` | `Guid` | bağlı banka oranı |
| `Criteria` | `Criteria` | snapshot (BankCommission'dan; okuma kolaylığı) |
| `BankCode` | `string` | snapshot |
| `Rate` | `decimal` | yüzde; **invariant `Rate > bankRate`** |
| BaseModel alanları | | |

**Benzersizlik:** `(MerchantId, BankCommissionId)` tek. İkinci giriş = **güncelle** (spec Edge Case). Handler upsert.
**Fabrika:** `Create(merchantId, bankCommission, rate)`
- `merchantId != Guid.Empty`
- **`rate > bankCommission.Rate`** değilse `Error` (kod: `MERCHANT_RATE_MUST_EXCEED_BANK_RATE`).
  **Kesin büyük**; eşit reddedilir (FR-008).
- `Criteria`/`BankCode` snapshot'lanır.
**Davranış:** `UpdateRate(decimal rate, BankCommission bankCommission)` — aynı invariant.
**Invariant konumu:** oran karşılaştırması **aggregate metodunda**; handler yalnız BankCommission'ı
yükleyip verir (in-process, aynı session).

---

## İlişkiler

```
Merchant (Merchant.Api)                       BankCommission (Commission.Api, global)
   Id ◄──── MerchantId (Guid, çağrısız)          Id ◄──── BankCommissionId
   │                                              │
   ├─ CountryCode/CityCode/Mcc = lookup kodu      │
   │   (kod-içi gömülü, DB'de değil, cascade yok) │
                                          MerchantCommission (Commission.Api)
                                             MerchantId, BankCommissionId, Criteria(snapshot), Rate
                                             invariant: Rate > BankCommission.Rate  (in-process)
```

- Merchant ↔ MerchantCommission: yalnız `Guid`; cross-servis çağrı YOK.
- Merchant ↔ Country/City/MCC: yalnız **kod** + gömülü lookup doğrulaması; nesne/cascade YOK.
- BankCommission ↔ MerchantCommission: aynı serviste; write-time in-process yükleme.

## Marten kayıt (Program.cs)

- Merchant.Api: `opts.Schema.For<Merchant>()`. (Lookup'lar Marten'da DEĞİL — kod-içi.)
- Commission.Api: `opts.Schema.For<BankCommission>()`, `opts.Schema.For<MerchantCommission>()`.
- `MultiTenanted` işareti YOK (multitenancy ertelendi — Karar 5).

## Doğrulama özeti (Result akışı — exception değil)

| Kural | Nerede | Mesaj kodu |
|---|---|---|
| Zorunlu alan boş (isim/email/telefon) | `Merchant.Create` | COMMON_MESSAGE_VALUE_IS_REQUIRED |
| E-posta / MCC format / webhook URL | `Merchant.Create` | COMMON_MESSAGE_INVALID_FORMAT |
| MCC/Country/City kayıtlı değil | handler (`I*Lookup`) | COMMON_MESSAGE_RECORD_NOT_FOUND |
| BankCode 4 hane / rate ≥ 0 | `BankCommission.Create` | COMMON_MESSAGE_INVALID_FORMAT / INVALID_RANGE |
| BankCommission duplicate | handler | COMMON_MESSAGE_RECORD_DUPLICATE |
| `merchantRate > bankRate` değil | `MerchantCommission.Create/UpdateRate` | MERCHANT_RATE_MUST_EXCEED_BANK_RATE |
| MerchantCommission tekrar giriş | handler | upsert → UpdateRate |