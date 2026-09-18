# Feature Specification: Merchant + Commission MCP Yüzeyi Genişletme (Merchant.Agent Söküm)

**Feature Branch**: `043-mcp-surface-expansion`

**Created**: 2026-09-17

**Status**: Draft

**Input**: User description: "MCP-first refactor: Merchant ve Commission BC'lerinde de Payment gibi
MCP tool yüzeyi genişletilecek. Hedef — merchant onboarding, statü yönetimi, komisyon politikası gibi
işlemler Claude desktop üzerinden doğal dil / MCP tool çağrısıyla yönetilsin. Admin (Razor BFF) yüzeyi
minimuma iner; yalnız 1-2 kritik ekran UI'da kalır. Kapsam: Payment, Merchant, Commission üç BC.
Netleştirme sonucu: Merchant.Agent (A2A host) sökülür (WorkflowContext benzeri durum yönetimi yok,
salt ince MCP-relay); Claude desktop Merchant.Api /mcp'ye DOĞRUDAN bağlanır (Payment deseni). Commission
create/update MCP'ye açılmaz, mevcut ekranından yürütülür (yalnız sorgulama MCP'ye açılır). Ekran sökümüne
bu feature'da başlanır: Admin 'Agent Chat' ekranı ve Merchant durum-değiştirme aksiyonları kaldırılır."

## Clarifications

### Session 2026-09-17

- Q: Merchant.cs'te statü modeli Active/Passive/Suspended arası serbest 3'lü geçiş (idempotent
  no-op); MCP tool'u tek generic mi yoksa üç ayrı tool mü olsun? → A: Üç ayrı tool
  (`activate_merchant`/`deactivate_merchant`/`suspend_merchant`), her biri hedef statüyü sabit taşır.
- Q: Merchants ekranında durum-değiştirme aksiyonu bulunamadı (kod taramasında); asıl yazma aksiyonu
  RegisterRequests/Index'teki Onayla/Reddet. Bu ekran bu feature'da ne olsun? → A: MCP'ye taşınır —
  operatör "bana talepte bulunan merchantları getir" der (liste sorgu tool'u), sonra Claude desktop
  üzerinden onaylar/reddeder; RegisterRequests/Index ekranı söküm hedefine girer.
- Q: Onay akışı ile komisyon politikası atama birbirine bağımlı mı olsun (onay komisyon politikası
  yoksa uyarsın/engellesın mı)? → A: Tamamen bağımsız kalsın, bu feature'da komisyon oranı hesaba
  katılmaz (ne uyarı ne engel); CommissionPolicy her zaman ayrı, PG operatörünün CommissionPolicies
  ekranından yürüttüğü sabit-yüzde bir adım olarak kalır — negatif komisyon teklif/pazarlık akışı
  (eski 019) bu feature'a girmez.
- Q: Başvuru (RegisterRequest) alındığında admin haberdar olsun mu? → A: Evet — `submit_registration`
  başarılı olduğunda (yeni Pending kaydı) admin'e sabit, bilgilendirici bir mail gider ("PG'ye kayıt
  yaptırmak isteyen var" tarzı); mükerrer (zaten Pending) denemede yeni mail GÖNDERİLMEZ, yalnız
  "talepte bulunmuştunuz" tarzı anlaşılır mesaj döner (mevcut RECORD_DUPLICATE davranışı, metin netliği).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Merchant durum yönetimi doğal dille (Priority: P1)

Admin operatörü Claude desktop'ta "X merchant'ını aktive et" / "Y merchant'ını askıya al" /
"Z merchant'ını pasife al" yazar. Merchant.Api'nin `/mcp` ucunda üç ayrı tool (`admin_activate_merchant`,
`admin_deactivate_merchant`, `admin_suspend_merchant`) doğrudan (A2A relay olmadan) çağrılır; hedef statü
(Active/Passive/Suspended) mevcut `ChangeStatus` kuralına göre uygulanır ve sonuç doğal dille dönülür.

**Why this priority**: Bu refactor'ın asıl hedefi — statü işlemlerini ekransız hale getirmek
(bugün bu işlem için Admin UI'da zaten bir ekran YOK, yalnız REST endpoint var — bkz. Assumptions).

**Independent Test**: Claude desktop'tan bir test merchant'ı için activate/deactivate/suspend
mesajları gönderilerek, Merchant.Api'deki statü değişiminin (sorgu tool'uyla) gerçekleştiği doğrulanır.

**Acceptance Scenarios**:

1. **Given** Passive statüde merchant, **When** operatör "bu merchant'ı aktive et" der, **Then**
   `admin_activate_merchant` çağrılır, statü Active'e geçer, sonuç doğal dille bildirilir.
