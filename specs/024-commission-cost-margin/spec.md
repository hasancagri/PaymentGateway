# Feature Specification: Commission Cost + Margin

**Feature Branch**: `024-commission-cost-margin`

**Created**: 2026-08-13

**Status**: Draft

**Input**: User description: "024 Commission BC gerçek domain kurulumu. Amaç: iyzico işlem maliyetini (TransactionReportItem.IyzicoCommission + IyzicoFee) baz alıp gateway marjını ekleyerek, gateway'in merchant'tan keseceği efektif komisyonu modelleyen bir Commission aggregate + vertical slice'lar."

## Overview

Gateway (iyzico ödeme kanalı) merchant'lardan komisyon keser. Komisyonun iki katmanı var:

1. **iyzico maliyeti** — iyzico'nun gateway'e kestiği ücret (işlem başına `IyzicoCommission` +
   `IyzicoFee`; taksit / kart tipine göre değişir). Gateway bunu kontrol etmez; iyzico rapor/payout
   verisinden gelir.
2. **Gateway marjı** — gateway'in bu maliyetin üstüne koyduğu kâr payı (yönetici tanımlar).

**Efektif komisyon** = iyzico maliyeti + gateway marjı. Bu, gateway'in bir işlemden merchant'a
yansıttığı toplam kesintidir. Merchant net hakediş = ödenen tutar − efektif komisyon.

Bu iş, Commission BC'nin gerçek domain'ini kurar: gateway marjını merchant başına tanımlayan bir
**CommissionPolicy** aggregate'i ve verili bir işlem bağlamı için efektif komisyonu hesaplayan
slice'lar.

## Clarifications

### Session 2026-08-13

- Q: Efektif komisyon hesaplanırken iyzico işlem maliyeti (`IyzicoCommission` + `IyzicoFee`)
  sisteme nereden gelir? → A: İşlem-SONRASI GERÇEK — iyzico rapor/payout verisinden
  (`TransactionReportItem.IyzicoCommission` + `IyzicoFee`). iyzico modeline sadık: maliyet
  taksitten formülle türetilmez; iyzico kendi hesaplar ve yalnız işlem-sonrası raporda döndürür
  (ödeme-öncesi `retrieveInstallmentInfo` yalnız kart-sahibi taksit fiyatlarını verir, merchant
  maliyetini vermez). Rapor verisi hesaplama isteğine girdi olarak gelir; canlı iyzico rapor/payout
  çağrısı ayrı iş (uyuyan `TransactionReports`/`Payouts` tipleri hammadde).
- Q: Gateway marjı (kâr eklentisi) nasıl modellenir? → A: YÜZDE + SABİT ÜCRET — iyzico'nun kendi
  komisyon yapısını aynalar (`MerchantCommissionRate` oran + `IyziCommissionFee` sabit). Marj =
  PaidPrice üstüne oran (%) + işlem başına sabit TL ücret. Taksit tier tablosu YOK — iyzico maliyeti
  zaten taksite göre raporda değiştiği için marj tek (oran+ücret) kalır.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Yönetici merchant için gateway marj politikası tanımlar (Priority: P1)

Gateway yöneticisi, bir merchant için iyzico maliyetinin üstüne uygulanacak marjı (kâr payı)
tanımlar. Bu politika olmadan gateway efektif komisyon hesaplayamaz; bu yüzden ilk ve en kritik
yetenek budur.

**Why this priority**: Marj politikası olmadan hiçbir efektif komisyon hesaplanamaz — tüm
akışın önkoşulu. Tek başına merchant fiyatlandırmasını kurar; MVP budur.

**Independent Test**: Bir merchant için marj politikası oluştur/güncelle, kaydın doğru marj ve
statüyle döndüğünü doğrula. iyzico veya ödeme akışı gerektirmez.

**Acceptance Scenarios**:

1. **Given** merchant var ve politikası yok, **When** yönetici geçerli marjla politika oluşturur,
   **Then** politika `Active` statüde kaydedilir ve marj değerleriyle döner.
2. **Given** merchant'ın aktif politikası var, **When** yönetici marjı günceller, **Then** yeni
   marj yürürlüğe girer ve sonraki hesaplamalarda kullanılır.
3. **Given** yönetici geçersiz marj girer (negatif oran veya tanımlı üst sınırı aşan), **When**
   oluşturma denenir, **Then** işlem reddedilir ve neden döner (kayıt oluşmaz).
4. **Given** merchant'ın aktif politikası var, **When** yönetici politikayı pasife alır, **Then**
   statü `Passive` olur ve hesaplama isteyen slice politikayı yok sayar.

---

### User Story 2 - Sistem bir işlem için efektif komisyonu hesaplar (Priority: P2)

