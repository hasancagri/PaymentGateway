# Feature Specification: Iyzico Payment Channel — Yapısal Eritme

**Feature Branch**: `022-iyzico-payment-channel`

**Created**: 2026-08-13

**Status**: Draft

**Input**: User description: "Payment BC'yi iyzico ödeme kanalına döndür ve legacy POS altyapısını sök. CP.VPOS silinir; BankRouter, PosAccount, BinCard tamamen sökülür. Iyzipay SDK'sı ayrı proje olarak kalmaz — gerekli sınıflar CLAUDE.md yapısına uygun biçimde Payment BC içine eritilir, kullanılmayanlar silinir; 'Iyzipay/Iyzico' proje adı yaşamaz. StoredCard/CardVault korunur. SubMerchant 023, komisyon 024."

**Kapsam netleştirmesi (kullanıcı, 2026-08-13)**: "Şu an için tek amacım kodu Solution
içerisine yedirmek" + "canlı ortamda test yapmaya gerek yok" — bu spec YAPISAL eritmedir:
çalışan uçtan-uca ödeme akışı, sandbox doğrulaması ve canlı test HEDEF DEĞİL. Kapanış
ölçütü derleme + kalıntı taramasıdır. Akışın iyzico ile gerçek çalışır hâle getirilmesi
(quote/charge canlı) sonraki iştir.

**Kapsam güncellemesi 2 (kullanıcı, 2026-08-13, plan öncesi)**: Kullanıcı üç BC'nin
(Payment, Merchant, Commission) `Domains/` içeriğini ELLE SİLDİ — eski aggregate/slice'lar
geri gelmez. Talimat: "Iyzico içerisindeki yapıları buralara ekleyeceğiz" — SDK yapıları
yalnız Payment'a değil, sorumluluk alanına göre ÜÇ BC'ye dağıtılır (ödeme/taksit/saklı
kart → Payment; SubMerchant → Merchant; payout/işlem raporları → Commission). Eski BC
testleri ölü aggregate'leri test ettiğinden test projeleri de silinir; StoredCard
aggregate'i kullanıcı silmiştir — 017 "korunur" maddesi CardVault klasörü (PAN koruma
altyapısı) ile sınırlıdır.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Legacy POS ekseni sökülür (Priority: P1)

Geliştirici çözümde eski sanal POS eksenini görmez: CP.VPOS kütüphanesi, BankRouter,
PosAccount ve BinCard kataloğu — seed verisi, Admin ekranları ve testleriyle — sistemden
çıkmıştır. Banka seçimi kavramı kalmaz (tek kanal iyzico).

**Why this priority**: Pivotun özü; eski eksen durdukça SDK eritmesi onun üzerine biner.

**Independent Test**: Kaynak taramasında CP.VPOS/BankRouter/PosAccount/BinCard kalıntısı
0; çözüm sıfır hatayla derlenir.

**Acceptance Scenarios**:

1. **Given** söküm sonrası çalışma kopyası, **When** `dotnet build` çalıştırılır,
   **Then** çözüm sıfır hatayla derlenir ve CP.VPOS çözümde/diskte yoktur.
2. **Given** kaynak ağacı, **When** eski eksen aranır, **Then** BankRouter, PosAccount,
   BinCard tipleri, BIN seed verisi ve bunların Admin ekranları/testleri bulunmaz.

---

### User Story 2 - Sağlayıcı istemcisi Payment BC içinde erir (Priority: P2)

Geliştirici iyzico ile konuşan kodu Payment BC'nin kendi yapısında bulur: ayrı "Iyzipay"
projesi yoktur; yalnız gereken istemci sınıfları (ödeme çekimi + taksit sorgusu işlemleri
ve modelleri) BC-içi sağlayıcı adaptör katmanında, BC'nin adlandırma/yapı kurallarına
uygun biçimde yaşar. Kullanılmayan SDK yüzeyi (abonelik, link ödemeleri vb. modüller),
örnek proje ve tüm SDK test projeleri silinmiştir.

