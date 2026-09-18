# Research: Merchant + Commission MCP Yüzeyi Genişletme

Faz 0 çıktısı. Tüm kararlar mevcut kod tabanı taranarak alındı; spec'te NEEDS CLARIFICATION
kalmadığı için "unknown" araştırması yok — burada yalnız uygulama kararları belgeleniyor.

## 1. MCP tool ince-sarmalayıcı deseni

**Decision**: Her yeni tool kendi `Features/Agents/{Commands,Queries}/<Name>.cs` dosyasında;
command/query record + `[Transactional]` (yalnız yazanlar) handler + `[McpServerToolType]`
sarmalayıcı aynı dosyada (conventions.md VSA dosya yapısı). Handler var olan aggregate metodunu
(`ChangeStatus`/`Approve`/`Reject`) çağırır, YENİ domain mantığı yazılmaz.

**Rationale**: 038/029'da kurulan `SubmitRegistrationMcpTool`/`RegistrationStatusMcpTool` deseni
birebir tekrar edilir — dış tüketici (Claude desktop) `ListTools` ile keşfeder, tool adı dış
sözleşmedir. Agent slice `Features/Commands|Queries`'e gitmez (bilinçli tekrar, conventions.md).

**Alternatives considered**: Var olan REST command handler'larını (`ChangeMerchantStatus`,
`ApproveRegisterRequest`, `RejectRegisterRequest`) `IMessageBus` ile agent slice'tan çağırmak —
REDDEDİLDİ, conventions.md açıkça yasaklıyor ("MCP tool YALNIZ Agents slice'ını çağırır").

## 2. Merchant statü tool granülerliği

