# Merchant.Api — Domain Süreci

**BC ne yapar:** Gateway'e katılan **satıcıyı (Merchant)** kaydeder ve yaşam döngüsünü (Active/
Passive/Suspended) yönetir. Merchant YALNIZ başvuru onayıyla doğar (dış agent MCP ile başvurur,
operatör MCP ile onaylar/reddeder — 044: doğrudan admin kayıt yolu SÖKÜLDÜ). Yönetim yüzeyi
MCP'dir; hassas kişisel veri tek dar BFF çiftinden (Admin ekranı) akar. Merchant doğduğunda/
statüsü değiştiğinde Identity.Server'a duyurur (OpenIddict makine istemcisi senkronu) —
merchant'ın gateway'e token'lı erişimi bu duyuruya bağlıdır.

> Domain-önce anlatı (EventStorming altitude). Sağdaki `(…)` = koda atlama köprüsü, süreç değil.
> Süreç değişince (yeni/silinen adım-event-policy) bu dosya güncellenir; mekanik rename'i guard yakalar.

## Süreç

### Yol A — Store S2S kayıt → admin onay/red → makine-callback teslim (087; doğuş yolu)

1. **Store S2S kayıt başvurusu açar** (makine kimliği + `X-Registration-Key` bootstrap sır; finansal/
   PII yalnız S2S gövdede, LLM'e/MCP'ye uğramaz). `correlationId` + `callbackUrl` başvuruda saklanır;
   doğrulama (zorunlu alanlar, e-posta, TR IBAN mod-97, tip-uyum matrisi + correlation/callbackUrl)
   aggregate'te. Aynı correlationId / mükerrer e-posta → idempotent (çift yaratmaz).
   `(RegisterRequest.Submit ← SubmitRegistration)`
2. **Başvuru Pending doğar;** kimlik/sır bu adımda ÜRETİLMEZ; yanıt yalnız kabul makbuzu
   (MerchantId/MerchantKey YOK). `(SubmitRegistration)`
3. **Store durumu S2S sorgular** (en son başvuru; hiç yoksa "None"). Yanıtta MerchantId/
   MerchantKey ASLA yok; red'i de buradan öğrenir. `(GetOnboardingApplicationStatus)`
4. **Operatör (Claude Desktop) bekleyen başvuruları listeler** — yanıtta başvuru sahibinin
   kişisel/kimlik-belirleyen verisi YOK (044: karar için Id + işletme adı/tipi + tarih yeterli).
   `(AdminGetPendingRegistrationsMcpTool "admin_get_pending_registrations")`
5. **Operatör başvuruyu onaylar.** Merchant Active doğar, tek-seferlik `MerchantKey` üretilir;
   başvuru Approved'a bağlanır; credential store `callbackUrl`'ine HMAC-imzalı dayanıklı callback'le
   teslim edilir (echo `correlationId`). Onay dönüşü credential İÇERMEZ (MerchantId opak tutamaç);
   reveal sayfası + onay-maili SÖKÜLDÜ (087 — insan-yüzeyi yok).
   `(RegisterRequest.Approve → Merchant.Create → MerchantCreated → DeliverCredentialCallback ← AdminApproveRegistrationMcpTool "admin_approve_registration")`
6. **Store imzalı callback'i doğrular + persist eder;** teslim başarısızsa Wolverine durable retry
   (kayıpsız yeniden gönderim). Credential log/trace/ekrana düşmez. `(DeliverCredentialCallback)`
7. **Identity.Server duyuruyu tüketir, OpenIddict istemcisi doğar.** `client_id=MerchantId`,
   `client_secret=MerchantKey`; Active olduğu için izinler hemen açılır (idempotent upsert).
   `(MerchantClientEventHandler.Handle(MerchantCreated))`
8. **Store credential-giriş ekranı kayıt anında ikiliyi doğrulatır** — yanıt yalnız
   `{valid}` (eşleşme + Active); başka bilgi sızmaz. `(ValidateMerchantCredentials)`
