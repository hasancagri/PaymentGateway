# Data Model: 047 Tek MCP Yüzeyinde PG

Yeni aggregate/tablo/event YOK — feature config + kimlik düzlemi işidir. Varlıklar kayıt/kontrat
düzeyinde:

## PG scope bölmesi (AgentPlatform AppRegistry — config)

| Alan | Değer |
|---|---|
| AppId | `pg` |
| ScopeNamespace.Prefix | `pg` (rezerv; ad üretimine karışmaz) |
| Scopes | `merchant.read`, `merchant.write`, `merchant.admin` → aud `merchant.api`; `commission.read`, `commission.write` → aud `commission.api` |
| Roles | `admin` rolüne additive katkı: 5 PG scope'u |
| Clients | boş (yeni istemci yok) |

Kural: scope adları app'ler-arası benzersiz (AppRegistryValidator zorlar; kesişim bugün 0).

## Downstream kaydı (EC Mcp.Gateway FacadeOption — config)

| Name | McpUrl | Not |
|---|---|---|
| `pg-merchant` | `http://localhost:5202/mcp` | sabit URL (ayrı Aspire) |
| `pg-commission` | `http://localhost:5203/mcp` | sabit URL |

## Tool→scope haritası (PG BC holder'ları — kod sabiti, tek kaynak `[RequiredScope]`)

- `MerchantAdminSurface.ToolScopeMap`: 10 tool (admin_get_merchant(s), admin_update/activate/
  deactivate/suspend_merchant, admin_get_pending_registrations, admin_approve/reject_registration,
  admin_resend_credential_link) → `merchant.admin`.
- `CommissionAdminSurface.ToolScopeMap`: `admin_get_commission_policy` → `commission.read`;
  `admin_create_commission_policy`, `admin_update_commission_margin`,
  `admin_change_commission_status` → `commission.write`.

## Platform kimlik bağlantısı (PG Common — Options POCO)

- `PlatformIdentityOption { Address, Audience }` — BC başına audience (`merchant.api` /
  `commission.api`); `"Platform"` JwtBearer şemasını besler. Options pattern (BindConfiguration +
  ValidateOnStart), `IConfiguration` doğrudan okunmaz.

## Emekli istemci (PG Identity.Server — seed)

- `external-admin-agent`: seed listesinden çıkar, `RetiredClientIds`'e girer → açılışta store'dan
  silinir (044 prune). `AdminAgentApplicationManager` + `ClaudeCallbackRedirectUris` onunla ölür.

## Durum geçişleri

Yok — mevcut Merchant statü makinesi ve token verme davranışı değişmez (FR-009).
