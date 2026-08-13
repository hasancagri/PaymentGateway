# Phase 0 Research: Commission Cost + Margin

Clarify (Session 2026-08-13) iki scope sorusunu çözdü; kalan kararlar plan-düzeyi teknik detaylar.
Her biri: Decision / Rationale / Alternatives.

## R1 — iyzico maliyet kaynağı (FR-012, clarify'da çözüldü)

- **Decision**: iyzico maliyeti = `TransactionReportItem.IyzicoCommission` + `IyzicoFee`,
  işlem-SONRASI iyzico rapor/payout verisinden; hesaplama isteğine **girdi** olarak geçer.
- **Rationale**: iyzico ödeme-öncesi (`retrieveInstallmentInfo` → `InstallmentDetail.InstallmentPrices`)
  yalnız **kart-sahibi taksit fiyatlarını** verir; merchant'a kesilen komisyon/maliyet orada YOK.
  Gerçek maliyet (`IyzicoCommission`/`IyzicoFee`) + iyzico'nun merchant'a kestiği oran
  (`PaymentTxDetailItem.MerchantCommissionRate`) yalnız işlem-sonrası raporda görünür. Gateway'in
  "taksit→maliyet oranı" tablosu iyzico modelinde yoktur; maliyet formülle türetilmez.
- **Alternatives**: (A) gateway ön-tahmin tablosu — iyzico'ya aykırı, tahmin gerçekten sapar;
  reddedildi. (C) çağıranın maliyeti girdi geçmesi — B'nin özel hâli; B'yi (rapor kaynağı, uyuyan
  tipler hammadde) seçtik, çağıran raporu okuyup girdi geçer.

## R2 — Gateway marjının şekli (FR-013, clarify'da çözüldü)

- **Decision**: Marj = **yüzde oran + sabit ücret** (`MarginRule { RatePercent, FixedFee }`).
  Marj tutarı = `PaidPrice * RatePercent + FixedFee`.
- **Rationale**: iyzico'nun kendi komisyon yapısını aynalar (`MerchantCommissionRate` oran +
  `IyziCommissionFee` sabit). iyzico maliyeti zaten taksite göre raporda değiştiği için gateway
  marjını taksit tier'ıyla tekrar kademelemek çift-tier karmaşası olur.
