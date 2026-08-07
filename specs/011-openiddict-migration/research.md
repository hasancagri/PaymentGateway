# Research: OpenIddict Migrasyonu + BC API Yetkilendirmesi (011)

**Date**: 2026-08-07 | **Plan**: [plan.md](plan.md)

Blueprint: ECommerce 029 migrasyonu (canlı-doğrulanmış, merged). Aşağıdaki kararlar 029'un hangi
parçalarının alındığını, hangilerinin bilinçli DIŞARIDA bırakıldığını ve bu repo'ya özgü farkları sabitler.

## D1 — IdP yüzeyi: yalnız client_credentials + tek uç

**Decision**: OpenIddict server yalnız `connect/token` ucunu açar; yalnız `AllowClientCredentialsFlow()`.
Authorize/userinfo/logout/refresh uçları, login/consent Razor sayfaları, PKCE akışı KURULMAZ.

**Rationale**: 011'de insan yok (Admin=M2M kararı). En küçük doğru yüzey; insan login'i + RBAC ayrı
feature'da bu iskeletin üstüne gelir (029'un tam yüzeyi orada devreye girer).

**Alternatives considered**: 029'u birebir kopyalamak (authorize+login sayfalarıyla) — ölü kod taşır,
temizlik hedefiyle çelişir; reddedildi.

## D2 — ASP.NET Identity kalır (kullanıcısız)

**Decision**: `AddIdentity<ApplicationUser, IdentityRole>` + EF store kurulur; kullanıcı seed edilmez
(BootstrapAdmin YOK — RBAC feature'ına ait).

**Rationale**: Spec FR-001; G3 (insan login) ve RBAC bu depoyu kullanacak. Şimdiden kurmak migration'ı
tek Initial'da toplar (029 dersi: tek clean migration).

**Alternatives considered**: Identity'yi tamamen atlayıp yalnız OpenIddict tabloları — sonra ikinci
migration + tablo şişmesi; kazanım yok, reddedildi.

## D3 — Scope claim JSON dizisi (029 R3 tuzağı)

**Decision**: `ScopeClaimArrayHandler` 029'dan birebir alınır: `GenerateTokenContext`'te access token
scope'unu boşluk-ayrık tek string'den JSON dizisine çevirir. Guard `TokenTypeIdentifiers.AccessToken`
(URN) ile kıyaslar — `TokenTypeHints` DEĞİL (bilinen no-op tuzağı).

**Rationale**: Servisler `RequireClaim("scope", x)` ile tek tek değer arar; RFC 9068 tek-string yazımında
policy sessizce 403 verir (029'da canlı yaşandı: redirect döngüsü). Spec FR-010 + edge case bunu test eder.

**Alternatives considered**: Servis tarafında string-split'li custom policy — 8 yerde özel kod, ECommerce
paritesi bozulur; reddedildi.

## D4 — Seed: statik + idempotent, secret'lar config'ten

**Decision**: `SeedHostedService` açılışta scope'ları ve 2 istemciyi (admin-ui, payment-agent) idempotent
yazar (varsa güncelle, yoksa yarat). ClientSecret değerleri `Config.cs`'e gömülmez; configuration'dan okunur
(appsettings dev varsayılanı + user-secrets/env override) — spec FR-011.

**Rationale**: 029 deseni (in-memory Duende'nin karşılığı) + ödeme sistemi için sıkılaştırılmış secret
hijyeni. Idempotent "yalnız kendi listesini yaz" davranışı G2'nin dinamik merchant client'larını EZMEZ.

**Alternatives considered**: (a) Secret'ları koda gömmek (029 böyle) — payment bağlamında kabul edilmedi.
(b) Elle SQL/console provizyon — dev akışını kırar; reddedildi.

## D5 — Token doğrulama: mevcut JwtBearer extension değişmeden

**Decision**: BC API'leri Common'daki `AddAuthenticationAndAuthorizationExtension`'ı (JwtBearer + scope
policy) OLDUĞU GİBİ kullanır; `IdentityOption` (Address/Audience) appsettings'e girer. OpenIddict
validation paketi KULLANILMAZ.

**Rationale**: Extension zaten repo'da (kopya, test edilmiş 029 paritesi); JWKS keşfi + `aud` doğrulaması
yeterli. D3 sayesinde scope policy'leri değişiklik istemez.

**Alternatives considered**: `OpenIddict.Validation.AspNetCore` — introspection/DB bağımlılığı gereksiz;
BC izolasyonuna aykırı eğilim (İlke I), reddedildi.

## D6 — Issuer/port: https://localhost:5101

**Decision**: Identity.Server sabit `https://localhost:5101` (launchSettings + `SetIssuer` + tüm
`IdentityOption:Address` değerleri). Dev cert ile HTTPS.

**Rationale**: ECommerce Identity 5001'de; A2A senaryosu iki sistemi AYNI ANDA koşturur (repo emsali:
postgres portu aynı sebeple 5433). Issuer/Authority uyuşmazlığı = token reddi; sabit port tutarlılığı korur.

**Alternatives considered**: Aspire dinamik port — issuer her koşuda değişir, appsettings Authority'leri
kırılır; reddedildi. 5001 paylaşımı — çakışma; reddedildi.

## D7 — İstemci token edinimi: DelegatingHandler (SagaTokenHandler deseni)

**Decision**: Admin BFF'de tek `AdminTokenHandler` (client_credentials, static cache, süreye 30 sn kala
yenileme) 4 typed HttpClient'a `AddHttpMessageHandler` ile takılır. Payment.Agent'ta aynı desenli handler,
MCP client'ının HTTP transport'una verilen HttpClient'a takılır.

