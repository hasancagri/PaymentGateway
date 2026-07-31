# Bank Referansı + Komisyon Grid Tasarımı

Tarih: 2026-07-31
BC: Commission.Api (+ Admin UI)
Durum: onaylandı, implementasyona geçiliyor

## Problem

Admin'de banka komisyonu tek-tek satır giriliyor. İki eksik:

1. **Banka listesi yok.** `BankCode` serbest string (`"0062"`). Filtre dropdown'ı ve
   "eksik combo" hesabı için kanonik bir banka kümesi gerekiyor. Kaynak bugün sadece
   CP.VPOS `BankService.AllBanks` (kütüphane, yanlış katman; Commission.Api ona bağımlı değil).
2. **Boşluksuz giriş yok.** Operatör hangi kombinasyonların komisyonsuz kaldığını göremiyor;
   eksik hücre checkout'ta taksit gösterilememesi demek.

## Kapsam (bu dilim)

1. `Bank` aggregate + tam CRUD (Commission.Api).
2. Admin "Bankalar" sayfası (liste/ekle/düzenle/sil).
3. BankCommission **grid** girişi: banka seç → bankanın taksitlerine göre tüm combo grid →
   dolu/eksik işaretli → toplu kaydet (bulk upsert).

### Kapsam dışı (bilinçli)

- BIN çözümleme / müşteri taksit-seçenekleri endpoint'i — ayrı dilim (`Yapılacaklar.md`).
- Bankalar arası dış-sistem entegrasyonu (CP.VPOS adapter) — BC değil, karar sabit.
- Debit/prepaid taksit kısıtı — grid tam gösterilir, operatör doldurur; ileride daraltılabilir.
- Yetkilendirme — proje geneli ertelenmiş.

## Model

### Bank aggregate (`Domains/Banks/Bank.cs`)

