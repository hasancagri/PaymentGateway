# Merchant Commission Grid (003) — Tasarım

Tarih: 2026-08-01
Branch: `feat/merchant-commission-grid`
Bounded Context: Commission (Commission.Api) + Admin UI

## 1. Amaç

Gateway'in üye işyeriyle (merchant) yaptığı komisyon anlaşmalarını yönetmek. 002'de
banka POS komisyonları (`BankCommission`) için yapılan referans + CRUD + grid işinin
merchant karşılığı. Operatör her merchant için komisyon oranlarını girerken, aynı
kombinasyondaki banka oranlarını da görür ve bilinçli değer belirler.

## 2. Çözülen tasarım sorunu (karar günlüğü)

Mevcut `MerchantCommission` aggregate tek bir `BankCommissionId`'ye bağlıydı ve
`rate > o bankanın oranı` hard invariant'ını taşıyordu. Bu **routing gerçeğiyle
uyumsuzdu**: `BankRouter` (Payment.Api) bir combo için maliyete göre banka seçer +
failover uygular; merchant bankayı bilmez/seçmez. Merchant tek combo için tek oran
öder, hangi banka servis ederse etsin.

Tartışmayla varılan sonuçlar:

- **Taksit başına gidilir.** Gerçek dünyada komisyon taksit-başına belirlenir (peşin
  en düşük, taksit arttıkça oran artar; banka tarafı kesin böyle). Merchant anahtarı
  banka ile aynı 4 eksen olur: `marka × tip × bölge × taksit`. Mevcut `Criteria` value
  object aynen kullanılır; yeni tip gerekmez.
  - Eski PFApplication `MerchantCommission` taksitsizdi (`marka × tip × bölge`), ama
    bu azınlık modeli; gateway taksit-başına anlaşacak.
- **Merchant satırı tek bankaya değil, criteria'ya bağlanır.** Bir criteria'yı birden
  çok banka servisler; merchant satırı o bankaların oran kümesine (min–max) bakar.
  `BankCommissionId` bağı düşer, `Criteria` snapshot doğrudan girdiden gelir.
- **Hard invariant DÜŞER, yerine soft-flag gelir.** Operatör (gateway) inisiyatif
  alabilmeli (ör. loss-leader combo), ama zarar görünür olmalı. Bkz. §4.
- **Read-time flag.** Flag saklanmaz; grid sorgusunda banka oranlarına karşı canlı
  hesaplanır. Bu, "banka oranı sonradan değişti/eklendi → merchant oranı artık altında"
  retroaktif senaryolarını bedava çözer (flag hep taze).
- **Banka kodu filtresi YOK.** (002'deki FR-013 karşılığı bilinçli düşer.)

## 3. Domain modeli

### `MerchantCommission` aggregate (refactor)

- Anahtar: `MerchantId (Guid)` + `Criteria`.
- Alanlar: `MerchantId`, `Criteria`, `Rate (decimal)`.
- Silinen: `BankCommissionId` bağı, `BankCode` snapshot (artık `Criteria` yeterli),
  `rate > bankCommission.Rate` invariant.
- `Create(merchantId, criteria, rate)`: `merchantId != Guid.Empty`, `rate > 0` sanity.
  Banka bağımlılığı YOK.
- `UpdateRate(rate)`: `rate > 0`.
- Benzersizlik: `(MerchantId, Criteria)` — aynı merchant + aynı combo tek satır.

Geriye uyum derdi yok: veri/seed yok, pre-release. Mevcut tek-tek Create/Update/Get
combo modeline refactor edilir.

### `Criteria` value object

Değişmez, mevcut hâliyle kullanılır: `CardBrand × CardType × TransactionRegion ×
InstallmentCount`. Taksit ekseni zorunlu.

## 4. Soft-flag = read-time projeksiyon

Flag **saklanmaz**. `GET /merchant-commissions?merchantId=` sorgusu her merchant
satırı için, o `Criteria`'yı servisleyen `BankCommission` kayıtlarını (aynı store,
Commission.Api) join eder ve hesaplar:

- `bankMin`, `bankMax`: o criteria'daki banka oranlarının min/max'ı.
- `belowBankCeiling`: `rate <= bankMax` ise `true` (margin riski işareti).
- Combo'yu hiç banka servislemezse `bankMin = bankMax = null`, `belowBankCeiling =
  false` (dormant hücre; operatör serbest).

Sonuç satırı: `{ criteria, rate, bankMin, bankMax, belowBankCeiling, isMissing }`.
`isMissing`: bu combo için henüz merchant oranı girilmemiş (grid'de boş hücre).

## 5. API (Commission.Api)

Vertical slice + CQRS, `[Transactional]` + `IDocumentSession`, `FeatureObjectResultModel<T>`.

- `POST /merchant-commissions` — tek kayıt (refactor: combo modeli, banka bağı yok).
- `PUT /merchant-commissions/{id}` — oran güncelle.
- `GET /merchant-commissions?merchantId=` — combo satırları + banka min/max + flag
  (§4). Banka kodu filtresi yok.
- `POST /merchant-commissions/bulk` — `[Transactional]` toplu upsert. 002'deki
  `BulkUpsertBankCommissions` pattern'i birebir örnek alınır.

## 6. Grid (Admin)

- Merchant seç → `marka × tip × bölge × taksit(1..15)` tüm combinasyonlar; eksik
  hücreler işaretli; toplu upsert.
- Merchant listesi Merchant.Api'den; `MerchantId` Guid; cross-call handler içinde YOK.
- Satır içi banka kolonu: `bankMin–bankMax`. `belowBankCeiling → true` ise satır
  kırmızı işaret. Dormant combo → "banka yok".
- 002'den taşınan: eksen filtreleri (marka/tip/bölge/taksit), boşları-doldur
  (`commission-grid.js`), pagination 20 (240 satır/merchant, gerekir).
- Eksen seçenekleri kaynağı: mevcut `GET /bank-commissions/criteria-options`
  (`Enum.GetNames`) yeniden kullanılır; UI enum kopyalamaz.
- Admin decimal binding: `Program.cs` `UseRequestLocalization(InvariantCulture)`
  zaten var (002 bug fix); merchant formları da yararlanır.

## 7. Kapsam dışı / ertelemeler

- Seed yok (operatör elle girer).
- Yetkilendirme yok (Identity BC ile gelecek).
- Banka kodu filtresi yok.
- CP.VPOS'a runtime bağımlılık yok.
- Merchant.Api'ye senkron cross-call yok; `MerchantId` opak Guid.

## 8. Test

Saf domain birim testleri (mevcut xUnit projesi):

- `MerchantCommission.Create` / `UpdateRate`: `rate > 0` sanity, `merchantId` boş red.
- Benzersizlik davranışı (aynı merchant+criteria).
- Read-time flag hesabı bir query/projeksiyon testiyle: `belowBankCeiling` sınır
  durumları (`rate == bankMax`, `rate > bankMax`, banka yok).

Banka HTTP çağrıları / Admin UI test edilmez.