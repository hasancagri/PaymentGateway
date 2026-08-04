# Feature Specification: Reference Data BC (Reference.Api) + Shared Card Taxonomy Kernel

**Feature Branch**: `010-reference-data-bc`

**Created**: 2026-08-03

**Status**: Draft

**Input**: User description: Country/City/MCC/Bank gibi statik referans verilerini tek kaynağa (Reference.Api) toplamak; iki serviste tekrar eden card taksonomi enum'larını (CardBrand/CardType) tek paylaşılan çekirdeğe (SharedKernel) indirgemek.

## Problem ve Bağlam

Bugün referans/katalog değerleri sistemde **dağınık ve kısmen kopyalı**:

- **Country / City / MCC** — Merchant.Api içinde kod-içi gömülü statik liste (`LookupData`, v1 stub: 1 ülke, 5 şehir, 6 MCC). Merchant onboarding doğrulamasında kullanılır. Büyütmek/yönetmek için kod değişikliği gerekir.
- **Bank** — iki yerde: Commission.Api'de `Bank` aggregate + Merchant.Api'de yerel `BankCatalog` kopyası. **Aynı gerçek iki kez modellenir.**
- **CardBrand / CardType** — **iki serviste birebir kopya enum**: Payment.Api (BinCards) ve Commission.Api (SharedKernel). Üstelik değerleri ıraksak (Payment `Visa=0`, CP.VPOS paritesi; Commission `VISA=1`, düz set). VPOS paritesi artık gerekli değil (VPOS üzerinden işlem yapılmayacak), yani ıraksama meşru bir domain farkı değil, tekrar borcu.

Bu dağınıklık: kod-içi veriyi büyütmeyi zorlaştırır, aynı kavramı iki kez bakımı gerektirir ve "aynı enum iki serviste" tekrarını doğurur.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Referans katalogları tek kaynaktan, tüketiciler yerel kopyayla okur (Priority: P1)

Platform sahibi olarak Country/City/MCC/Bank katalogunun **tek kaynak-of-truth**'a (Reference.Api) sahip olmasını; her tüketici bounded context'in bu veriyi **yerel bir kopya** üzerinden okuyup, onboarding gibi sıcak yollarda dış servise senkron bağımlı olmamasını istiyorum.

**Why this priority**: Feature'ın çekirdeği. Tek kaynak + yerel okuma, hem tekrarı bitirir hem de sıcak-yol availability riskini kaldırır. Bu olmadan diğer parçaların anlamı yok.

**Independent Test**: Reference.Api ayağa kalkar, katalog seed'i yüklenir; Merchant onboarding, geçerli bir ülke/şehir/MCC/banka kodu ile başarılı, geçersizle reddedilir — ve Reference.Api **kapalıyken bile** Merchant doğrulaması yerel kopyadan çalışmaya devam eder.

**Acceptance Scenarios**:

1. **Given** Reference.Api seed'li ve Merchant yerel kopyası güncel, **When** merchant "34" (İstanbul, TR) ile onboarding yapar, **Then** doğrulama geçer.
2. **Given** aynı durum, **When** merchant tanımsız bir şehir kodu gönderir, **Then** doğrulama Türkçe hata mesajıyla reddedilir.
3. **Given** Merchant yerel kopyası dolu, **When** Reference.Api erişilemez, **Then** onboarding doğrulaması yerel kopyadan çalışmaya devam eder (sıcak yolda senkron bağımlılık yok).
4. **Given** çapraz tutarlılık kuralı, **When** bir şehir kodu ait olmadığı ülkeyle gönderilir, **Then** reddedilir (mevcut `BelongsTo` davranışı korunur).

---

### User Story 2 - Card taksonomi enum'ları tek yerde tanımlı (Priority: P1)

Geliştirici olarak `CardBrand` ve `CardType`'ın **tek bir paylaşılan çekirdekte** (SharedKernel) tanımlı olmasını; Payment.Api ve Commission.Api'nin yerel kopyalarını silip ona referans vermesini; mevcut komisyon grid verisinin yeni kanonik değerlere **kayıpsız** taşınmasını istiyorum.

**Why this priority**: Kullanıcının doğrudan direktifi ("aynı enum iki serviste olmasın"). US1'den bağımsız teslim edilebilir; kendi başına tekrar borcunu kapatır.

