# Merchant.Api — Domain Süreci

**BC ne yapar:** Gateway'e katılan **satıcıyı (Merchant)** kaydeder ve yaşam döngüsünü (Active/
Passive/Suspended) yönetir; hem admin düzleminden doğrudan kayıt hem de dış (ECommerce) agent'ın
MCP ile açtığı **başvuru → onay/red** akışını destekler. Merchant doğduğunda/statüsü değiştiğinde
Identity.Server'a duyurur (OpenIddict makine istemcisi senkronu) — merchant'ın gateway'e token'lı
erişimi bu duyuruya bağlıdır.

> Domain-önce anlatı (EventStorming altitude). Sağdaki `(…)` = koda atlama köprüsü, süreç değil.
> Süreç değişince (yeni/silinen adım-event-policy) bu dosya güncellenir; mekanik rename'i guard yakalar.

## Süreç

### Yol A — Admin doğrudan kayıt (023)

1. **Operatör merchant kaydı açar.** Zorunlu alanlar + e-posta biçimi + TR IBAN (mod-97) + işyeri
   tipine göre uyum matrisi (Personal → kimlik no; PrivateCompany → kimlik no + vergi dairesi +
   unvan; LimitedOrJointStockCompany → vergi dairesi + vergi no + unvan) doğrulanır.
   `(Merchant.Create → MerchantCreated)`
2. **Merchant Active doğar.** Benzersiz kimlik + tek-seferlik `MerchantKey` (makine erişim sırrı,
   `"mk_" + Guid`) üretilir; yanıtta yalnız bu ilk anda döner, sonraki hiçbir sorguda görünmez
   (GetMerchant'taki MerchantKey alanı BİLİNÇLİ istisna — redeem-link modeli gelince kaldırılacak).
3. **Identity.Server duyuruyu tüketir, OpenIddict istemcisi doğar.** `client_id=MerchantId`,
   `client_secret=MerchantKey`; statü Active olduğu için `merchant.read`/`merchant.write`/
   `cards.write`/`payment.charge` izinleri hemen açılır (idempotent upsert — yeniden teslimde
   aynı sonuç). `(MerchantClientEventHandler.Handle(MerchantCreated))`
4. **Operatör bilgileri günceller.** Create ile aynı doğrulama seti; Id/MerchantKey/Status
   değişmez. `(Merchant.UpdateDetails)`
5. **Operatör statüyü değiştirir** (Active/Passive/Suspended arası serbest geçiş). Aynı statüye
   geçiş idempotent no-op (event yayınlanmaz); gerçek değişiklikte duyuru gider.
   `(Merchant.ChangeStatus → MerchantStatusChanged)`
6. **Identity.Server izinleri statüye göre açar/kapar.** Yalnız Active token alabilir; Passive/
   Suspended'da izinler temizlenir (kayıt/secret hash durur, yalnız permission seti değişir).
   `(MerchantClientEventHandler.Handle(MerchantStatusChanged))`

### Yol B — Agent başvurusu → admin onay/red (029)

1. **Dış agent (ECommerce ChatAgent) başvuru açar.** Aynı alan seti + aynı doğrulama (023 ile
   bilinçli inline kopya); aynı e-postada bekleyen (Pending) başvuru varsa reddedilir, Approved
   varsa "zaten onaylı" hatası döner; Rejected yeniden başvuruyu engellemez.
   `(RegisterRequest.Submit ← SubmitRegistrationMcpTool "submit_registration")`
2. **Başvuru Pending doğar,** admin onayını bekler; kimlik/sır bu adımda ÜRETİLMEZ, merchant
   henüz YOK.
3. **Agent durumu e-posta ile sorgulayabilir** (en son başvuru, case-insensitive).
   `(RegistrationStatus ← RegistrationStatusMcpTool "registration_status")`
4. **Operatör başvuruyu onaylar.** Başvuru bilgileriyle Yol A'daki fabrika çağrılır (Merchant
   Active doğar + MerchantKey üretilir); başvuru Approved'a geçer, doğan MerchantId bağlanır;
   yalnız Pending'den mümkündür. `(RegisterRequest.Approve → Merchant.Create → MerchantCreated)`
5. **Identity.Server aynı duyuruyu tüketir** (Yol A adım 3 ile birebir — istemci doğar, Active
   izinleri açılır).
6. **Agent onay sonrası durumu sorguladığında MerchantId + MerchantKey döner** (bilinçli dev-açık
   karar — redeem-link teslim modeli gelince bu alanlar kaldırılacak); operatör bu ikiliyi kendi
   panelindeki merchant kimlik formuna elle taşır.
7. **Operatör başvuruyu reddedebilir** (yalnız Pending'den); neden zorunlu, kayıtta saklanır ve
   durum sorgusunda karşı tarafa iletilir; Rejected terminaldir ama aynı e-posta yeniden
   başvurabilir. `(RegisterRequest.Reject)`

## Domain kuralları (süreci yöneten değişmezler)

- **Merchant her zaman Active doğar** (Yol A ve Yol B ile aynı fabrika — `Merchant.Create`).
  Eski kademeli "Provisioning" ön-statüsü (013 doktrini) bu BC'de artık YOK; `MerchantStatus`
  yalnız Active/Passive/Suspended içerir.
- **Token verme statü-kapılı:** yalnız Active merchant OpenIddict'ten token alabilir; Identity.Server
  string statüye göre karar verir (BC enum'u Shared'a sızmaz).
- **MerchantKey tek-seferlik açığa çıkar** — yalnız Create/Approve yanıtında; her iki yolun
  yayınladığı `MerchantCreated` taşır, `MerchantStatusChanged` sır taşımaz.
- **İki yol da aynı doğrulama kuralını BİLİNÇLİ olarak tekrar eder** (`Merchant.Create` ve
  `RegisterRequest.Submit` birbirini çağırmaz — aggregate'ler arası çağrı yasak); tip-uyum matrisi
  ve IBAN/e-posta biçimi her iki yerde ayrı ayrı doğrulanır.
- **RegisterRequest terminal statüleri (Approved/Rejected) tarihçe olarak silinmez;** yalnız
  Rejected'dan yeniden başvuru açılabilir, Approved'dan asla (mükerrer engeli e-posta üstünden).
- **Admin işlemleri (create/update/status/approve/reject/list) `AdminPlaneOnly` — merchant kendi
  token'ıyla kendi kaydı dışında hiçbir şeyi değiştiremez** (statüsünü kendi askıya alamaz).

## Sınır (bu BC'nin dokunmadığı)

OpenIddict istemci kaydı/izin senkronu Identity.Server'ın işi (bu BC yalnız event yayınlar).
Merchant'ın çekim/kart-vault/komisyon davranışı Payment.Api ve (varsa) Commission.Api'nin işi —
bu BC yalnız statü referansı için event kaynağı. `Program.cs`'te hâlâ deklare edilen
`MerchantProvisioned` yayın kanalı ve `MerchantCommissionGridReady` dinleyici kuyruğu bu BC'de
FİİLEN kullanılmıyor (ne yayınlanıyor ne tüketiliyor) — eski 013 kademeli-statü tasarımından kalma
ölü altyapı, güncel süreç Yol A/Yol B'nin dışındadır. İyzico'ya gerçek SubMerchant kaydı bu BC'de
YAPILMAZ (SubMerchantKey alanı hep null, ayrı iş).
