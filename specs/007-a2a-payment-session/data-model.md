# Phase 1 — Data Model: A2A Ödeme Oturumu (007 — taksit seçimine kadar)

Payment BC (Payment.Api) içinde. Marten aggregate; anayasa II (zengin domain). **Çekim 007 dışı**
olduğundan ödeme/3D/işlem alanları modele **girmez** — pay feature'ında eklenecek.

## Aggregate: `PaymentSession`

A2A task'ının kalıcı domain izdüşümü. Bir agent-başlatımlı akışın kimliği + fazı.
`AggregateRoot`'tan türer; private setter + statik `Create` + davranış metotları.

### Alanlar

| Alan | Tip | Not |
|------|-----|-----|
| `Id` | `Guid` | Session kimliği (A2A `contextId` eşlemesi için de) |
| `CardToken` | `string` | Kayıtlı kart token referansı. **PAN değil.** |
| `CartAmount` | `decimal` | Sepet tutarı (TL). Sunucu-otoriter tutar kaynağı. |
| `Status` | `PaymentSessionStatus` | Faz makinesi (aşağıda) |
| `_offeredInstallments` | `List<OfferedInstallment>` | private; readonly expose. Faz 1'de dolar. |
| `SelectedInstallmentCount` | `int?` | Faz 2'de set; ⊂ offered |
| `FailReason` | `string?` | Başarısızlıkta neden (kart verisi sızdırmaz) |
| `CreatedAt` / `UpdatedAt` | denetim | `AggregateRoot`/`BaseModel` konvansiyonu |

> **007 dışı (pay feature'ında eklenecek):** `PaymentId`, `ResultTransactionId`, `ResultBankCode`,
> `AttemptedBankCodes`, `Redirect3DContent`. Bunlar çekimle gelir.

### Enum: `PaymentSessionStatus` (`Enumeration`)

```
Opened               # Create sonrası, henüz taksit sunulmadı (geçici ara durum)
QuoteProvided        # Faz 1 tamam: taksit listesi sunuldu (A2A input-required)
InstallmentSelected  # Faz 2: kullanıcı taksit seçti — 007'nin TERMİNAL fazı (çekim seam'e devredilir)
Failed               # geçersiz token / POS yok / sepet ≤ 0 / boş taksit listesi
```

> **007 dışı:** `Awaiting3D`, `Completed` — çekim fazları, pay feature'ında.

### Value Object: `OfferedInstallment`

Kullanıcıya sunulan bir satır. Arka plandaki POS/banka **taşınmaz** (kullanıcı görmez, SC-004).

| Alan | Tip | Not |
|------|-----|-----|
| `InstallmentCount` | `int` | 1 = peşin (tek çekim) |
| `UserTotalAmount` | `decimal` | **= `CartAmount`** (Model A; sapma 0, FR-010) |
| `MonthlyAmount` | `decimal` | `CartAmount / InstallmentCount` (2 hane yuvarla) |

> En ucuz destekleyen POS `BankRouter`'la quote anında seçilir ama VO'ya **yazılmaz** — kullanıcıya
> banka/POS gösterilmez. (Pay feature'ı seçim anında POS'u taze hesaplar.)

### Davranış metotları (invariant'lar aggregate'te)

| Metot | Kural / invariant |
|-------|-------------------|
| `static Create(token, cartAmount)` | `cartAmount > 0` yoksa reddet. Status = `Opened`. |
| `OfferInstallments(IEnumerable<OfferedInstallment>)` | Status `Opened` olmalı. Boş liste → `Fail("ödeme alınamıyor")`. Her satır `UserTotalAmount == CartAmount` (Model A doğrulaması, aksi halde invariant ihlali). Status → `QuoteProvided`. |
| `SelectInstallment(int count)` | Status `QuoteProvided` **veya** `InstallmentSelected` olmalı (tekrar seçim güncelleme). `count` ⊂ `_offeredInstallments` değilse reddet (FR-012). Status → `InstallmentSelected`. |
| `Fail(reason)` | Terminal başarısızlık. Status → `Failed`. |

**Faz sırası (FR-017/018)**: quote yapılmamış (`Opened`) oturuma `SelectInstallment` reddedilir.
Tekrarlı `SelectInstallment` idempotent-güncelleme: çift faz geçişi yok, çekim tetiklenmez.

### Faz geçiş diyagramı

```
Create ─► Opened ──OfferInstallments──► QuoteProvided ──SelectInstallment──► InstallmentSelected
             │            │(boş liste)                          ▲______________│ (tekrar seçim: güncelle)
             │            ▼
             └───────► Failed  ◄──(geçersiz token / POS yok / tutar ≤ 0)

                    [InstallmentSelected = 007 terminal — çekim seam'e devredilir]
```

## Tüketilen mevcut varlıklar (değişmez / referans)

- **`PosAccount`** (aggregate) — komisyon gridi; `BankRouter` girdisi.
- **`BankRouter`** (domain service) — `Route(amount, installment, card, accounts)` maliyet sıralı
  aday POS. Model A'da `amount = CartAmount`; komisyon yalnız **sıralama** için, kullanıcı tutarına
  girmez.
- **`CardInfo`** (domain tipi, `Domains/Payments/BankRouter.cs` — CP.VPOS tipi **değil**):
  `(BankCode, IsCreditCard, InstallmentBankCodes)`. `ICardVault.ResolveCardInfoAsync(token)`
  çıktısı; içte **008'in `ResolveBinCard.Resolve(session, bin)`** çözümünü çağırır (BIN → DB
  katalog → `CardInfo?`, bulunamazsa `null`). **PAN yok** (007). CP.VPOS BinService **kullanılmaz**
  — 008 native çözümü geldi, çeviri gerekmez.

## Tutarlılık kuralları

- **CR-1 (Model A)**: her `OfferedInstallment.UserTotalAmount == CartAmount` (SC-002, tolerans 0).
- **CR-2 (filtre)**: yalnız aktif POS'un desteklediği taksit sayısı listede (FR-008, SC-003).
- **CR-3 (banka kartı)**: kart kredi değilse yalnız `InstallmentCount = 1` (FR-009).
- **CR-4 (seçim)**: select yalnız sunulan `InstallmentCount`'lardan (FR-012).
- **CR-5 (faz)**: select, oturum `QuoteProvided`/`InstallmentSelected` fazındayken (FR-017).
- **CR-6 (TL)**: yalnız TL (anayasa).