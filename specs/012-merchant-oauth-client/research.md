# Research: Merchant OAuth İstemci Düzlemi (012)

Phase 0 çıktısı. Tüm belirsizlikler kapatıldı; kararlar D1..D9.

## D1 — Identity.Server'a olay tüketicisi: Wolverine + RabbitMQ listener

**Decision**: Identity.Server'a `UseWolverine` eklenir; `merchant.lifecycle` fanout
exchange'ine bağlı `identity.merchant-sync` kuyruğu dinlenir. Handler'lar
`MerchantClientEventHandlers` sınıfında (Wolverine auto-discovery), OpenIddict
`IOpenIddictApplicationManager` ile client yaratır/günceller. Durable inbox KURULMAZ
(Marten/EF message store entegrasyonu yok) — kuyruk RabbitMQ tarafında durable,
tüketim idempotent olduğundan yeniden teslim güvenli.

**Rationale**: Merchant.Api'nin `ReferenceDataUpdated` tüketim deseninin (010) aynısı;
proje bu kası kurdu. Wolverine message-store'suz da RabbitMQ listener çalıştırır;
idempotent handler + durable kuyruk dev fazı için yeterli. EF-tabanlı Wolverine
durability eklemek fazla defansif (kullanıcı prensibi: dev'de defansif kurulum yok).

**Alternatives considered**: (a) Token anında Merchant API'ye HTTP sorgusu — token yolu
Merchant.Api'ye runtime bağımlı olur, reddedildi (brainstorm). (b) Elle/Admin kaydı —
çift yönetim, reddedildi. (c) Wolverine EF durability — dev fazında gereksiz karmaşıklık.

## D2 — Olay kontratları: MerchantCreated + MerchantStatusChanged (Shared)

**Decision**: `Shared/IntegrationEvents.cs`'e iki record eklenir:
- `MerchantCreated(Guid MerchantId, string MerchantKey, string Status)`
- `MerchantStatusChanged(Guid MerchantId, string NewStatus)`

Yeni fanout exchange `merchant.lifecycle` (`RabbitMqConstants`'a sabit);
Merchant.Api publisher, Identity.Server tüketici. Status string taşınır (enum değil) —
Shared kontratını BC iç enum'una bağlamamak için (`ReferenceDataUpdated.Kind` emsali).

**Rationale**: Mevcut fanout deseniyle bire bir uyum. MerchantKey'in event'te taşınması
kabul edilen risk: bus iç ağda, dev fazı; OpenIddict secret'ı store'da hash'leyerek
saklar (confidential client). StatusChanged secret taşıMAZ (bkz. D4). 013 (conjoined
tenancy) aynı olayları başka amaçla tüketebilir — kontrat genel tutuldu.

**Alternatives considered**: Tek genel `MerchantLifecycleChanged` olayı — created/status
ayrımı handler'da string-switch'e döner; iki ayrı tip Wolverine dispatch'ine daha uygun.

## D3 — Merchant BC yayın noktaları: CreateMerchant + YENİ SetMerchantStatus slice'ı

**Decision**: `CreateMerchantCommandHandler` başarı yolunda `IMessageBus.PublishAsync`
ile `MerchantCreated` yayınlar. Merchant status değiştiren slice bugün YOK (aggregate
metotları var, feature yok) — yeni `SetMerchantStatus` slice'ı eklenir:
`PUT merchants/{merchantId}/status` (`merchant.write` + `AdminPlaneOnly` policy),
gövdede hedef durum; handler aggregate'in `Activate/Deactivate/Suspend` metodunu çağırır
ve `MerchantStatusChanged` yayınlar.

**Rationale**: US3 (askıya alma → token reddi) uçtan uca ancak bu slice'la gösterilebilir.
Uç admin düzlemine ait (merchant kendini askıdan alamamalı) → `AdminPlaneOnly`.
Admin UI ekranı kapsam dışı — quickstart admin token'ıyla curl kullanır.

**Alternatives considered**: Status değişimini kapsam dışı bırakmak — US3 ve FR-003
test edilemez kalırdı; reddedildi.

## D4 — Status-gating: istemci kaydı kalıcı, izinler açılıp kapanır

**Decision**: Identity tüketicisi client'ı SİLMEZ. `MerchantStatusChanged(NewStatus)`:
- `Active` → descriptor'a `GrantTypes.ClientCredentials` + `Endpoints.Token` + scope
  izinleri geri yazılır.
- `Passive`/`Suspended` → bu izinler descriptor'dan çıkarılır (kayıt ve secret hash'i durur).

İzinsiz client'ın token isteği OpenIddict tarafından reddedilir (`unauthorized_client`).
`MerchantCreated` tüketimi idempotent: `FindByClientIdAsync` → varsa descriptor update,
yoksa create.

**Rationale**: Silme yerine izin kapama, yeniden aktivasyonda MerchantKey'in tekrar
taşınmasını gereksiz kılar (StatusChanged yalnız durum taşır). Idempotency doğal:
aynı olay iki kez işlense sonuç aynı descriptor.

**Alternatives considered**: (a) Sil/yeniden yarat — reaktivasyonda secret'ın yeniden
taşınması gerekir, kontratı kirletir. (b) Token ucunda custom status kontrolü —
OpenIddict izin modeli aynı işi bildirimsiz yapıyor; ek kod gereksiz.

## D5 — Token ömrü: GLOBAL 15 dakika

**Decision**: `SetAccessTokenLifetime(TimeSpan.FromMinutes(15))` server config'inde
global uygulanır — admin-ui ve payment-agent token'ları da 15 dk olur.

**Rationale**: OpenIddict'te istemci-başına ömür ancak custom event handler'la olur;
mevcut `AdminTokenHandler`/`AgentTokenHandler` zaten süre-bilinçli (−30 sn proaktif
yenileme + dolunca taze alma) olduğundan davranışsal fark yok (FR-007 ihlal edilmez —
"davranış korunur", token metadata'sı değil). Tek satır vs custom handler: YAGNI.

**Alternatives considered**: Per-client lifetime event handler'ı — spec'in hiçbir
gereksinimini daha iyi karşılamıyor, karmaşıklık ekler; reddedildi.

## D6 — merchant_id claim'i: application Properties'ten TokenEndpoint'e

**Decision**: Identity tüketicisi client yaratırken descriptor `Properties`'ine
`merchant_id = <guid>` yazar. `TokenEndpoint.HandleAsync` client'ın application kaydını
okur; `merchant_id` property'si varsa aynı adlı claim'i access token'a ekler
(destination: AccessToken). Statik istemcilerde (admin-ui, payment-agent) property yok →
claim yok → mevcut davranış.

**Rationale**: "ClientId Guid'e benziyorsa merchant'tır" çıkarımı kırılgan; property
açık işaret. Claim adı `merchant_id` (snake_case, JWT geleneği).

**Alternatives considered**: ClientId parse etmek — örtük, reddedildi. Ayrı istemci-tipi
tablosu — OpenIddict Properties zaten bu iş için var.

## D7 — Enforcement: Common'da MerchantScoped + AdminPlaneOnly policy'leri

**Decision**: Common'a iki policy + tek karar çekirdeği:

- **Saf çekirdek** `MerchantScopeEvaluator.IsAllowed(merchantIdClaim, routeMerchantId)`:
  claim yok → izin (admin/agent düzlemi); claim var + route değeri yok → RET (fail-closed);
  claim var + route değeri var → eşitlik. Birim testler bu fonksiyona yazılır.
- **`MerchantScoped` policy**: `IAuthorizationRequirement` + handler;
  `IHttpContextAccessor` ile route'daki `merchantId` değerini okur, evaluator'a verir.
  Başarısızlık → 403 (authenticated kullanıcıda ASP.NET Core varsayılanı).
- **`AdminPlaneOnly` policy**: token'da `merchant_id` claim'i VARSA ret — yalnız
  claim'siz (statik istemci) token geçer. Admin-düzlemi uçları için (`SetMerchantStatus`).

Kayıt: `AddAuthenticationAndAuthorizationExtension` iki policy'yi + handler'ları +
`IHttpContextAccessor`'ı standart ekler (bugün yalnız Merchant.Api kullanır; kayıt
zararsız, G3'te tüm BC'ler kullanacak). Uçlar policy'yi AÇIKÇA beyan eder:
`.RequireAuthorization(AuthorizationScopes.MerchantRead, AuthorizationPolicies.MerchantScoped)`.

Route standardizasyonu: `GetMerchant` route'u `{id:guid}` → `{merchantId:guid}`
(URL şekli aynı, yalnız şablon adı) — evaluator tek anahtar (`merchantId`) okur.

**Rationale**: Karar mantığı saf fonksiyonda → host'suz birim test (proje konvansiyonu).
Fail-closed varsayılan, listeleme (`GetAllMerchants`) ve `GetMerchantByKey` uçlarını
otomatik kapatır — spec edge case'i. Mekanizma tek yerde (FR-009), G3 yeniden kullanır.

**Alternatives considered**: (a) Endpoint filter/middleware — policy modeli anayasanın
"yetkiyi açıkça beyan et" kuralıyla daha uyumlu. (b) Her handler'da elle if —
mekanizma dağılır, FR-009 ihlali; reddedildi.

## D8 — Merchant token'ının diğer BC'lerden reddi: mevcut audience + scope zinciri

**Decision**: Ek iş YOK. Merchant istemcisine yalnız `merchant.read`/`merchant.write`
scope izni verilir → token audience'ı `merchant.api`. Payment/Commission API'leri
JwtBearer `Audience` doğrulamasıyla (`payment.api`/`commission.api`) 401, scope
policy'leri 403 keser. SC-005 mevcut zincirle sağlanır; quickstart'ta doğrulanır.

**Rationale**: 011 altyapısı bu izolasyonu zaten kuruyor; yeniden inşa gereksiz.

## D9 — Quickstart istemcisi: curl, örnek uygulama yok

**Decision**: Merchant sistemini temsilen quickstart curl kullanır (token al → API çağır).
Örnek istemci uygulaması/SDK yazılmaz. Entegrasyon kuralı ("token'ı cache'le, süresi
dolunca MerchantKey'le taze al, 401'de bir kez yenile") quickstart'a not düşülür.

**Rationale**: Spec ölçütleri HTTP düzeyinde doğrulanabilir; örnek uygulama YAGNI.