**Rationale**: ECommerce `SagaTokenHandler` kanıtlanmış desen; "düz kod" tercihi (kullanıcı: dolaylama
katmanı yerine çıplak framework çağrısı) — Common'a soyutlama ÇIKARILMAZ, her istemci kendi handler'ını taşır.

**Alternatives considered**: Duende.IdentityModel token yönetim paketi — yeni bağımlılık, mevcut desen
dururken gereksiz; reddedildi.

## D8 — Temizlik kapsamı

**Decision**: Silinecekler: Identity.Server `Pages/*` (login/consent/device/ciba/grants/serversidesessions
dahil tüm Duende quickstart UI), `ApiKeys/`, `Data/ApiKey.cs`+`UserScope.cs`, Duende migration'ları/keys,
Duende `Config.cs` içeriği; Common `Auths/ApiKey*` + `Extensions/ApiKeyAuthenticationExtension.cs`;
CPM'den 3 Duende paketi. `src/services/gateway` (YARP, AppHost dışı uyur kopya) BU FEATURE'DA dokunulmaz.

**Rationale**: Spec FR-002/FR-008. Gateway ayrı bir karar (silme/canlandırma) — kapsam sürüklenmesin.

**Alternatives considered**: Gateway'i de silmek — kullanıcı kararı alınmadı, ertelendi.

## D9 — G2 hazırlığı (merchant = MerchantKey istemcisi; BU FEATURE'DA DEĞİL)

**Decision**: 011 hiçbir merchant client'ı açmaz. Hazırlık garantileri: (a) client store DB-tabanlı
(OpenIddict application tablosu) — G2 çalışma anında client yaratabilir; (b) seed yalnız kendi statik
listesine dokunur; (c) token ucu client_credentials dalı `sub=client_id` üretir — G2 `merchant_id` claim +
status-gated scope süzmesini bu dala ekler; (d) scope adları registry'de genişletilebilir (`cards.write`,
`charge` G2/G5'te).

**Rationale**: Kullanıcı kararı (2026-08-07): "G2'de kalsın, 011 hazırlasın". Yol haritası sırası korunur.

**Alternatives considered**: Statik test-merchant client'ı seed'lemek — yarım desen; provizyon/durum
senkronu olmadan yanlış güven verir; reddedildi.

## D10 — Wolverine mesaj-düzeyi scope zorlaması kullanılmaz

**Decision**: Common'daki `ScopeAuthorizationMiddleware`/`[RequiredScope]` altyapısı KALIR ama 011'de
hiçbir mesaja uygulanmaz; koruma endpoint/policy düzeyindedir.

**Rationale**: Tek transit RabbitMQ trafiği iç `ReferenceDataUpdated` fanout'u (dış girdi değil). Endpoint
koruması İlke V'i karşılar; mesaj-düzeyi zorlama gelecekte dış-tetikli mesaj gelirse devreye alınır.

**Alternatives considered**: Her handler'a `[RequiredScope]` — token taşıyan iç mesaj yok, ölü kural üretir.