9. **Operatör başvuruyu reddedebilir** (yalnız Pending'den); neden zorunlu; red callback
   GÖNDERMEZ — store durumu status sorgusuyla öğrenir. Rejected terminaldir ama aynı e-posta
   yeniden başvurabilir. `(RegisterRequest.Reject ← AdminRejectRegistrationMcpTool "admin_reject_registration")`

### Yol B — Admin yönetimi MCP'den (043/044)

1. **Operatör merchant listesini opsiyonel statü filtresiyle sorgular** — sır (MerchantKey/
   SubMerchantKey) VE kişisel veri (044 kırpımı) yanıtta YOK.
   `(AdminGetMerchantsMcpTool "admin_get_merchants")`
2. **Operatör tekil merchant detayını görür** (hassas-dışı alan seti).
   `(AdminGetMerchantMcpTool "admin_get_merchant")`
3. **Operatör hassas-dışı alanları doğal dille günceller** (tip, ad, adres, iletişim adı, vergi
   dairesi, unvan). Handler hassas alanları MEVCUT değerlerden geçirir — invariant'lar tek yerde
   (aggregate) çalışmaya devam eder. `(Merchant.UpdateDetails ← AdminUpdateMerchantMcpTool "admin_update_merchant")`
4. **Operatör statüyü değiştirir** — üç sabit-hedef tool (idempotent no-op, hata dönmez); gerçek
   değişiklikte duyuru yayınlanır (044: yayın MCP tool handler'larında — REST yolu söküldü).
   `(Merchant.ChangeStatus → MerchantStatusChanged ← AdminActivateMerchantMcpTool "admin_activate_merchant")`
   `(AdminDeactivateMerchantMcpTool "admin_deactivate_merchant")` `(AdminSuspendMerchantMcpTool "admin_suspend_merchant")`
5. **Identity.Server izinleri statüye göre açar/kapar.** Yalnız Active token alabilir.
   `(MerchantClientEventHandler.Handle(MerchantStatusChanged))`
6. **`merchant.admin` scope eksikse istek reddedilir** (fail-closed) — `ecommerce-onboarding`
   admin tool'larını ÇAĞIRAMAZ. `(ScopeAuthorizationMiddleware.Before)`

### Yol C — Hassas veri tek ekrandan (044)

1. **Hassas alanlar (Email, GSM, TCKN, IBAN, vergi no) hiçbir MCP sözleşmesinde YER ALMAZ** —
   agent sohbette hassas değişiklik istenirse Admin hassas-veri sayfasının linkini verir.
2. **Operatör Admin ekranından hassas verileri görüntüler/düzenler** — dar BFF çifti
   (`AdminPlaneOnly`; claim'li merchant token'ı giremez). Handler hassas-DIŞI alanları mevcut
   değerlerden geçirir; tip-koşullu kurallar aggregate'te aynen çalışır.
   `(GetMerchantSensitive)` `(Merchant.UpdateDetails ← UpdateMerchantSensitive)`

### Yol D — Merchant self-servis key yenileme (046)

1. **Store S2S yenileme tetikler** (ecommerce-onboarding m2m; merchant key ile DEĞİL — chicken-egg).
   Çıplak MerchantId; yoksa ad/e-posta tam-eşleşme çözümlenir (belirsiz/yok → key DEĞİŞMEZ).
   `(ResolveMerchantByName ← ReissueMerchantKey)`
2. **Merchant taze key alır — yalnız Active'te.** Active değilse Result error, key değişmez.
   `(Merchant.ReissueKey → MerchantKeyReissued ← ReissueMerchantKey)`
3. **Eski key HER temsilde anında ölür:** Identity client_secret + Payment KeyHash yenisiyle değişir.
   `(MerchantClientEventHandler.Handle(MerchantKeyReissued))` `(MerchantApiConsumers.Handle(MerchantKeyReissued))`
4. **Yeni key store `callbackUrl`'ine HMAC-imzalı dayanıklı callback'le teslim edilir** (echo
   `correlationId`; register ile simetrik). S2S yanıtı yalnız kabul makbuzu (key/RevealUrl İÇERMEZ);
   reveal sayfası SÖKÜLDÜ (087). `(DeliverCredentialCallback ← ReissueMerchantKey)`
5. **Her yenileme salt-append denetim kaydı bırakır** (kim/ne zaman/neden); silinemez/değişmez.
   Merchant kendi geçmişini okur (MerchantScoped). `(GetMerchantKeyReissueHistory)`

## Domain kuralları (süreci yöneten değişmezler)

- **Merchant yalnız başvuru onayıyla ve her zaman Active doğar** (044: `Merchant.Create`'in tek
  çağıranı onay handler'ları; doğrudan admin kayıt REST'i YOK).
- **Token verme statü-kapılı:** yalnız Active merchant OpenIddict'ten token alabilir; Identity.Server
  string statüye göre karar verir (BC enum'u Shared'a sızmaz).
- **MerchantKey insana HİÇ render edilmez (087):** credential store'a YALNIZ makine-handoff ile
  gider — HMAC-imzalı dayanıklı callback (reveal sayfası + onay-maili + resend SÖKÜLDÜ). `MerchantCreated`/
  `MerchantKeyReissued` Identity/Payment senkronu için taşır; `MerchantStatusChanged` sır taşımaz.
  MerchantId tek başına sır DEĞİL (opak tutamaç; admin MCP'de görünebilir). Dayanak ADR
  `adr-mcp-control-plane-no-secret-return`.
- **Credential callback tek tetik = onay/reissue:** onaysız teslim YOK; red callback göndermez.
  İmza ham gövde üstünde HMAC-SHA256(`CallbackSecret`) hex; idempotent (echo `correlationId`); 2xx
  dışı → durable retry. `CallbackSecret` ≠ `BootstrapRegistrationKey` ≠ `MerchantKey` (üç ayrı sır).
- **Register ucu iki katman yetki:** m2m bearer (`merchant.write`, taşıma kimliği) + `X-Registration-Key`
  (bootstrap sır, kayıt-ucu dar yetkisi; constant-time, fail-closed). Finansal/PII yalnız S2S gövdede.
- **Kişisel veri agent kanalından geçmez (044):** MCP girdi/çıktı sözleşmelerinde hassas alan
  adı bile yok; kırpma alan silmedir, null/boş döndürme değil. MerchantScoped `GetMerchant` de
  MerchantKey döndürmez (087 scrub).
- **Başvuru ve fabrika doğrulamaları BİLİNÇLİ tekrar eder** (`RegisterRequest.Submit` ve
  `Merchant.Create` birbirini çağırmaz — aggregate'ler arası çağrı yasak).
- **RegisterRequest terminal statüleri (Approved/Rejected) tarihçe olarak silinmez;** yalnız
  Rejected'dan yeniden başvuru açılabilir.
- **Key yenileme yalnız Active merchant'ta (046/087):** taze `mk_` key üretir; eski key her temsilde
  (Identity secret + Payment hash) anında geçersizdir; teslim makine-callback + salt-append denetim
  kaydı. S2S yanıtı/log key SIZDIRMAZ (yalnız iç `MerchantKeyReissued` + imzalı callback gövdesi taşır).
- **Admin MCP tool'ları `merchant.admin` capability scope ister (043)** — `/mcp` endpoint'i tek
  (`merchant.write`); tool-bazlı ince yetki Wolverine middleware'iyle EK katman. Hassas BFF çifti
  `AdminPlaneOnly` — merchant kendi statüsünü/verisini admin düzleminden değiştiremez.

## Sınır (bu BC'nin dokunmadığı)

OpenIddict istemci kaydı/izin senkronu Identity.Server'ın işi (bu BC yalnız event yayınlar).
Merchant'ın çekim/komisyon davranışı Payment.Api/Commission.Api'nin işi — bu BC yalnız statü
referansı için event kaynağı. `Program.cs`'te hâlâ deklare edilen `MerchantProvisioned` yayın
kanalı ve `MerchantCommissionGridReady` dinleyici kuyruğu FİİLEN kullanılmıyor (013 kalıntısı,
ölü altyapı). İyzico'ya gerçek SubMerchant kaydı bu BC'de YAPILMAZ (SubMerchantKey hep null).
Admin-düzlemi REST CRUD (create/update/status/liste + register-requests grubu) 044'te SÖKÜLDÜ —
kalan REST: MerchantScoped tekil okuma + hassas-veri BFF çifti. Merchant.Agent (A2A) + Admin
CRUD sayfaları 043/044'te SÖKÜLDÜ — tüm yönetim dış MCP istemcisinden yürür (047: Claude Desktop →
ECommerce store fasadı → PG `/mcp` downstream; kimlik AgentPlatform IdP).