2. **Given** zaten Active olan merchant, **When** `admin_activate_merchant` tekrar çağrılır, **Then**
   statü makinesinin idempotent no-op kuralı gereği hata DÖNMEZ, "zaten aktif" bilgisi doğal
   dille bildirilir (durum değişmez).
3. **Given** yetkisiz/scope'suz istemci, **When** merchant statü tool'larından biri çağrılır,
   **Then** istek fail-closed reddedilir (401/403), statü değişmez.

---

### User Story 2 - Başvuru onay/red yönetimi doğal dille (Priority: P2)

Operatör Claude desktop'ta "bana bekleyen başvuruları getir" der; `admin_get_pending_registrations`
tool'u Pending durumundaki RegisterRequest'leri (ECommerce tarafında girilen merchant bilgileriyle)
listeler. Operatör bilgileri kontrol eder, sorun görmezse "şu başvuruyu onayla" / "şunu şu
gerekçeyle reddet" der; `admin_approve_registration`/`admin_reject_registration` tool'ları çağrılır. Komisyon
oranı bu akışa dahil DEĞİLDİR — ayrı, bağımsız bir adım (bkz. US4).

**Why this priority**: Bugün bu işlem yalnız Admin RegisterRequests/Index ekranından (Onayla/Reddet
butonları) yapılabiliyor; bu ekranın MCP'ye taşınması söküm hedefinin ana parçası.

**Independent Test**: Test amaçlı bir RegisterRequest oluşturulur, Claude desktop'tan listelenip
onaylanır/reddedilir; RegisterRequest statüsü (Approved/Rejected) ve onayda doğan Merchant kaydı
doğrulanır.

**Acceptance Scenarios**:

1. **Given** Pending durumunda başvurular var, **When** operatör listelenmesini ister, **Then**
   `admin_get_pending_registrations` başvuru bilgilerini (sır/anahtar hariç) doğal dille döner.
2. **Given** geçerli bir Pending başvuru, **When** operatör onaylar, **Then** RegisterRequest
   Approved olur, yeni Merchant doğar, sonuç (MerchantId dahil) doğal dille bildirilir.
3. **Given** geçerli bir Pending başvuru, **When** operatör gerekçeyle reddeder, **Then**
   RegisterRequest Rejected olur, gerekçe kaydedilir.
4. **Given** zaten Approved/Rejected (terminal) başvuru, **When** tekrar onay/red istenir,
   **Then** domain invariant hatası (INVALID_OPERATION) olduğu gibi döner.
5. **Given** yeni bir başvuru (`submit_registration`) başarıyla Pending olarak kaydolur, **When**
   kayıt işlemi tamamlanır, **Then** admin'e "PG'ye kayıt yaptırmak isteyen var" tarzı bilgilendirici
   bir mail gider (talep detayına link/ID içerebilir, PII sınırlı).
6. **Given** aynı e-postayla Pending başvuru zaten var, **When** aynı kişi tekrar `submit_registration`
   çağırır, **Then** yeni kayıt AÇILMAZ, yeni mail GÖNDERİLMEZ; "talepte bulunmuştunuz, onay bekleniyor"
   tarzı anlaşılır bir mesaj döner.

---

### User Story 3 - Merchant/başvuru sorgulama doğal dille (Priority: P3)

Operatör "merchantları listele" / "Z merchant'ının durumu ne?" der ya da "başvurum onaylandı mı?"
diye sorar; mevcut `registration_status` tool'u ve yeni `admin_get_merchants` (liste, opsiyonel
statü filtresi) tool'u ile cevap ekransız döner.

**Why this priority**: Statü değişikliğinin doğrulanması için de ekrana dönülmemesi gerekir;
US1'i tamamlayan okuma ayağı.

**Independent Test**: `admin_get_merchants` çağrılır, dönen liste Merchants/Details ekranındaki
değerlerle karşılaştırılır.

**Acceptance Scenarios**:

1. **Given** var olan merchant'lar, **When** `admin_get_merchants` çağrılır (opsiyonel statü
   filtresiyle ya da filtresiz tümü), **Then** her biri için güncel statü + temel alanlar (isim,
   e-posta vb. — sır/anahtar HARİÇ) doğal dille dönülür.
2. **Given** filtreye uyan hiçbir merchant yok, **When** `admin_get_merchants` çağrılır, **Then**
   boş liste/"bulunamadı" yanıtı döner, hata fırlatmaz.

---

### User Story 4 - Komisyon politikası sorgulama doğal dille (Priority: P4)

Operatör "X merchant'ının komisyon oranı ne?" der; Commission.Api'ye yeni eklenen salt-okuma
MCP tool'u politikayı döner. Politika **oluşturma/güncelleme bu feature'a dahil değildir** —
o işlem mevcut CommissionPolicies ekranından yürütülmeye devam eder (bilinçli karar: finansal
write, ekran üzerinden kalır).

