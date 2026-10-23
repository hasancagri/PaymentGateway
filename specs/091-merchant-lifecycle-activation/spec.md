# Feature Specification: Store Lifecycle / Activation

**Feature Branch**: `pivot/bank-adapter-skeleton`

**Created**: 2026-10-22

**Status**: Draft

**Input**: User description: "091 Store Lifecycle / Activation — super-admin EC store'unu internet satışına açar; PG store'u bankalarda onboard eder, ilk banka onayında canlı"

## Özet

Bir store'un (ör. ECommerceAgent sitesi) PG üzerinden gerçek kart ödemesi kabul edebilmesi için
önce bankalarda bir merchant hesabı açması gerekir. Bu feature, super-admin'in store'u internet
satışına açma sürecini tanımlar: store profili toplanır, PG bunu her bankaya başvuru olarak gönderir,
banka kendi tarafında inceler, onay sonucu PG'ye imzalı webhook ile döner ve **ilk banka onayında**
store canlı satışa geçer. PG komisyon almaz; banka farkları adapter'ların ardında gizlenir.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Store başvurusunu başlat ve profili topla (Priority: P1)

Super-admin, ECommerce tarafından PG'ye bir aktivasyon talimatı gönderir. PG bir onboarding kaydı
açar ve tek kullanımlık, süreli bir link döner. Store, bu link üzerinden PG'nin tek sabit hosted
formunu doldurur (yasal ünvan, vergi no, settlement IBAN, işkolu/MCC, web sitesi, yetkili kişi).
Form gönderilince başvuru, yapılandırılmış tüm bankalara iletilecek hâle gelir.

**Why this priority**: Başvuru verisi toplanmadan hiçbir banka onayı mümkün değil; bu, tüm sürecin
giriş kapısı ve bağımsız değer üreten ilk dilim (store artık "başvurdu" durumunda).

**Independent Test**: Talimat gönderilir, link alınır, form doldurulup gönderilir; sistemde ilgili
store için "başvuru hazır / bankalara iletildi" durumu ve toplanan profil doğrulanır.

**Acceptance Scenarios**:

1. **Given** sistemde kayıtlı bir store yok, **When** super-admin aktivasyon talimatı gönderir, **Then** bir onboarding kaydı oluşur ve tek kullanımlık süreli bir link üretilir.
2. **Given** geçerli bir onboarding linki, **When** store tüm zorunlu profil alanlarını doldurup gönderir, **Then** başvuru kaydedilir ve yapılandırılmış her banka için birer başvuru (Referred) durumu oluşur.
3. **Given** eksik/geçersiz zorunlu alan, **When** form gönderilir, **Then** gönderim reddedilir ve hangi alanın hatalı olduğu bildirilir; başvuru bankalara iletilmez.
4. **Given** link süresi dolmuş veya daha önce kullanılmış, **When** store linki açar, **Then** erişim reddedilir.

---

### User Story 2 - İlk banka onayında store canlı (Priority: P1)

Başvuru bankalara iletildikten sonra her banka kendi tarafında inceler. Bir banka onayladığında,
sonuç PG'ye imzalı bir bildirimle (webhook) döner; onaya ait banka credential'ı güvenli biçimde
saklanır ve o banka için başvuru "Onaylı" olur. **İlk onay geldiği anda** store canlı satışa geçer;
sonradan onaylanan bankalar kademeli olarak eklenir. Store canlı olunca gerçek kart çekimi (089/090)
devreye girer ve geçici "sağlayıcı yok" durumu sona erer.

**Why this priority**: Feature'ın asıl değeri burada doğar — store gerçek satış yapabilir hâle gelir.
US1 olmadan test edilemez ama birlikte MVP'yi tamamlar.

**Independent Test**: Bir banka onayı bildirimi geldiğinde, o bankanın başvurusu "Onaylı"ya döner,
store "Canlı" olur ve store'a "canlı oldun" bildirimi gider; credential hiçbir dönüşte/logda görünmez.

**Acceptance Scenarios**:

1. **Given** store'un bankalara iletilmiş başvurusu var ve henüz onay yok, **When** bir bankadan onay bildirimi gelir, **Then** o banka "Onaylı" olur, store "Canlı" olur ve store'a aktivasyon bildirimi iletilir.
2. **Given** store zaten bir bankadan onayla canlı, **When** ikinci bankadan onay gelir, **Then** o banka da "Onaylı" eklenir, store "Canlı" kalır (çift aktivasyon tetiklenmez).
3. **Given** store canlı, **When** gerçek bir ödeme başlatılır, **Then** ödeme onaylı bankalardan biriyle işlenir (geçici "sağlayıcı yok" dönmez).
4. **Given** bir onay bildirimi geldi, **When** herhangi bir dönüş/log/bildirim üretilir, **Then** banka credential'ı bunların hiçbirinde görünmez (yalnız güvenli saklamada).

---

### User Story 3 - Banka reddi ve yeniden başvuru (Priority: P2)

