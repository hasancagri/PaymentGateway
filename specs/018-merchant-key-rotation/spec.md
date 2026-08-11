# Feature Specification: Merchant Key Rotasyonu

**Feature Branch**: `018-merchant-key-rotation`

**Created**: 2026-08-11

**Status**: Draft

**Input**: User description: "Merchant, metin talebiyle ('key'imi yenile') yeni bir MerchantKey alabilmeli;
eski key talep anında geçersiz olmalı. Yeni key mevcut teslim kanalıyla (aktivasyon linki + tek sefer
gösterim) verilmeli."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Metinle key yenileme talebi (Priority: P1)

Merchant yöneticisi, agent'a metinle "key'imi yenile" / "yeni merchant key istiyorum" / "key sızdı,
rotasyon yap" der; sistem eski key'i geçersiz kılar, yeni key üretir ve teslim sürecini başlatır.

**Why this priority**: Sızıntı şüphesinde tek çare; bugün key ömür boyu sabit — rotasyon yolu hiç yok.

**Independent Test**: Aktif merchant'la rotasyon talep edilir; eski key'le erişim düşer, mail gelir.

**Acceptance Scenarios**:

1. **Given** aktif bir merchant, **When** yöneticisi metinle rotasyon ister, **Then** yeni key üretilir,
   eski key o anda geçersiz olur ve kayıtlı iletişim adresine aktivasyon linki gider.
2. **Given** rotasyon tamamlandı, **When** yönetici aktivasyon linkini açar, **Then** yeni key yalnız
   bir kez gösterilir; ikinci açılışta gösterilmez.
3. **Given** teslim alınmamış bekleyen bir rotasyon varken, **When** yeni rotasyon istenir, **Then** önceki
   teslim bileti geçersiz olur; yalnız en son key ve bileti geçerlidir.

---

### User Story 2 - Eski key anında ölür (Priority: P1)

Rotasyon talebinin ardından eski key ile yapılan tüm kimlik doğrulama/istekler reddedilir; yeni key
girilene kadar merchant'ın entegrasyonu bilinçli olarak kapalıdır (fail-closed).

**Why this priority**: Rotasyonun varlık sebebi sızıntıyı kesmek; eski key yaşarsa özellik anlamsız.

**Independent Test**: Rotasyon sonrası eski key ile token/istek denenir; tümü yetki hatasıyla düşer.

**Acceptance Scenarios**:

1. **Given** rotasyon yapıldı, **When** eski key ile kimlik doğrulama denenir, **Then** reddedilir.
2. **Given** rotasyon yapıldı ve yeni key teslim alınıp entegrasyona girildi, **When** yeni key ile
   kimlik doğrulama denenir, **Then** başarılı olur.

---

### User Story 3 - Rotasyon durumu görünür (Priority: P2)

Merchant yöneticisi, metinle durum sorduğunda bekleyen bir rotasyon olup olmadığını (yeni key teslim
alınmadıysa) ve son rotasyon zamanını öğrenir.

**Why this priority**: "Mail gelmedi/link kayboldu" durumunda teşhis; yazma olmadan değer taşır.

**Independent Test**: Rotasyon sonrası durum sorulur; bekleyen teslim bilgisi ve zaman görünür.

**Acceptance Scenarios**:

1. **Given** teslim alınmamış rotasyon var, **When** yönetici durum sorar, **Then** bekleyen teslim ve
   talep zamanı bildirilir; key değeri asla metinle verilmez.

---

### Edge Cases

- Henüz aktive olmamış (ilk key'ini teslim almamış) merchant rotasyon isterse: ret; önce ilk teslim.
- Yetkisiz/yabancı domain için rotasyon istenirse: ret; yalnız kendi merchant'ının key'i döndürülür.
- Teslim maili ulaşmazsa: yeni rotasyon talebi yeni bilet üretir (senaryo US1-3); eski biletler ölür.
- Key değeri hiçbir kanalda (chat yanıtı, log, durum sorgusu) düz metin görünmez; yalnız teslim sayfası.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Merchant yöneticisi, agent'a doğal dil talebiyle key rotasyonu başlatabilmeli.
- **FR-002**: Rotasyon anında eski key geçersiz olmalı; eski key ile kimlik doğrulama reddedilmeli.
- **FR-003**: Yeni key, mevcut teslim kanalıyla verilmeli: iletişim adresine aktivasyon linki,
  tek-kullanımlık bilet, tek sefer gösterim.
- **FR-004**: Yeni rotasyon talebi, teslim alınmamış önceki bileti geçersiz kılmalı (tek geçerli bilet).
- **FR-005**: Rotasyon yalnız talep sahibinin kendi merchant kaydı için yapılabilmeli (tenant izolasyonu).
- **FR-006**: İlk aktivasyonunu tamamlamamış merchant rotasyon başlatamamalı.
- **FR-007**: Durum sorgusu bekleyen teslim + son rotasyon zamanını bildirmeli; key değerini asla içermemeli.
- **FR-008**: Rotasyon olayı, key'e bağlı kimlik doğrulama yüzeylerine (OAuth istemci sırrı) yansımalı;
  eski sır ile token alınamamalı.

### Key Entities

- **Merchant**: Gateway'deki mağaza kaydı; MerchantKey (gizli), aktivasyon/teslim bileti, statü.
  Rotasyon key'i ve bileti yeniler; kimlik ve diğer alanlar değişmez.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Yönetici, tek metin talebiyle rotasyonu başlatabilir; sonuç mesajını 5 sn içinde görür.
- **SC-002**: Eski key, rotasyon talebinin ardından hiçbir yüzeyde kimlik doğrulayamaz (0 başarılı istek).
- **SC-003**: Yeni key yalnız teslim sayfasında ve yalnız bir kez görünür; başka hiçbir kanalda görünmez.
- **SC-004**: Teslim alınmamış rotasyonun ardından yeni talep, önceki bileti %100 geçersiz kılar.
- **SC-005**: Yabancı merchant için rotasyon denemesi %100 reddedilir.

## Assumptions

- Teslim kanalı mevcut onay akışıyla aynıdır (mail + aktivasyon linki + tek sefer gösterim); yeni kanal yok.
- Kesinti kabulü: rotasyon ile yeni key'in entegrasyona girişi arasında merchant entegrasyonu çalışmaz
  (fail-closed); bu bilinçli üründür, sızıntı senaryosu önceliklidir.
- Tüketici taraf (ör. ECommerce admin ekranı/persona satırı) kapsam dışıdır; kendi reposunda ele alınır.
- Rotasyon geçmişi ayrıntılı audit gerektirmez; son rotasyon zamanı yeterlidir.