**Why this priority**: En düşük risk, en dar kapsam; Commission.Api bugün hiç MCP yüzeyine sahip
değil, önce salt-okuma ile başlanır.

**Independent Test**: Var olan bir CommissionPolicy için doğal dil sorgusu gönderilir, dönen
marj/tarife CommissionPolicies ekranındaki değerle karşılaştırılır.

**Acceptance Scenarios**:

1. **Given** merchant için tanımlı komisyon politikası, **When** sorgulanır, **Then** marj/tarife
   bilgisi doğal dille dönülür.
2. **Given** politikası olmayan merchant, **When** sorgulanır, **Then** "tanımlı politika yok"
   yanıtı döner.
3. **Given** operatör politika oluşturmak/güncellemek ister, **When** bu niyet MCP'ye iletilir,
   **Then** sistem bunu MCP ile yürütmez; mevcut CommissionPolicies ekranına yönlendirir.

---

### Edge Cases

- Merchant.Agent (A2A) söküldükten sonra Admin'in eski "Agent Chat" ekranına erişilirse ne olur?
  (Ekran kaldırılmış olmalı; kalıntı link/route bırakılmaz.)
- MCP tool çağrısı sırasında ilgili BC (Merchant.Api/Commission.Api) ayakta değilse istemci
  (Claude desktop) ne görür? (Bağlantı hatası, domain hatası değil — ayrımı net olmalı.)
- Aynı anda hem MCP hem Admin UI'dan aynı merchant'a çelişen statü isteği gelirse (concurrency)
  mevcut aggregate/optimistic-concurrency kuralları aynen geçerli kalır.
- Terminal (Approved/Rejected) bir RegisterRequest'e tekrar onay/red istenirse domain invariant
  hatası (INVALID_OPERATION) döner, RegisterRequests/Index ekranı yokken de aynı hata MCP'den gelir.
- Merchant Active olur ama CommissionPolicy hiç tanımlanmamışsa sistem bunu engellemez/uyarmaz
  (US2/US4 bağımsız akışlar — bilinçli karar, bkz. Clarifications).
- Admin bildirim maili (Mail.Worker) ayakta değilse/başarısız olursa `submit_registration` yine de
  başarıyla döner (mail outbox garantisi ayrık, mevcut Mail.Worker retry+error-queue kuralı geçerli;
  bkz. `docs/conventions.md` "Servisler-arası desenler").

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Merchant.Api MUST expose üç ayrı MCP tool (`admin_activate_merchant`,
  `admin_deactivate_merchant`, `admin_suspend_merchant`), her biri `ChangeStatus`'u sabit bir hedef
  statüyle (Active/Passive/Suspended) çağırır; doğrudan dış MCP istemcisinden (Claude desktop)
  erişilebilir, A2A relay olmadan.
- **FR-002**: Merchant.Api MUST expose `admin_get_merchants` — salt-okuma, LİSTE döndüren
  (opsiyonel statü filtreli) MCP tool; yalnız Merchant aggregate'ini sorgular (RegisterRequest'e
  karışmaz); sır/MerchantKey/SubMerchantKey döndürmez.
- **FR-003**: Commission.Api MUST expose a read-only MCP query tool for commission policy lookup
  (merchant başına marj/tarife); create/update bu feature kapsamı DIŞINDADIR.
- **FR-004**: System MUST remove Merchant.Agent (A2A host) component entirely; hiçbir akış A2A
  relay'e bağımlı kalmaz.
- **FR-005**: System MUST remove Admin "Agent Chat" ekranını (Merchant.Agent'a bağımlıydı) —
  kalıntı route/link bırakılmaz.
- **FR-006**: Merchant.Api MUST expose `admin_get_pending_registrations` (liste),
  `admin_approve_registration`, `admin_reject_registration` MCP tool'ları (yeni Agents slice'ları,
  mevcut Approve/Reject aggregate metotlarını çağırır); System MUST remove Admin
  RegisterRequests/Index ekranını (Onayla/Reddet dahil) bu tool'lar canlı olduktan sonra.
- **FR-007**: Yeni MCP tool'lar MUST mevcut scope-tabanlı yetki modelini (merchant.read/write
  eşdeğeri) fail-closed uygulamak; scope'suz istemci reddedilir.
- **FR-008**: Domain invariant'ları (statü makinesi, mevcut aggregate kuralları) MUST MCP
  yolundan da REST'teki ile birebir aynı şekilde uygulanmak — bypass edilmez.
