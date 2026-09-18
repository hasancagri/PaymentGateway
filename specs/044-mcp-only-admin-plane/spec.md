# Feature Specification: MCP-Only Admin Düzlemi

**Feature Branch**: `044-mcp-only-admin-plane`

**Created**: 2026-09-18

**Status**: Draft

**Input**: User description: "MCP-only admin düzlemi: Admin operasyonlarının tek arayüzü Claude
desktop (MCP). REST yalnız makine kanalları. Hassas kişisel veri (Email, GsmNumber,
IdentityNumber/TCKN, Iban, TaxNumber) MCP'den ne yanıt ne girdi olarak geçmez; tek hassas-veri
ekranı PG Admin UI'da kalır. Yeni MCP tool'lar + REST/Admin UI sökümü."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Merchant yönetimi tamamen sohbetten (Priority: P1)

Admin, Claude desktop sohbetinde bir merchant'ın detayını görür ("X merchant'ının bilgilerini
göster") ve hassas olmayan profil alanlarını günceller ("adresini şöyle değiştir", "ticari
unvanını güncelle"). Hiçbir yönetim ekranı açmaz.

**Why this priority**: Projenin ana hedefi (admin operasyonları tek arayüzden); merchant profili
en sık dokunulan yönetim verisi ve bugün MCP'de tekil görüntüleme/güncelleme hiç yok.

**Independent Test**: Claude desktop'tan `admin_get_merchant` + `admin_update_merchant` çağrılır;
güncelleme kalıcıdır, yanıtlarda hassas alan görünmez. Tek başına değer üretir (ekransız merchant
profil yönetimi).

**Acceptance Scenarios**:

1. **Given** Active bir merchant, **When** admin sohbetten tekil merchant sorgular, **Then**
   profil alanları döner; Email/GsmNumber/TCKN/Iban/TaxNumber/MerchantKey yanıtta YOKtur.
2. **Given** mevcut bir merchant, **When** admin hassas-dışı alanı (ör. adres, ticari unvan)
   sohbetten günceller, **Then** değişiklik kalıcı olur ve doğrulama kuralları (boş ad vb.) çalışır.
3. **Given** var olmayan merchantId, **When** güncelleme denenir, **Then** anlaşılır
   kayıt-bulunamadı hatası döner (çökme yok).

---

### User Story 2 - Kişisel veri agent kanalından geçmez (Priority: P2)

Admin, kişisel veri (Email, GSM, TCKN, IBAN, vergi no) görmesi/güncellemesi gerektiğinde
sohbetten değil, gateway'in kendi yönetim ekranındaki tek hassas-veri sayfasından çalışır.
Sohbetteki hiçbir araç yanıtı bu alanları içermez.

**Why this priority**: KVKK sınırı — kişisel veri üçüncü taraf LLM sağlayıcısından geçmemeli.
Mevcut araçlar bugün Email+GSM sızdırıyor; kırpılması yeni araçlardan bağımsız acil düzeltme.

**Independent Test**: Mevcut liste araçları (`admin_get_merchants`, `admin_get_pending_registrations`)
çağrılır — Email/GSM artık yanıtta yok. Hassas-veri sayfasında aynı merchant'ın hassas alanları
görüntülenir ve güncellenir.

**Acceptance Scenarios**:

1. **Given** merchant listesi araçları, **When** sohbetten çağrılır, **Then** yanıtta
   Email/GsmNumber alanı bulunmaz (sözleşmeden çıkarılmıştır).
2. **Given** bir merchant, **When** admin hassas-veri sayfasını açar, **Then** Email, GSM, TCKN,
   IBAN, vergi no görüntülenir ve düzenlenebilir; kaydetme doğrulamadan geçer.
3. **Given** sohbette hassas alan güncelleme niyeti, **When** admin bunu ister, **Then** araç bu
   alanları parametre olarak KABUL ETMEZ (sözleşmede yoktur); agent admin'i hassas-veri sayfasına yönlendirir.

---

### User Story 3 - Komisyon politikası yönetimi sohbetten (Priority: P3)

