# Feature Specification: Taksitli Ödeme (Per-Taksit En Avantajlı Banka)

**Feature Branch**: `pivot/bank-adapter-skeleton`

**Created**: 2026-10-23

**Status**: Draft

**Input**: User description: "093 Taksitli ödeme + kart-ailesi komisyon + per-taksit maliyet routing — BIN'den kart ailesi çözülür; her taksit için en avantajlı banka + taksitli toplam ekranda gösterilir; müşteri seçer, o bankadan o taksitle çekilir. 092 absorbe." Detaylı tasarım: `~/.claude/plans/dreamy-gathering-crab.md`.

## Özet

Bugün pay-by-link yalnız tek-çekim; müşteri taksit göremiyor, routing statik. Bu feature gerçek taksitli
ödemeyi getirir: müşteri hosted ödeme sayfasında kartın ilk hanelerini (BIN) girer → kart ailesi (issuer
bankası + şema + tip) çözülür → **her taksit sayısı için en avantajlı (en düşük efektif komisyonlu) banka ve
taksitli toplam/aylık tutar hesaplanır** ve **o banka ekranda gösterilir**. En avantajlı banka taksit sayısına
göre değişebilir (2 takside X, 4 takside Y). Müşteri bir taksit seçer; çekim **o taksitin gösterilen
bankasından**, seçilen taksit sayısıyla geçer. Komisyon oranı banka×şema×tip×taksit bazlı bir **öngörü
read-model**'den gelir (banka otorite — İlke VII). Önceki 092 (maliyet-routing) bu feature'a absorbe edilir.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Taksit seçeneklerini ve en avantajlı bankayı gör (Priority: P1)

Müşteri ödeme sayfasında kartının ilk hanelerini girince, kart ailesine göre her taksit sayısı için en
avantajlı banka + o taksitin toplam ve aylık tutarı listelenir; en avantajlı banka her taksitte farklı olabilir.

**Why this priority**: Değerin görünür yüzü — müşteri en uygun taksit/bankayı görür. US2 olmadan tek başına
bile "fiyat şeffaflığı" sağlar (quote).

**Independent Test**: Belirli bir BIN + tutar + öngörü oran matrisi verildiğinde, her taksit seçeneğinin
en-düşük-toplamlı bankayı + doğru taksitli tutarı gösterdiği; en az bir senaryoda farklı taksitlerde farklı
bankanın öne çıktığı doğrulanır.

**Acceptance Scenarios**:

1. **Given** BIN kart ailesini (issuer/şema/tip) çözüyor ve birden çok uygun banka var, **When** taksit seçenekleri hesaplanır, **Then** her taksit için en düşük taksitli toplamı veren banka + toplam + aylık tutar listelenir.
2. **Given** X bankası 2 takside, Y bankası 4 takside daha düşük orana sahip, **When** seçenekler gösterilir, **Then** 2 taksit X'i, 4 taksit Y'yi gösterir (per-taksit en avantajlı).
3. **Given** bir banka bu kart tipini/işlemi desteklemiyor, **When** seçenekler hesaplanır, **Then** o banka hiçbir taksitte aday olmaz.
4. **Given** bir banka için o taksit oranı tanımsız, **When** hesaplanır, **Then** güvenli varsayılan oran kullanılır (banka adaylıktan düşmez).

---

### User Story 2 - Seçilen taksitle o bankadan öde (Priority: P1)

Müşteri bir taksit seçeneğini seçip öder. Çekim, o taksit için ekranda gösterilen bankadan, seçilen taksit
sayısıyla ve taksitli toplam tutarla geçer. Gösterilen banka = çekilen banka.

**Why this priority**: Akışı tamamlar — seçim gerçek çekime döner. US1 ile MVP.

**Independent Test**: Bir taksit seçimi verildiğinde, çekimin o taksitin bankasına, doğru InstallmentCount +
taksitli tutarla gittiği; gösterilen bankanın çekilen bankayla aynı olduğu doğrulanır.

