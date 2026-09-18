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

### Yol A — Agent başvurusu → admin onay/red (029; tek doğuş yolu — 044)

1. **Dış agent (ECommerce ChatAgent) başvuru açar.** Zorunlu alanlar + e-posta biçimi + TR IBAN
   (mod-97) + işyeri tipine göre uyum matrisi doğrulanır; aynı e-postada Pending başvuru varsa
   reddedilir, Approved varsa "zaten onaylı" hatası döner; Rejected yeniden başvuruyu engellemez.
   `(RegisterRequest.Submit ← SubmitRegistrationMcpTool "submit_registration")`
2. **Başvuru Pending doğar,** admin'e bilgilendirme maili gider (mükerrer denemede GİTMEZ);
   kimlik/sır bu adımda ÜRETİLMEZ, merchant henüz YOK. `(SendEmailRequested)`
3. **Agent durumu e-posta ile sorgulayabilir** (en son başvuru, case-insensitive).
   `(RegistrationStatus ← RegistrationStatusMcpTool "registration_status")`
4. **Operatör (Claude Desktop) bekleyen başvuruları listeler** — yanıtta başvuru sahibinin
   kişisel/kimlik-belirleyen verisi YOK (044: karar için Id + işletme adı/tipi + tarih yeterli).
   `(AdminGetPendingRegistrationsMcpTool "admin_get_pending_registrations")`
5. **Operatör başvuruyu onaylar.** Başvuru bilgileriyle fabrika çağrılır — Merchant Active doğar,
   tek-seferlik `MerchantKey` üretilir; başvuru Approved'a geçer, doğan MerchantId bağlanır;
   yalnız Pending'den mümkündür.
   `(RegisterRequest.Approve → Merchant.Create → MerchantCreated ← AdminApproveRegistrationMcpTool "admin_approve_registration")`
6. **Identity.Server duyuruyu tüketir, OpenIddict istemcisi doğar.** `client_id=MerchantId`,
   `client_secret=MerchantKey`; Active olduğu için izinler hemen açılır (idempotent upsert).
   `(MerchantClientEventHandler.Handle(MerchantCreated))`
7. **Operatör başvuruyu reddedebilir** (yalnız Pending'den); neden zorunlu, durum sorgusunda
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
- **MerchantKey tek-seferlik açığa çıkar** — yalnız Approve yanıtında; `MerchantCreated` taşır,
  `MerchantStatusChanged` sır taşımaz. (MerchantScoped `GetMerchant`'taki MerchantKey alanı
  BİLİNÇLİ istisna — redeem-link modeli gelince kaldırılacak.)
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