Bir banka başvuruyu reddederse (eksik evrak, uygunsuzluk vb.), red sebebi PG'ye bildirilir ve o
banka için başvuru "Reddedildi" olur. Store, başka bankalardan onay almışsa canlı kalır. Super-admin,
reddeden bankaya düzeltilmiş bilgiyle yeniden başvurabilir.

**Why this priority**: Gerçek senaryoda red olağandır; süreci kilitlememek için gerekir ama MVP'nin
canlıya çıkışı için şart değil.

**Independent Test**: Red bildirimi geldiğinde o bankanın başvurusu "Reddedildi" + sebep taşır;
yeniden başvuru o bankayı tekrar "Referred"a çeker; diğer bankaların durumu etkilenmez.

**Acceptance Scenarios**:

1. **Given** bir bankaya iletilmiş başvuru, **When** bankadan red bildirimi gelir, **Then** o banka "Reddedildi" olur ve red sebebi kaydedilir; diğer bankalar etkilenmez.
2. **Given** bir banka "Reddedildi", **When** super-admin o bankaya yeniden başvurur, **Then** o banka tekrar "Referred" olur ve yeni başvuru iletilir.
3. **Given** tüm bankalar reddetti ve hiç onay yok, **When** durum sorgulanır, **Then** store "Canlı değil" ve her bankanın red sebebi görünür.

---

### User Story 4 - Kaçan sonuçların mutabakatı (Priority: P3)

Bir banka sonucu bildirimi kaybolur/gecikirse, PG kendi dayanıklı süreciyle banka tarafındaki güncel
başvuru durumunu periyodik olarak yoklar ve kaçan onay/red'i yakalayıp store durumunu düzeltir.

**Why this priority**: Dayanıklılık güvencesi; mutlu-yol onsuz da çalışır, ama kayıp bildirim store'u
sonsuza dek beklemede bırakmamalı.

**Independent Test**: Bir banka onayı bildirimi hiç ulaşmamışken, mutabakat tetiklenince o bankanın
durumu banka tarafıyla hizalanır (onaylıysa store canlı olur).

**Acceptance Scenarios**:

1. **Given** bir banka onayladı ama bildirim PG'ye ulaşmadı, **When** mutabakat çalışır, **Then** o banka "Onaylı"ya çekilir ve gerekirse store canlı olur.
2. **Given** banka tarafı hâlâ "inceleme sürüyor", **When** mutabakat çalışır, **Then** durum değişmez (erken terminal karar verilmez).

---

### Edge Cases

- Aynı store için zaten canlı/başvurusu sürerken yeni aktivasyon talimatı gelirse ne olur? (Tekrarlı başvuru engellenir/var olan sürece yönlendirilir.)
- Bir banka, PG'nin sabit formunda olmayan bir zorunlu alan isterse ne olur? (O banka başvuru-öncesi reddedilir; form büyütülmez, store diğer bankalarda devam eder.)
- Onay bildirimi geçersiz/eşleşmeyen imza veya yanlış token ile gelirse? (Reddedilir, durum değişmez.)
- Aynı onay bildirimi iki kez gelirse? (İkincisi etkisiz — tekrar aktivasyon/çift kayıt olmaz.)
- Banka sonucu "belirsiz/zaman aşımı" ise? (Onay/red sayılmaz; mutabakatla netleşene dek beklemede kalır.)

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sistem, yetkili bir dış çağrıyla (ECommerce S2S) bir store için onboarding sürecini başlatabilMELİ ve her sürece bir correlation kimliği atamalı.
- **FR-002**: Sistem, başlatılan her süreç için tek kullanımlık, süreli (TTL) bir form linki üretMELİ; link süresi dolduğunda veya bir kez kullanıldıktan sonra erişimi reddetmeli.
- **FR-003**: Sistem, tek ve sabit bir hosted form üzerinden ortak çekirdek store profilini (yasal ünvan, vergi no, settlement IBAN, MCC/işkolu, web sitesi, yetkili kişi) toplamalı; banka başına ayrı ekran SUNMAMALI.
- **FR-004**: Sistem, form gönderiminde zorunlu alanları doğrulaMALI; eksik/geçersizse gönderimi reddedip hatalı alanı bildirmeli ve başvuruyu bankalara iletMEMELİ.
- **FR-005**: Sistem, geçerli başvuruyu yapılandırılmış her bankaya ayrı başvuru olarak iletMELİ (banka başına durum izlenir); banka farklılıkları çağıran katmana sızDIRILMAMALI.
- **FR-006**: Sistem, bir bankanın gerektirdiği ve ortak çekirdekte bulunmayan zorunlu bir alan varsa o bankaya başvuruyu başvuru-öncesi reddedip sebebini kaydetMELİ; diğer bankaları etkilememelidir.
- **FR-007**: Sistem, banka sonuçlarını (onay/red) banka başlatımlı, imzalı ve doğrulanabilir bir bildirimle (webhook) karşılaMALI; imza/token eşleşmeyen bildirimi reddetmeli.
- **FR-008**: Sistem, bir onay bildiriminde gelen banka credential'ını yalnız güvenli saklamada tutMALI; credential hiçbir API dönüşünde, logda, izde veya bildirimde GÖRÜNMEMELİ.
- **FR-009**: Sistem, ilk banka onayı geldiğinde store'u "Canlı" duruma geçirMELİ; sonraki onaylar store'u canlı tutmalı ve çift aktivasyon tetiklememelidir.
- **FR-010**: Sistem, store "Canlı" olduğunda gerçek kart çekiminin onaylı bankalarla yapılmasını mümkün kılMALI (geçici "sağlayıcı yok" durumu sona erer).
- **FR-011**: Sistem, bir banka reddinde o bankanın durumunu red sebebiyle kaydetMELİ ve super-admin'in o bankaya yeniden başvurmasına izin vermeli.
- **FR-012**: Sistem, kaçan/geç kalan banka sonuçları için dayanıklı bir mutabakat süreciyle banka tarafındaki güncel durumu yoklayıp drift'i düzeltMELİ; "belirsiz/sürüyor" durumda erken terminal karar vermemeli.
- **FR-013**: Sistem, çağırana yalnız opak bir süreç tutamacı + özet durum döndürMELİ (credential/sır içermez); store'a aktivasyon sonucunu imzalı bildirimle iletmeli.
- **FR-014**: Sistem, aynı store için zaten aktif bir süreç/canlı durum varken gelen yeni aktivasyon talimatını tekrarlı başvuru olarak ele almalı (var olana yönlendirir veya reddeder), çift kayıt üretmemeli.
- **FR-015**: Sistem herhangi bir agent/MCP yüzeyi expose etMEMELİ; aktivasyon akışı yalnız S2S + hosted form + webhook üzerinden yürümeli.

