# Identity.Server — Domain Süreci

**BC ne yapar:** Sistemin iç makine (M2M) kimlik sağlayıcısıdır (OpenIddict). `client_credentials` token
verir; istenen scope'lardan audience üretir; downstream servisler yalnız scope/claim görür. Sabit issuer
`https://localhost:5101` (anayasa İlke V makine düzlemi).

> **088 Phase-0 durumu:** Merchant/Commission BC + Admin BFF söküldü → merchant yaşam-döngüsü tüketimi,
> merchant istemci upsert'i ve aktivasyon sayfası KALDIRILDI. Geriye saf M2M token ucu + scope/audience
> üretimi kaldı. Statik seed istemci kalmadı; ölü istemciler store'dan prune edilir.

> Domain-önce anlatı (EventStorming altitude). Sağdaki `(…)` = koda atlama köprüsü, süreç değil.
> Süreç değişince (yeni/silinen adım-policy) bu dosya güncellenir; mekanik rename'i guard yakalar.

## Süreç

1. **Açılışta API scope'ları idempotent seed edilir** (`payment.read` / `payment.write`); yalnız
   `Config`'teki sabit scope listesine dokunulur `(SeedHostedService.StartAsync)`.
2. **Ölü istemciler store'dan silinir.** Seed listesinden çıkarılan kimlikler (`RetiredClientIds` —
   eski agent + admin-ui + aktivasyon + onboarding istemcileri) store'dan kalıcı silinir; ölü ama yetkili
   kimlik bırakılmaz (fail-closed).
3. **Bootstrap admin (opsiyonel) idempotent seed edilir** — yalnız config doluysa VE kullanıcı yoksa
   oluşturulur (`BootstrapAdmin`); insan login yolu (`/Account/Login`) RBAC zemini için durur.
4. **İstemci `/connect/token`'a `client_credentials` ile gelir.** OpenIddict grant/secret/scope'u zaten
   doğrulamıştır — buraya yalnız geçerli istek düşer `(TokenEndpoint.HandleAsync)`.
5. **`sub` = `ClientId`; istenen scope'lar token'a yazılır** `(TokenEndpoint.HandleAsync)`.
6. **İstenen scope'lardan `ListResourcesAsync` ile `aud` (audience) üretilir** (`ScopeResources` haritası);
   servislerin `ValidateAudience`'ı bunu arar `(TokenEndpoint.HandleAsync)`.
7. **Access token üretilirken `scope` claim'i JSON dizisine çevrilir** — servislerin `RequireClaim("scope", x)`
   tek-tek arama beklentisiyle uyum için (TUZAK, aşağıda) `(ScopeClaimArrayHandler.HandleAsync)`.
8. **Token 15 dakika ömürle imzalanıp döner** — self-contained JWT'de anlık iptal yok `(Program.cs
   SetAccessTokenLifetime)`.

## Domain kuralları (süreci yöneten değişmezler)

- **Yalnız makine düzlemi (client_credentials).** İnsan OAuth akışı yok; `/Account/Login` sayfası durur
  ama token ucu yalnız M2M verir. (Store→gateway ödeme yetkisi AYRI otoritede — AgentPlatform platform
  IdP; bu sunucu değil, anayasa İlke V.)
- **Scope → audience eşlemesi merkezi.** `ScopeResources` her API scope'unu bir kaynağın audience'ına
  bağlar; yeni scope buraya + ilgili istemcinin scope listesine eklenir (`SeedHostedService` idempotent upsert).
- **Ölü kimlik fail-closed silinir.** `RetiredClientIds`'teki her id açılışta store'dan silinir — seed
  create/update'li olduğundan liste-dışı kalmak yetmez (kayıt durursa token almaya devam ederdi).
- **TUZAK (`ScopeClaimArrayHandler`).** `scope` claim'i JSON dizisidir; tek-string bırakılırsa servislerin
  `RequireClaim("scope", x)` arayışı sessizce 403 üretir. Yalnız ACCESS TOKEN'a dokunur (029'da canlı
  yaşanan tuzak) — dokunma.

## Sınır (bu BC'nin dokunmadığı)

Ödeme/merchant/komisyon iş mantığı yok — yalnız kimlik doğrulama + token'a scope/audience basma. Kendi
kullanıcı deposu (`ApplicationUser` / Identity) kurulu; şirket-içi tek SSO'ya evrilmesi ayrı tasarım
oturumuna bırakıldı (constitution deferred TODO). 088 ile merchant yaşam-döngüsü tüketimi, merchant istemci
senkronu ve aktivasyon sayfası söküldü — bu yollar fiilen yok.
