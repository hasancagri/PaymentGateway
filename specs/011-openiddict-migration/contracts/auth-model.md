# Contract: Kimlik ve Yetki Modeli (011)

**Date**: 2026-08-07 | **Plan**: [../plan.md](../plan.md) | **Data model**: [../data-model.md](../data-model.md)

## Token ucu (Identity.Server — https://localhost:5101)

```
POST /connect/token
Content-Type: application/x-www-form-urlencoded

grant_type=client_credentials&client_id=admin-ui&client_secret=<secret>&scope=merchant.read merchant.write
```

Cevap: `{ access_token, token_type: "Bearer", expires_in }`. Access token düz imzalı JWT:

- `iss` = `https://localhost:5101` (tüm servislerin `IdentityOption:Address` değeriyle birebir)
- `sub` = client_id (M2M; 029 paritesi)
- `aud` = istenen scope'ların resource'ları (ör. `merchant.api`) — servis `ValidateAudience` ile kendi adını arar
- `scope` = **JSON dizisi** (ScopeClaimArrayHandler; tek-string RFC 9068 biçimi DEĞİL)

Hata sözleşmesi: tanımsız client / yanlış secret / izinsiz scope → OAuth2 standart `invalid_client` /
`invalid_scope` cevabı (400). Başka uç YOK: authorize/userinfo/logout 404 döner (yüzey minimal).

## Korunan yüzeyler ve scope matrisi

Kural: GET → `<bc>.read`, durum değiştiren (POST/PUT/DELETE) → `<bc>.write`. Endpoint,
`.RequireAuthorization("<scope>")` ile policy'yi AÇIKÇA beyan eder (İlke V).

### Merchant.Api (aud: merchant.api)

| Route grubu | read scope | write scope |
|-------------|-----------|-------------|
| `api/v1/merchants` | merchant.read | merchant.write |
| `api/v1/merchants/{merchantId}/settlement-accounts` | merchant.read | merchant.write |

### Commission.Api (aud: commission.api)

| Route grubu | read scope | write scope |
|-------------|-----------|-------------|
| `api/v1/banks` | commission.read | commission.write |
| `api/v1/bank-commissions` | commission.read | commission.write |
| `api/v1/merchant-commissions` | commission.read | commission.write |

### Payment.Api (aud: payment.api)

| Route grubu | read scope | write scope |
|-------------|-----------|-------------|
| `api/v1/pos-accounts` | payment.read | payment.write |
| `api/v1/bin-cards` (`GET /`, `GET /{bin}`) | payment.read | — |
| `api/v1/bin-cards/import` (`POST`) | — | payment.write |
| `/mcp` (tüm MCP yüzeyi) | — | payment.write (aşağıda) |

MCP yüzeyi tek policy ile korunur (`payment.write`): `get_installment_options` / `quote_installments_by_bin`
/ `select_installment` session AÇAR/DEĞİŞTİRİR (salt-okur değil); `payment_status` tek başına read olsa da
yüzey bölünmez — tool çağıran tek istemci `payment-agent` zaten write taşır. (İnce ayrım gerekirse sonraki
feature'da tool-başına policy'ye bölünür.)

### Korunmayanlar

- Reference.Api: HTTP yüzeyi yok (event-only) — kapsam dışı.
- Sağlık/keşif uçları (`MapDefaultEndpoints`, Scalar doc): anonim kalır (koruma iş uçlarına).
- RabbitMQ `ReferenceDataUpdated` fanout'u: iç trafik, token taşımaz (D10).

## İstemci token edinimi

| İstemci | Mekanizma | Config anahtarları |
|---------|-----------|--------------------|
| Admin BFF | `AdminTokenHandler` (DelegatingHandler, static cache, -30 sn yenileme) 4 typed client'a takılır | `IdentityOption:Address`, `AdminAuth:ClientId/ClientSecret` |
| Payment.Agent | Aynı desenli handler; MCP client'ın HTTP transport HttpClient'ına takılır | `IdentityOption:Address`, `AgentAuth:ClientId/ClientSecret` |

Başarısız token edinimi: istemci hatayı YUTMAZ — çağrı başarısız olur ve kullanıcıya/log'a düşer
(spec US3 senaryo 2: sessiz başarı yok).

## G2 uyum notları (bu feature'da implement edilmez)

- Merchant istemcileri OpenIddict application tablosuna ÇALIŞMA ANINDA eklenecek (seed'e değil).
- `client_id=merchantId`, `client_secret=MerchantKey`; token'a `merchant_id` claim'i G2'de girer.
- Status-gated scope süzmesi TokenEndpoint client_credentials dalına G2'de eklenir; 011 bu dalı
  genişletilebilir bırakır (istenen scope ⊆ client izinleri kontrolü OpenIddict'te hazır).