### Key Entities *(include if feature involves data)*

- **Store Onboarding**: Bir store'un aktivasyon sürecinin tutarlılık sınırı; genel durum (Taslak/Başvuruldu/Canlı) + banka başına başvuru koleksiyonu + correlation kimliğini taşır.
- **Bank Application (per store-bank)**: Belirli bir (store, banka) çifti için başvuru durumu (Referred/Onaylı/Reddedildi), red sebebi ve onaylıysa saklı credential referansı.
- **Store Profile**: Forma girilen ortak çekirdek bilgiler (ünvan, vergi no, IBAN, MCC, URL, yetkili); banka başvurularının ortak kaynağı.
- **Bank Credential**: Banka onayında inen, store'un o bankadaki hesabını temsil eden sır; yalnız güvenli saklamada, opak referansla anılır.
- **Onboarding Session/Link**: Formu açan tek kullanımlık, TTL'li ve per-session gizli token'lı erişim bağlamı.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Super-admin, talimattan form gönderimine kadar olan adımı 5 dakikanın altında tamamlayabilir.
- **SC-002**: Store, en az bir banka onayı geldikten sonra 1 dakika içinde canlı duruma geçer ve gerçek ödeme kabul edebilir.
- **SC-003**: Onaylanan bir store için banka credential'ı, sistemin ürettiği hiçbir dönüş/log/bildirimde %0 oranında görünmez (hedef: sıfır sızıntı).
- **SC-004**: Kaybolan banka sonuçlarının %100'ü mutabakatla en geç bir sonraki mutabakat turunda yakalanır; hiçbir store kayıp bildirim yüzünden kalıcı beklemede kalmaz.
- **SC-005**: Bir banka reddi veya ek-alan reddi, store'un diğer bankalardaki başvuru/canlı durumunu etkilemez (izolasyon %100).

## Assumptions

- **Kimlik:** ECommerce → PG aktivasyon çağrısı paylaşılan API key (X-Api-Key) ile yetkilendirilir (2026-10-22 kimlik birleşimi, CLAUDE.md); form açılışı anonim per-session secret token'ladır.
- **Komisyon yok:** PG araya girip komisyon/pay kesmez; store banka hesabı doğrudan store adınadır.
- **Çok-store modeli:** Mimari birden çok store'u destekler; bu dilimde pratikte tek store (ECommerceAgent) onboard edilir.
- **Banka otoritedir:** Onay kararı ve credential bankaya aittir; PG yalnız sonucu yansıtır. Banka inceleme/onayı banka tarafındaki operasyon yüzeyinde (ayrı simülatör repo'su + AgentPlatform) yapılır — 091 kapsamı dışı.
- **Bağımlılık:** BankSimulator'ın onboarding intake + çalışan-onay + sonuç webhook'u HENÜZ YOK; PG tarafı adapter port + stub'a karşı geliştirilir, uçtan-uca doğrulama simülatör bu yüzeyi ekleyince yapılır.
- **Red sonrası:** Reddeden bankaya yeniden başvuru desteklenir; diğer bankaların durumu bağımsızdır (my-call).
- **Çağırana dönüş:** PG yalnız opak tutamaç + özet durum döndürür; "canlı oldun" bilgisi store'a imzalı webhook ile iletilir (my-call).
- **Bildirim:** E-posta kullanılmaz (Mail.Worker sökülü); tüm sistem-sistem bildirimleri imzalı webhook'ladır.
- **Para birimi:** Yalnız TL (anayasa alan kısıtı).