# Feature Specification: Reference.Api Removal

**Feature Branch**: `021-reference-api-removal`

**Created**: 2026-08-13

**Status**: Draft

**Input**: User description: "Reference.Api projesini ve ona bağlı tüm kalıntıları sistemden sök. Kapsam: src/services/Reference.Api projesi + tests/Reference.Api.Tests silinir, çözümden (slnx) ve Aspire AppHost orkestrasyonundan çıkarılır, Reference BC'yi besleyen/tüketen integration event bağları temizlenir, SharedKernel'de yalnız Reference için var olan tipler kaldırılır (başka BC kullanıyorsa kalır). Sistemin geri kalanı davranış kaybetmeden derlenmeye ve çalışmaya devam eder. Country/City/MCC verisi tamamen silinir — hiçbir yere taşınmaz. Gerekçe: iyzico pivotu (022-024) sonrası banka/BIN/referans-veri eksenleri kalkıyor; Reference BC'nin varlık sebebi ortadan kalktı."

**Kapsam güncellemesi (kullanıcı kararı, 2026-08-13)**: Keşifte Reference'ın Bank kataloğunun
Merchant (settlement banka doğrulaması/ad türetme) ve Commission (banka oluşturma + katalog
listesi + teklif akışı) tarafından aktif kullanıldığı görüldü. Kullanıcı kararı: **read-model'ler
ve kataloğa bağlı doğrulama/zenginleştirme davranışları da sökülür** — settlement banka kodu
katalogsuz (serbest) kalır, banka oluşturma katalog doğrulamasız çalışır. Bu bilinçli bir davranış
değişikliğidir; 023 settlement'ı iyzico'ya devredecek, 024 banka eksenini tamamen kaldıracak.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Reference.Api sistemden çıkar (Priority: P1)

Geliştirici çözümü derlediğinde ve sistemi orkestratörle ayağa kaldırdığında Reference
servisi artık yoktur: proje, testleri, veritabanı tanımı ve orkestrasyon kaydı silinmiştir.
Kalan tüm servisler derlenir ve başlar.

**Why this priority**: Sökümün özü; servis ortadan kalkmadan bağımlılık temizliği doğrulanamaz.

**Independent Test**: `dotnet build` sıfır hata; orkestratör başlatıldığında Reference
servisi ve veritabanı listede yoktur, kalan servisler sağlıklı başlar.

**Acceptance Scenarios**:

1. **Given** söküm tamamlanmış çalışma kopyası, **When** `dotnet build` çalıştırılır,
   **Then** çözüm sıfır hatayla derlenir ve çözümde Reference projesi yoktur.