Verili bir işlem bağlamı (merchant, ödenen tutar, iyzico maliyeti, taksit/kart tipi) için sistem
efektif komisyonu (iyzico maliyeti + gateway marjı) ve merchant net hakedişini hesaplar.

**Why this priority**: Politikanın iş değeri hesaplamada ortaya çıkar. P1'e bağımlı (politika
olmalı), ama efektif komisyon dökümü asıl çıktı.

**Independent Test**: Bilinen marjlı bir politika + bilinen iyzico maliyeti gir; efektif komisyon
ve net hakedişin beklenen değere eşit olduğunu doğrula.

**Acceptance Scenarios**:

1. **Given** merchant'ın aktif marj politikası ve bir işlemin iyzico maliyeti var, **When** efektif
   komisyon istenir, **Then** sonuç = iyzico maliyeti + hesaplanan marj, ayrıca net hakediş = ödenen
   tutar − efektif komisyon döner.
2. **Given** merchant'ın aktif politikası YOK, **When** efektif komisyon istenir, **Then** hesaplama
   yapılmaz ve "politika yok" durumu döner (sessiz sıfır değil).
3. **Given** hesaplanan efektif komisyon ödenen tutarı aşar, **When** hesaplama yapılır, **Then**
   tutarsız sonuç işaretlenir/reddedilir (negatif hakediş dönmez).

---

### User Story 3 - Merchant kendi efektif komisyon oranını görüntüler (Priority: P3)

Merchant, kendisine uygulanan gateway marj politikasını / efektif oranını görür (yalnız kendi
kaydı).

**Why this priority**: Şeffaflık; çekirdek fiyatlandırma/hesaplamadan sonra gelir.

**Independent Test**: Merchant-scoped token ile kendi politikasını GET et; başka merchant'ın
politikasına erişimin reddedildiğini doğrula.

**Acceptance Scenarios**:

1. **Given** merchant'ın aktif politikası var, **When** kendi politikasını sorgular, **Then** marj
   ve efektif oran bilgisi döner.
2. **Given** merchant başka merchant'ın politikasını ister, **When** sorgu yapılır, **Then** erişim
   reddedilir (fail-closed).

---

### Edge Cases

- Merchant için birden çok politika oluşturma denemesi (tekil aktif politika kuralı).
- iyzico maliyet alanı (`IyzicoCommission`/`IyzicoFee`) boş, sıfır veya ayrıştırılamaz string.
- Taksit sayısı marj tablosunda tanımlı değil (kapsanmayan tier).
- Para birimi TRY dışı (çoklu para desteği kapsamı).
- Marj güncellemesi geçmiş hesaplanmış işlemleri etkilemez (yalnız ileriye dönük).
- Pasif/olmayan politikada hesaplama isteği.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sistem, bir merchant için gateway marjını tanımlayan bir komisyon politikası
  oluşturmayı MUST desteklesin (yalnız yönetici düzlemi).
- **FR-002**: Sistem, mevcut marj politikasının marj değerlerini güncellemeyi MUST desteklesin.
- **FR-003**: Sistem, marj politikasını aktif/pasif yapmayı MUST desteklesin; pasif politika
  hesaplamada yok sayılır.
- **FR-004**: Sistem, marjı MUST doğrulasın (negatif oran reddi; tanımlı üst sınır kontrolü);
  geçersiz girişte kayıt oluşmaz ve neden döner.
- **FR-005**: Sistem, her merchant için EN FAZLA bir aktif marj politikası MUST tutsun.
- **FR-006**: Sistem, verili işlem bağlamı için efektif komisyonu MUST hesaplasın:
  efektif komisyon = iyzico maliyeti (`IyzicoCommission` + `IyzicoFee`) + gateway marjı.
- **FR-007**: Sistem, efektif komisyon yanında merchant net hakedişini (ödenen tutar − efektif
  komisyon) MUST döndürsün.
- **FR-008**: Aktif politika yoksa sistem hesaplama yapmamalı ve açık bir "politika yok" durumu
  MUST döndürsün (sessiz sıfır değil).
- **FR-009**: Efektif komisyon ödenen tutarı aşarsa sistem tutarsızlığı MUST işaretlesin (negatif
  hakediş üretmez).
- **FR-010**: Merchant, kendi marj politikasını / efektif oranını MUST görüntüleyebilsin; başka
  merchant'ın politikasına erişim fail-closed reddedilir.
- **FR-011**: Yazma işlemleri (oluştur/güncelle/statü) `commission.write` + yönetici düzlemi
  (claim'li merchant token'ı giremez) ile korunur; okuma `commission.read` ile.