**Independent Test**: SharedKernel enum'ları eklenir, iki servis ona referans verir, yerel kopyalar silinir; çözüm derlenir, mevcut komisyon grid'leri kanonik değerlerle okunur ve eski kayıtlar doğru markaya eşlenir.

**Acceptance Scenarios**:

1. **Given** SharedKernel kanonik `CardBrand`/`CardType` (Payment int seti kanonik), **When** Payment.Api ve Commission.Api derlenir, **Then** ikisi de SharedKernel'e referans verir, yerel enum dosyası kalmaz, çözüm 0 hata.
2. **Given** eski Commission grid'i `VISA=1` ile kaydedilmiş satırlar, **When** migrasyon çalışır, **Then** o satırlar kanonik `Visa=0`'a eşlenir ve grid sorguları doğru markayı döner.
3. **Given** kanonik enum, **When** BinCard verisi okunur, **Then** Payment tarafı değer değişmeden çalışır (kanonik = Payment seti).

---

### User Story 3 - Katalog büyütme ve güncelleme tek yerden yayılır (Priority: P2)

Operasyon/geliştirici olarak referans veriyi (tam iller, ISO ülke, tam MCC, tam banka listesi) **tek yerden** büyütüp güncelleyebilmeyi; değişikliğin tüketicilere otomatik yayılmasını istiyorum.

**Why this priority**: Tek-kaynağın asıl değeri burada; ama US1'in üstüne gelir (önce kaynak+kopya mekanizması olmalı).

**Independent Test**: Reference.Api'de bir katalog kaydı eklenir/güncellenir; tüketici yerel kopyası olay üzerinden güncellenir ve yeni değer doğrulamada kabul edilir.

**Acceptance Scenarios**:

1. **Given** Reference.Api'ye yeni bir şehir eklenir, **When** değişiklik yayılır, **Then** Merchant yerel kopyası kısa süre içinde yeni şehri tanır (eventual consistency).
2. **Given** büyük listeler (MCC/Bank), **When** liste okunur, **Then** sonuç sayfalı döner (tam liste tek yanıtta değil).

---

### User Story 4 - Taze tüketici boş katalogla açılmaz (bootstrap) (Priority: P2)

Platform sahibi olarak yeni başlatılan bir tüketici instance'ının **boş yerel kopyayla** açılıp her şeyi reddetmemesini; açılışta kataloğu bir kez toplu çekip sonra olaylarla güncel kalmasını istiyorum.

**Why this priority**: Olay-beslemeli read model'in bilinen boşluğu; olmadan taze instance kırılır. US1/US3'ün sağlamlığı buna bağlı.

**Independent Test**: Yerel kopyası boş bir tüketici başlatılır; bootstrap sonrası yerel kopya dolu olur ve doğrulama ilk istekte çalışır.

**Acceptance Scenarios**:

1. **Given** yerel kopyası boş taze tüketici, **When** başlatılır, **Then** açılış toplu çekimiyle (snapshot) yerel kopya dolar.
2. **Given** bootstrap tamamlandı, **When** sonradan katalog değişir, **Then** tüketici artık olaylarla güncellenir (tekrar toplu çekim gerekmez).

### Edge Cases

- Reference.Api hiç seed edilmemişken tüketici başlarsa? (Boş snapshot → doğrulama her şeyi reddeder; seed sırası tanımlı olmalı.)
- Bir güncelleme olayı kaçarsa / sırasız gelirse tüketici kopyası nasıl tutarlı kalır? (Çözüm: durable kuyruk + at-least-once redelivery + idempotent upsert; işlenemeyen mesaj DLQ. Bkz Q4.)
- Migrasyon sırasında Commission grid'inde kanonik sette **karşılığı olmayan** eski değer bulunursa? (Örn. eski bir marka; kayıp/uyumsuz veri nasıl işaretlenir.)
- Aynı kod hem eski hem yeni int'e denk gelirse migrasyon idempotent mi? (İki kez çalışırsa veriyi bozmamalı.)
- Merchant yerel kopyası ile Reference.Api arasında geçici tutarsızlık penceresinde onboarding olursa? (Eventual consistency kabul; katalog nadiren değişir.)

## Requirements *(mandatory)*

### Functional Requirements

**Reference.Api (kaynak-of-truth)**

