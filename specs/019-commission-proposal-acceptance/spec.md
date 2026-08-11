# Feature Specification: Komisyon Teklifi ve Merchant Kabulü

**Feature Branch**: `019-commission-proposal-acceptance`

**Created**: 2026-08-11

**Status**: Draft

**Input**: User description: "Komisyon grid'i merchant'a Excel ekli mail ile TEKLİF edilir; merchant kabul
veya (gerekçeyle) ret eder. Kabul edilmeden merchant aktive olamaz. Kabul sonrası komisyon değiştirilemez.
Karşı-teklif YOK (A yolu); ret → admin revize eder, yeniden teklif."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Teklif gönderimi (Priority: P1)

Admin, merchant'ın komisyon grid'ini eksiksiz doldurup "Teklif Et" der; sistem grid'i Excel'e döker ve
merchant'ın iletişim adresine kabul/ret linkli mail gönderir.

**Why this priority**: Bugün Finalize merchant'a hiçbir şey iletmiyor; onaysız oran dayatması iş modeline aykırı.

**Independent Test**: Eksiksiz grid'le teklif verilir; Mailpit'te Excel ekli + iki linkli mail görünür.

**Acceptance Scenarios**:

1. **Given** eksiksiz ve tavan-uyumlu grid, **When** admin teklif eder, **Then** Excel ekli mail gider,
   teklif "Beklemede" olur ve tek-kullanımlık karar bileti üretilir.
2. **Given** eksik hücreli grid, **When** admin teklif eder, **Then** teklif REDdedilir; eksik sayısı bildirilir.
3. **Given** bekleyen bir teklif varken, **When** admin yeni teklif eder, **Then** önceki bilet geçersiz olur;
   yalnız son teklif karar alabilir.

---

### User Story 2 - Merchant kararı: kabul / ret (Priority: P1)

Merchant, maildeki linkten teklifi kabul eder ya da gerekçe yazarak reddeder; karar gateway'e tek-kullanımlık
biletle döner.

**Why this priority**: Dönüş kanalı olmadan teklif tek yönlü bildirimde kalır; feature'ın özü karar döngüsü.

**Independent Test**: Kabul linki tıklanır → teklif Kabul olur; ret linki + gerekçe → Ret olur, gerekçe kayıtlı.

**Acceptance Scenarios**:

1. **Given** bekleyen teklif, **When** merchant kabul linkini kullanır, **Then** teklif Kabul olur ve
   merchant aktivasyon koşulu (komisyon) sağlanır.
2. **Given** bekleyen teklif, **When** merchant ret linkinden gerekçeyle reddeder, **Then** teklif Ret olur,
   gerekçe admin'e görünür, grid yeniden düzenlenebilir.
3. **Given** kullanılmış veya süresi dolmuş bilet, **When** linke gidilir, **Then** işlem RET edilir; durum değişmez.

---

### User Story 3 - Kabul sonrası değişmezlik (Priority: P1)

Kabul edilmiş komisyon grid'i değiştirilemez; oran güncelleme/teklif denemeleri reddedilir.

**Why this priority**: "Komisyon değiştirilemez" şartı sözleşme güvenidir; kabulün anlamı budur.

**Independent Test**: Kabul sonrası oran güncelleme ve yeni teklif denenir; ikisi de RET.

**Acceptance Scenarios**:

1. **Given** kabul edilmiş teklif, **When** admin oran güncellemeye çalışır, **Then** RET edilir.
2. **Given** kabul edilmiş teklif, **When** admin yeni teklif etmeye çalışır, **Then** RET edilir.

---

### User Story 4 - Teklif durumu görünürlüğü (Priority: P2)

Admin, komisyon ekranında teklifin durumunu (yok / beklemede / kabul / ret + gerekçe + zaman) görür.

**Why this priority**: Ret gerekçesi görülemezse revizyon döngüsü işlemez; okuma yüzeyi karar destektir.

**Independent Test**: Ret sonrası ekranda "Ret + gerekçe" görünür; kabul sonrası "Kabul + zaman" görünür.