**Acceptance Scenarios**:

1. **Given** müşteri "4 taksit · Y Bankası · toplam T" seçeneğini seçti, **When** öder, **Then** çekim Y bankasına 4 taksit + T tutarıyla gider.
2. **Given** seçilen taksitin bankası çekimi teknik/iş kuralıyla reddeder, **When** sonuç döner, **Then** işlem başarısız olur (başka bankaya GEÇİLMEZ — gösterilen=çekilen); müşteri tekrar deneyebilir.
3. **Given** istemciden gelen taksit/banka seçimi, **When** çekim başlar, **Then** o taksitin bankası sunucuda yeniden hesaplanıp doğrulanır (istemciye güvenilmez).
4. **Given** onaylı çekim, **When** tamamlanır, **Then** kaydedilen/bildirilen tutar taksitli toplamdır (Amount × (1+oran)).

---

### Edge Cases

- BIN bilinmiyor (kart ailesi çözülemez): taksit seçenekleri yine hesaplanabilir mi? → aile belirsizse yalnız genel/varsayılan oran kullanılır; on-us hesaplanamaz (fail-open), seçenekler yine sunulur.
- Hiç uygun banka yok (hepsi yetenek-dışı): taksit seçeneği üretilmez; müşteriye uygun seçenek olmadığı bildirilir.
- Tek taksit (peşin): "1 taksit" de bir seçenek; en avantajlı banka + tutar (oran×amount, 1 taksit oranı) gösterilir.
- Quote ile ödeme arasında oran değişirse: ödeme anında sunucu yeniden hesaplar; gösterilen tutar bağlayıcı olan sunucu-hesabıdır (tutarsızlık olursa müşteriye güncel tutar/yeniden onay).
- Gösterilen banka ödeme anında reddederse: işlem başarısız (failover yok); müşteri başka taksit/kart dener.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sistem, girilen BIN'den (ilk 6–8 hane) kart ailesini (issuer bankası + şema + tip) çözMELİ; tam kart numarası sorgu anahtarı olarak KULLANILMAMALI (İlke VI).
- **FR-002**: Sistem, yapılandırılmış her taksit sayısı için uygun bankaları eleyip (yetenek + kart ailesi) en düşük efektif komisyonlu bankayı seçMELİ; on-us yalnız eşit oranda tiebreaker olmalı.
- **FR-003**: Sistem, her taksit seçeneği için taksitli toplamı (Amount × (1 + oran)) ve aylık tutarı hesaplaMALI.
- **FR-004**: Sistem, acquirer bankayı müşteriye GÖSTERMEMELİ (gerçek-hayat UX; routing gizli iç karardır). Müşteri yalnız taksit sayısı + taksitli toplam + aylık tutar görür. En-avantajlı banka seçimi sunucuda, görünmez.
- **FR-005**: Sistem, en avantajlı bankanın taksit sayısına göre farklı olmasına izin verMELİ (seçim taksit başına bağımsız).
- **FR-006**: Müşteri bir taksit seçtiğinde, sistem çekimi o taksitin bankasından, seçilen taksit sayısıyla (InstallmentCount) ve taksitli toplam tutarla başlatMALI.
- **FR-007**: Sistem komisyon oranını banka × şema × tip × taksit bazlı bir öngörü read-model'inden okuMALI (banka otorite, PG öngörü kopyası — İlke VII); tanımsız oran için güvenli varsayılan kullanmalı (banka düşmemeli).
- **FR-008**: Sistem taksit akışında çok-banka failover UYGULAMAMALI — gösterilen banka = çekilen banka; o banka reddederse işlem başarısız olmalı (müşteri tekrar dener).
- **FR-009**: Sistem, seçilen taksitin bankasını ve tutarını çekim anında sunucuda yeniden hesaplayıp doğrulaMALI; istemciden gelen banka/tutar seçimine körü körüne GÜVENMEMELİ.
- **FR-010**: Sistem kart verisini yalnız çekim anında transient taşıMALI; saklamamalı/loglamamalı (İlke VI).
- **FR-011**: Sistem yalnız TRY işlem desteklemeli; taksitli toplam da TRY olmalı.
- **FR-012**: Sistem, onaylı taksitli çekimde kaydettiği/store'a bildirdiği tutarı taksitli toplam (FinalAmount) olarak taşımalı.

