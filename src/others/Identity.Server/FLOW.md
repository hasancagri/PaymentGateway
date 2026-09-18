# Identity.Server — Domain Süreci

**BC ne yapar:** Sistemin tek kimlik sağlayıcısıdır (OpenIddict). İnsan akışı yok — yalnız
`client_credentials` (M2M) token verir; verirken **istemci türüne göre** (statik sistem istemcisi
vs. merchant istemcisi) claim + scope demeti belirler; downstream servisler yalnız scope/claim görür.

> Domain-önce anlatı (EventStorming altitude). Sağdaki `(…)` = koda atlama köprüsü, süreç değil.
> Süreç değişince (yeni/silinen adım-policy) bu dosya güncellenir; mekanik rename'i guard yakalar.

## Süreç

1. **Açılışta statik istemciler + API scope'ları idempotent seed edilir**              `(SeedHostedService`
   (admin-ui, payment-agent, merchant-agent, identity-activation,                       ` .StartAsync)`
   ecommerce-onboarding); yalnız `Config`'teki sabit liste dokunulur.
2. **Merchant.Api bir merchant onaylandığında `MerchantCreated` yayınlar**              `(MerchantClientEventHandler`
   (admin `CreateMerchant` ya da başvuru-onayı `ApproveRegisterRequest`);                ` .Handle(MerchantCreated))`
   tüketici bunu OpenIddict istemci kaydına idempotent upsert eder — `ClientId`=
   `MerchantId`, `ClientSecret`=`MerchantKey`, statüye göre izin demeti.
