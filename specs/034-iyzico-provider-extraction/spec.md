# Feature Specification: Iyzico.Provider Çekirdek Çıkarımı

**Feature Branch**: `034-iyzico-provider-extraction`

**Created**: 2026-08-15

**Status**: Draft

**Input**: User description: "Iyzico.Provider çekirdek çıkarımı: 3 serviste (Payment.Api, Merchant.Api, Commission.Api) birebir aynı (md5-özdeş) 14 iyzico transport dosyasını yeni paylaşılan class lib src/others/Iyzico.Provider'a taşı. BC-özel istek/yanıt alt klasörleri ve IyzicoProviderSettings BC'de kalır. Sınır kuralı korunur: çekirdek saf transport, BC dışına istek/yanıt tipi sızmaz."

## User Scenarios & Testing *(mandatory)*

Aktör: bu kod tabanının **geliştiricisi/bakımcısı**. "Value" = tek-kaynak transport çekirdeği; bir iyzico transport düzeltmesi üç yerde değil bir yerde yapılır.

### User Story 1 - Tek-kaynak transport çekirdeği (Priority: P1)

Geliştirici bugün 14 iyzico transport dosyasının (HTTP istemci, hash üretici, JSON kurucu, istek biçimleyici…) üç serviste **birebir aynı** (md5-özdeş) kopyasını tutuyor. Bir transport davranışını değiştirmek üç dosyayı senkron güncellemeyi gerektiriyor; bir kopya kayarsa BC'ler sessizce farklı davranır. Bu çekirdek tek paylaşılan projeye taşınır; her BC ona referans verir.

**Why this priority**: Tekrarın kendisi buradaki tek problem; çözülmezse özellik yok. MVP bu.

**Independent Test**: Taşımadan sonra `md5` ile hiçbir BC'de eski çekirdek dosyası kalmadığı doğrulanır; tek kopya paylaşılan projededir; üç BC de derlenir.

**Acceptance Scenarios**:

1. **Given** 14 çekirdek dosya üç serviste özdeş kopya halinde, **When** çekirdek `src/others/Iyzico.Provider`'a taşınır ve BC'ler referans verir, **Then** çözüm 0 hata derlenir ve BC'lerde çekirdek dosya kopyası kalmaz.
2. **Given** paylaşılan çekirdekte bir transport düzeltmesi yapılır, **When** çözüm derlenir, **Then** düzeltme üç BC'ye tek değişiklikle yansır (kopya senkronu gerekmez).

### User Story 2 - Sınır kuralının korunması (Priority: P1)

Anayasa kuralı: sağlayıcı istek/yanıt tipleri bir BC'den diğerine sızmaz. Çekirdek çıkarımı bu kuralı bozmamalı — yalnız BC-bağımsız transport altyapısı paylaşılır; BC-özel istek/yanıt tipleri (Payments/Installments/StoredCards, Onboarding, Payout/Reporting) ve BC-özel config (secret'lı ayarlar) ilgili BC'de kalır.

**Why this priority**: Yanlış çıkarım (her şeyi paylaşmak) mevcut mimari sınırını çiğner; P1.

**Independent Test**: Paylaşılan projede yalnız transport dosyaları bulunur; hiçbir BC'ye ait istek/yanıt tipi veya secret'lı config paylaşılan projede yer almaz. Bir BC'nin istek/yanıt tipi başka BC'den erişilemez.

**Acceptance Scenarios**:

1. **Given** paylaşılan çekirdek, **When** içeriği denetlenir, **Then** yalnız BC-bağımsız transport bileşenleri vardır; BC-özel istek/yanıt tipi ve secret'lı config yoktur.
2. **Given** Payment BC, **When** derlenir, **Then** başka BC'nin istek/yanıt tiplerine (Onboarding, Payout) erişimi yoktur.

### User Story 3 - Çalışma-anı davranışı değişmez (Priority: P1)

Bu bir yeniden düzenleme; hiçbir çalışma-anı davranışı değişmez. Ödeme çekimi, kart saklama, onboarding, rapor akışları taşımadan önce nasıl davranıyorsa sonra da aynı davranır.

**Why this priority**: Refactor'ün güvenlik koşulu; davranış kayarsa iş başarısız.

**Independent Test**: Mevcut testler (`dotnet test`) taşımadan sonra da yeşil; canlı smoke (opsiyonel) aynı sonucu verir.

**Acceptance Scenarios**:

1. **Given** taşıma tamamlandı, **When** `dotnet test` koşar, **Then** tüm testler taşımadan önceki gibi geçer.
2. **Given** bir iyzico çağrısı yapılır (ör. ödeme çekimi), **When** istek gönderilir, **Then** üretilen istek gövdesi/başlıkları/imzası taşımadan önce ile aynıdır.

