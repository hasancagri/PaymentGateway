# Phase 0 Research: Merchant.Api + Commission.Api

Tarih: 2026-07-31. Bu dilimin kapsamı: iki yeni BC (Merchant.Api, Commission.Api). Identity /
BFF / provision / multitenancy **sonraki dilim** (Obsidian `DropShop/Yapılacaklar.md`).

## Karar 0 — Legacy (otherProjects) yalnız referans

- **Decision**: `src/otherProjects/*` (eski PFApplication) bire bir taşınmaz. Domain kavramları
  (MCC, kart kombinasyonu, komisyon) fikir referansı; kod/tip/EF-anemik model kopyalanmaz.
  Her parça anayasa + CLAUDE.md ile sıfırdan.
- **Rationale**: Kullanıcı direktifi. Legacy anemik/katmanlı, anayasa Marten+vertical-slice ile çelişir.

## Kod gerçeği

Tasarım "MerchantManagement.Api / CommissionManagement.Api var" diyor — **repoda YOK**. İkisi
sıfırdan yazılır. İsimler: `Merchant.Api`, `Commission.Api`.

## Karar 1 — Tek servis, iki aggregate

- **Decision**: `Commission.Api` tek BC; içinde `BankCommission` (maliyet) + `MerchantCommission`
  (gelir) iki ayrı aggregate. İki ayrı servis DEĞİL.
- **Rationale**: `merchantRate > bankRate` invariant'ı ikisini aynı tutarlılık sınırına bağlar.
  Ayrı servis = her yazımda cross-servis senkron çağrı (dağıtık invariant, en kötüsü). Ayrım
  kriteri bounded context/dil sınırı — bunlar bağımsız değil. Tek serviste invariant in-process.
- **Alternatives**: (a) İki ayrı servis — reddedildi (dağıtık invariant). (b) BankCommission'ı
  Payment BC'de otoriter bırakıp Commission'a event/projeksiyonla akıtmak — bu dilim için YAGNI;
  PosAccount uzlaştırma Obsidian todo.

## Karar 2 — İki aggregate, iki farklı yapı

- **Decision**: Aynı tip değiller.
  - `BankCommission`: `BankCode × Criteria(marka,tip,bölge,taksit) → Rate`. Gateway'in bankaya
    ödediği. **Global** (tenant yok).
  - `MerchantCommission`: `MerchantId + BankCommissionId + Criteria(snapshot) → Rate`. Merchant'ın
    gateway'e ödediği. Belirli bir `BankCommission`'a bağlanır (invariant onun oranına karşı).
- **Rationale**: "İlgili banka oranından yüksek" (spec FR-008) → merchant kaydı hangi banka oranını
  aştığını bilmeli. `BankCommissionId` link'i invariant'ı tek/net referansa oturtur; belirsiz
  "kombinasyonun banka oranı" (çoklu banka olabilir) sorununu çözer.
- **Alternatives**: MerchantCommission'ı banka-bağımsız tutup invariant'ı "en yüksek/seçilen banka
  oranı"na karşı çalıştırmak — belirsiz + routing'e bağımlı; reddedildi. Bank-başına merchant oranı
  granülaritesi kabul edildi (admin UI kombinasyon matrisi zaten bunu doldurur).

## Karar 3 — Taksit ekseni her iki aggregate'te

- **Decision**: `Criteria = CardBrand × CardType × TransactionRegion × InstallmentCount`. Hem
  `BankCommission` hem `MerchantCommission` taksit taşır. Invariant taksit-taksit eşleşir:
  `merchantRate(taksit=n) > bankRate(taksit=n)`.
- **Rationale**: Banka komisyonu esasen taksite göre değişir (peşin ≪ 12 taksit). Taksitsiz
  invariant belirsiz. `Payment.Api/PosAccount.CommissionRates` de taksit başına oran tutuyor →
  tutarlı.
- **Alternatives**: Taksitsiz kombinasyon — reddedildi (invariant anlamsızlaşır).

## Karar 4 — BankCommission ↔ PosAccount çift kaynak (ertelendi)

- **Decision**: Bu dilimde `BankCommission` (Commission.Api) ve `PosAccount.CommissionRates`
  (Payment.Api) **ayrı** bırakılır; Commission.Api invariant'ı kendi `BankCommission`'ıyla
  in-process zorlar. Tek-otoriter-kaynak uzlaştırması **sonraki dilim** (Obsidian todo yazıldı).
- **Rationale**: Şimdi birleştirmek cross-BC bağ/altyapı getirir; dilim küçük. Bilinçli çift kaynak.
- **Alternatives**: Şimdi uzlaştırmak — kapsam şişer, reddedildi.

## Karar 5 — Multitenancy ertelendi, düz MerchantId filtresi

- **Decision**: Marten conjoined multitenancy **ertelendi**. `MerchantCommission` sorguları düz
  `Where(c => c.MerchantId == merchantId)` ile filtrelenir. `MultiTenanted` işareti + `ForTenant`
  sonraki dilim (Obsidian todo).
- **Rationale**: Kullanıcı direktifi. SC-004 (sızıntı yok) bu dilimde açık filtreyle karşılanır;
  Marten enforcement sonra sertleştirir.
