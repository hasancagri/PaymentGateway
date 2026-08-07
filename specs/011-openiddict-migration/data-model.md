# Data Model: OpenIddict Migrasyonu + BC API Yetkilendirmesi (011)

**Date**: 2026-08-07 | **Plan**: [plan.md](plan.md)

Domain aggregate yok — altyapı feature'ı. Model: kimlik konfigürasyon varlıkları + identityDb şeması.

## Kimlik konfigürasyon varlıkları (kod-sahipli seed)

### ClientSeed (Config.cs → SeedHostedService → OpenIddict application store)

| Alan | Tip | Kural |
|------|-----|-------|
| ClientId | string | benzersiz; `admin-ui`, `payment-agent` |
| ClientSecret | string | **config'ten** (`Clients:<id>:Secret`); koda gömülmez; store hash'ler |
| DisplayName | string | görünen ad |
| AllowClientCredentials | bool | 011'de her iki istemci için true (tek grant) |
| Scopes | string[] | least-privilege; aşağıdaki istemci tablosu |

011 istemcileri:

| ClientId | Scopes | Kullanan |
|----------|--------|----------|
| `admin-ui` | merchant.read, merchant.write, commission.read, commission.write, payment.read, payment.write | Admin BFF typed HttpClient'ları (AdminTokenHandler) |
| `payment-agent` | payment.read, payment.write | Payment.Agent MCP client (token handler) |

### Scope → Resource (audience) haritası

| Scope | Resource (aud) | BC |
|-------|----------------|----|
| payment.read, payment.write | payment.api | Payment |
| merchant.read, merchant.write | merchant.api | Merchant |
| commission.read, commission.write | commission.api | Commission |

- `AuthorizationScopes` (Common) bu 6 sabiti içerecek şekilde yeniden yazılır; ECommerce seti silinir.
- Reference BC: HTTP yüzeyi yok → scope/audience yok.
- G2 genişlemesi (bu feature'da DEĞİL): `cards.write`, `charge` + merchant client'ları (D9).

## identityDb şeması (EF Core, tek Initial migration)

| Tablo grubu | İçerik | Not |
|-------------|--------|-----|
| AspNet* (Identity çekirdeği) | Users, Roles, UserRoles, Claims... | Kullanıcı seed edilmez; G3/RBAC için zemin |
| OpenIddict* | Applications, Authorizations, Scopes, Tokens | `options.UseOpenIddict()`; client/scope seed buraya |

Silinen (Duende dönemi) yapılar: PersistedGrant tabloları/migration'ları, `ApiKeys`+`UserScopes`
tabloları ve entity'leri (`ApiKey.cs`, `UserScope.cs`) — Initial migration'da hiç yer almaz.

## State / geçiş

Durum makinesi yok. Token yaşam döngüsü OpenIddict'in standart davranışı; istemci tarafında cache
(süre sonuna 30 sn kala yenileme — D7). Seed idempotent: her açılışta statik liste upsert (D4).