3. **Merchant.Api statü değiştirdiğinde (`ChangeMerchantStatus`) `MerchantStatusChanged`**  `(MerchantClientEventHandler`
   yayınlanır; tüketici KAYDI SİLMEZ/SECRET'I DEĞİŞTİRMEZ — yalnız izin demetini             ` .Handle(MerchantStatusChanged))`
   statüye göre yeniden yazar (`PopulateAsync` ile mevcut secret hash'i taşınır).
4. **Merchant/sistem istemcisi `/connect/token`'a `client_credentials` ile gelir.**     `(TokenEndpoint.HandleAsync)`
   OpenIddict grant/secret/scope'u zaten doğrulamıştır — buraya yalnız geçerli istek düşer.
5. **`sub` = `ClientId`; istemci merchant ise application `Properties`'teki**            `(TokenEndpoint.HandleAsync)`
   `merchant_id` access token'a claim olarak eklenir (statik istemcilerde property
   yok → claim yok — bu ayrım downstream'de admin-düzlemi/merchant-düzlemi ayrımını kurar).
6. **İstenen scope'lardan `ListResourcesAsync` ile `aud` (audience) üretilir**           `(TokenEndpoint.HandleAsync)`
   (`ScopeResources` haritası); servislerin `ValidateAudience`'ı bunu arar.
7. **Access token üretilirken `scope` claim'i tek boşluk-ayrık string yerine**           `(ScopeClaimArrayHandler`
   JSON dizisine çevrilir (RFC 9068 vs. servislerin `RequireClaim("scope", x)`             ` .HandleAsync)`
   tek-tek arama beklentisi arasındaki uyumsuzluk — **TUZAK**, aşağıda ayrı madde).
8. **Token 15 dakika ömürle imzalanıp döner** — self-contained JWT'de anlık iptal          `(Program.cs`
   yok; askıya alınan/pasifleşen merchant en geç bu sürede düşer.                          ` SetAccessTokenLifetime)`
9. *(Tarihsel/ölü yol — bkz. Sınır)* aktivasyon sayfası + `MerchantProvisioned` tüketicisi kodda
   durur ama hiçbir yayıncı `MerchantProvisioned` üretmiyor; 013'ün Provisioning zinciri 023'te söküldü.

## Domain kuralları (süreci yöneten değişmezler)

- **İki istemci düzlemi, tek token ucu.** Statik sistem istemcileri (admin-ui, payment-agent,
  merchant-agent, identity-activation, ecommerce-onboarding) `Config`'te sabit seed edilir ve
  `merchant_id` claim'i TAŞIMAZ; merchant istemcileri (`ClientId`=`MerchantId`) yalnız Merchant.Api
  event'leriyle çalışma anında yaratılır/güncellenir (`MerchantClientEventHandler`) ve claim taşır.
  Downstream `AdminPlaneOnlyRequirement`/`MerchantScopeRequirement` bu claim'in varlığına göre ayrışır.
- **Rol = statü + scope demeti, kayıt/secret'tan bağımsız.** `MerchantStatusChanged` yalnız
  `Permissions` kümesini yeniden yazar (`AddMerchantPermissions`); `ClientId`/`ClientSecret` hash'i
  DEĞİŞMEZ (`PopulateAsync` taşır). Bugünkü tek statü ayrımı: **Active** → tam demet (`merchant.read`,
  `merchant.write`, `cards.write`, `payment.charge`); **Active olmayan** (Passive/Suspended) →
  `GrantsToken` false, `Permissions` boşalır → token isteği `unauthorized_client` ile reddedilir.
- **`payment.write` hiçbir statüde merchant'a verilmez** (yalnız `payment-agent` gibi statik
  istemcilerin scope'unda) — merchant kendi çekim-dışı ödeme yazma yetkisine sahip olamaz.
- **Scope → audience eşlemesi merkezi.** `Config.ScopeResources` her API scope'unu tek bir
  kaynağın (`payment.api`/`merchant.api`/`commission.api`) audience'ına bağlar; yeni scope eklenince
  buraya + ilgili istemcinin `Scopes` listesine eklenir (`SeedHostedService` idempotent upsert eder).
- **`merchant.admin` capability scope (043).** Merchant.Api'nin 6 admin MCP tool'unu tool-bazlı
  korur (`cards.write`/`payment.charge` deseni); YALNIZ `admin-ui` ve `external-admin-agent`
  `Scopes` listesinde — `ecommerce-onboarding` (submit_registration'ı çağıran sistem istemcisi)
  bu scope'u ALMAZ, admin/sistem-istemci ayrımının temeli budur.
- **TUZAK (`ScopeClaimArrayHandler`).** `context.TokenType` URN'dir (`TokenTypeIdentifiers.AccessToken`),
  kısa hint (`TokenTypeHints`) DEĞİL; hint'le kıyaslarsan handler no-op kalır → scope tek string
  kalır → servislerin `RequireClaim("scope", x)` tek-tek arayışı sessizce 403 üretir (029'da canlı
  yaşanan tuzak). Yalnız ACCESS TOKEN'a dokunur, diğer token türleri etkilenmez.
- **Event tüketimi idempotent, message store yok.** `MerchantCreated`/`MerchantStatusChanged` inline
  işlenir (`ProcessInline`); aynı olay N kez teslim edilse OpenIddict istemci kaydı aynı sonuca gelir
  (create-or-update deseni) — durable inbox yerine bu idempotency tek güvence.

## Sınır (bu BC'nin dokunmadığı)

Merchant/ödeme/komisyon iş mantığı yok — yalnız kimlik doğrulama + token'a claim/scope basma. Kendi
kullanıcı deposu (`ApplicationUser`/Identity) kurulu ama **hiç kullanıcı seed edilmiyor, insan login
akışı yok** (yalnız client_credentials). `Activation/MerchantActivationClient.cs` +
`Pages/Activation/Index.cshtml.cs` + `MerchantProvisioned` tüketicisi (`MerchantClientEventHandler
.Handle(MerchantProvisioned)`) kodda duruyor ama **013'ün Provisioning/aktivasyon-bileti zinciri 023
ile söküldü** (`MerchantStatus` artık yalnız Active/Passive/Suspended — Provisioning değeri yok, hiçbir
yayıncı `MerchantProvisioned` üretmiyor, `Merchant.Api`'de `/api/v1/merchants/activation/redeem` ucu
yok) — bu yol fiilen ölü, yeniden canlanırsa ayrı spec bekliyor (bkz. `specs/023-merchant-submerchant-model/spec.md`
Assumptions). Merchant istemci kaydını SİLMEZ (yalnız izin demetini boşaltır) — kalıcı silme yok.