- **FR-009**: Var olan `submit_registration`/`registration_status` MCP tool sözleşmeleri
  (isim/şekil) MUST değişmeden kalmak (dış tüketiciler bağımlı).
- **FR-010**: CommissionPolicies Admin ekranı (create/update) MUST bu feature'da dokunulmadan
  kalmak — kritik ekran olarak korunur.
- **FR-011**: `submit_registration` başarıyla yeni bir Pending kaydı açtığında System MUST admin'e
  `mail.delivery` (Mail.Worker) üzerinden bilgilendirici bir mail göndermek ("PG'ye kayıt yaptırmak
  isteyen var" tarzı, sabit alıcı — çoklu admin/rol dağıtımı bu feature kapsamı DIŞINDADIR).
- **FR-012**: Aynı e-postayla Pending başvuru varken tekrar `submit_registration` çağrılırsa System
  MUST yeni kayıt açmamak, yeni mail göndermemek; kullanıcıya "talepte bulunmuştunuz" anlamına gelen
  anlaşılır bir mesaj döndürmek (mevcut RECORD_DUPLICATE davranışı korunur, metin netleştirilir).

### Key Entities

- **Merchant**: statü makineli aggregate (Merchant.Api, Active/Passive/Suspended arası serbest
  geçiş); bu feature statü geçişlerini MCP'ye taşır.
- **CommissionPolicy**: marj/tarife aggregate (Commission.Api); bu feature yalnız okuma yüzeyini
  MCP'ye taşır, yazma değişmez.
- **SendEmailRequested**: Mail.Worker'ın tükettiği fat-message mail kontratı (`mail.delivery`
  exchange); Merchant.Api yayıncı olarak zaten kayıtlı ama aktif çağıran yoktu — bu feature ilk
  aktif çağıran noktayı (`submit_registration` sonrası admin bildirimi) ekler.
- **RegisterRequest**: Pending → Approved/Rejected statü makineli aggregate (Merchant.Api); mevcut
  müşteri-yüzeyi MCP sözleşmesi (submit_registration/registration_status) korunur; bu feature admin
  onay/red tarafına (bugün yalnız Admin UI'da) yeni MCP tool'ları (admin_get_pending_registrations/
  admin_approve_registration/admin_reject_registration) ekler ve o ekranı kaldırır.
- **Merchant.Agent**: kaldırılan bileşen (A2A host, stateless relay) — bu feature'ın sonunda yok.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Operatör, merchant durumunu (activate/deactivate/suspend) hiçbir ekran açmadan,
  yalnız doğal dil mesajıyla değiştirebilir.
- **SC-002**: Operatör, merchant durumu ve komisyon politikasını hiçbir ekran açmadan, yalnız
  doğal dil sorgusuyla öğrenebilir.
- **SC-003**: Admin arayüzünde RegisterRequests onay/red ekranı erişilebilir olmaktan çıkar; onay/
  red yalnız MCP'den yürütülür.
- **SC-004**: Admin arayüzünde "Agent Chat" ekranı erişilebilir olmaktan çıkar.
- **SC-005**: Merchant.Agent kaldırıldıktan sonra kayıt başvuru akışı (submit_registration/
  registration_status) hatasız çalışmaya devam eder (regresyon yok).
- **SC-006**: Yeni bir başvuru geldiğinde admin, ekranı hiç açmadan (mail bildirimiyle) haberdar olur.

## Assumptions

- Claude desktop, Merchant.Api/Commission.Api'ye mevcut OAuth istemci düzleminden (client_credentials
  + scope) kimlik doğrulamayla bağlanır; yeni bir kimlik doğrulama modeli gerekmez.
- Admin login ekranı (042) ve CommissionPolicies create/update ekranı bu feature kapsamında kritik
  kalan ekranlardır; söküm hedefindeki ekranlar netleşti: Admin "Agent Chat" ve RegisterRequests
  onay/red ekranı.
- Merchants liste/detay salt-okuma görünümü, durum-değiştirme aksiyonu zaten barındırmadığı için
  bu feature'da dokunulmadan (raporlama amaçlı) kalabilir; kesin karar plan aşamasında netleşir.
- Mevcut Merchant statü makinesi geçişleri (Active/Passive/Suspended, serbest 3'lü) bu feature'da
  değişmez; yalnız tetikleme kanalına (MCP) yeni bir giriş eklenir.
- Admin bildirim e-posta adresi Options/config'ten okunan sabit tek bir alıcıdır; rol-bazlı/çoklu
  admin dağıtımı bu feature kapsamı dışıdır.
- `mail.delivery` altyapısı (Mail.Worker, SendEmailRequested kontratı, outbox garantisi) hazır ve
  değişmeden kullanılır; yalnız Merchant.Api tarafında yeni bir yayın noktası eklenir.