### Key Entities *(include if feature involves data)*

- **Komisyon Oranı (öngörü read-model)**: Banka × şema (Visa/MC/Troy/Amex) × tip (credit/debit) × taksit → oran. Bankanın MDR'ının PG'deki salt-okur öngörü kopyası; başta seed, sonra banka-beslemeli. Güvenli varsayılan içerir.
- **Taksit Seçeneği (quote satırı)**: Bir taksit sayısı için seçilen banka + taksitli toplam + aylık tutar. Müşteriye gösterilir; seçilen satır çekimi bağlar.
- **Kart Ailesi (BIN çözümü)**: Issuer bankası + şema + tip — taksit/banka hesabının girdisi.
- **Ödeme oturumu (PaymentLink genişler)**: Seçilen taksit sayısı + hesaplanan taksitli toplam (FinalAmount) + baz tutar.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Müşteri BIN'i girdikten sonra taksit seçeneklerini 2 saniyenin altında görür.
- **SC-002**: Her taksit seçeneğinde gösterilen banka, o taksitte en düşük taksitli toplamı veren bankadır (%100).
- **SC-003**: Seçilen taksit seçeneğinde gösterilen banka ile çekilen banka %100 aynıdır (failover yok).
- **SC-004**: Gösterilen taksitli toplam ile çekilen tutar %100 aynıdır (Amount × (1+oran)).
- **SC-005**: En az bir test senaryosunda, farklı taksit sayılarında farklı banka en avantajlı çıkar (per-taksit seçim kanıtı).
- **SC-006**: Yetenek-dışı (kart tipi/işlem desteklemeyen) banka hiçbir taksit seçeneğinde görünmez (%100).

## Assumptions

- **Fiyatlama (kullanıcı kararı):** vade farkı müşteriye eklenir; taksitli toplam = Amount × (1 + ilgili oran); "peşin fiyatına taksit" DEĞİL.
- **Banka görünürlüğü (kullanıcı kararı — REVİZE 2026-10-23):** acquirer banka müşteriye GÖSTERİLMEZ (gerçek ödeme sayfalarında taksitin yanında acquirer banka görünmez; routing gizli). Önceki "bankayı göster" kararı geri alındı. Per-taksit en-avantajlı banka seçimi sunucuda sürer; değeri FİYATTA görünür (her taksitin toplamı en-ucuz bankaya göre). İstenirse kartın KENDİ bankası (issuer, BIN'den) kart alanının yanında gösterilebilir — ayrı, opsiyonel.
- **Failover (kullanıcı kararı, a):** taksit akışında çok-banka failover yok; gösterilen banka = çekilen; reddederse başarısız. Bu, 090 NonSecure çok-banka failover'ını taksit/bağlı-acquirer lehine değiştirir.
- **092 absorbe:** maliyet-bazlı routing bu feature'ın içinde (per-taksit efektif maliyet); ayrı 092 kapatılır/referanslanır.
- **Oran kaynağı:** öngörü read-model başta sabit seed'den; gerçek banka-beslemeli oran handoff (onboarding/mutabakat) sonraki dilim.
- **Bağımlılık (kapsam dışı):** BankSimulator'ın taksit + kart-ailesi komisyon paritesi + wire'da taksit kabulü ayrı repo işi; PG taksit sayısını gönderir, sim onurlandırmazsa demo'da izlenir.
- **Para birimi:** yalnız TRY (anayasa alan kısıtı).
- **Kart verisi:** İlke VI — transient, saklanmaz; BIN prefix lookup tam PAN'ı anahtar yapmaz.