`AggregateRoot` (Guid Id + `IsActive` + `IsDeleted` `BaseModel`'den miras).

| Alan | Tip | Kural |
|------|-----|-------|
| `Code` | string | 4 hane, immutable, iş anahtarı (unique — handler kontrol) |
| `Name` | string | zorunlu |
| `SupportedInstallments` | `List<int>` | boş değil, her biri 1..15, distinct |
| `IsActive` | bool (miras) | pasif banka grid/router dışı |

- `Create(code, name, installments)` → `ResultDomain<Bank>` (fabrika + validasyon).
- `Update(name, isActive, installments)` → `ResultDomain` (Code değişmez).
- `SoftDelete()` → `IsDeleted=true`, `DeletedTime=UtcNow`.
- Sabit: `MaxInstallment = 15`.

Kimlik: Marten belge kimliği `Id` (Guid). Rota + benzersizlik iş anahtarı `Code` üzerinden
(`Query<Bank>().Where(b => b.Code == code)`).

### Vertical slice (`Domains/Banks/`)

- `Features/Commands/CreateBank.cs` — `POST /banks`. Aynı Code varsa `RECORD_DUPLICATE`.
- `Features/Commands/UpdateBank.cs` — `PUT /banks/{code}`. Name + IsActive + installments.
- `Features/Commands/DeleteBank.cs` — `DELETE /banks/{code}`. Soft-delete.
  **Guard:** bankaya bağlı (aynı `BankCode`, `!IsDeleted`) `BankCommission` varsa reddet →
  yeni kod `BANK_HAS_COMMISSIONS`.
- `Features/Queries/GetBanks.cs` — `GET /banks?includeInactive=bool`. Liste.
- `Features/Queries/GetBank.cs` — `GET /banks/{code}`. Detay (grid'i beslemek için).
- `BankEndpointExtension.cs` — grup `api/v{version}/banks`, mevcut pattern.

### Seed (`Domains/Banks/BankSeeder.cs`)

Marten `IInitialData` → başlangıçta 42 bankayı (CP.VPOS `BankService.AllBanks`'ten kopyalanan
Code+Name statik listesi) yoksa ekler. Varsayılan `SupportedInstallments = [1,2,3,6,9,12]`.
CP.VPOS'a **runtime bağımlılık yok** — liste seeder içine gömülü (referans olarak kopyalandı).

### Program.cs değişiklikleri

- `opts.Schema.For<Bank>();`
- `opts.InitialData.Add(new BankSeeder());` (veya eşdeğeri)
- `app.AddBankGroupEndpointExtension(apiVersionSet);`

## Bulk komisyon girişi

### Endpoint (`BankCommissions/Features/Commands/BulkUpsertBankCommissions.cs`)

`POST /bank-commissions/bulk`

```
BulkUpsertBankCommissionsCommand(string BankCode, List<Item> Items)
Item(CriteriaDto Criteria, decimal Rate)
```

Handler:
1. Bankayı Code ile yükle; yoksa/pasifse hata.
2. Bankanın `SupportedInstallments`'ında olmayan taksit gelirse hata (`INVALID_RANGE`).
3. Her item: `(BankCode, Criteria)` var mı → `UpdateRate`, yoksa `Create`. `[Transactional]`.
4. Yanıt: `{ created, updated }` sayıları.

Tek-tek `POST /bank-commissions` mevcut haliyle kalır (geriye uyum).

## Admin UI

### Bankalar (`Pages/Banks/`)

- `Index` — tablo (Code, Name, taksitler, Aktif). "Yeni Banka", satır başı "Düzenle"/"Sil".
- `Create` — Code, Name, taksitler (virgülle veya checkbox 1..15).
- `Edit` (`{code}`) — Name, IsActive, taksitler.
- `Delete` (`{code}`) — onay + soft-delete post.
- Nav'a `Bankalar` linki (`_Layout.cshtml`).

### BankCommission grid (`Pages/BankCommissions/Create.cshtml` yeniden)

- Üstte banka **dropdown** (`GET /banks` aktifler). Seçim → sayfa o bankayı yükler.
- `GET /banks/{code}` (taksitler) + `GET /bank-commissions?bankCode` (mevcut oranlar).
- Grid satırları: `CardBrand(4) × CardType(3) × TransactionRegion(2) × SupportedInstallments`.
  Her satır: oran input'u; mevcutsa dolu, yoksa boş + **eksik** işareti (CSS `.missing`).
- Kaydet → doldurulan satırlar `POST /bank-commissions/bulk`.

### Client (`Clients/CommissionApiClient.cs` + `ApiModels.cs`)

`ICommissionApiClient`'e ekle (yeni HttpClient yok, `commission-api` tekrar kullanılır):
`CreateBankAsync`, `GetBanksAsync`, `GetBankAsync`, `UpdateBankAsync`, `DeleteBankAsync`,
`BulkUpsertBankCommissionsAsync`. Modeller: `CreateBankRequest`, `UpdateBankRequest`,
`BankListItem`, `BankDetail`, `BanksResponse`, `BulkBankCommissionsRequest`.

`MessageText` haritasına `BANK_HAS_COMMISSIONS` → "Bankaya bağlı komisyon var, önce onları sil".

## Test

Saf domain birim testleri (`tests/Commission.Api.Tests`), banka HTTP yok:
- `Bank.Create`: geçerli; kötü Code (uzunluk); boş installments; aralık dışı taksit; duplicate distinct.
- `Bank.Update`: Code değişmez; geçersiz installments.
- `Bank.SoftDelete`: bayrak + zaman.
- Bulk upsert handler saf kısımları (kriter eşleme) — mümkünse.

## Riskler / notlar

- Grid büyük (144 satır @ 6 taksit). Kabul; kullanıcı "tüm kombinasyonlar" istedi. Görsel
  gruplama ile okunur tutulur.
- `Code` iş anahtarı ama Marten kimliği `Id`. Route/benzersizlik Code sorgusuyla — tutarlı kullan.
- Seed idempotent olmalı (`IInitialData` her açılışta çalışır; var olanı ekleme).