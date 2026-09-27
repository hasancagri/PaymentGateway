# Feature Specification: Tek MCP Yüzeyinde PG — Store Fasadına Bağlanma + Platform Kimliği

**Feature Branch**: `047-platform-single-mcp`

**Created**: 2026-09-26

**Status**: Draft

**Input**: User description: "PG BC'lerinin MCP yüzeyleri (Merchant/Commission) ECommerce Mcp.Gateway fasadına downstream bağlanır; insan/agent kimlik düzlemi AgentPlatform Identity.Server'a taşınır. Sonuç: Claude Desktop'ta TEK MCP kaydıyla hem ECommerce hem PaymentGateway tool'larına scope-budamalı erişim."

**Kademe**: Tam — 3 repo etkilenir (PG + ECommerce fasadı + AgentPlatform scope/rol kaydı); PG MCP yüzeyinin kimlik otoritesi değişir; dış istemci (Claude Desktop) kayıt kontratı değişir.

## Clarifications

### Session 2026-09-26 (sohbet kararları)

- Q: PG Identity.Server'ın kaderi? → A: YAŞAR, dokunulmaz. Düzlem ayrımı kilit: insan/agent kimliği → AgentPlatform (tek login); makine/merchant kimliği (MerchantKey, statü-kapılı token, S2S hesapları) → PG. Tam IdP konsolidasyonu ayrı feature adayı (048), bu iş kapsam DIŞI.
- Q: İki login ekranı mı? → A: HAYIR. Claude Desktop yalnız fasada bağlanır; login tek (AgentPlatform). Fasat oturum token'ını PG downstream'lerine taşır; PG MCP yüzeyi platform token'ını doğrular.
- Q: PG'ye özel yetkilendirme nasıl sürer? → A: Tanım platformda (PG'ye ait scope bölmesi + rol ataması), zorlama PG'de (tool-bazlı scope middleware + domain kapıları). Merchant-makine düzlemi role hiç girmez, PG mekanizmasında kalır.
- Q: PG'de kaç MCP yüzeyi? → A: TEK (EC mantığı): dışa açık MCP yüzeyi yalnız store fasadı; Merchant/Commission `/mcp`'leri iç downstream tesisatı. Payment.Api'nin tool'suz `/mcp` ucu SİLİNİR; PG gateway'inde MCP rotası kalmaz.
- Q: Tool adı çakışırsa? → A: PG tarafı yeniden adlandırılır (EC değişmez). Bugünkü durum ölçüldü: PG 14 tool, EC listesiyle kesişim 0 — bugün rename gerekmiyor, kural ileriye dönük.
- Q: PG Identity.Server adı? → A: `Merchant.Identity` olarak yeniden adlandırılacak — AYRI küçük PR (bu spec'in kapsamı dışında, mekanik rename).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Admin tek kayıtla PG tool'larını störde görür (Priority: P1)

Admin, Claude Desktop'taki TEK store kaydıyla bağlanıp platform login'i yapar. Tool listesinde ECommerce tool'larının yanında PG'nin merchant yönetimi + komisyon tool'ları da vardır; PG admin işlemleri (merchant onaylama, komisyon marjı güncelleme) bugün pg-merchant/pg-commission kayıtlarıyla yapılanla birebir çalışır.

**Why this priority**: Feature'ın varlık sebebi — tek MCP bağlantısı emeli; çalışmazsa iş anlamsız.

**Independent Test**: Admin token'ıyla store `/mcp` oturumu aç; `tools/list`'te PG tool'larının varlığını ve bir PG yazma tool'unun başarısını doğrula.

**Acceptance Scenarios**:

1. **Given** admin rollü kullanıcı store kaydıyla login, **When** `/mcp` oturumu açılır, **Then** listede EC tool'ları + PG'nin 14 merchant/commission tool'u birlikte görünür.
2. **Given** aynı oturum, **When** PG yazma tool'u çağrılır (ör. `admin_update_merchant`), **Then** işlem başarılıdır; PG'nin tool-bazlı scope zorlaması geçilmiştir.
3. **Given** Claude Desktop config'i, **When** kayıtlara bakılır, **Then** PG'ye ait ayrı MCP kaydı/köprüsü yoktur (bugünkü 3 kayıt → 1).

---

### User Story 2 - Yetkisiz oturum PG tool'u ne görür ne çağırır (Priority: P1)

Müşteri rollü kullanıcı (veya dış DCR istemcisi) aynı store ucuna bağlanır. PG tool'ları listede görünmez; adıyla doğrudan çağrılırsa işlem gerçekleşmez (PG tarafında scope reddi son savunma). Rolünde KISMİ PG yetkisi olan kullanıcı (ör. yalnız komisyon okuma) yalnız o scope'un tool'larını görür.

**Why this priority**: Güvenlik sınırı — PG admin yüzeyi paraya/merchant verisine dokunur; US1 ile aynı mekanizmanın diğer yüzü.

**Independent Test**: Müşteri token'ıyla oturum aç; PG tool sayısının 0 olduğunu ve adıyla doğrudan çağrının reddedildiğini doğrula.

**Acceptance Scenarios**:

1. **Given** müşteri token'lı oturum, **When** `tools/list` çekilir, **Then** PG tool'u 0'dır.
2. **Given** müşteri token'lı oturum, **When** PG tool'u adıyla `tools/call` edilir, **Then** işlem gerçekleşmez; yetki hatası döner.
3. **Given** rolünde yalnız komisyon-okuma olan kullanıcı, **When** oturum açılır, **Then** PG tool'larından yalnız komisyon okuma görünür (scope başına budama, hep-ya-hiç değil).
4. **Given** dış DCR istemcisi, **When** PG admin scope'u talep eder, **Then** bağlantı kırılmaz; token PG admin scope'suz basılır (istemci tavanı).

---

### User Story 3 - PG'nin eski agent kimlik yüzeyi ve fazla MCP uçları sökülür, mevcut akışlar bozulmaz (Priority: P2)

PG'nin kendi Claude Desktop istemci kaydı (`external-admin-agent`), mcp-remote köprü kurulumu, Payment.Api'nin tool'suz `/mcp` ucu ve PG gateway'indeki MCP rota kalıntıları kaldırılır. Merchant istemci düzlemi, S2S servis hesapları ve ödeme akışı (MerchantKey + callback teyidi + imzalı store bildirimi) davranış değiştirmeden sürer.

**Why this priority**: Ölü ama yetkili kimlik yüzeyi + sahipsiz uç bırakmama (fail-closed hijyeni); US1/US2 canlıya çıkmadan da sistem çalışır.

**Independent Test**: PG IdP'den `external-admin-agent` ile token alınamadığını, Payment `/mcp`'nin 404 olduğunu, hosted ödeme ve onboarding S2S akışlarının regresyonsuz olduğunu doğrula.

**Acceptance Scenarios**:

1. **Given** söküm sonrası, **When** `external-admin-agent` ile PG IdP'den token istenir, **Then** istemci yoktur, token verilmez (emekli istemci temizliği dahil).
2. **Given** söküm sonrası, **When** Payment.Api `/mcp`'ye bağlanılır, **Then** uç yoktur (404); PG gateway'inde MCP rotası kalmamıştır.
3. **Given** söküm sonrası, **When** hosted ödeme akışı uçtan uca koşulur (sepet → ödeme → callback → store bildirimi), **Then** davranış bugünle birebir.
4. **Given** söküm sonrası, **When** admin hassas-veri sayfası ve merchant onboarding S2S akışı kullanılır, **Then** PG IdP token'larıyla bugünkü gibi çalışır.

---

### Edge Cases

- Tool adı çakışması: bugün 0 (ölçüldü); ileride doğarsa PG tool'u yeniden adlandırılır, EC değişmez. Benzersizlik birleşik listenin ön şartı.
- PG BC'lerinden biri kapalıyken fasat: tool listesi o BC'nin tool'ları OLMADAN gelir; fasat çökmez, kalan tool'lar çalışır.
- PG ayrı Aspire evreninde koşar: fasat PG'ye service-discovery ile değil sabit adresle ulaşır; iki sistemin birlikte ayakta olması canlı doğrulamanın ön şartı.
- Aynı scope adı iki IdP'de (ör. merchant.read hem PG merchant token'ında hem platform admin token'ında): yüzey başına TEK otorite — PG MCP yüzeyi yalnız platform issuer'ı, REST yüzeyleri yalnız PG IdP; karışım yok.
- Oturum açıkken rol değişimi: mevcut oturum eski tool setiyle sürer; yeni set token yenilenince (085 kabulüyle aynı).
- PG gateway config'i EC kalıntısı rotalarla dolu (basket/catalog/order... — PG'de bu BC'ler yok): MCP rotaları bu feature'da sökülür; kalan bayat rotaların genel temizliği ayrı iş.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: PG'nin merchant yönetimi ve komisyon tool'ları (14 tool), store fasadının TEK `/mcp` ucundan, EC tool'larıyla aynı listede sunulmalı.
- **FR-002**: PG tool görünürlüğü oturum token'ının scope'larına göre budanmalı: PG scope'u olmayan oturum PG tool'unu ne görür ne çağırabilir; budama scope başına çalışır (kısmi yetki destekli).
- **FR-003**: PG'nin yetki scope'ları platform scope kayıt defterine PG'ye AİT ayrı bölme olarak girmeli; platform rol ekranından rollere atanabilmeli; yeni PG scope'u EC tarafına dokunmadan eklenebilmeli.
- **FR-004**: Platform-basımlı token PG MCP yüzeyinde geçerli kimlik olmalı; PG'nin tool-bazlı scope zorlaması ve domain kapıları aynen çalışmalı (budama kaçağında çağrı PG'de reddedilir — son savunma).
- **FR-005**: PG MCP yüzeyi YALNIZ platform-basımlı token kabul etmeli; PG IdP token'ları MCP yüzeyine girememeli (yüzey başına tek otorite). REST yüzeylerinin kimlik otoritesi değişmez.
- **FR-006**: Claude Desktop'ta PG için ayrı kayıt/login gerekmemeli; PG'nin kendi dış-agent istemci kaydı kaldırılmalı ve token alamamalı.
- **FR-007**: Birleşik tool listesinde adlar benzersiz olmalı; çakışma PG tarafında yeniden adlandırmayla çözülür (bugün çakışma 0 — kural ileriye dönük).
- **FR-008**: Fasat keşfi PG tool'larını kapsamalı (tam-katalog yönlendirme kaydı); PG BC'si erişilemezken fasat kalan tool'larla hizmet vermeli.
- **FR-009**: Merchant istemci düzlemi, S2S servis hesapları ve ödeme akışı (MerchantKey doğrulama, callback teyidi, imzalı store bildirimi) davranış değiştirmemeli.
- **FR-010**: PG'de tool taşımayan MCP ucu kalmamalı: Payment.Api'nin tool'suz `/mcp` ucu kaldırılmalı; PG gateway'indeki MCP rotaları (EC kalıntıları dahil) sökülmeli. Dışa açık MCP yüzeyi yalnız store fasadıdır.