- **FR-001**: Sistem, Country/City/MCC/Bank referans verisini **tek bir bounded context'te** (Reference.Api) kaynak-of-truth olarak tutMALIdır; bu veri başka hiçbir serviste ayrıca yazılabilir kaynak olarak modellenMEMELİdir.
- **FR-002**: Reference.Api katalog verisini açılışta bilinen bir başlangıç setinden **seed** etMELİdir (dış POS kütüphanesine bağımlılık olmadan).
- **FR-003**: Reference.Api her katalog türü için okuma ucu sağlaMALIdır: ülke listesi, şehir listesi (ülkeye göre filtrelenebilir), MCC listesi, banka listesi.
- **FR-004**: Büyük listeler (MCC, Bank) **sayfalı** döndürülMELİdir; tam liste tek yanıta sığdırılmaz.
- **FR-005**: Reference.Api, katalog verisi değiştiğinde (v1'de seed/yeniden-seed) tüketicilerin güncellenebilmesi için bir **değişiklik olayı** (`ReferenceDataUpdated`) yayınlaMALIdır. (Runtime yönetim CRUD + Admin UI bu sürümde **kapsam dışı**, sonraki spec.)
- **FR-006**: Reference.Api, taze bir tüketicinin açılışta kataloğu toplu çekebilmesi için bir **anlık-görüntü (snapshot)** okuması sağlaMALIdır.

**Tüketici yerel kopya (read model)**

- **FR-007**: Referans veriyi tüketen her bounded context, doğrulamayı **kendi yerel kopyası** üzerinden yapMALIdır; sıcak yolda (onboarding doğrulaması) Reference.Api'ye **senkron bağımlı olmaMALIdır**.
- **FR-008**: Tüketici yerel kopyası, Reference.Api değişiklik olaylarıyla güncel tutulMALIdır (eventual consistency kabul edilir).
- **FR-008a**: Olay tüketimi **idempotent upsert** olMALIdır; aynı olay birden çok kez işlense bile yerel kopya bozulMAZ (at-least-once teslimat varsayımı).
- **FR-008b**: Tüketici, kısa süreli çevrimdışıyken olayları kaybetmemek için **dayanıklı (durable) kuyruk** kullanMALIdır; başarısız işlem sınırlı retry sonrası **DLQ**'ya düşMELİdir (sessizce yutulmaz).
- **FR-008c**: Reference.Api olay yayını **publish-then-save** sırasını izleMELİ ve yalnız **değişen** kayıtları yaymaLIdır (diff); çökmede kayıp yerine tekrar tercih edilir.
- **FR-009**: Tüketici açılışta yerel kopyası boşsa snapshot ile doldurMALIdır (boş-katalog başlangıcı doğrulamayı yanlışlıkla kilitlemez).
- **FR-010**: Merchant.Api'deki mevcut doğrulama arabirimleri (ülke/şehir/MCC/banka var-mı + şehir-ülke çapraz kontrol) **davranışça korunMALIdır**; tüketici iş kodu (onboarding) değişmeMELİdir.
- **FR-011**: Merchant.Api'deki kod-içi gömülü Country/City/MCC verisi ve yerel Bank kataloğu kopyası **kaldırılMALIdır**; yerini Reference.Api-beslemeli yerel kopya alır.

**Bank konsolidasyonu (Commission de tüketici)**

- **FR-011a**: Commission.Api'deki yerel banka **kataloğu** (code→ad) kaldırılMALI; banka adı/varlığı Reference.Api-beslemeli yerel read model'den doğrulanMALIdır.
- **FR-011b**: Bankanın **komisyon-özel** öznitelikleri (desteklenen taksitler, aktiflik) Commission'da **kalMALIdır**; banka koduyla anahtarlanır ve komisyon grid'inin filtre kümesi olmaya devam eder.
- **FR-011c**: Komisyon grid'i banka **koduyla** anahtarlı kalMALIdır (bu anahtar migrasyona tabi değildir); yalnız banka adı/varlık doğrulamasının kaynağı yerel katalogdan Reference-beslemeli read model'e geçer.

**SharedKernel (card taksonomi)**

- **FR-012**: `CardBrand` ve `CardType` **tek bir paylaşılan çekirdekte** tanımlanMALIdır; Payment.Api ve Commission.Api yerel kopyalarını kaldırıp buna referans verMELİdir.
- **FR-013**: Kanonik enum değer seti **Payment setine** eşit olMALIdır (Payment tarafı değer değiştirmeden çalışır).
- **FR-014**: Commission.Api'nin kalıcı komisyon grid verisi, eski enum değerlerinden **kanonik değerlere kayıpsız migrate** edilMELİdir; migrasyon idempotent olMALIdır (tekrar çalışınca veriyi bozmaz).
- **FR-015**: Migrasyon sırasında kanonik sette karşılığı olmayan eski bir değere rastlanırsa, bu durum **sessizce yutulMAMALIdır** (işaretlenir/raporlanır).

**Kapsam dışı bırakılanlar (invariant korunur)**

- **FR-016**: Aggregate davranışsal durum enum'ları (`MerchantStatus`, `SettlementAccountStatus`, `PaymentSessionStatus`) taşınMAMALIdır; ait oldukları bounded context'te kalır.
- **FR-017**: Dış POS kütüphanesinin (CP.VPOS) kendi enum'ları değiştirilMEMELİ ve slice sınırını geçirilMEMELİdir.

### Key Entities *(include if feature involves data)*

- **Country (referans)**: Ülke kodu → ad (ör. "TR" → "Türkiye"). Kaynak-of-truth Reference.Api.
- **City (referans)**: Şehir kodu → ad + bağlı olduğu ülke kodu (ör. "34" → "İstanbul", "TR"). Ülke ile çapraz tutarlılık taşır.
- **MCC (referans)**: Merchant Category Code → ad (ör. "5411" → "Grocery Stores"). Büyük liste.
- **Bank katalog (referans)**: Banka kodu → ad. Bugün Commission `Bank` aggregate (code→ad kısmı) + Merchant `BankCatalog` + Commission `BankCatalog` olarak kopyalı; code→ad kaynak-of-truth Reference.Api'ye taşınır.
- **Bank komisyon katılımı (Commission'da kalır)**: Banka koduyla anahtarlı; desteklenen taksitler + aktiflik. Reference kataloğuna değil, Commission'ın komisyon modeline aittir; adı/varlığı yerel read model'den doğrular.
- **Tüketici yerel kopyası (read model)**: Tüketici BC'sinin kendi deposundaki referans veri izdüşümü; olaylarla güncellenir, doğrulama bunu okur.
- **CardBrand (taksonomi enum)**: Kart markası (Visa/MasterCard/Troy/Amex/…); paylaşılan çekirdekte tek tanım.
- **CardType (taksonomi enum)**: Kart tipi (kredi/banka/…); paylaşılan çekirdekte tek tanım.

## Clarifications

### Session 2026-08-03

- **Q1: Commission.Api'nin `Bank` aggregate'i, Reference.Api Bank kaynak-of-truth olduktan sonra ne olacak?**
  **Karar (Option B):** Bank tam olarak Reference.Api'ye taşınır; Commission da tüketici olur ("yapacaksak en baştan yapalım"). Ancak `Bank` saf referans değildir — `SupportedInstallments` (taksit desteği) + aktiflik gibi **komisyon-özel** öznitelik taşır. Bu yüzden Bank **ikiye ayrılır**:
  - **code→ad katalog gerçeği** kaynak-of-truth = Reference.Api. Hem Commission hem Merchant kendi yerel `BankCatalog` (code→ad) kopyasını **kaldırır** ve Reference-beslemeli yerel read model okur.
  - **komisyon-özel öznitelikler** (`SupportedInstallments`, aktiflik) Commission'da **kalır**, banka koduyla anahtarlanır. Commission artık banka **adını/varlığını** kendi kataloğundan değil, Reference-beslemeli yerel read model'den doğrular.
  - Komisyon grid'i banka **koduyla** anahtarlı (int enum değil) → kod stabil, bu yüzden Bank tarafında ağır veri remap'i yoktur (CardBrand int migrasyonundan düşük riskli).

- **Q4: Tüketici read model'i nasıl senkron tutulur (ilk doldurma + kaçan olay kurtarma)?** → A: **ECommerce-hizalı** (Option A). Referans mimari 007 ingestion deseni: **publish-then-save** (önce yayınla sonra kaydet, çökmede kayıp yerine redelivery), **at-least-once + idempotent upsert** (aynı olay iki kez gelse bozulmaz), üretici-tarafı **diff yayını** (yalnız yeni/değişen kayıt), **durable kuyruk**, sınırlı retry tükenince **DLQ**. Drift'i periyodik resync değil idempotency+redelivery çözer. Bootstrap: taze tüketici açılışta bir kez snapshot çeker (boot-zamanı, sıcak yol değil); sıcak yol (doğrulama) daima yerel.

- **Q3: `Bank.SupportedInstallments` Reference kataloğuna mı taşınır, Commission'da mı kalır?** → A: **Commission'da kalır** (Option A). Gerekçe: komisyon grid anahtarı `Criteria` zaten `InstallmentCount` eksenini taşır (`Bank + CardBrand + CardType + TransactionRegion + InstallmentCount → Rate`); `SupportedInstallments` bu ekseni bağlayan filtre kümesidir, komisyon grid mantığına sıkı bağlı — evrensel banka kataloğu bilgisi değil. Reference.Api yalnız **code→ad** kataloğuna sahip olur. Banka koduyla anahtarlı komisyon-özel öznitelik Commission'da kalır.

- **Q2: Reference.Api bu sürümde katalog yönetim yüzeyi sunacak mı?** → A: **Seed + salt-okuma** (Option A). v1'de yazma yüzeyi yok; katalog embedded JSON seed'inden dolar, büyütme = seed genişletme. `ReferenceDataUpdated` olayı yine kurulur (seed/yeniden-seed'de yayılır) ki tüketici read-model deseni baştan otursun. Yönetim CRUD + Admin UI sonraki spec'e ertelenir.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: `CardBrand`/`CardType` sistemde **tam olarak bir kez** tanımlı olur; hiçbir iki serviste kopya enum kalmaz (kaynak taramasıyla doğrulanabilir).
- **SC-002**: Country/City/MCC ve Bank **code→ad** kataloğu için **yazılabilir kaynak tek bir servistir** (Reference.Api); Merchant.Api'deki gömülü liste ve **hem Merchant hem Commission**'daki yerel `BankCatalog` (code→ad) kopyaları kaldırılmıştır.
- **SC-002a**: Commission komisyon grid'i konsolidasyon sonrası çalışmaya devam eder; banka kod anahtarları değişmez, banka adı/varlığı Reference-beslemeli read model'den doğrulanır.
- **SC-003**: Merchant onboarding doğrulaması, Reference.Api **erişilemez olsa bile** yerel kopyadan çalışır (sıcak yolda kesinti yok).
- **SC-004**: Mevcut komisyon grid verisi migrasyon sonrası **%100 doğru markaya** eşlenir; hiçbir grid satırı yanlış markaya kaymaz.
- **SC-005**: Taze başlatılan bir tüketici, ilk doğrulama isteğinde dolu katalogla yanıt verir (bootstrap boş-katalog reddi yaşanmaz).
- **SC-006**: Referans veri tek yerden büyütülebilir; yeni bir katalog kaydı eklendiğinde tüketici kopyaları kod değişikliği olmadan güncellenir.
- **SC-007**: Merchant onboarding doğrulamasını çağıran mevcut iş kodu değişmez (arabirim aynı kalır).

## Assumptions

- Referans veri **nadiren** değişir; tüketiciler ile kaynak arasında eventual consistency kabul edilebilir (anlık tutarlılık gerekmez, bu yüzden senkron gRPC gerekmez).
- Reference.Api diğer BC'lerle **aynı mimari deseni** izler (kendi Postgres şeması, document/event store, vertical slice + CQRS, Aspire ile ayağa kalkma) — anayasa İlke I–III gereği.
- Card markası/tipi **evrensel, stabil bir sözcük**tür (Visa/MasterCard/…) ve paylaşılan çekirdekte co-owned bir kontrat olarak tutulması bounded context izolasyonunu ihlal etmez (bilinçli paylaşılan sözleşme).
- Yetkilendirme bu feature'da **kapsam dışı** (proje geneli erteleme; anayasa TODO(AUTHZ_MODEL)).
- Seed başlangıç seti gömülü bir veri kaynağından gelir; dış POS kütüphanesi (CP.VPOS) seed bağımlılığı **yoktur** (008 deseni).
- Cache gerekirse tüketici okuma sorgusuna declarative bir aspect ile eklenir; bu feature elle dağıtık-cache (Redis) altyapısı **kurmaz** (read model sıcak yol için yeterli; cache ayrı/ortogonal karar).
- İlk sürümde tek doğrulayan tüketici **Merchant.Api**'dir; başka tüketiciler (ör. Payment/Commission katalog ihtiyacı) sonraki feature'larda aynı read-model desenini kullanır.