**Why this priority**: Kullanıcı kararı — dış SDK adası ve "Iyzipay/Iyzico" proje adı
istenmiyor; kod sahipliği BC'ye geçer.

**Independent Test**: Çözümde Iyzipay/Iyzipay.Samples/Iyzipay.Tests/Iyzipay.Tests.Functional
projeleri yoktur; adaptör katmanı Payment BC içindedir ve çözüm derlenir.

**Acceptance Scenarios**:

1. **Given** eritme sonrası çözüm, **When** proje listesine bakılır, **Then** dört
   Iyzipay* projesi de yoktur ve diskte kalıntıları bulunmaz.
2. **Given** Payment BC kaynağı, **When** adaptör katmanına bakılır, **Then** yalnız
   kullanılan işlemler (ödeme çekimi, taksit sorgusu) ve onların modelleri vardır;
   kullanılmayan SDK modülleri yoktur.
3. **Given** Payment BC dışındaki projeler, **When** sağlayıcı tipleri aranır, **Then**
   hiçbir dış proje sağlayıcı tiplerine referans vermez (CP.VPOS sınır kuralının devamı).

---

### User Story 3 - Payment BC yüzeyi yeni eksene göre derlenir (Priority: P3)

Geliştirici Payment BC'nin kalan yüzeyini (ödeme oturumu, saklı kart) eski eksene
referanssız ve derlenir hâlde bulur: BankRouter/BinCard'a dayanan parçalar ya sağlayıcı
adaptörüne bağlanmış ya da sökülmüştür. Saklı kart (vault) yapısı korunur. Çalışan ödeme
akışı bu spec'in hedefi değildir — kod bütünlüğü hedeftir.

**Why this priority**: Eritmenin kapanışı; çözüm bütünlüğü olmadan söküm tamam sayılmaz.

**Independent Test**: `dotnet build` sıfır hata; kalan test projeleri yeşil; PaymentSession
ve StoredCard tipleri eski eksen tiplerine 0 referans verir.

**Acceptance Scenarios**:

1. **Given** eritme sonrası çözüm, **When** derleme + test koşulur, **Then** sıfır hata
   ve kalan testlerin tamamı yeşildir.
2. **Given** Payment BC kaynağı, **When** ödeme oturumu ve saklı kart kodu incelenir,
   **Then** BankRouter/PosAccount/BinCard'a referans yoktur; saklı kart yapısı durur.

---

### Edge Cases

- Eski eksene bağlı Payment slice'ları (banka-aday teklifi, BIN çözümü vb.): adaptöre
  bağlanamayanlar SİLİNİR (Merchant 021 emsali — 023+ yeniden kurar); yarım/kırık slice
  bırakılmaz.
- Payment.Agent MCP tool'ları eski eksene dayanıyorsa aynı kurala tabidir: derlenmeyen/
  anlamsızlaşan tool sökülür; agent projesi derlenir kalır.
- SDK'nın kendi deterministik testleri proje ile birlikte gider; adaptöre yeni test yazma
  zorunluluğu yoktur (canlı doğrulama ileride, akış işi ele alındığında).
- Kart taksonomisi paylaşılan çekirdeği: Payment tüketimi BIN kataloğuyla ölür; başka BC
  kullanımı sürüyorsa tip korunur (karar planlamada).
- Sağlayıcı kimlik bilgisi alanları (anahtar/sır/adres) yapılandırma iskeleti olarak
  konabilir; sır depoya girmez. Anahtar yokluğu bu spec'te sorun değildir (akış
  çalıştırılmıyor).
- Eski PaymentSession kayıtları dev DB sıfırlamasıyla gider; migration yok.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: CP.VPOS projesi, çözüm/AppHost/gitignore izleri ve tüm referansları dahil
  silinmelidir.
