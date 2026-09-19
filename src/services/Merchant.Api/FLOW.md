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

### Yol A — Hosted form başvurusu → admin onay/red → mail'le teslim (045; doğuş yolu)

1. **Store S2S form oturumu açtırır** (makine kimliği; e-posta = başvuru kimliği). Aynı e-postada
   Pending/Approved varsa oturum AÇILMAZ, yalnız durum döner; oturum süreli (~24 saat) + tek
   başvuruluk. `(OnboardingFormSession.Create ← CreateFormSession)`
2. **Müstakbel merchant formu PG'nin hosted sayfasında doldurur** — PII (TCKN/IBAN) store'a ve
   LLM'e uğramaz. Doğrulama (zorunlu alanlar, e-posta, TR IBAN mod-97, tip-uyum matrisi)
   aggregate'te; hata formu yeniden gösterir, oturumu TÜKETMEZ.
   `(RegisterRequest.Submit ← SubmitOnboardingForm)`
3. **Başvuru Pending doğar, oturum tüketilir,** admin'e bilgilendirme maili gider; kimlik/sır bu
   adımda ÜRETİLMEZ. `(OnboardingFormSession.Consume → SendEmailRequested)`
4. **Store durumu S2S sorgular** (en son başvuru; hiç yoksa "None"). Yanıtta MerchantId/
   MerchantKey ASLA yok. `(GetOnboardingApplicationStatus)`
5. **Operatör (Claude Desktop) bekleyen başvuruları listeler** — yanıtta başvuru sahibinin
   kişisel/kimlik-belirleyen verisi YOK (044: karar için Id + işletme adı/tipi + tarih yeterli).
   `(AdminGetPendingRegistrationsMcpTool "admin_get_pending_registrations")`
6. **Operatör başvuruyu onaylar.** Merchant Active doğar, tek-seferlik `MerchantKey` üretilir;
   başvuru Approved'a bağlanır; AYRICA tek gösterimlik teslim linki (~1 saat) üretilip başvuru
   e-postasına mail'lenir (outbox — commit'siz mail yok).
   `(RegisterRequest.Approve → Merchant.Create → MerchantCreated → CredentialRevealLink.Create → SendEmailRequested ← AdminApproveRegistrationMcpTool "admin_approve_registration")`
7. **Merchant teslim sayfasını açar: MerchantId + MerchantKey BİR KEZ gösterilir** — gösterim
   linki tüketir; ikinci açılış/süre sonu nötr sayfa. Operatör yeni teslim linki üretebilir
   (eskiler ölür, yeni mail). `(CredentialRevealLink.Consume ← RevealCredentials)`
   `(CredentialRevealLink.Kill ← AdminResendCredentialLinkMcpTool "admin_resend_credential_link")`
8. **Identity.Server duyuruyu tüketir, OpenIddict istemcisi doğar.** `client_id=MerchantId`,
   `client_secret=MerchantKey`; Active olduğu için izinler hemen açılır (idempotent upsert).
   `(MerchantClientEventHandler.Handle(MerchantCreated))`
9. **Store credential-giriş ekranı kayıt anında ikiliyi doğrulatır** — yanıt yalnız
   `{valid}` (eşleşme + Active); başka bilgi sızmaz. `(ValidateMerchantCredentials)`
10. **Operatör başvuruyu reddedebilir** (yalnız Pending'den); neden zorunlu, durum sorgusunda
    karşı tarafa iletilir; Rejected terminaldir ama aynı e-posta yeniden başvurabilir.
    `(RegisterRequest.Reject ← AdminRejectRegistrationMcpTool "admin_reject_registration")`

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

## Domain kuralları (süreci yöneten değişmezler)

- **Merchant yalnız başvuru onayıyla ve her zaman Active doğar** (044: `Merchant.Create`'in tek
  çağıranı onay handler'ları; doğrudan admin kayıt REST'i YOK).
- **Token verme statü-kapılı:** yalnız Active merchant OpenIddict'ten token alabilir; Identity.Server
  string statüye göre karar verir (BC enum'u Shared'a sızmaz).
- **MerchantKey tek-seferlik açığa çıkar (045):** insan teslimi YALNIZ tek gösterimlik reveal
  sayfası; `MerchantCreated` Identity senkronu için taşır, `MerchantStatusChanged` sır taşımaz.
  (029'un ECom-yönlü MCP çifti — submit_registration + registration_status — 045 US4'te SÖKÜLDÜ;
  MerchantScoped `GetMerchant`'taki key alanı ayrı fasılda değerlendirilir.)
- **Form/teslim token'ları sunucu-durumlu, süreli, tek kullanımlık** — 256-bit URL-safe; loglara
  yazılmaz; nötr 404 (token doğruluğu sızdırılmaz); yeni teslim linki eskiyi öldürür.
- **Kişisel veri agent kanalından geçmez (044):** MCP girdi/çıktı sözleşmelerinde hassas alan
  adı bile yok; kırpma alan silmedir, null/boş döndürme değil.
- **Başvuru ve fabrika doğrulamaları BİLİNÇLİ tekrar eder** (`RegisterRequest.Submit` ve
  `Merchant.Create` birbirini çağırmaz — aggregate'ler arası çağrı yasak).
- **RegisterRequest terminal statüleri (Approved/Rejected) tarihçe olarak silinmez;** yalnız
  Rejected'dan yeniden başvuru açılabilir.
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
CRUD sayfaları 043/044'te SÖKÜLDÜ — tüm yönetim dış MCP istemcisinden (Claude Desktop,
`external-admin-agent`) yürür.