### Edge Cases

- Çekirdekteki bir tip, ayrı assembly'e taşınınca BC-içi alt klasörlerden erişilebilir kalmalı (görünürlük yeterli olmalı; yalnız çekirdek-içi kullanılan yardımcı gizli kalabilir).
- BC-özel config, çekirdeğe taşınan transport-config tipine map'lenmeye devam etmeli (secret'lar BC'de kalır, çekirdeğe sızmaz).
- Aynı ada sahip framework tipiyle (transport'ta özel `HttpClient` sınıfı gibi) çakışma taşımadan önce de vardı; taşıma bunu kötüleştirmemeli.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sistem, üç serviste birebir aynı (md5-özdeş) 14 iyzico transport dosyasını tek paylaşılan kütüphaneye (`src/others/Iyzico.Provider`) taşımalı ve BC'lerdeki kopyaları silmeli.
- **FR-002**: Üç BC (Payment.Api, Merchant.Api, Commission.Api) paylaşılan kütüphaneye referans vermeli; çözüm tanımına yeni proje eklenmeli.
- **FR-003**: BC-özel istek/yanıt alt klasörleri (Payments/Installments/StoredCards, Onboarding, Payout/Reporting) ilgili BC'de kalmalı; paylaşılan kütüphaneye taşınmamalı.
- **FR-004**: BC-özel config (iyzico secret'lı ayar tipi) ilgili BC'de kalmalı; yalnız BC-bağımsız transport-config tipi paylaşılan kütüphanede olmalı ve BC config'i buna map'lenmeye devam etmeli.
- **FR-005**: BC-içi alt klasörlerden çağrılan transport tipleri, ayrı assembly'den erişilebilir görünürlüğe yükseltilmeli; yalnız çekirdek-içi kullanılan yardımcı tip gizli kalmalı.
- **FR-006**: Çözüm 0 hata derlenmeli ve mevcut testler yeşil kalmalı.
- **FR-007**: Hiçbir çalışma-anı davranışı değişmemeli — üretilen iyzico istek gövdeleri, başlıkları ve imzaları taşımadan önce ile bit düzeyinde aynı olmalı.
- **FR-008**: Merkezi paket yönetimi kuralı korunmalı — paylaşılan kütüphanenin bağımlılık sürümleri yalnız merkezi sürüm dosyasından gelmeli (proje dosyasında sürüm yazılmamalı).

### Key Entities

- **Iyzico.Provider (paylaşılan kütüphane)**: BC-bağımsız iyzico transport çekirdeği — HTTP istemci, hash/digest üreticileri, JSON kurucu, istek biçimleyici/dönüştürücü, transport temel tipleri, transport-config tipi, transport sabitleri. Yalnız System + JSON serileştirme bağımlıdır; domain bilmez.
- **BC transport uzantısı**: her BC'nin `Provider/<Alan>` alt klasörlerinde kalan iyzico istek/yanıt tipleri; çekirdek temel tiplerini miras alır/çağırır. BC'ye özeldir, paylaşılamaz.
- **BC transport config**: her BC'nin secret'lı iyzico ayar tipi; çekirdeğin transport-config tipine map'lenir. BC'de kalır.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Taşımadan sonra hiçbir BC'de çekirdek transport dosyası kopyası kalmaz (özdeş çekirdek dosya sayısı 3×14=42 → paylaşılan 14, BC'de 0).
- **SC-002**: Bir iyzico transport düzeltmesi tek dosyada yapılır ve üç BC'ye yansır (önceden üç dosya senkronu gerekiyordu).
- **SC-003**: Çözüm 0 derleme hatasıyla derlenir; mevcut test paketi %100 taşımadan önceki gibi geçer.
- **SC-004**: Paylaşılan kütüphanede BC-özel tip (istek/yanıt veya secret'lı config) sayısı 0'dır.

## Assumptions

- Çekirdek 14 dosya üç serviste gerçekten md5-özdeş (doğrulandı); davranış-korumalı taşıma güvenli.
- Üç BC aynı iyzico transport çekirdeğini kullanır; sürüm/varyant ayrımı yoktur.
- Merkezi paket yönetiminde gerekli JSON serileştirme paketi zaten kayıtlıdır (doğrulandı: Newtonsoft.Json 13.0.4).
- Hedef framework BC/paylaşılan projelerle aynıdır (net10.0).
- Canlı sandbox smoke opsiyoneldir; birincil doğrulama derleme + mevcut testler + üretilen istek karşılaştırmasıdır.
- BC-içi alt klasörlerin namespace'leri (`<BC>.Provider.<Alan>`) korunur; yalnız çekirdek namespace'i `Iyzico.Provider`'a değişir ve BC'ler global using ile bağlanır.