- **Alternatives**: Şimdi conjoined — ertelendi.

## Karar 6 — Uçlar korumasız (yetki ertelendi)

- **Decision**: Merchant.Api + Commission.Api uçları bu dilimde **korumasız**. Scope enforcement
  (`merchants.manage`/`commissions.manage`) Identity dilimiyle gelir.
- **Rationale**: CLAUDE.md "Yetkilendirme yok (Identity BC ile gelecek); endpoint'ler şimdilik
  korumasız" ile birebir. Anayasa V TODO(AUTHZ_MODEL) hâlâ açık.
- **Alternatives**: Şimdi scope enforcement — Identity dilimine bağımlı, reddedildi.

## Karar 7 — Merchant ↔ Commission bağı: cross-call yok, imzalı claim'e güven

- **Decision**: Commission.Api hiçbir zaman Merchant.Api'ye "bu merchant var mı" doğrulama çağrısı
  **atmaz**. Merchant varlığı **provisioning sırasıyla** garanti: akış Merchant.Api (source of
  truth) → Identity provision; Identity `merchant_id` claim'ini ancak merchant yaratıldıktan sonra
  basar. Dolayısıyla:
  - **Merchant-scoped erişim** (self-service, sonraki dilim): imzalı `merchant_id` claim = merchant'ın
    var olduğunun transitif kanıtı. Commission.Api claim'i okur, çağrı yok. (Tenant filtresi de bu
    claim'den — Karar 5 ile aynı mekanizma.)
  - **Admin erişimi** (bu dilimin komisyon aktörü): admin'de `merchant_id` claim YOK (global). Hedef
    `MerchantId` istekte **parametre** gelir; claim'den değil.
  - **Bu dilim**: uçlar korumasız (token yok). `MerchantId` yalnızca `Guid` parametresi olarak alınır;
    doğrulama yapılmaz. Standing ilke: imzalı `merchant_id` claim'ine güven, Merchant.Api'ye asla
    runtime doğrulama çağrısı atma. (Claim üretimi/doğrulaması Identity diliminde.)
- **Rationale**: Anayasa I — BC izolasyonu; senkron cross-call ancak anlık tutarlılık zorunluysa.
  İmzalı claim, çağrısız güven sağlar (kullanıcı önerisi). Admin akışında merchant zaten var olan
  listeden seçildiği için varlık anlık bağ değil.
- **Alternatives**: Her komisyon yazımında senkron doğrulama çağrısı — gereksiz coupling, reddedildi.
- **Not (kenar)**: Claim provision anındaki varlığı kanıtlar; merchant sonradan silinirse claim
  bayat olabilir. Bu dilim kapsamı dışı (silme + revoke akışı ileride).

## Karar 8 — Merchant durum modeli

- **Decision**: `MerchantStatus : Enumeration` → `Active`, `Passive`, `Suspended`. `Merchant.Create`
  → `Active`. `Deactivate()`/`Activate()`/`Suspend()` davranış metotları.
- **Rationale**: Spec Key Entities "durum (aktif/pasif/askıda)". Tip-güvenli enum (anayasa II).
- **Alternatives**: bool IsActive (BaseModel'de var) tek başına — 3 durumu ifade edemez; enum eklenir.

## Karar 9 — Doğrulama kalemleri (Merchant.Create)

- **Decision**: Zorunlu + format: isim (boş değil), e-posta (format), telefon (boş değil), ülke +
  şehir (boş değil), MCC (tam 4 hane, rakam), webhook URL (mutlak `http(s)` URL). Hata →
  `MessageItem` + `CommonResourceConstants` (COMMON_MESSAGE_VALUE_IS_REQUIRED / INVALID_FORMAT).
- **Rationale**: Spec FR-003 + US1-AC3 ("geçersiz e-posta, 4 haneli olmayan MCC" reddi).
- **Alternatives**: Value object'lere bölmek (Email/Mcc VO) — mümkün; bu dilimde inline doğrulama
  yeterli, VO'ya çıkarma refactor adayı (isteğe bağlı).

## Özet karar tablosu

| # | Konu | Karar |
|---|---|---|
| 0 | Legacy otherProjects | Salt referans; sıfırdan yazılır |
| 1 | Servis sayısı | Tek Commission.Api, 2 aggregate |
| 2 | Aggregate yapıları | BankCommission (global) + MerchantCommission (link'li), ayrı tipler |
| 3 | Taksit | Her iki Criteria'ya taksit; invariant taksit-taksit |
| 4 | BankCommission↔PosAccount | Ayrı bırak, uzlaştırma sonraki dilim (todo) |
| 5 | Multitenancy | Ertelendi; düz MerchantId filtresi (todo) |
| 6 | Yetki | Korumasız; scope Identity dilimiyle |
| 7 | Merchant↔Commission | Cross-call yok; imzalı merchant_id claim'e güven, admin'de parametre |
| 8 | Merchant durum | Enumeration Active/Passive/Suspended |
| 9 | Doğrulama | isim/e-posta/telefon/ülke+şehir/MCC(4)/webhook URL |