**Decision**: Üç ayrı tool — `admin_activate_merchant`, `admin_deactivate_merchant`,
`admin_suspend_merchant` (sınıflar `AdminActivateMerchant`/vb.) — her biri `ChangeStatus
(MerchantStatus.X)`'i sabit statüyle çağırır (parametre olarak statü almaz), `[RequiredScope
(AuthorizationScopes.MerchantAdmin)]` taşır (bkz. #9). (Clarify Q1 kararı + admin-adlandırma #10.)

**Rationale**: Kullanıcı tercihi — "3 farklı status için 3 farklı MCP". Ayrıca doğal dil çağrısında
LLM'in serbest string statü parametresi üretmesi yerine sabit tool seçmesi daha az hata payı
bırakır (yanlış enum string riski yok).

**Alternatives considered**: Tek `set_merchant_status(merchantId, status)` — daha az yüzey ama
kullanıcı tarafından reddedildi.

## 3. RegisterRequest admin onay/red MCP tool'ları

**Decision**: `admin_get_pending_registrations` (query, `ListRegisterRequests` REST slice'ının
filtrelenmiş eşdeğeri — yalnız Pending), `admin_approve_registration(requestId)`,
`admin_reject_registration(requestId, reason)` — hepsi `[RequiredScope(MerchantAdmin)]` (bkz. #9).
Var olan `RegisterRequestEndpointExtension`'daki REST uçları (Admin BFF'in kullandığı) DOKUNULMADAN
kalır (geriye dönük uyumluluk gerekmiyor ama silmeye gerek yok — Admin sayfası kalksa da endpoint
başka hiçbir tüketiciyi bozmadan durabilir; asıl söküm hedefi Razor sayfası).

**Rationale**: Clarify Q2 — bu ekran MCP'ye taşınıyor, Admin RegisterRequests/Index kaldırılıyor.

**Alternatives considered**: REST endpoint'lerini de silmek — gereksiz risk (başka olası tüketici
kontrolü gerektirir), YAGNI: yalnız Razor sayfası ve varsa yalnız-o-sayfaya özel client kodu silinir.

## 4. Commission.Api MCP server kurulumu (sıfırdan)

**Decision**: `Commission.Api/Program.cs`'e Merchant.Api ile birebir aynı desende
`AddMcpServer().WithHttpTransport(o => o.Stateless = true).WithToolsFromAssembly()` +
`app.MapMcp("/mcp").RequireAuthorization(AuthorizationScopes.CommissionRead)` eklenir (yalnız
read tool olduğu için `CommissionRead` yeterli, `CommissionWrite` GEREKMEZ).

**Rationale**: Commission.Api'de bugün hiç MCP yüzeyi yok (grep doğrulandı); Merchant.Api'nin
029'da kurduğu desen referans alınır.

**Alternatives considered**: Yok — tek makul kurulum yolu, mevcut pakette zaten var olan
`ModelContextProtocol` referansı (Merchant.Api/Payment.Api'de kullanılıyor) Commission.Api'ye
CPM üzerinden eklenir.

## 5. Komisyon sorgu tool'u — hangi veri

**Decision**: `admin_get_commission_policy(merchantId)` var olan `GetCommissionPolicy` REST
query'sinin (Features/Queries) DOMAIN mantığını agent slice'ında bilinçli tekrar eder (aynı Marten
sorgusu); politika yoksa "tanımlı politika yok" mesajı (NotFound değil, FeatureObjectResultModel
ile). Endpoint zaten `CommissionRead` istediği için ek `[RequiredScope]`/middleware GEREKMEZ
(`ecommerce-onboarding`'de commission scope'u hiç yok — bkz. #9).

**Rationale**: Conventions.md — agent slice REST slice'la kod paylaşmaz (bilinçli tekrar).

**Alternatives considered**: `IMessageBus.InvokeAsync` ile REST query'i çağırmak — REDDEDİLDİ (aynı
kural — agent slice `Features/Queries`'e gitmez).

## 6. Admin bildirim maili (submit_registration sonrası)

**Decision**: `SubmitRegistrationCommandHandler`'a `IMessageBus bus` parametresi eklenir; başarılı
`session.Store(request)` sonrası (aynı `[Transactional]` handler içinde, outbox garantisi için)
`bus.PublishAsync(new SendEmailRequested(adminEmail, subject, body))` çağrılır. `adminEmail` yeni bir
Options POCO'dan (`Merchant.Api/Options/AdminNotification.cs` — `AdminEmail` alanı) okunur (Options
pattern, `IConfiguration` doğrudan okuma yasak — CLAUDE.md).

**Rationale**: Mail.Worker altyapısı (`mail.delivery` exchange, `SendEmailRequested` kontratı,
Merchant.Api'nin publisher kaydı) zaten `Program.cs`'te dormant halde duruyor (FLOW.md doğrulandı) —
bu feature ilk aktif çağıran noktayı ekliyor, yeni altyapı KURULMUYOR.

**Alternatives considered**: Yeni bir domain event (`RegisterRequestSubmitted`) yayınlayıp ayrı bir
consumer'da mail üretmek — REDDEDİLDİ, tek kullanım yeri için gereksiz dolaylama (YAGNI); doğrudan
handler içinde publish yeterli (conventions: "fat message, sorgu yok" — Mail.Worker'a geri sormaz).

## 7. Mükerrer başvuru mesajı netliği

**Decision**: `SubmitRegistrationCommandHandler`'daki mevcut `COMMON_MESSAGE_RECORD_DUPLICATE` kodu
KORUNUR (kod değişmez — sözleşme); yalnız MCP tool'un `[Description]`/dönen `Message` alanı
istemci tarafında "talepte bulunmuştunuz, onayı bekleniyor" şeklinde daha anlaşılır bir metne
çevrilir (resource constants dosyasında mesaj metni güncellenir, kod adı sabit kalır).

**Rationale**: FR-012 — kullanıcıya dönen metin netliği, hata KODU (sözleşme) değişmeden.

**Alternatives considered**: Yeni bir hata kodu eklemek — gereksiz (mevcut kod zaten doğru anlamı
taşıyor, yalnız insan-okunur metin iyileştirmesi gerekiyor).

## 8. Merchant.Agent + Admin sayfa söküm sırası

**Decision**: Söküm sırası: (1) yeni MCP tool'lar yazılıp canlı doğrulanır, (2) Admin
"RegisterRequests"/"AgentChat" Razor sayfaları + `MerchantAgentClient.cs` silinir, (3)
`src/agents/Merchant.Agent` projesi + `AppHost.cs`'teki kaydı + `.slnx`'teki referansı silinir.

**Rationale**: FR-006 sıralaması ("önce MCP, sonra UI söküm") — geçiş sırasında sistem hiçbir an
işlevsiz kalmaz.

**Alternatives considered**: Hepsini tek adımda silmek — regresyon riski yüksek, canlı doğrulama
penceresi kalmaz.

## 9. Admin vs sistem-istemci ayrımı — yeni `merchant.admin` capability scope

**Decision**: `Identity.Server/Config.cs`'e yeni scope `merchant.admin` (audience `merchant.api`,
`ScopeResources`'a eklenir) — YALNIZ `admin-ui` ve `external-admin-agent` (042'de kurulan Claude
Desktop client'ı) `Scopes` listesine eklenir; `ecommerce-onboarding` client'ına VERİLMEZ. Merchant.
Api'nin 6 yeni admin tool'u (`admin_activate_merchant`/vb.) kendi command/query record'unda
`[RequiredScope(AuthorizationScopes.MerchantAdmin)]` taşır; `Program.cs`'teki Wolverine `opts`'a
`opts.Policies.AddMiddleware(typeof(ScopeAuthorizationMiddleware), chain =>
chain.MessageType.GetCustomAttribute<RequiredScopeAttribute>() is not null)` eklenir (PG'nin
`Common`'ında zaten dormant duran middleware, İLK KEZ aktive ediliyor). `/mcp` endpoint'i TEK
kalır (`RequireAuthorization(MerchantWrite)`, DEĞİŞMEZ) — ikinci endpoint yok.

**Rationale**: Kod taraması gösterdi ki `ecommerce-onboarding` (submit_registration'ı çağıran
sistem istemcisi) `merchant.read`+`merchant.write` taşıyor — `external-admin-agent` (Claude
Desktop, 042) de AYNI iki scope'u taşıyor. Coarse endpoint-policy (`merchant.write`) veya var olan
`AdminPlaneOnly` policy'si (yalnız `merchant_id` claim'i yokluğuna bakar, ikisi de claim'siz) bu
ikisini AYIRAMIYOR — `ecommerce-onboarding` teorik olarak `admin_activate_merchant`'ı da
çağırabilirdi. PG'nin zaten kullandığı capability-scope deseni (`cards.write`, `payment.charge` —
base `payment.write` YETMEDİĞİNDE eklenen ek yetki katmanı) burada birebir uygulanır. Commission.Api
tarafında ek scope GEREKMEDİ çünkü `ecommerce-onboarding`'in `commission.read`/`write` scope'u hiç
yok — doğal ayrım zaten var.

**Alternatives considered**: (a) ECommerce'in `/mcp` + `/mcp-admin` (path-bazlı tool budama, ayrı
endpoint) deseni — REDDEDİLDİ: o desen ECommerce Catalog'da `/mcp`'nin ANONİM (herkese açık ürün
arama) olmasından doğuyor; PG'de zaten HERKES authenticated, ikinci endpoint + session-bazlı tool
filtreleme gereksiz karmaşıklık (YAGNI). (b) `ecommerce-onboarding`'den `merchant.read`'i almak —
yetersiz, asıl çakışan scope `merchant.write`. (c) Yeni bir `AdminPlaneOnly`-benzeri policy (ör.
client_id allowlist) — REDDEDİLDİ, capability-scope zaten PG idiomunda var ve OpenIddict scope
sistemiyle doğal uyumlu (token içinde görünür, denetlenebilir).

## 10. Tool-name sabitleri + `Admin<Verb><Noun>` adlandırma

**Decision**: `src/others/Shared/McpToolNames.cs` (YENİ dosya) — `MerchantAdminTools` (6 sabit:
`ActivateMerchant = "admin_activate_merchant"` vb.) ve `CommissionAdminTools` (`GetCommissionPolicy
= "admin_get_commission_policy"`) sınıfları. `[McpServerTool(Name = Shared.MerchantAdminTools.X)]`
şeklinde referans edilir (literal YOK). Sınıf adları `Admin<Verb><Noun>` (`AdminActivateMerchant`,
`AdminGetMerchants` vb.) — var olan customer-facing `SubmitRegistration`/`RegistrationStatus`
sınıflarından (isim çakışması yaratmadan) görsel olarak ayrışır.

**Rationale**: ECommerceWithAgentFramework'ün 070 feature'ında (`Shared/McpToolNames.cs`,
`CatalogAdminTools` vb.) kurulan, iki repo arasında paylaşılan mimari katmanın (docs/conventions.md)
parçası sayılabilecek bir disiplin — tool adı dış sözleşimdir (integration-event kontratı emsali),
sabit olması rename'i derleme hatasıyla yakalar. Var olan `submit_registration`/`registration_status`
inline literal'leri GERİYE DÖNÜK değiştirilMEZ (FR-009 kapsam dışı, ilgisiz kodun dokunulması YAGNI
ihlali olurdu) — yalnız YENİ tool'lar bu deseni kullanır.

**Alternatives considered**: Var olan iki tool'u da constants'a taşımak (tam tutarlılık) —
REDDEDİLDİ, bu feature'ın kapsamı dışında stabil kodu dokunmadan bırakmak tercih edildi (minimal
diff).

## 11. RFC 9728 / PKCE+PRM MCP keşif zinciri — kapsam DIŞI

**Decision**: ECommerce'in 070/073 feature'larındaki `AddMcpAdminResourceMetadata`/
`MapMcpResourceMetadata` (401 challenge + protected-resource-metadata, dinamik OAuth keşfi) bu
feature'a DAHİL EDİLMEZ.

**Rationale**: PG'de zaten memory'de kayıtlı ayrı bir gelecek işi olarak duruyor ("MCP auth
PKCE+PRM... PG tek-BC → ayrı fasad gerekli mi açık, brainstorm+spec"); 042 zaten `external-admin-
agent` için statik seed + PKCE akışını kurmuş durumda (Claude Desktop bugün de bağlanabiliyor).
Bu feature'ın hedefi (statü/onay yönetimini MCP'ye taşımak) bu keşif zincirine bağımlı değil.

**Alternatives considered**: Şimdiden kurmak — REDDEDİLDİ, scope genişletme (bu feature zaten 12
FR taşıyor); ayrı spec'e bırakılması memory'de zaten karar altına alınmıştı.

## Özet — çözülmüş NEEDS CLARIFICATION

Yok. Spec clarify aşamasında (4 soru) tüm kritik belirsizlikler çözüldü; plan aşamasında 1 ek
mimari soru (admin/sistem-istemci ayrımı, #9) kullanıcıyla netleşti. Bu araştırma yalnız uygulama
detaylarını (dosya/tool/scope eşlemesi) belgeliyor.