### Key Entities

- **PG scope bölmesi**: platform kayıt defterinde PG'ye ait scope seti (merchant yönetimi + komisyon); rol demetlerine atanabilir; sahibi PG.
- **Downstream kaydı**: fasadın PG BC'lerine yönlendirme bilgisi (ad + adres); tool→BC eşlemesi keşifle kurulur.
- **Emekli istemci**: PG IdP'den kaldırılan `external-admin-agent`; açılışta store'dan da temizlenir (mevcut prune mekanizması).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Claude Desktop'ta MCP kaydı 3'ten (store + pg-merchant + pg-commission) 1'e iner; admin TEK login ile bugün üç kayıtta yaptığı işlerin tamamını yapabilir.
- **SC-002**: PG yetkisi olmayan oturumların tool listesinde PG tool sayısı 0; adıyla doğrudan çağrı %100 reddedilir.
- **SC-003**: Mevcut canlı akışlar regresyonsuz: hosted ödeme uçtan uca, merchant onboarding S2S, admin hassas-veri sayfası — davranış bugünle birebir.
- **SC-004**: Yeni bir PG yetkisi tanımlamak yalnız PG bölmesi + rol ataması değişikliği gerektirir; EC kod/config değişikliği 0.
- **SC-005**: PG'de dışa açık MCP ucu 0 (tool taşıyan BC `/mcp`'leri yalnız fasat downstream'i olarak yaşar); Payment `/mcp` ve gateway MCP rotaları 404.

## Assumptions

- Fasat (Mcp.Gateway) ECommerce reposunda kalır; platforma taşınması ayrı iş (085 kabulü sürer).
- PG Identity.Server yaşar; `Merchant.Identity` rename'i AYRI mekanik PR (bu spec kapsam dışı).
- Platform tarafı değişiklikleri (scope bölmesi + rol ataması + istemci tavanı) AgentPlatform reposunda ayrı PR ile ve ÖNCE merge edilir (085 deseni).
- Kimlik düzlem ayrımı kalıcı mimari karar: insan → platform (rol→scope), makine/merchant → PG (statü→scope). Tam IdP konsolidasyonu ileride ayrı feature (048 adayı).
- mcp-remote köprü script'i (`scripts/claude-desktop-pg-mcp.sh`) ve pg-* Desktop kayıtları sökülür.
- Scope budama, keşif cache'i ve istemci-tavanı mekanizmaları 085'te canlı doğrulanmış haliyle kullanılır; bu feature yeni budama mekanizması icat etmez.
- Ödeme yolu MCP dışıdır (MerchantKey/X-Api-Key + imzalı bildirim); bu feature ödeme yoluna dokunmaz.