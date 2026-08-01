# Phase 0 Research: Merchant Komisyon Grid

Tüm kritik kararlar tasarım oturumunda netleşti; `NEEDS CLARIFICATION` yok. Aşağıda kararlar,
gerekçeleri ve reddedilen alternatifler.

## R1: Komisyon anahtarı — kombinasyon mu, banka mı?

- **Karar**: `(MerchantId, Criteria)`; `Criteria` = kart markası × tip × bölge × **taksit**. Merchant
  komisyonu herhangi bir tek bankaya bağlanmaz.
- **Gerekçe**: `BankRouter` (Payment.Api) bir kombinasyonu maliyete göre bankaya yönlendirir + failover
  uygular; merchant bankayı bilmez/seçmez. Komisyonu tek bankaya bağlamak routing gerçeğiyle çelişir.
- **Reddedilen**: Mevcut tek-banka modeli (`BankCommissionId` + `rate > banka oranı`). Merchant bankayı
  seçmediği için "hangi banka" tanımsız; pahalı rotada sessiz zarar riski.

## R2: Taksit ekseni kalsın mı?

- **Karar**: Kalır (taksit-başına). Merchant `Criteria` = banka ile aynı 4 eksen; mevcut `Criteria` VO
  aynen kullanılır, yeni tip gerekmez.
- **Gerekçe**: Gerçek dünyada komisyon taksit-başına belirlenir (peşin en düşük, taksit arttıkça artar).
  Taksit-başına model, banka tarafıyla simetriktir ve tavan-altı hesabını taksit-taksit iyi-tanımlı kılar.
- **Reddedilen**: Taksitsiz düz oran (eski PFApplication `MerchantCommission`: marka×tip×bölge). Azınlık
  model; taksit maliyet farkını yansıtamaz, banka tarafıyla asimetrik.

## R3: Margin koruması — hard invariant mı, soft-flag mı?

- **Karar**: Soft-flag. Kayıt engellenmez; `rate <= o kombinasyonun MAX banka oranı` ise satır işaretlenir.
- **Gerekçe**: Gateway bilinçli olarak tavan-altı (loss-leader) oran belirleyebilmeli; ama zarar görünür
  olmalı. Hard block bu esnekliği alır. Merchant oranı taksit-taksit tanımlı olduğundan işaret iyi-tanımlıdır.
- **Reddedilen**: (a) Hard block — bilinçli düşük oranı imkânsız kılar. (b) Saf advisory (işaret yok) —
  sessiz zarar riski en yüksek.

## R4: Tavan-altı işareti nerede hesaplanır — saklı mı, read-time mı?

- **Karar**: Read-time. `GetMerchantCommissions` handler'ı okuma anında banka oranlarından hesaplar;
  aggregate'te saklanmaz.
- **Gerekçe**: Banka oranı sonradan değişir/eklenir/silinirse saklı işaret bayatlar. Read-time hesap her
  zaman güncel banka oranlarını yansıtır → retroaktif senaryoları bedava çözer (SC-003). Ayrıca aggregate'i
  banka-bağımlılığından arındırır (İlke I/II ile uyumlu).
- **Reddedilen**: Kayıtta saklı `belowBankCeiling` bayrağı — banka oranı değişince tutarsızlaşır, senkron
  bakım gerektirir.

## R5: Banka aralığı verisi grid'e nasıl ulaşır?

- **Karar**: Backend `GetMerchantCommissions?merchantId=` **enriched** döner: satırlar = merchant'ın
  oranı olan kombinasyonlar ∪ en az bir bankanın servislediği kombinasyonlar. Her satır:
  `criteria, rate (nullable), bankMin, bankMax, belowBankCeiling, isMissing`.
- **Gerekçe**: Grid, oranı girilmemiş ama banka servisli kombinasyonlarda da banka aralığını göstermeli.
  Backend'de hesaplamak test edilebilir (SC-002/003) ve İlke II/III'e uyar (mantık UI'da değil).
  `BankCommission` kümesi tek sorguyla belleğe alınıp kombinasyona göre gruplanır (kardinalite düşük).
- **Reddedilen**: Saf client-side hesap (JS'te grup-by) — test edilemez, mantığı UI'a kaçırır. Ayrı bir
  "rate-ranges" endpoint'i — gereksiz ikinci çağrı; enriched GET tek kaynakta toplar.

## R6: Toplu kayıt (grid) deseni

- **Karar**: Yeni `POST /merchant-commissions/bulk` — `[Transactional]` upsert. `BankCode` yerine
  `MerchantId` alır; her item `(Criteria, Rate)`. Mevcut `(MerchantId, Criteria)` → `UpdateRate`; yok → `Create`.
- **Gerekçe**: 002 `BulkUpsertBankCommissions` deseni birebir uygulanır (aynı istekte tekrarlanan kriteri
  bellekte izle, atomik geri sarma). Tek-tek POST/PUT geriye uyum için korunur.
- **Reddedilen**: Ayrı ayrı N POST çağrısı — atomik değil, kısmi başarı bırakır.

## R7: Merchant listesi

- **Karar**: Admin, merchant listesini `IMerchantApiClient.GetAllAsync` (`GET /api/v1/merchants`) ile alır.
  Commission.Api handler'ı merchant doğrulamak için Merchant.Api'ye senkron çağrı YAPMAZ.
- **Gerekçe**: BC izolasyonu (İlke I). `MerchantId` opak referanstır; grid'i doldurmak için merchant
  adları yalnız UI katmanında zenginleştirilir.
- **Reddedilen**: Backend'de merchant doğrulama/enrichment — cross-BC senkron bağ, izolasyonu kırar.

## R8: Banka kodu filtresi

- **Karar**: YOK. Merchant grid'i banka eksenine göre filtre sunmaz.
- **Gerekçe**: Merchant komisyonu bankaya bağlı değil; banka yalnız referans aralığıdır. Banka filtresi
  anlamsız. (002'deki FR-013 karşılığı bilinçli düşer.)

## R9: Kaldırılan artıklar

- `MerchantCommission.BankCommissionId`, `.BankCode`, `rate > banka oranı` invariant metotları.
- `CommissionResourceConstants.MERCHANT_RATE_MUST_EXCEED_BANK_RATE` (kullanılmaz kalır).
- **Gerekçe**: Ölü kod + yanıltıcı invariant bırakmamak (YAGNI, İlke IV temizliği).