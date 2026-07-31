# Merchant Onboarding + API Key + Admin UI — Tasarım

Tarih: 2026-07-31
Branch: feat/microservices-migration
Durum: Onaylandı, plan bekliyor

## Amaç

Admin bir merchant oluşturur ve o merchant'a bağlı bir API key (`umk_`) üretir. Bu key,
tamamlanmış E-Ticaret (DropShop) uygulamasına verilir; e-ticaret bu key ile Payment
servisini çağırır. Admin ayrıca merchant için kart kombinasyonu başına komisyon belirler.

Bu spec **ilk dilimi** kapsar: merchant yaratma + key üretme + admin UI. Merchant
self-service portal ve tenant enforcement **kapsam dışı** (sonraki dilimler), ama
multitenancy mekanizması bu spec'te kararlaştırılır.

## Karar özeti (bu oturumda kilitlendi)

| Konu | Karar |
|---|---|
| IdP | **Identity.Server (Duende)**. Keycloak sökülür. |
| Rol | **Yok**. Yetki yalnızca scope. `[Authorize(Roles=...)]` kullanılmaz. |
| Admin yetkisi | `merchants.manage` + `commissions.manage` scope. Rol değil. |
| Admin/merchant ayrımı | Scope + `merchant_id` claim varlığı. Admin'de merchant_id yok (global görür). |
| Provisioning yönü | **İş → altyapı**: MerchantManagement önce, sonra Identity.Server. |
| Provisioning coupling | **Senkron orkestrasyon** (event değil). Raw key tek istekte döner. |
| merchant_id binding | `ApplicationUser.MerchantId Guid?` alanı + `merchant_id` claim. |
| Bootstrap admin | **Seed**: username + generated password, bir kez konsola/log'a basılır. |
| Admin UI | **Ayrı Razor Pages BFF projesi**. Identity.Server'a OIDC code-flow. |
| Multitenancy | **Marten conjoined (shared DB, tenant_id kolonu)**. DB-per-tenant reddedildi. |

## Üç eksen — asla karıştırma

- **Scope** = hangi operasyon (`payment.write`, `merchants.manage`) → enforcement.
- **Tenant** = `merchant_id` claim → veri izolasyonu (Marten tenant_id).
- **Role** = kapsam dışı (bu dilimde yok).

## Multitenancy — Marten conjoined (shared database)

Tek veritabanı, tenant-scoped document'lara Marten `tenant_id` kolonu ekler. Session
`ForTenant(merchantId)` ile açılır → query'ler otomatik filtrelenir, handler unutamaz.
Tenant = Merchant, tenant_id = MerchantId.

**Tenant-scoped tipler** (Marten `MultiTenanted`, tenant_id = merchant_id):
`MerchantCommission`, `Payment`, `Settlement`, `MerchantBankAccount`.