- **Alternatives**: düz yüzde (sabit ücret esnekliği yok); taksit tier tablosu (gereksiz karmaşa,
  iyzico maliyeti zaten tier'lı) — ikisi de reddedildi.

## R3 — Yuvarlama kuralı (SC-002 deterministik)

- **Decision**: Tüm para değerleri `decimal`; ara ve son değerler **2 ondalık (kuruş)**,
  `Math.Round(x, 2, MidpointRounding.AwayFromZero)`. Marj tutarı hesaplanır ve yuvarlanır, sonra
  iyzico maliyetiyle toplanır.
- **Rationale**: TL kuruş hassasiyeti; AwayFromZero banker's-rounding sürprizini önler, elle
  aritmetikle %100 eşleşir (SC-002). Tek para birimi (TL) → `decimal` yeter, para VO gereksiz.
- **Alternatives**: `MidpointRounding.ToEven` (banker's) — elle beklentiyle çakışabilir; `double`
  — kayan nokta sapması, para için yasak.

## R4 — Marj üst sınırı (FR-004 "tanımlı üst sınır")

- **Decision**: `MarginRule` VO'da domain sabiti caps: `MaxRatePercent = 0.20m` (%20),
  `MaxFixedFee = 100m` TL. Negatif oran/ücret VE cap aşımı reddedilir (`ResultDomain.Error`,
  neden döner). Değer sonradan Options'a taşınabilir (YAGNI: şimdilik sabit).
- **Rationale**: FR-004 somut bir guard ister; gerçekçi tavan, absürt girişleri (%1000, negatif)
  keser. Sabit tutmak MVP'yi basit tutar; iş gerektirince Options POCO'ya çıkar.
- **Alternatives**: cap'siz (FR-004 ihlali); Options'tan okuma (şimdi gereksiz — tek değer,
  config sürükleme).

## R5 — "Politika yok" ve "tutarsız" durumları (FR-008/FR-009)

- **Decision**: Hesaplama, aktif politika yoksa **açık hata** döner (`MessageItem.Code =
  COMMISSION_POLICY_NOT_FOUND` / benzeri resource sabiti) — sessiz 0 YOK. Efektif komisyon >
  PaidPrice ise `ResultDomain.Error` (negatif hakediş üretilmez). Politika Passive ise hesaplama
  isteği "aktif değil" hatası döner.
- **Rationale**: FR-008/FR-009/SC-003/SC-005 fail-loud ister; sessiz sıfır fiyatlandırma hatasını
  gizler.
- **Alternatives**: 0 komisyon dönmek (yasak); exception fırlatmak (Result pattern ihlali, İlke IV).

## R6 — Tek aktif politika enforcement (FR-005)

- **Decision**: `CreateCommissionPolicy` handler'ı, store öncesi Marten ile aynı MerchantId'de
  `Status == Active` politika var mı sorgular; varsa `Error` döner (aggregate cross-aggregate
  göremez, kural handler'da). UpdateMargin mevcut politikayı günceller (yeni yaratmaz).
- **Rationale**: Tekil-aktif kuralı BC-içi sorguyla, İlke I ihlali olmadan uygulanır (kendi DB).
- **Alternatives**: unique index (Marten document'te oran/statü bileşik indeks karmaşık); event
  sourcing projection (aşırı). Handler-query en yalın.

## R7 — MerchantId geçerliliği (cross-BC)

- **Decision**: 024, MerchantId'yi **doğrulamadan** kabul eder (dış referans; Merchant BC kaynağı).
  Cross-BC canlı çağrı/DB erişimi YOK. Var-olmayan merchant'a politika teknik olarak yazılabilir;
  yanlış-kullanım admin-düzlemi sorumluluğu.
- **Rationale**: İlke I — Commission, Merchant DB'sine erişemez. `merchant.lifecycle` tüketip yerel
  merchant-var listesi tutmak ayrı iş (YAGNI); MVP güvenir.
- **Alternatives**: senkron gRPC doğrulama (sanksiyon gerektirir, aşırı); event-beslemeli yerel
  read model (ayrı iş).

## R8 — Hesaplama slice'ı: Command mı Query mi (CQRS)

- **Decision**: `CalculateEffectiveCommission` = **Query** (durum değiştirmez), ama gövdeli girdi
  taşıdığı için `POST .../effective-commission`. `[Transactional]` YOK. Yetki `commission.read` +
  `AdminPlaneOnly` (sistem/admin çağırır; makine token'ı claim'siz → AdminPlaneOnly geçer).
- **Rationale**: Hesaplama saf okuma/aritmetik → Query slice. POST yalnız gövde taşımak için (REST
  GET gövde taşımaz). İlke III: yalnız veri dönen slice Queries altında.
- **Alternatives**: Command (yanlış — state değişmez); GET+query-string (çok alan, iyzico string
  maliyet + PaidPrice + installment; POST daha temiz).

## R9 — iyzico string maliyet alanlarının ayrıştırılması

- **Decision**: Calculate girdisi iyzico maliyetini **string** alır (`IyzicoCommission`,
  `IyzicoFee` — `TransactionReportItem`'daki gibi string), aggregate içinde
  `decimal.TryParse(..., CultureInfo.InvariantCulture)` ile ayrıştırır; eksik/boş/ayrıştırılamaz →
  `Error` (FR-012/edge case). PaidPrice `decimal` alınır.
- **Rationale**: iyzico raporu bu alanları string döndürür; girdiyi kaynağa sadık tutar,
  ayrıştırma domain kuralı (fail-loud). Invariant culture — iyzico nokta-ondalık.
- **Alternatives**: decimal zorlamak (çağıranı erken ayrıştırmaya iter, iyzico string sözleşmesini
  bozar); Türkçe culture parse (iyzico nokta kullanır → yanlış).

## R10 — Test iskeleti (İlke: saf domain birim testi)

- **Decision**: `tests/Commission.Api.Tests` (xUnit) `PaymentGateway.slnx`'e eklenir. Kapsam:
  `MarginRule.Create` (negatif/cap/geçerli), `CommissionPolicy` (Create, UpdateMargin, statü
  makinesi idempotent no-op), `CalculateEffectiveCommission` (aritmetik+yuvarlama SC-002,
  not-active, ayrıştırılamaz maliyet, efektif>PaidPrice reddi SC-005). DB/HTTP yok.
- **Rationale**: Anayasa "Geliştirme Akışı — Test": saf domain birim testi; davranışlı aggregate +
  aritmetik önceliklidir. 023 `Merchant.Api.Tests` deseni.
- **Alternatives**: entegrasyon/handler testi (anayasa dışı, bilinçli erteleme); test yok (024
  aritmetik-ağır → determinizm kanıtı şart).

## Çözülmemiş NEEDS CLARIFICATION

Yok. Spec'teki 2 marker clarify'da kapandı; R1–R10 kalan teknik kararları sabitledi.