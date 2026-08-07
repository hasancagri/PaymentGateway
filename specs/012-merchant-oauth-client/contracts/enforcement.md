# Contract: Uç-başına yetki matrisi (Merchant BC)

Policy'ler: `AuthorizationScopes.MerchantRead/Write` (mevcut) +
`AuthorizationPolicies.MerchantScoped` / `AdminPlaneOnly` (YENİ, Common).
Her uç policy'lerini `RequireAuthorization(...)` ile AÇIKÇA beyan eder (İlke V).

| Uç | Scope policy | Yeni policy | Merchant token sonucu | Admin token sonucu |
|---|---|---|---|---|
| `POST merchants/` | merchant.write | MerchantScoped | 403 (route'ta merchantId yok → fail-closed) | 200 |
| `GET merchants/` (liste) | merchant.read | MerchantScoped | 403 (fail-closed) | 200 |
| `GET merchants/{merchantId}` ¹ | merchant.read | MerchantScoped | kendi id → 200; başkası → 403 | 200 |
| `GET merchants/by-key/{merchantKey}` | merchant.read | MerchantScoped | 403 (fail-closed) | 200 |
| `PUT merchants/{merchantId}/status` (YENİ) | merchant.write | **AdminPlaneOnly** | 403 (claim'li token giremez) | 200 |
| `POST merchants/{merchantId}/settlement-accounts/` | merchant.write | MerchantScoped | kendi → 200; başkası → 403 | 200 |
| `GET .../settlement-accounts/` | merchant.read | MerchantScoped | kendi → 200; başkası → 403 | 200 |
| `GET .../settlement-accounts/{accountId}` | merchant.read | MerchantScoped | kendi → 200; başkası → 403 | 200 |
| `PUT .../settlement-accounts/{accountId}` | merchant.write | MerchantScoped | kendi → 200; başkası → 403 | 200 |
| `PUT .../settlement-accounts/{accountId}/status` | merchant.write | MerchantScoped | kendi → 200; başkası → 403 | 200 |

¹ Route şablonu `{id:guid}` → `{merchantId:guid}` yeniden adlandırılır (URL şekli değişmez).

## Diğer BC'ler (değişiklik yok — mevcut zincir doğrular)

| Yüzey | Merchant token sonucu | Mekanizma |
|---|---|---|
| Payment.Api uçları + `/mcp` | 401 | audience uyuşmazlığı (`aud=merchant.api` ≠ `payment.api`) |
| Commission.Api uçları | 401 | audience uyuşmazlığı |

## Davranış kuralları

- `MerchantScoped`: `merchant_id` claim'i YOKSA her zaman geçer (admin-ui/payment-agent
  regresyonsuz). Claim VARSA route'daki `merchantId` ile birebir eşleşme zorunlu;
  route değeri yoksa RET. Ret sonucu HTTP 403 (authenticated + yetkisiz).
- `AdminPlaneOnly`: `merchant_id` claim'i VARSA RET, yoksa geçer.
- Karar çekirdeği `MerchantScopeEvaluator` (saf statik) — birim test yüzeyi.