2. **Given** söküm tamamlanmış çalışma kopyası, **When** sistem orkestratörle başlatılır,
   **Then** Reference servisi ve referans veritabanı orkestrasyonda yer almaz; kalan
   servisler (Payment, Merchant, Commission, Admin, Identity, agent'lar, Mail) başlar.
3. **Given** kaynak ağacı, **When** Reference'a ait dosya aranır, **Then** proje, test
   ve tohum (seed) verisi dahil hiçbir kalıntı bulunmaz.

---

### User Story 2 - Katalog-bağımlı akışlar katalogsuz çalışır (Priority: P2)

Admin kullanıcısı settlement hesabı ve banka tanımı işlemlerine devam edebilir: banka
kodu/adı artık merkezi katalogdan doğrulanmaz/türetilmez, kullanıcı girdisi olarak alınır.
Merchant sorguları (tekil/anahtarla/agent) katalog zenginleştirmesi olmadan tutarlı yanıt
döner. Hiçbir akış çalışamaz duruma düşmez.

**Why this priority**: Sökümün yan etkisi kontrol altına alınmalı — bağımlı akışlar
kırılmadan, bilinçli sadeleşmiş davranışla yaşamalı.

**Independent Test**: Katalog verisi hiç yokken settlement hesabı oluşturulabilir,
banka tanımlanabilir, merchant sorguları hatasız döner; Admin ekranları bu akışları
yürütebilir.

**Acceptance Scenarios**:

1. **Given** katalog verisi olmayan taze sistem, **When** admin yeni settlement hesabı
   oluşturur, **Then** işlem banka kataloğu doğrulaması olmadan başarıyla tamamlanır
   (IBAN doğrulaması gibi katalogdan bağımsız kurallar aynen korunur).
2. **Given** katalog verisi olmayan taze sistem, **When** admin banka tanımlar,
   **Then** banka kod+ad kullanıcı girdisiyle oluşur; merkezi katalog kontrolü yapılmaz.
3. **Given** mevcut merchant kayıtları, **When** merchant tekil/anahtarla/agent
   üzerinden sorgulanır, **Then** yanıtlar katalog zenginleştirmesi olmadan tutarlı döner
   ve hata oluşmaz.
4. **Given** Admin arayüzü, **When** settlement ve banka ekranları kullanılır,
   **Then** katalog listesine bağımlı öğeler serbest girişe dönmüştür ve akış tamamlanır.

---

### User Story 3 - Event sözleşmesi ve yerel kopyalar temizlenir (Priority: P3)

Sistemde referans-veri yayın/tüketim sözleşmesi ve yerel katalog kopyaları kalmaz:
paylaşılan event sözleşmesi, Merchant'taki dört yerel kopya (ülke/şehir/MCC/banka) ve
Commission'daki banka kopyası, tüketici handler'larıyla birlikte silinir.

**Why this priority**: Ölü sözleşme ve tüketici bırakmak gelecek geliştirmeleri yanıltır;
temizlik sökümün kalıcılığını garanti eder.

**Independent Test**: Kaynak taramasında referans-veri event'i, yerel kopya tipleri ve
handler'ları hiçbir projede bulunmaz; mesajlaşma altyapısında bu event'e bağlı tüketici
kaydı kalmaz.

**Acceptance Scenarios**:

1. **Given** söküm tamamlanmış kaynak ağacı, **When** referans-veri event sözleşmesi
   aranır, **Then** paylaşılan sözleşmelerde ve hiçbir BC'de izi yoktur.
2. **Given** çalışan sistem, **When** mesajlaşma altyapısı incelenir, **Then** referans
   verisine bağlı yayıncı/tüketici/kuyruk kaydı bulunmaz.

---

### Edge Cases

- Taze kurulum (boş veritabanları): katalog hiç var olmadığından tüm akışlar ilk günden
  katalogsuz davranışla çalışmalı; "katalog boş" özel durumu diye bir şey kalmamalı.
- Kart taksonomisi (kart tipi/markası) paylaşılan çekirdekte kalır — Payment ve Commission
  aktif kullanıyor; Reference'a özgü olmadığı için silinmez.
- Settlement hesabındaki mevcut kayıtlar banka adını katalogdan türetmiş olabilir; sökümden
  sonra bu kayıtlar okunabilir kalmalı (dev ortamında veritabanı sıfırlama kabul görür,
  geriye-uyum migration üretilmez).
- Mesajlaşma altyapısında (broker) eski referans-veri kuyruğu/exchange kalıntısı dev
  ortamı sıfırlamasıyla temizlenir; koddan kayıt kalkması yeterlidir.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Reference servisi projesi ve test projesi kaynak ağacından silinmeli;
  çözüm dosyasından ve orkestrasyon tanımından (servis kaydı + özel veritabanı) çıkarılmalıdır.
- **FR-002**: Referans-veri yayın sözleşmesi (güncelleme event'i ve öğe tipi) paylaşılan
  sözleşmelerden kaldırılmalı; sistemde bu sözleşmenin yayıncısı ve tüketicisi kalmamalıdır.
- **FR-003**: Merchant'taki yerel katalog kopyaları (ülke, şehir, MCC, banka) ve bunları
  besleyen tüketici, kayıt/konfigürasyonlarıyla birlikte silinmelidir.
- **FR-004**: Commission'daki yerel banka kopyası ve tüketicisi, kayıt/konfigürasyonlarıyla
  birlikte silinmelidir.
- **FR-005**: Kataloğa bağlı doğrulama ve zenginleştirme davranışları kaldırılmalıdır:
  settlement hesabında banka bilgisi katalog doğrulaması olmadan alınır; banka tanımlama
  merkezi katalog kontrolü olmadan kullanıcı girdisiyle çalışır; merchant ve settlement
  sorguları katalog verisi olmadan tutarlı yanıt döner. Katalogdan bağımsız kurallar
  (ör. IBAN doğrulaması) aynen korunur.
- **FR-006**: Admin arayüzü etkilenen akışlarda çalışmaya devam etmelidir; katalog
  listesine dayanan öğeler serbest girişe dönüştürülür.
- **FR-007**: Paylaşılan çekirdekteki kart taksonomisi tipleri KORUNMALIDIR (Payment ve
  Commission kullanıyor; Reference'a özgü değil).
- **FR-008**: Söküm sonrası çözüm sıfır hatayla derlenmeli; kalan tüm test projeleri
  yeşil kalmalıdır (Reference test projesi silindiği için toplam test sayısının düşmesi
  beklenen sonuçtur).
- **FR-009**: Country/City/MCC verisi hiçbir yere taşınmaz — tohum verisi dahil tamamen
  silinir.

### Key Entities

- **Referans katalogları (silinen)**: ülke, şehir, MCC ve banka kod+ad listeleri —
  kaynak-of-truth ve tüm yerel kopyalarıyla birlikte sistemden çıkar.
- **Settlement hesabı (davranışı sadeleşen)**: banka bilgisi artık katalog
  doğrulamasız; katalogdan bağımsız kurallar değişmez.
- **Banka tanımı - Commission (davranışı sadeleşen)**: kod+ad kullanıcı girdisiyle;
  komisyon grid ilişkileri değişmez (024'e kadar).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Tek derleme komutu sıfır hatayla tamamlanır ve çözümde Reference'a ait
  proje sayısı 0'dır.
- **SC-002**: Sistem orkestratörle başlatıldığında Reference servisi/veritabanı olmadan
  kalan tüm servisler sağlıklı duruma geçer.
- **SC-003**: Katalog verisi olmayan taze sistemde settlement hesabı oluşturma ve banka
  tanımlama akışları %100 tamamlanabilir.
- **SC-004**: Kaynak taramasında referans-veri event'ine, yerel kopya tiplerine veya
  Reference projesine ait 0 kalıntı bulunur.
- **SC-005**: Kalan test projelerinin tamamı yeşildir; söküm mevcut testlerden hiçbirini
  kırmaz.

## Assumptions

- Dev aşamasındayız: veri migration üretilmez, veritabanları sıfırlanabilir (yerleşik
  proje pratiği). Mevcut kayıtların katalog-türetilmiş alanları için geriye-uyum çalışması
  yapılmaz.
- Settlement hesabı ve banka tanımında "ad" alanının nasıl alınacağı (kullanıcı girdisi
  mi, yalnız kod mu) planlama aşamasında netleşir; spec düzeyinde şart yalnız akışların
  katalogsuz tamamlanabilmesidir.
- Kart taksonomisi paylaşılan çekirdeği (Payment/Commission tüketicisi) bu sökümün dışındadır.
- Banka ekseninin bütünsel kaldırılması (Commission Bank + grid) 024'ün işidir; 021 yalnız
  katalog bağımlılığını keser, banka/grid yapılarına dokunmaz.
- Broker'daki (mesaj kuyruğu) eski kuyruk kalıntıları dev ortam sıfırlamasıyla gider;
  koddan kayıt kaldırmak yeterlidir.