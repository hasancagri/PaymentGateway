# Feature Specification: Hassas Merchant Verisi — Hosted Link Erişimi

**Feature Branch**: `048-sensitive-hosted-link`

**Created**: 2026-09-27

**Status**: Draft

**Input**: User description: "Admin hassas merchant verisini (Email/GSM/TCKN/IBAN/vergi no) LLM/agent context'ine SOKMADAN görüntüleyip düzenleyebilmeli. Agent sohbete yalnız süreli+tek-kullanımlık token link düşürür; insan tarayıcıda açar, görür/düzenler. Admin Razor projesi sökülür."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Agent hassas veriyi sohbete sızdırmadan link verir (Priority: P1)

Admin, agent (Claude Desktop vb.) üzerinden bir merchant'ın hassas verisini görmek ister. Agent, veriyi sohbete YAZMADAN, o merchant'a özel süreli bir link üretir ve sohbete yalnız linki düşürür. Admin linki tarayıcıda açar, hassas veriyi orada görür.

**Why this priority**: Ürünün varlık sebebi — kişisel/finansal veri (KVKK/PII) LLM bağlamına girmeden yetkili insana ulaşmalı. Bu akış olmadan feature yok.

**Independent Test**: Agent'tan link istenir; sohbet çıktısında hiçbir hassas alan bulunmaz (yalnız link + açıklama); link tarayıcıda açılınca doğru merchant'ın değerleri görünür.

**Acceptance Scenarios**:

1. **Given** yetkili admin ve geçerli merchantId, **When** agent'tan hassas-veri linki istenir, **Then** yanıt yalnız link + son-kullanım zamanı içerir; hiçbir hassas alan sohbete yazılmaz.
2. **Given** üretilmiş geçerli link, **When** admin tarayıcıda açar, **Then** o merchant'ın güncel hassas alanları sayfada görünür.
3. **Given** yetkisiz istek (gerekli yetki yok), **When** link istenir, **Then** link üretilmez, erişim reddedilir.

---

### User Story 2 - Admin hassas veriyi düzenler (Priority: P2)

Admin, açtığı sayfada hassas alanları (ör. yanlış IBAN) düzeltip kaydeder. Kayıt doğrulanır; başarılıysa link geçersiz olur.

**Why this priority**: Görüntüleme tek başına değerli (P1) ama düzeltme yeteneği kanalı tam kılar; yanlış IBAN ödemeyi durdurur.

**Independent Test**: Geçerli link açılır, bir alan değiştirilir, kaydedilir; değer kalıcı olur ve aynı link tekrar kullanılamaz.

**Acceptance Scenarios**:

1. **Given** açık geçerli sayfa, **When** admin alanı düzenleyip kaydeder, **Then** değer kalıcı olur ve onay gösterilir.
2. **Given** başarılı kayıt sonrası, **When** aynı link tekrar açılır, **Then** erişim reddedilir (tek kullanım).
3. **Given** geçersiz girdi (ör. hatalı IBAN), **When** kaydedilir, **Then** hata gösterilir ve link YAŞAR (düzeltilebilir).

---

### User Story 3 - Süresi geçmiş/kullanılmış link nötr reddedilir (Priority: P2)

Süresi dolmuş, tüketilmiş veya bilinmeyen bir linke erişim, bilgi sızdırmadan aynı nötr "bulunamadı" ile reddedilir.

**Why this priority**: Güvenlik değişmezi — token'lı sayfa hassas veri gösterdiği için sızan/eski link asla veri açmamalı; hata mesajı token doğruluğunu sızdırmamalı.

**Independent Test**: Süresi geçmiş, tüketilmiş ve hiç var olmayan üç token ile erişilir; üçü de ayırt edilemez aynı nötr sonucu döner.

**Acceptance Scenarios**:

1. **Given** üretimden 15 dk sonrası, **When** link açılır, **Then** nötr bulunamadı döner, veri gösterilmez.
2. **Given** tüketilmiş link, **When** açılır, **Then** nötr bulunamadı döner.
3. **Given** bilinmeyen/uydurma token, **When** açılır, **Then** aynı nötr bulunamadı döner (var olan-ama-süreli'den ayırt edilemez).

---

### Edge Cases

- Sayfa yenilenir (henüz kaydetmeden): link yaşar (yalnız başarılı kayıt veya süre öldürür).
- Uygulama kapatılıp saatler sonra açılır: süre gerçek zamanda aktığından link ölüdür (mutlak son-kullanım, geri sayım değil).
- Aynı merchant için yeni link üretilir: eski link iptal EDİLMEZ; süre öldürür (bilinçli basitlik).
- Kaydetme anında merchant silinmiş/bulunamaz: nötr bulunamadı.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Hassas alanlar (Email, GSM, TCKN, IBAN, vergi no) agent/LLM context'ine veya MCP tool yanıtına ASLA girmemeli.
- **FR-002**: Sistem, belirli bir merchant için süreli tek-kullanımlık erişim linki üretebilmeli; yanıt yalnız link + son-kullanım zamanı + kısa açıklama içermeli.
- **FR-003**: Link üretimi yetki gerektirmeli (yönetim düzeyi); yetkisiz istek link üretmemeli.
- **FR-004**: Link 15 dakika sonra geçersiz olmalı (mutlak son-kullanım; uygulama kapalıyken de geçen süre sayılır).
- **FR-005**: Link, başarılı bir düzenleme kaydından sonra tekrar kullanılamamalı (tek kullanım); yalnız görüntüleme/başarısız kayıt linki tüketmemeli.
- **FR-006**: Yetkili insan linki tarayıcıda açtığında o merchant'ın güncel hassas alanlarını görebilmeli.
- **FR-007**: İnsan hassas alanları düzenleyip kaydedebilmeli; kayıt doğrulanmalı; geçersiz girdi hata döndürmeli ve linki yaşatmalı.
- **FR-008**: Bilinmeyen, süresi geçmiş veya tüketilmiş linkler ayırt edilemez tek nötr "bulunamadı" ile reddedilmeli (token doğruluğu sızmamalı).
- **FR-009**: Sayfa arama motorlarınca indekslenmemeli.
- **FR-010**: Denetim izi link üretimini kaydetmeli; kayıt token'ı DEĞİL, hedef merchant + isteyen kimliği içermeli.
- **FR-011**: Hassas veri sayfası, agent kimliğinden bağımsız, insanın erişebileceği bir kanaldan sunulmalı (link taşınabilir; platform bağımsız — mobil dahil tarayıcı).
- **FR-012**: Eski Admin ekran uygulaması (Razor hassas-veri sayfası) SÖKÜLMELİ; hassas-veri kanalı bu hosted link olmalı. Kullanılmayan admin kimlik istemcisi de temizlenmeli.

### Key Entities *(include if feature involves data)*

- **Hassas Erişim Oturumu (SensitiveEntrySession)**: bir hosted link'in yetkisini temsil eder. Nitelikler: tekil token (yetki), hedef merchant, isteyen kimliği (denetim), son-kullanım zamanı, tüketim zamanı. Kullanılabilirlik = süresi geçmemiş ve tüketilmemiş.
- **Merchant Hassas Alanları**: Email, GSM, TCKN/kimlik no, IBAN, vergi no (mevcut merchant kaydında). Bu feature yalnız görüntüleme/düzenleme kanalını değiştirir; alanların kendisi değişmez.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Hassas alanların agent/sohbet çıktısında görünme oranı %0 (hiçbir senaryoda sızmaz).
- **SC-002**: Üretilen linkin %100'ü 15 dakika sonra veri açmaz.
- **SC-003**: Başarılı düzenlemeden sonra aynı linkin ikinci kez veri açma oranı %0.
- **SC-004**: Bilinmeyen/süreli/tüketilmiş link yanıtları dışarıdan ayırt edilemez (aynı durum + gövde).
- **SC-005**: Yetkili admin, agent'tan link alıp tarayıcıda hassas veriyi görene kadar tek akışta ilerleyebilir (ayrı parola/OTP üretimi gerekmez).

## Assumptions

- **Değer gösterimi TAM** (maskesiz): sayfa bir düzenleme/doğrulama ekranı olduğundan admin IBAN/TCKN'yi tam görmeli (yanlışı düzeltmek için). Güvenlik token+süre+tek-kullanımla sağlanır, maskelemeyle değil. Maskeleme (ör. IBAN son-4) ileri bir sertleştirme opsiyonu, bu sürümde kapsam dışı.
- Link'in dışarıdan erişilir taban adresi yapılandırmadan gelir (agent çağrısı bir proxy'den geldiğinden istek adresi tarayıcıda çözülmez).
- Mevcut merchant hassas-veri okuma/yazma davranışı (doğrulama dahil) yeniden kullanılır; bu feature yalnız erişim kanalını (link + hosted sayfa) ekler ve eski Admin ekranını söker.
- Yetki, mevcut yönetim scope'uyla (`merchant.admin`) ifade edilir; yeni bir yetki türü gerekmez.
- Hassas veri sayfası, verinin yaşadığı serviste (merchant servisi) sunulur; servisler-arası ek çağrı gerekmez.