Admin, bir merchant için komisyon politikasını sohbetten oluşturur ("X'e %2,5 + 1 TL marj
tanımla"), marjını günceller ve politika statüsünü değiştirir. Komisyon ekranı yoktur.

**Why this priority**: 043'ün salt-okuma kararının bilinçli tersine çevrilmesi; komisyon
verisinde kişisel veri yok, tam MCP'ye açılmasının önünde engel yok. Merchant yüzeyinden bağımsız
çalışır.

**Independent Test**: Claude desktop'tan komisyon oluştur/güncelle/statü-değiştir araçları
çağrılır; mevcut `admin_get_commission_policy` ile sonuç teyit edilir.

**Acceptance Scenarios**:

1. **Given** politikasız bir merchant, **When** admin sohbetten politika oluşturur, **Then**
   politika kalıcı olur ve sorgu aracı yeni politikayı döner.
2. **Given** politikalı bir merchant, **When** admin marj değerlerini sohbetten günceller,
   **Then** yeni marj kalıcı olur; geçersiz değer (negatif oran vb.) anlaşılır hata döner.
3. **Given** aktif bir politika, **When** admin statüsünü sohbetten değiştirir, **Then** statü
   geçişi domain kurallarına göre uygulanır veya reddedilir.

---

### User Story 4 - Eski admin yüzeyinin sökümü (Priority: P4)

Yeni MCP araçları + hassas-veri sayfası canlı doğrulandıktan sonra: merchant/komisyon/başvuru
REST yönetim uçları, Admin UI'ın CommissionPolicies + Merchants sayfaları ve ilgili istemcileri
ile doğrudan merchant oluşturma yolu tamamen kaldırılır. Merchant doğuşunun tek yolu başvuru
onayı olur.

**Why this priority**: Çift yüzey (ekran + sohbet) bakım yükü ve tutarsızlık riski; ancak yeni
yüzey doğrulanmadan sökülürse sistem işlevsiz kalır — bu yüzden en son.

**Independent Test**: Sökülen uçlara istek 404 döner; Admin UI'da yalnız hassas-veri sayfası
kalır; build + tüm testler yeşil; merchant yalnız başvuru onayıyla doğar.

**Acceptance Scenarios**:

1. **Given** söküm tamamlandı, **When** eski merchant/komisyon/başvuru yönetim uçlarına istek
   atılır, **Then** 404 döner; makine kanalları (aktivasyon redeem, ödeme, olay akışı) çalışmaya devam eder.
2. **Given** söküm tamamlandı, **When** admin merchant oluşturmak ister, **Then** tek yol
   başvuru (submit) + onay akışıdır; doğrudan oluşturma hiçbir kanaldan mümkün değildir.
3. **Given** söküm tamamlandı, **When** Admin UI açılır, **Then** yalnız hassas-veri sayfası
   (ve giriş) vardır; CommissionPolicies/Merchants sayfaları 404 döner.

---

### Edge Cases

- Hassas-dışı güncelleme aracına bilinmeyen/geçersiz alan değeri gelirse (boş ad, geçersiz tip)
  domain doğrulaması anlaşılır hata döner; kısmi yazma olmaz.
- Aynı merchant'a ikinci komisyon politikası oluşturma denemesi reddedilir (tekillik kuralı).
- Hassas-veri sayfası var olmayan/silinmiş merchant için anlaşılır hata gösterir.
- Söküm sonrası eski uçları çağıran unutulmuş bir tüketici kalmadığı doğrulanır (bilinen tek
  tüketici Admin UI; aktivasyon redeem ucu sökümden ETKİLENMEZ).
- Sohbet aracı yanıtlarında hassas alanların "boş string" olarak bile görünmemesi — alanlar
  sözleşmeden tamamen çıkarılır.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Admin, tekil merchant profilini sohbet aracıyla görüntüleyebilmeli; yanıt hassas
  alan (Email, GsmNumber, IdentityNumber, Iban, TaxNumber, MerchantKey/SubMerchantKey) içermemeli.
- **FR-002**: Admin, merchant'ın hassas-dışı alanlarını (Name, Type, Address, ContactName,
  ContactSurname, TaxOffice, LegalCompanyTitle) sohbet aracıyla güncelleyebilmeli; mevcut domain
  doğrulamaları aynen uygulanmalı.
- **FR-003**: Hassas alanlar hiçbir sohbet aracının ne girdi ne çıktı sözleşmesinde yer almamalı;
  mevcut `admin_get_merchants` ve `admin_get_pending_registrations` yanıtlarından Email+GsmNumber
  çıkarılmalı.
- **FR-004**: Admin, merchant hassas alanlarını (Email, GsmNumber, IdentityNumber, Iban,
  TaxNumber) gateway'in kendi yönetim ekranındaki TEK hassas-veri sayfasından görüntüleyip
  güncelleyebilmeli; bu sayfa admin-düzlemi yetkisiyle korunmalı.