**Global tipler** (tenant yok, admin-owned, tüm tenant'lara ortak):
`Merchant` registry, `BankCommission`, `Bank` listesi, Identity/IAM kullanıcıları.

**Cross-tenant erişim:**
- Admin'de merchant_id yok → tenant-scoped tipte cross-tenant okuma **explicit**
  (Marten `AnyTenant` / tenant-bound olmayan session).
- Admin komisyon **yazarken**: `ForTenant(merchantId)` (hedef merchant'ı seçer).
- Admin liste **okurken**: cross-tenant explicit.
- Merchant user (sonraki dilim): `ForTenant(claim.merchant_id)`, hep kendi.

**Bu dilimde:** mekanizma kararı + tenant-scoped tiplerin işaretlenmesi. Claim→ForTenant
enforcement middleware **kapsam dışı** (sonraki dilim). DB-per-tenant reddedildi çünkü
EOD/settlement/komisyon cross-tenant okuma işin kalbi; N-DB fan-out + N-migration +
provisioning'i DB-yaratmaya çevirir. Row-level'da tek `GROUP BY MerchantId`.

## Aktörler ve kanallar

| Aktör | Kanal | Kimlik |
|---|---|---|
| Admin | JWT (Identity.Server login, Admin BFF üzerinden) | `merchants.manage` + `commissions.manage` scope, merchant_id YOK |
| E-Ticaret app (makine) | `umk_` API key | key → user → `merchant_id` + payment scope |
| Merchant user | — | Bu dilimde YOK (sonraki dilim) |

## Mevcut yapı taşları (kod olarak var)

- `MerchantManagement.Api` — `Merchant.Create`, endpoint'ler. Not: kendi `GenerateApiKey`'i
  var ama key artık **Identity.Server** tarafında üretilecek (`umk_`); MerchantManagement'ın
  kendi key akışı ilk dilimde kullanılmaz (sökme/bırakma kararı plan aşamasında).
- `CommissionManagement.Api` — `MerchantCommission(MerchantId, Criteria{CardBrand,CardType,
  TransactionRegion}, BankCommissionId, Rate)`, invariant `merchantRate > bankRate`.
  `CreateMerchantCommission`, `GetMerchantCommissions` var.
- `Identity.Server` (Duende) — `ApplicationUser : IdentityUser`, `UserScope`, `ApiKey`
  (`umk_` prefix, SHA-256 hash, revoke-only), `ApiKeyService` (Generate/Hash/Issue/Resolve),
  `Config.cs` scope/resource/client tanımları.

## Akış: merchant yaratma (senkron orkestrasyon)

```
Admin BFF (Razor Page "Create Merchant") — merchants.manage scope taşır
  |
  1. POST MerchantManagement /merchants (CreateMerchant)   → MerchantId üretir [source of truth]
  |
  2. POST Identity.Server /provision { merchantId, scopes=[payment.read,payment.write] }
     - ApplicationUser oluştur, MerchantId ata
     - UserScope kayıtları ekle
     - ApiKeyService.IssueAsync → umk_ raw key
     → response: { userId, rawKey }
  |
  3. raw umk_ key admin ekranında BİR KEZ gösterilir → e-ticaret app'e verilir
```

Yön: iş servisi (MerchantManagement) → auth altyapısı (Identity.Server). Ters değil.
Event kullanılmaz çünkü `umk_` raw key tek-seferlik sır; senkronda tek istekte döner.

## Yapılacak değişiklikler

### Identity.Server
- `ApplicationUser`'a `Guid? MerchantId` alanı + migration.
- `ProfileService`: token'a `merchant_id` claim bas (varsa).
- `Config.cs`: yeni scope'lar `merchants.manage`, `commissions.manage`; Admin BFF için
  OIDC code-flow client (RedirectUris + AllowedScopes: openid/profile/email + iki manage scope).
- Provision endpoint: `{ merchantId, scopes[] }` → user + UserScope + `umk_` key üret,
  `{ userId, rawKey }` dön. Internal secret / `apikeys.manage` ile korunur.
- Seed: bir admin `ApplicationUser` (username + generated password, bir kez log'a),
  `merchants.manage` + `commissions.manage` UserScope.

### Admin BFF (yeni Razor Pages projesi)
- OIDC code-flow client Identity.Server'a.
- Sayfa: Create Merchant → MerchantManagement + provision orkestrasyonu → raw key göster.
- Sayfa: Merchant Commissions → CommissionManagement, kart kombinasyonu başına rate.
- API çağrıları bearer token (merchants.manage / commissions.manage) ile.

### CommissionManagement
- `MerchantCommission` Marten'da `MultiTenanted` işaretlenir (tenant_id = merchant_id).
- Yazma `ForTenant(merchantId)`; admin liste okuma cross-tenant explicit.

### Keycloak sökümü
- Son commit'lerdeki Keycloak wiring (ServiceDefaults, IAM.Api Keycloak Admin API) kaldırılır.
- Kapsam plan aşamasında ayrıntılanır (ayrı iş kalemi olabilir).

## Komisyon UI (admin)

- `MerchantCommission` zaten kart kombinasyonu (CardBrand × CardType × TransactionRegion)
  başına rate tutuyor.
- Admin ekranı kombinasyon matrisini gösterip boş rate'leri doldurur. Legacy'deki
  `GetAllPossibleMerchantCommissions` deseni referans alınabilir.
- Invariant: `merchantRate > bankRate` (domain zorluyor).

## Kapsam dışı (sonraki dilimler)

- Payment.Api'nin `umk_` çözmesi (ResolveAsync → ClaimsPrincipal).
- Merchant self-service portal + login.
- Tenant enforcement middleware (claim → `ForTenant`). Mekanizma bu spec'te kararlı;
  wiring sonraki dilim.
- Rol modeli.
- Keycloak sökümünün tam kapsamı (ayrı olabilir).

## Açık noktalar (plan aşamasında çözülecek)

- Provision endpoint güvenliği: internal secret vs `apikeys.manage` client-credentials.
- MerchantManagement.GenerateApiKey sökülsün mü, kalsın mı.
- Keycloak söküm kapsamı ayrı spec mi.
- Payment/Settlement tipleri henüz yok/yarım; `MultiTenanted` işareti onlar gelince.