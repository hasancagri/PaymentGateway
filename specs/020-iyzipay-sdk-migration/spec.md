# Feature Specification: Iyzipay SDK Migration

**Feature Branch**: `020-iyzipay-sdk-migration`

**Created**: 2026-08-13

**Status**: Draft

**Input**: User description: "otherProjects altındaki Iyzipay SDK projelerini (Iyzipay, Iyzipay.Tests, Iyzipay.Samples) PaymentGateway .NET çözümüne taşı. CP.VPOS emsali: kütüphane olduğu gibi korunur ama çözümün parçası olur ve derlenir. Eski hedefler (net45/netstandard2.1) çözümün modern .NET sürümüne taşınacak. Ödeme akışı entegrasyonu (PosAccount/BankRouter/satış) bu kapsamda DEĞİL — yalnız projelerin çözüme sağlıklı taşınması, derlenmesi ve testlerin koşması."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Iyzipay kütüphanesi çözümün derlenen parçası olur (Priority: P1)

Geliştirici PaymentGateway çözümünü açtığında Iyzipay SDK'sını çözümün içinde görür;
`dotnet build` tek komutla Iyzipay dahil tüm çözümü sıfır hatayla derler. Kütüphane
bugün gitignore'lu `src/otherProjects/` altında olduğundan sürüm kontrolünde YOKTUR;
taşıma sonrası kaynak kodu versiyonlu ağaçta yaşar ve git geçmişine girer (CP.VPOS
emsali: `src/services/CP.VPOS`).

**Why this priority**: Sürüm kontrolünde olmayan ve derlenmeyen kod, ileride
planlanan Iyzico ödeme kanalı entegrasyonunun üzerine inşa edilemez. Önce kodun
güvenli, derlenir ve izlenir hâle gelmesi gerekir.

**Independent Test**: Temiz klonda `dotnet build` çalıştırılır; Iyzipay projesi
çözümün parçası olarak sıfır hatayla derlenir ve tüm kaynak dosyaları git'te izlenir.

**Acceptance Scenarios**:

1. **Given** taşıma tamamlanmış bir çalışma kopyası, **When** `dotnet build`
   çalıştırılır, **Then** Iyzipay kütüphanesi dahil tüm çözüm sıfır hatayla derlenir.
2. **Given** taşıma tamamlanmış bir çalışma kopyası, **When** `git status` bakılır,
   **Then** Iyzipay kaynak dosyaları izlenen (tracked) dosyalardır; gitignore
   tarafından dışlanmaz.
3. **Given** çözüm dosyası, **When** proje listesine bakılır, **Then** eski
   `net45`/`netstandard2.1` hedefleri kalmamıştır; Iyzipay projeleri çözümün modern
   .NET sürümünü hedefler.

---

### User Story 2 - Deterministik testler koşar (Priority: P2)

