# Feature Specification: Merchant OAuth İstemci Düzlemi (G2 — Makine Kimliği)

**Feature Branch**: `012-merchant-oauth-client`

**Created**: 2026-08-07

**Status**: Draft

**Input**: User description: "G2 Merchant kimlik düzlemi (makine): Merchant'lar OAuth client_credentials istemcisi olur. client_id=merchantId, client_secret=MerchantKey (006'daki mevcut mk_+Guid değeri; üçüncü bir sır üretilmez). MerchantKey yalnız Identity.Server connect/token ucuna gider, BC API'lerine asla taşınmaz. Token: 15 dakika ömürlü self-contained JWT, içinde merchant_id claim'i + mevcut merchant.read/merchant.write scope adları. Admin-ui ve payment-agent token'ları değişmez. Client senkronu event-driven: Merchant BC MerchantCreated/MerchantStatusChanged event'leri yayar, Identity.Server tüketip client yaratır/pasifler; backfill yok. Status-gated issuance: yalnız Active merchant token alır. Erişim kapsamı: yalnız Merchant BC'de kendi kaydı + kendi settlement account uçları. Enforcement: merchant_id claim'i path'teki merchantId ile eşleşmeli, uyuşmazlıkta 403; claim yoksa mevcut davranış. Kapsam dışı: MerchantKey rotasyonu, insan login/RBAC (G3), tenant-per-database (013), ödeme çekim akışı."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Merchant sistemi kendi kimliğiyle token alır (Priority: P1)

Bir merchant'ın arka plan sistemi (e-ticaret sitesi sunucusu), gateway'e onboarding
sırasında kendisine verilen iki değeri (merchantId + MerchantKey) kullanarak
Identity.Server'dan kısa ömürlü bir erişim token'ı alır. Üçüncü bir kullanıcı adı/şifre
veya ayrı bir istemci sırrı yoktur; MerchantKey sırrın kendisidir. Token yalnız 15 dakika
geçerlidir ve içinde merchant'ın kimliği (merchant_id) ile yetki türleri
(merchant.read, merchant.write) taşınır.

**Why this priority**: Düzlemin temeli — token alınamıyorsa diğer hiçbir senaryo çalışmaz.
G2'nin varlık sebebi merchant'ın makine-makine kimlik kazanmasıdır.

**Independent Test**: Onboard edilmiş Active bir merchant'ın merchantId + MerchantKey
çiftiyle token ucundan token alınabildiği, token içeriğinde merchant_id ve doğru scope'ların
bulunduğu tek başına doğrulanabilir.

**Acceptance Scenarios**:

1. **Given** Active statüsünde onboard edilmiş bir merchant, **When** sistemi
   merchantId + MerchantKey ile token isterse, **Then** 15 dakika ömürlü, merchant_id
   claim'i ve merchant.read + merchant.write scope'larını taşıyan bir token alır.
2. **Given** herhangi bir merchant, **When** yanlış MerchantKey ile token isterse,
   **Then** istek kimlik hatasıyla reddedilir; token verilmez.
3. **Given** sistemde hiç kayıtlı olmayan bir merchantId, **When** token isterse,
   **Then** istek kimlik hatasıyla reddedilir.

---

### User Story 2 - Merchant yalnız kendi verisine erişir (Priority: P1)

Token alan merchant sistemi, Merchant BC'de kendi merchant kaydını okuyabilir ve kendi
settlement account'larını (payout banka hesapları) yönetebilir. Başka bir merchant'ın
kaynağına aynı token'la yapılan her istek reddedilir. Admin ve payment-agent gibi
merchant kimliği taşımayan mevcut istemcilerin davranışı değişmez (global erişim sürer).

**Why this priority**: Kimlik tek başına değersiz — değeri "kendi verine eriş,
başkasınınkine erişme" garantisiyle kazanır. US1 ile birlikte MVP'yi oluşturur.

**Independent Test**: İki merchant onboard edilip birinin token'ıyla (a) kendi
settlement account listesi çekilerek 200, (b) diğerinin listesi denenerek 403 alındığı;
admin token'ıyla her iki merchant'ın da görülebildiği bağımsız doğrulanabilir.

**Acceptance Scenarios**:

1. **Given** merchant 123'ün geçerli token'ı, **When** kendi kaydını veya
   `merchants/123/settlement-accounts` uçlarını çağırırsa, **Then** istek başarılıdır.
2. **Given** merchant 123'ün geçerli token'ı, **When** `merchants/456/...` gibi başka
   bir merchant'ın kaynağına istek atarsa, **Then** 403 Forbidden alır.
3. **Given** merchant kimliği taşımayan admin token'ı, **When** herhangi bir merchant'ın
   kaynağına istek atarsa, **Then** mevcut davranış korunur (erişim başarılı).