- **FR-005**: Admin, komisyon politikası oluşturma, marj güncelleme ve statü değiştirme
  işlemlerini sohbet araçlarıyla yapabilmeli; domain kuralları (tekillik, geçerli değer aralığı,
  statü geçişleri) aynen uygulanmalı.
- **FR-006**: Yeni sohbet araçları mevcut admin yetki kapısıyla (admin scope) korunmalı; yetkisiz
  token'la çağrı reddedilmeli.
- **FR-007**: Doğrudan merchant oluşturma yolu tamamen kaldırılmalı; merchant doğuşunun tek yolu
  başvuru + admin onayı olmalı.
- **FR-008**: Merchant/komisyon/başvuru REST yönetim uçları (yalnız admin-düzlemi olanlar) ve
  Admin UI'ın CommissionPolicies + Merchants sayfaları ile ilgili API istemcileri kaldırılmalı;
  söküm yeni yüzey canlı doğrulanmadan BAŞLAMAMALI.
- **FR-011**: Merchant'ın KENDİ token'ıyla kendi kaydını/politikasını okuduğu uçlar (merchant
  self-service düzlemi) sökümden ETKİLENMEMELİ — bunlar admin yüzeyi değildir.
- **FR-009**: Makine kanalları değişmeden kalmalı: ödeme uçları, iyzico geri dönüşü, aktivasyon
  redeem, başvuru gönderme/durum araçları, statü yönetim araçları, olay akışı.
- **FR-010**: Söküm sonrası sistemde hiçbir ölü referans (kullanılmayan istemci, sayfa, uç
  kaydı) kalmamalı; derleme ve tüm testler hatasız geçmeli.

### Key Entities

- **Merchant**: Gateway müşterisi site; hassas (Email, GSM, TCKN, IBAN, vergi no) ve hassas-dışı
  (ad, tip, adres, iletişim adı, vergi dairesi, unvan) alan kümeleri bu özellikle resmen ayrışır.
- **CommissionPolicy**: Merchant başına komisyon marjı + statü; artık tam yaşam döngüsü sohbet
  araçlarından yönetilir.
- **RegisterRequest**: Merchant doğuşunun tek kaynağı hâline gelir (doğrudan oluşturma kalkar).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Admin, hassas alan gerektirmeyen TÜM merchant ve komisyon yönetim işlemlerini
  yalnız sohbetten, hiçbir yönetim ekranı açmadan tamamlayabilir.
- **SC-002**: Sohbet araçlarının hiçbirinin yanıt/girdi sözleşmesinde Email, GSM, TCKN, IBAN,
  vergi no, MerchantKey alanı yoktur (sözleşme taraması %100 temiz).
- **SC-003**: Hassas alan görüntüleme/güncelleme yalnız hassas-veri sayfasından yapılabilir ve
  uçtan uca çalışır (görüntüle → düzenle → kalıcı).
- **SC-004**: Söküm sonrası eski yönetim uçları ve sayfaları erişilemezdir (404); makine
  kanalları kesintisiz çalışır.
- **SC-005**: Derleme sıfır hata; mevcut test takımı sıfır regresyonla geçer; kaldırılan yüzeye
  ait ölü kod kalmaz.

## Assumptions

- Hassas alan kümesi kullanıcı kararıyla sabitlendi: Email, GsmNumber, IdentityNumber (TCKN),
  Iban, TaxNumber (+ zaten gizli MerchantKey/SubMerchantKey). ContactName/Surname hassas-dışı
  sayıldı (kullanıcı çizgisi).
- Başvuru gönderme (`submit_registration`) mevcut haliyle kalır: merchant kendi verisini kendi
  kanalından girer (veri sahibinin kendisi), admin düzlemi kişisel-veri kısıtı bunu kapsamaz.
- Admin-düzlemi REST uçlarının bilinen tek tüketicisi Admin UI'dır (kod taramasıyla doğrulandı).
  Merchant-düzlemi okuma uçları (tekil merchant + kendi komisyon politikası) merchant token'ıyla
  dışarıdan tüketilir ve kapsam DIŞIdır.
- Hassas-veri sayfası mevcut Admin UI altyapısında (aynı giriş/oturum düzeni) yaşar; yeni bir
  uygulama açılmaz.
- Komisyon politikası oluşturma/güncelleme kuralları mevcut domain davranışlarıyla aynıdır; bu
  özellik yeni komisyon kuralı EKLEMEZ, yalnız erişim kanalını değiştirir.
- Söküm sıralaması 043 desenini izler: yeni yüzey canlı doğrulanmadan eski yüzey silinmez.