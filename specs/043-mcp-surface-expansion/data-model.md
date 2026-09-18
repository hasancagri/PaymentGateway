# Data Model: Merchant + Commission MCP Yüzeyi Genişletme

Bu feature **yeni persist edilen alan/tablo eklemez** — Marten şemasında (`merchantDb`,
`commissionDb`) değişiklik yok. Aşağıda yalnız var olan aggregate'lerin bu feature'ın MCP
tool'larınca kullanılan/dönen alanları ve yeni bir konfigürasyon (Options) tipi listelenir.

## Merchant (mevcut aggregate, `Merchant.Api/Domains/Merchants/Merchant.cs`)

Değişiklik yok. Bu feature yalnız `ChangeStatus(MerchantStatus)` metodunu üç yeni admin MCP
tool'undan çağırır (`admin_activate_merchant`/`admin_deactivate_merchant`/`admin_suspend_merchant`,
scope `merchant.admin`).

| Alan | Tip | `admin_get_merchants` yanıtında (US3) | Not |
|---|---|---|---|
| Id | Guid | ✅ | |
| Status | MerchantStatus (Active/Passive/Suspended) | ✅ | serbest 3'lü geçiş, idempotent no-op |
| Type | MerchantType | ✅ | |
| Name | string | ✅ | |
| Email | string | ✅ | |
| GsmNumber, Address | string | ✅ | |
| ContactName/Surname | string | ✅ | |
| IdentityNumber/TaxOffice/TaxNumber/LegalCompanyTitle | string? | ✅ | tipe göre nullable |
| **MerchantKey** | string | ❌ **DIŞLANIR** | sır — hiçbir MCP yanıtında görünmez (FR-002) |
| **SubMerchantKey** | string? | ❌ **DIŞLANIR** | iyzico dahili referans, operatör ilgisi dışı |
| Iban | string | ❌ (varsayılan dışlanır) | plan/implementasyon aşamasında gerekirse eklenir |

## RegisterRequest (mevcut aggregate, `Merchant.Api/Domains/RegisterRequests/RegisterRequest.cs`)

Değişiklik yok (statü makinesi Pending → Approved/Rejected zaten var). Bu feature `Approve`/
`Reject` metotlarını üç yeni admin MCP tool'undan (`admin_get_pending_registrations` sorgu,
`admin_approve_registration`/`admin_reject_registration` komut, scope `merchant.admin`) çağırır.

| Alan | Tip | `admin_get_pending_registrations` yanıtında | Not |
|---|---|---|---|
| Id (RequestId) | Guid | ✅ | approve/reject tool'una parametre olarak geri verilir |
| Status | RegisterRequestStatus | ✅ (her zaman Pending, filtre) | |
| Type, Name, Email, GsmNumber, Address | — | ✅ | operatörün onay kararı için gerekli |
| ContactName/Surname, IdentityNumber, TaxOffice, TaxNumber, LegalCompanyTitle | — | ✅ | KYC-benzeri karar verisi, sır DEĞİL |
| RejectReason | string? | ❌ (yalnız Rejected'da dolu, liste zaten Pending) | |
| MerchantId | Guid? | ❌ (yalnız Approved'da dolu) | |

## CommissionPolicy (mevcut aggregate, `Commission.Api/Domains/CommissionPolicies/`)

Değişiklik yok. Bu feature yalnız salt-okuma erişim ekler (`admin_get_commission_policy`, scope
`commission.read`); create/update MCP'ye açılmaz (FR-003/FR-010).

| Alan | Tip | `admin_get_commission_policy` yanıtında |
|---|---|---|
| MerchantId | Guid | ✅ |
| Margin | MarginTariff (bracket/tier yapılı, 030 kademeli komisyon) | ✅ |
| Status | CommissionPolicyStatus | ✅ |

## YENİ: `merchant.admin` capability scope (Identity.Server)

Persist edilen domain verisi değil, OpenIddict scope/client kaydı (`identityDb`, idempotent
seed). `AuthorizationScopes.MerchantAdmin = "merchant.admin"`, audience `merchant.api`
(`Config.ScopeResources`). Yalnız `admin-ui` ve `external-admin-agent` client'larının `Scopes`
listesine eklenir; `ecommerce-onboarding`'e VERİLMEZ (bkz. research.md #9 — admin/sistem-istemci
ayrımının temeli budur).

## YENİ: Options — Admin Bildirim Ayarı (Merchant.Api)

Persist edilmez, config'ten okunur (Options pattern, CLAUDE.md — `IConfiguration` doğrudan okuma
yasak).

```
Merchant.Api/Options/AdminNotification.cs
  AdminEmail: string   // submit_registration sonrası bilgilendirme mailinin sabit alıcısı
```

`AddOptions<AdminNotification>().BindConfiguration(nameof(AdminNotification))
.ValidateDataAnnotations().ValidateOnStart()` — mevcut `Onboarding` Options'ıyla aynı desende.

## SendEmailRequested (mevcut paylaşılan kontrat, `Shared/IntegrationEvents.cs`)

Değişiklik yok — `record SendEmailRequested(string To, string Subject, string Body, bool
IsHtml = false, EmailAttachmentTable? Attachment = null)`. `submit_registration` handler'ı bu
kontratı `To = AdminNotification.AdminEmail`, `Subject`/`Body` = "PG'ye kayıt yaptırmak isteyen
var" tarzı sabit şablonla doldurup publish eder.

## Durum geçişleri (değişmeyen, referans amaçlı)

- **Merchant**: Active ⇄ Passive ⇄ Suspended (serbest, idempotent no-op aynı statüde).
- **RegisterRequest**: Pending → Approved (terminal) | Pending → Rejected (terminal, aynı e-posta
  yeniden başvurabilir). Terminal durumdan tekrar geçiş İNVALID_OPERATION_ERROR.