- **FR-002**: BankRouter, PosAccount ve BinCard ekseni bütünüyle sökülmelidir: tipler,
  seed verisi, içe aktarma/çözümleme uçları, Admin ekranları ve testleri.
- **FR-003**: Dört Iyzipay* projesi (kütüphane, Samples, iki test) çözümden ve diskten
  kalkmalı; yalnız kullanılan sağlayıcı işlemleri (ödeme çekimi, taksit sorgusu) ve
  modelleri Payment BC içindeki adaptör katmanına, BC adlandırma/yapı kurallarıyla
  taşınmalıdır. "Iyzipay/Iyzico" adında proje kalmamalıdır.
- **FR-004**: Sağlayıcı tipleri Payment BC'nin slice sınırını geçmemelidir; hiçbir dış
  proje sağlayıcı tiplerine referans vermez.
- **FR-005**: Eski eksene bağımlı Payment slice'ları ve agent tool'ları ya adaptör
  temelinde derlenir hâle getirilmeli ya da sökülmelidir; kırık/yarım kod bırakılmaz.
- **FR-006**: Saklı kart (StoredCard/vault) yapısı korunmalıdır; kart verisi sızıntısı
  kuralları değişmez.
- **FR-007**: Sağlayıcı yapılandırması (anahtar/sır/adres) strongly-typed config
  iskeletiyle tanımlanabilir; depoya sır girmez. Çalışır akış ve canlı doğrulama bu
  spec'in kapsamı DIŞIDIR.
- **FR-008**: Sonuçta çözüm sıfır hatayla derlenmeli, kalan tüm test projeleri yeşil
  kalmalı ve kalıntı taraması (eski eksen + silinen SDK modülleri + Iyzipay proje adları)
  0 sonuç vermelidir.

### Key Entities

- **Sağlayıcı adaptörü (yeni, BC-içi)**: iyzico istemci kodunun eritilmiş hâli — ödeme
  çekimi + taksit sorgusu işlemleri ve modelleri; BC dışına sızmaz.
- **PaymentSession (sadeleşen)**: banka-aday ekseni referansları temizlenir; kalan yapı
  derlenir. Akışın yeniden çalışır kılınması sonraki iş.
- **StoredCard/Vault (korunan)**: dokunulmaz.
- **Silinen**: CP.VPOS, BankRouter, PosAccount, BinCard (+seed +uçlar +ekranlar +testler),
  Iyzipay* projeleri ve kullanılmayan SDK modülleri.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Tek derleme komutu sıfır hatayla biter; çözümde CP.VPOS ve Iyzipay* proje
  sayısı 0'dır.
- **SC-002**: Kalıntı taraması (BankRouter/PosAccount/BinCard/CP.VPOS/Iyzipay proje
  adları) spec artefaktları hariç 0 sonuç döner.
- **SC-003**: Kalan tüm test projeleri yeşildir.
- **SC-004**: Sağlayıcı tipleri Payment BC dışındaki projelerde 0 referans alır.

## Assumptions

- ÇALIŞIR ödeme akışı, sandbox/canlı doğrulama, 3D, iade/iptal, SubMerchant (023) ve
  komisyon modeli (024) kapsam DIŞI. Bu spec sonrası Payment BC "derlenir ama uçtan uca
  akış bağlanmamış" ara durumda olabilir — bilinçli (021 Merchant emsali).
- Adaptör katmanının BC-içi konumu/adları planlamada CLAUDE.md kurallarına göre netleşir;
  şart: ayrı proje yok, sınır sızmaz, "Iyzipay/Iyzico" proje adı yok.
- Hangi SDK sınıflarının "kullanılan" sayılacağı planlamada netleşir; çekirdek = non-3D
  satış (ödeme çekimi) + taksit sorgusu + bunların zorunlu bağımlılıkları.
- Dev aşaması: veri migration yok, DB sıfırlama serbest; kalıcı kayıt uyumu aranmaz.