Geliştirici `dotnet test` ile Iyzipay test projesini çalıştırır; dış servise (canlı
iyzico API'si) ve gizli anahtara ihtiyaç duymayan deterministik testler geçer. Canlı
sandbox hesabı ve API anahtarı gerektiren fonksiyonel testler derlenmeye devam eder
ama varsayılan test koşusuna dahil edilmez (anahtar yokken kırmızı koşu üretmez).

**Why this priority**: Test projesi kütüphanenin taşıma sonrası davranışsal olarak
sağlam kaldığının kanıtıdır; ama canlı-API testleri anahtar olmadan koşamayacağı
için varsayılan koşu deterministik olmalıdır.

**Independent Test**: Hiçbir iyzico kimlik bilgisi tanımlı olmadan `dotnet test`
çalıştırılır; koşu yeşildir (deterministik testler geçer, canlı-API testleri koşuya
girmez).

**Acceptance Scenarios**:

1. **Given** iyzico kimlik bilgisi tanımlı olmayan bir ortam, **When** Iyzipay test
   projesi `dotnet test` ile koşulur, **Then** koşu başarıyla tamamlanır ve hiçbir
   test dış servis yokluğundan dolayı başarısız olmaz.
2. **Given** test projesi, **When** derlenir, **Then** canlı-API (fonksiyonel)
   testler dahil tüm test kaynakları hatasız derlenir.

---

### User Story 3 - Örnek kullanım kodu derlenir referans olarak korunur (Priority: P3)

Geliştirici, ileride yapılacak ödeme kanalı entegrasyonunda başvurmak üzere Iyzipay
örnek kullanım kodlarını (Samples) çözüm içinde derlenir hâlde bulur. Örnekler canlı
API'ye istek attığından hiçbir otomatik koşuya dahil edilmez; yalnız derlenmeleri
güvence altındadır.

**Why this priority**: Örnekler SDK'nın kullanım kılavuzudur; derlenir tutulmaları
API yüzeyi değişirse anında fark edilmesini sağlar. Ancak çalışmaları değil yalnız
derlenmeleri gerekir.

**Independent Test**: `dotnet build` sonrası Samples projesi hatasız derlenmiştir;
`dotnet test` varsayılan koşusu Samples'daki canlı-API senaryolarını çalıştırmaz.

**Acceptance Scenarios**:

1. **Given** çözüm, **When** `dotnet build` çalıştırılır, **Then** Samples projesi
   sıfır hatayla derlenir.
2. **Given** kimlik bilgisi tanımlı olmayan bir ortam, **When** çözüm genelinde test
   koşulur, **Then** Samples'daki canlı-API senaryoları koşuya girmez ve koşuyu
   kırmızıya düşürmez.

---

### Edge Cases

- Eski `net45` hedefine özgü kalıntılar (Mono framework-path geçici çözümleri,
  `System.Configuration`/`App.config` bağımlılığı) modern hedefte anlamsızdır; taşıma
  bunları temizler, geride koşullu eski-hedef bloğu kalmaz.
- README'nin belgelediği `net45` vs `netstandard2.1` ondalık serileştirme farkı
  (sondaki sıfırların kırpılması) tek modern hedefe geçince tek davranışa iner;
  buna bağlı test beklentileri modern davranışa göre doğrulanır.
- `bin/`, `obj/` gibi derleme çıktıları taşıma sırasında versiyonlu ağaca sızmamalıdır.
- Taşıma sonrası `src/otherProjects/` altında Iyzipay kopyası KALMAZ; aynı kodun iki
  kopyası (biri izlenen, biri gitignore'lu) bulunamaz.
- Mevcut projeler (Payment.Api dahil) Iyzipay'e referans VERMEZ; taşıma mevcut
  derleme ve test sonuçlarını değiştirmez.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Iyzipay kütüphane projesi gitignore'lu `src/otherProjects/` alanından
  çıkarılıp sürüm kontrolündeki kaynak ağacına taşınmalı ve çözüm dosyasına
  (PaymentGateway.slnx) eklenmelidir.
- **FR-002**: Iyzipay test projesi ve örnek (Samples) projesi de aynı şekilde
  taşınmalı ve çözüme eklenmelidir; üç projenin tamamı `dotnet build` ile sıfır
  hatayla derlenmelidir.
- **FR-003**: Üç projenin hedef çatısı, çözümün geri kalanının kullandığı modern
  .NET sürümüyle hizalanmalıdır; eski `net45` ve `netstandard2.1` hedefleri ve
  bunlara özgü koşullu yapı kalıntıları (Mono yolu geçici çözümleri, eski çerçeve
  referans blokları) kaldırılmalıdır.
- **FR-004**: Kütüphanenin kaynak kodu davranışsal olarak OLDUĞU GİBİ korunmalıdır
  (CP.VPOS emsali): değişiklikler proje/derleme altyapısı ve modern hedefin derleme
  gereksinimleriyle sınırlıdır; API yüzeyi ve iş mantığı yeniden yazılmaz.
- **FR-005**: Dış servis ve kimlik bilgisi gerektirmeyen deterministik testler
  `dotnet test` ile koşup geçmelidir; canlı iyzico API'si ve API anahtarı gerektiren
  fonksiyonel testler ile Samples senaryoları derlenmeli ama varsayılan test
  koşusunun dışında tutulmalıdır.
- **FR-006**: Taşıma tamamlandığında `src/otherProjects/` altında Iyzipay'e ait
  kopya kalmamalıdır (çift kopya yasak).
- **FR-007**: Taşıma mevcut projelerin hiçbirine Iyzipay referansı EKLEMEMELİDİR;
  ödeme akışı entegrasyonu (PosAccount/BankRouter/satış/3D) kapsam dışıdır.
- **FR-008**: Derleme çıktıları (`bin/`, `obj/`) ve geliştiriciye özel dosyalar
  sürüm kontrolüne girmemelidir.

### Key Entities

- **Iyzipay kütüphanesi**: iyzico ödeme servisinin resmî .NET istemcisi; model,
  istek/yanıt tipleri ve HTTP iletişim katmanı. Taşınan ana varlık.
- **Iyzipay test projesi**: kütüphanenin birim (deterministik) ve fonksiyonel
  (canlı sandbox) testleri.
- **Iyzipay örnek projesi**: SDK kullanım örnekleri; derlenir referans, koşmaz.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Temiz klonda tek `dotnet build` komutu, Iyzipay'in üç projesi dahil
  tüm çözümü sıfır hatayla derler.
- **SC-002**: Hiçbir iyzico kimlik bilgisi tanımlı olmadan test koşusu yeşil biter;
  dış servis yokluğu tek bir testi bile kırmızıya düşürmez.
- **SC-003**: Iyzipay kaynak dosyalarının %100'ü git tarafından izlenir; gitignore
  dışlaması kalmaz.
- **SC-004**: Mevcut projelerin derleme ve test sonuçları taşımadan etkilenmez:
  taşıma öncesi geçen tüm testler taşıma sonrası da geçer.

## Assumptions

- Hedef konum CP.VPOS emsalini izler: kütüphane `src/services/` altına taşınır,
  test projesi `tests/` altına taşınır; Samples kütüphanenin yanında durur. Kesin
  yerleşim planlama aşamasında netleşir.
- CP.VPOS gibi Iyzipay de Central Package Management dışında tutulabilir (harici
  kütüphane, kendi sürümlerini korur); CPM'e dahil edilip edilmeyeceği planlama
  aşamasının kararıdır — spec düzeyinde şart yalnız çözümün derlenmesidir.
- "Çözümün modern .NET sürümü" mevcut servislerin hedeflediği sürümdür (bugün
  net10.0); ayrı bir sürüm politikası tanımlanmaz.
- Fonksiyonel testler ve Samples canlı iyzico sandbox'ına HTTP isteği atar; API
  anahtarı bu depoda tutulmaz. Bu senaryoların elle, anahtar sağlanarak koşulması
  bilinçli olarak kapsam dışıdır.
- Eski `net45` çatısına özgü README talimatları (Newtonsoft sürüm ayrımı, .NET
  Framework kurulumu) taşınan projeler için geçerliliğini yitirir; README'nin
  güncellenmesi kozmetiktir ve zorunlu değildir.
- İleride Iyzico ödeme kanalı entegrasyonu (PosAccount-adayı modeli, BankRouter
  katılımı) ayrı bir spec döngüsüdür; bu taşıma onun ön koşulunu hazırlar.