**Acceptance Scenarios**:

1. **Given** reddedilmiş teklif, **When** admin komisyon ekranını açar, **Then** ret durumu + gerekçe + zaman görünür.

---

### Edge Cases

- Bekleyen teklif varken hücre düzenlenirse: serbest; ancak kayıtlı hücreler bekleyen teklifin Excel'iyle
  uyuşmayabilir — yeni teklif zorunluluğu ekranda uyarılır (bekleyen bilet ölür).
- Teklif maili ulaşmazsa: admin yeniden teklif eder (yeni bilet, yeni mail); eski bilet ölür.
- Karar biletinin TTL'i dolarsa: linkler işlem yapmaz; admin yeniden teklif eder.
- Merchant'ın iletişim adresi yoksa teklif verilemez; admin'e eksik bildirilir.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Admin, eksiksiz ve tavan-uyumlu grid'i tek eylemle teklif edebilmeli (bütünlük denetimi korunur).
- **FR-002**: Teklif, grid'in tablo halini (Excel) ekleyerek merchant iletişim adresine mail ile gitmeli.
- **FR-003**: Mail, tek-kullanımlık ve süreli karar linkleri (kabul / gerekçeli ret) içermeli.
- **FR-004**: Karar ucu kimlik doğrulaması istememeli; yetki biletin kendisidir (tek kullanım + TTL + son teklif).
- **FR-005**: Kabul, merchant aktivasyonunun komisyon koşulunu sağlamalı (mevcut koşul zinciri korunur).
- **FR-006**: Ret, gerekçeyi kaydetmeli; grid yeniden düzenlenebilir ve yeniden teklif edilebilir olmalı.
- **FR-007**: Kabul sonrası oran değişikliği ve yeni teklif reddedilmeli (değişmezlik).
- **FR-008**: Yeni teklif, önceki bekleyen bileti geçersiz kılmalı; aynı anda tek karar alabilir teklif olmalı.
- **FR-009**: Admin ekranı teklif durumunu (yok/beklemede/kabul/ret+gerekçe+zaman) göstermeli.
- **FR-010**: Mevcut Draft/Ready ayrımı kalkmalı; "teklif yok" hali serbest düzenleme dönemidir.

### Key Entities

- **Komisyon Teklifi (CommissionProposal)**: Merchant başına tek aktif kayıt; durum (Beklemede/Kabul/Ret),
  karar bileti (tek kullanım + TTL), karar zamanı, ret gerekçesi. Mevcut grid başlık kaydının evrimi.
- **Komisyon hücreleri (MerchantCommission)**: Mevcut yapı; teklif yokken/ret sonrası düzenlenebilir,
  kabul sonrası kilitli.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Eksiksiz grid'den tek eylemle teklif; mail (Excel + 2 link) teslim kuyruğuna 5 sn içinde düşer.
- **SC-002**: Kabul linki tek tıkla sonuç sayfası gösterir; merchant aktivasyon koşulu anında işlenir.
- **SC-003**: Kabul sonrası oran değiştirme denemelerinin %100'ü reddedilir.
- **SC-004**: Kullanılmış/süresi dolmuş biletle yapılan denemelerin %100'ü etkisizdir.
- **SC-005**: Ret gerekçesi admin ekranında kayıp/gecikme olmadan görünür.

## Assumptions

- Karşı-teklif kapsam dışı (A yolu); ret + revize + yeniden teklif döngüsü yeterli. A2A pazarlık ayrı aday.
- Excel üretimi kod içi kütüphaneyle yapılır (deterministik yol); MCP yalnız agent yüzeyi kuralı korunur
  (Excel.Mcp ve get_merchant_commission_grid agent senaryoları için kalır).
- Mail teslimi mevcut mail kuyruğu/worker'ıyla olur; yeni kanal yok.
- Bilet deseni (tek kullanım + TTL + son-teklif-geçerli) merchant aktivasyon biletiyle aynı kurallardadır.
- Banka grid'i (tavanlar) bu akışın dışında, gateway-otoriter kalır; teklif yalnız merchant grid'i içindir.