4. **Given** merchant 123'ün token'ı, **When** Payment veya Commission uçlarına istek
   atarsa, **Then** yetki yetersizliğiyle reddedilir (bu uçlar merchant'a açık değildir).

---

### User Story 3 - Onboarding ve statü değişimi istemci kaydını kendiliğinden yönetir (Priority: P2)

Yeni bir merchant onboard edildiğinde hiçbir elle adım olmadan token alabilir hâle gelir:
Merchant BC onboarding'de bir olay yayar, Identity tarafı bu olayı tüketip istemci kaydını
kendi tarafında oluşturur. Merchant askıya alındığında/pasifleştirildiğinde aynı yolla
istemci kaydı kapatılır ve merchant YENİ token alamaz; elindeki token en fazla 15 dakika
daha yaşar (bilinçli tasarım — anlık iptal yerine kısa ömür). Merchant yeniden aktif
edilirse token alma hakkı geri gelir.

**Why this priority**: Yaşam döngüsü otomasyonu düzlemi "kurulabilir"den "işletilebilir"e
taşır; ama US1+US2 elle oluşturulmuş istemci kaydıyla da gösterilebilir.

**Independent Test**: Yeni merchant onboard edilip ardından token alınabildiği; merchant
askıya alınıp yeni token isteğinin reddedildiği; yeniden aktifleştirilince token
alınabildiği uçtan uca doğrulanabilir.

**Acceptance Scenarios**:

1. **Given** çalışan sistem, **When** yeni bir merchant onboard edilirse, **Then** ek bir
   elle adım olmadan bu merchant'ın kimlik bilgileriyle token alınabilir.
2. **Given** Active bir merchant, **When** askıya alınır veya pasifleştirilirse, **Then**
   sonraki token istekleri reddedilir.
3. **Given** askıya alınmış bir merchant, **When** yeniden aktifleştirilirse, **Then**
   token istekleri yeniden kabul edilir.
4. **Given** askıya alınmış ama elinde süresi dolmamış token bulunan bir merchant,
   **When** bu token'la kendi kaynağına istek atarsa, **Then** token süresi dolana kadar
   istek başarılıdır (kabul edilen 15 dakikalık pencere).

---

### Edge Cases

- Aynı onboarding olayı iki kez işlenirse (yeniden teslim) istemci kaydı çiftlenmemeli —
  tüketim idempotent olmalı.
- Identity tarafındaki olay tüketicisi geçici olarak kapalıyken onboard edilen merchant,
  tüketici ayağa kalkıp kuyruğu işleyince token alabilmeli (kuyruk dayanıklılığı).
- Süresi dolmuş token'la gelen istek kimlik hatası almalı; merchant sistemi taze token
  alıp isteği tekrarlayabilmeli (entegrasyon rehberine "token saklama, her çağrıda
  yenileme mantığından iste" kuralı yazılır).
- Merchant kimliği taşıyan bir token, path'inde merchantId geçmeyen bir Merchant BC ucuna
  gelirse (ör. tüm merchant'ları listeleme), istek 403 ile reddedilmeli — merchant yalnız
  kendi kaynağına, kendi kimliğiyle adreslenen uçlardan erişir.
- MerchantKey yalnız token ucuna gider; BC API'lerine MerchantKey taşıyan bir istek
  tasarım gereği hiçbir yerde kabul edilmez (böyle bir doğrulama yolu yoktur).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sistem, onboard edilmiş bir merchant'ın merchantId (istemci kimliği) +
  MerchantKey (istemci sırrı) çiftiyle makine-makine token isteğini kabul etmeli ve
  başka hiçbir ek sır/hesap gerektirmemelidir.
- **FR-002**: Verilen token 15 dakika ömürlü ve kendi kendine doğrulanabilir olmalı;
  içinde merchant kimliğini taşıyan merchant_id bilgisi ile merchant.read ve
  merchant.write yetki türleri bulunmalıdır. Merchant başına yeni yetki türü adı
  ÜRETİLMEZ (yetki türü = ne yapabilirsin, kimlik = kimsin ayrımı).
- **FR-003**: Token verme statü-kapılı olmalıdır: yalnız Active statüsündeki merchant
  token alabilir; Suspended ve Passive merchant'ların token istekleri reddedilir.
  Daha önce verilmiş token'lar süreleri dolana kadar geçerli kalır (bilinçli karar).
- **FR-004**: Merchant BC, merchant onboarding'inde ve statü değişiminde integration
  event yayınlamalıdır (MerchantCreated, MerchantStatusChanged — mevcut Shared fanout
  deseniyle).
- **FR-005**: Identity tarafı bu olayları tüketerek istemci kaydını kendi veri alanında
  oluşturmalı/güncellemelidir; tüketim idempotent olmalıdır (aynı olay iki kez işlense
  tek istemci kaydı). Backfill mekanizması bilinçli olarak YOKTUR (dev fazı — mevcut
  merchant'lar ortam sıfırlanarak yeniden onboard edilir).
- **FR-006**: Merchant kimliği taşıyan token'la gelen isteklerde, istekteki hedef
  merchant (path'teki merchantId) token'daki merchant_id ile eşleşmek zorundadır;
  uyuşmazlıkta istek 403 Forbidden ile reddedilir.
- **FR-007**: Merchant kimliği TAŞIMAYAN token'ların (admin-ui, payment-agent) mevcut
  davranışı aynen korunmalıdır; bu istemcilerin tanımı, scope'ları ve akışları değişmez.
- **FR-008**: Merchant token'ının erişim alanı yalnız Merchant BC'deki kendi merchant
  kaydını okuma ve kendi settlement account uçlarıdır; Payment ve Commission uçlarına
  merchant token'ı ile erişim mümkün olmamalıdır.
- **FR-009**: Eşleşme kontrolü (enforcement) tek bir ortak mekanizma olarak kurulmalı ve
  uçlarda açıkça beyan edilmelidir (anayasa İlke V — örtük yetki yok); G3 insan düzlemi
  aynı mekanizmayı yeniden kullanacaktır.
- **FR-010**: MerchantKey yalnız token alma ucuna iletilir; BC API'leri MerchantKey ile
  doğrudan kimlik doğrulama YAPMAZ (API-key deseni bilinçli reddedilmiştir).

### Key Entities

- **Merchant istemci kaydı**: Identity tarafında tutulan makine istemcisi; kimliği
  merchantId, sırrı MerchantKey, durumu merchant statüsünü izler (Active → token
  alabilir; değilse alamaz). Merchant BC'deki Merchant aggregate'inin kimlik düzlemindeki
  izdüşümüdür; iki taraf olaylarla senkron kalır.
- **MerchantCreated / MerchantStatusChanged olayları**: Merchant BC'nin yayınladığı,
  istemci kaydının yaşam döngüsünü süren bütünleşme sözleşmeleri (Shared).
- **Merchant erişim token'ı**: 15 dakika ömürlü, kendi kendine doğrulanabilir kimlik
  belgesi; merchant_id (kimlik) + merchant.read/merchant.write (yetki türleri) taşır.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Yeni onboard edilen bir merchant, hiçbir elle adım olmadan onboarding
  sonrasında kimlik bilgileriyle token alabilir (olay işlendikten sonraki ilk denemede).
- **SC-002**: Bir merchant token'ıyla kendi kaynağına yapılan istekler %100 başarılı,
  başka merchant'ın kaynağına yapılan istekler %100 reddedilir (403).
- **SC-003**: Askıya alınan bir merchant en geç 15 dakika içinde tüm erişimini kaybeder
  (yeni token reddi anında, eldeki token en fazla süresi kadar).
- **SC-004**: Mevcut istemcilerin (admin ekranları, ödeme ajanı) tüm akışları
  regresyonsuz çalışır — 011'in canlı senaryoları (S1-S6) yeşil kalır.
- **SC-005**: Merchant token'ıyla Payment/Commission uçlarına yapılan istekler %100
  reddedilir.

## Assumptions

- MerchantKey (mk_ + Guid) istemci sırrı olarak yeterli entropiye sahiptir ve 006
  kararıyla immutable'dır; rotasyon bu kapsamda değildir.
- Dev fazındayız: geriye dönük veri taşıma/backfill kurulmaz; mevcut merchant'lar ortam
  sıfırlanarak (Docker reset) yeniden onboard edilir.
- Mevcut mesajlaşma altyapısı (Shared integration event + fanout) ve 011'de kurulan
  kimlik iskeleti (token ucu, scope-policy düzeni) aynen kullanılabilir durumdadır.
- Merchant statüsü tekil doğruluk kaynağı Merchant BC'dir; kimlik tarafı yalnız olaylarla
  beslenen bir izdüşüm tutar.
- Storage düzeyinde tenant izolasyonu (Marten conjoined tenancy — merchant'a ait
  dokümanlarda tenant kolonu + sorguların tenant'a kilitlenmesi) ayrı bir spec'te (013)
  ele alınacaktır; bu spec'in ürettiği merchant_id kimliği o çalışmanın girdisidir.
- İnsan kullanıcı girişi ve merchant'a bağlı rol/kullanıcı modeli (RBAC) G3'tedir; bu
  spec yalnız makine düzlemini kurar.