- **FR-012**: iyzico maliyeti işlem-SONRASI GERÇEK kaynaktan gelir: iyzico rapor/payout verisinin
  `IyzicoCommission` + `IyzicoFee` alanları (`TransactionReportItem`). Sistem maliyeti taksit
  sayısından türetmez; hesaplama isteğine bu rapor değerleri girdi olarak geçer. Canlı iyzico
  rapor/payout çekimi bu iterasyonun DIŞINDA (uyuyan `TransactionReports`/`Payouts` tipleri
  hammadde). Maliyet alanı eksik/sıfır/ayrıştırılamaz string ise hesaplama reddedilir (FR-008 ile
  tutarlı; sessiz 0 sayılmaz).
- **FR-013**: Gateway marjı YÜZDE + SABİT ÜCRET kombinasyonu olarak modellenir: PaidPrice üstüne
  bir oran (%) + işlem başına sabit ücret. Bu, iyzico'nun kendi komisyon yapısını (`MerchantCommissionRate`
  oran + `IyziCommissionFee` sabit) aynalar. Taksit tier'ına göre kademeli marj tablosu YOK — iyzico
  maliyeti zaten taksite göre raporda değiştiği için gateway marjı tek (oran + sabit ücret) kalır.

### Key Entities *(include if feature involves data)*

- **CommissionPolicy** (aggregate): bir merchant için gateway marj tanımı. Alanlar: MerchantId,
  marj kuralı/kuralları, statü (Active/Passive), oluşturma/güncelleme zamanı. Davranış:
  oluştur, marj güncelle, aktif/pasif yap, efektif komisyon hesapla.
- **MarginRule** (value object): gateway marjı — bir oran (%) + işlem başına sabit ücret (FR-013).
  Taksit tier eşlemesi YOK. Negatif oran/ücret ve tanımlı üst sınır aşımı reddedilir (FR-004).
- **EffectiveCommission** (hesap sonucu / read model): bir işlem için iyzico maliyeti
  (`IyzicoCommission` + `IyzicoFee`), gateway marjı (oran-tutarı + sabit ücret), toplam efektif
  komisyon ve net hakediş (PaidPrice − efektif komisyon) dökümü.
- **TransactionCost** (dış hammadde / hesaplama girdisi): iyzico işlem-sonrası rapor/payout
  verisinden gelen fiili maliyet (`IyzicoCommission`, `IyzicoFee`) + işlem bağlamı (PaidPrice,
  Installment) — 022'de uyuyan `TransactionReports`/`Payouts` tipleri. Hesaplama isteğine girdi
  olarak geçer; bu iterasyonda canlı çekilmez.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Yönetici bir merchant için marj politikasını tek işlemde oluşturabilir ve kayıt
  anında doğrulanmış marjla döner.
- **SC-002**: Bilinen marj + bilinen iyzico maliyeti için hesaplanan efektif komisyon, elle
  yapılan aritmetiğe %100 eşittir (yuvarlama kuralı dahil deterministik).
- **SC-003**: Aktif politikası olmayan merchant için hesaplama isteği %100 açık "politika yok"
  durumu döner; hiçbir durumda sessiz 0 komisyon dönmez.
- **SC-004**: Merchant yalnız kendi politikasını görebilir; çapraz merchant erişim denemeleri
  %100 reddedilir.
- **SC-005**: Efektif komisyonun ödenen tutarı aştığı hiçbir senaryoda negatif net hakediş
  üretilmez (işaretlenir/reddedilir).

## Assumptions

- Merchant kayıtları 023'te kurulan Merchant BC'de yaşar; bu iş merchant'ı yeniden tanımlamaz,
  yalnız MerchantId'ye politika bağlar.
- Para birimi varsayılan TRY; çoklu para bu iterasyonun dışında (edge case olarak işaretlenir,
  ayrı iş).
- Marj güncellemesi ileriye dönüktür; geçmiş hesaplamalar/işlemler yeniden fiyatlanmaz.
- Efektif komisyon hesabı saf domain aritmetiğidir; hesaplama sırasında iyzico'ya CANLI çağrı
  yapılmaz (maliyet ya girdi ya da önceden çekilmiş rapor verisi — FR-012 ile netleşir).
- iyzico maliyet string alanları ondalık tutar olarak ayrıştırılır; ayrıştırılamayan/eksik değer
  hesaplamayı reddeder (sessiz 0 sayılmaz).
- `Provider/` (iyzico istemci çekirdeği) ve `SubMerchant` malzemesi bu işte uyumaya devam eder;
  iyzico'ya gerçek payout/rapor çağrısı ayrı iş.
- Auth altyapısı 011/012'den devralınır (OpenIddict scope + AdminPlaneOnly/MerchantScoped).

## Dependencies

- 023 Merchant BC (MerchantId kaynağı, `merchant.lifecycle` olayları).
- 022'de Commission.Api'de uyuyan `Domains/{Payouts,TransactionReports}` iyzico maliyet tipleri.
- Identity.Server scope'ları: `commission.read`, `commission.write` (011 seed'inde mevcut).