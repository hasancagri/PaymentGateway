# Contract: Merchant.Api `/mcp` — Yeni Admin Tool'ları

Yüzey: `POST /mcp` (Streamable HTTP, mevcut, TEK endpoint — ikinci endpoint YOK),
`RequireAuthorization(AuthorizationScopes.MerchantWrite)` endpoint-seviyede DEĞİŞMEZ. Aşağıdaki
6 tool AYRICA kendi command/query record'unda `[RequiredScope(AuthorizationScopes.MerchantAdmin)]`
taşır (Wolverine `ScopeAuthorizationMiddleware` uygular) — `merchant.admin` scope'unu YALNIZ
`admin-ui` ve `external-admin-agent` (Claude Desktop) client'ları taşır, `ecommerce-onboarding`
TAŞIMAZ (bkz. research.md #9). Tool adları `Shared.MerchantAdminTools` sabitlerinden okunur.
Var olan `submit_registration`/`registration_status` sözleşmeleri bu dosyada TEKRAR EDİLMEZ
(FR-009 gereği değişmiyor, scope'ları da DEĞİŞMEZ — yalnız `merchant.write`/`merchant.read`).

## admin_activate_merchant (`MerchantAdminTools.ActivateMerchant`)

- **Scope**: `merchant.admin` (+ endpoint-seviye `merchant.write`)
- **Input**: `merchantId: Guid`
- **Output**: `{ merchantId, previousStatus, newStatus: "Active", changed: bool }`
- **Davranış**: `Merchant.ChangeStatus(MerchantStatus.Active)` çağırır. Zaten Active ise
  `changed=false`, hata DÖNMEZ (idempotent no-op — statü makinesi kuralı).
- **Hata**: merchant bulunamazsa `NotFound`; scope eksikse `401/403` (`UnauthorizedAccessException`
  → global exception handler).

## admin_deactivate_merchant (`MerchantAdminTools.DeactivateMerchant`)

- **Scope/Davranış/Hata**: activate ile birebir aynı, hedef `MerchantStatus.Passive`.

## admin_suspend_merchant (`MerchantAdminTools.SuspendMerchant`)

- **Scope/Davranış/Hata**: activate ile birebir aynı, hedef `MerchantStatus.Suspended`.

## admin_get_merchants (`MerchantAdminTools.GetMerchants`)

- **Scope**: `merchant.admin`
- **Input**: `status: string?` (opsiyonel — `Active`/`Passive`/`Suspended`; boşsa tümü)
- **Output**: `Merchant[]` — `{ merchantId, status, type, name, email, gsmNumber, address,
  contactName, contactSurname }` (bkz. data-model.md — MerchantKey/SubMerchantKey/Iban HARİÇ).
- **Davranış**: yalnız `Merchant` aggregate'i sorgular (RegisterRequest'e karışmaz — plan
  clarify kararı).

## admin_get_pending_registrations (`MerchantAdminTools.GetPendingRegistrations`)

- **Scope**: `merchant.admin`
- **Input**: (parametresiz)
- **Output**: `RegisterRequest[]` — yalnız `Status == Pending`: `{ requestId, type, name, email,
  gsmNumber, address, contactName, contactSurname, identityNumber?, taxOffice?, taxNumber?,
  legalCompanyTitle? }`.
- **Davranış**: `session.Query<RegisterRequest>().Where(r => r.Status == Pending)` — agent slice
  kendi sorgusu (mevcut `ListRegisterRequests` REST query'siyle KOD PAYLAŞMAZ, bilinçli tekrar).

## admin_approve_registration (`MerchantAdminTools.ApproveRegistration`)

- **Scope**: `merchant.admin`
- **Input**: `requestId: Guid`
- **Output**: `{ requestId, status: "Approved", merchantId: Guid }`
- **Davranış**: `RegisterRequest.Approve(newMerchantId)` → yeni `Merchant` doğar (mevcut
  `ApproveRegisterRequestCommandHandler` ile birebir aynı iş kuralı, agent slice kendi kopyasını
  taşır).
- **Hata**: `requestId` yoksa `NotFound`; terminal statüdeyse `INVALID_OPERATION_ERROR`.

## admin_reject_registration (`MerchantAdminTools.RejectRegistration`)

- **Scope**: `merchant.admin`
- **Input**: `requestId: Guid`, `reason: string`
- **Output**: `{ requestId, status: "Rejected" }`
- **Davranış**: `RegisterRequest.Reject(reason)`.
- **Hata**: `requestId` yoksa `NotFound`; terminal statüdeyse `INVALID_OPERATION_ERROR`; `reason`
  boşsa `COMMON_MESSAGE_VALUE_IS_REQUIRED`.

## submit_registration (DEĞİŞEN DAVRANIŞ, sözleşme/scope AYNI)

- **Input/Output şekli ve scope (`merchant.write`) DEĞİŞMEZ** (FR-009).
- **Yeni yan etki**: başarılı Pending kaydında `SendEmailRequested` publish edilir (yalnız admin'e
  — bkz. data-model.md "Options — Admin Bildirim Ayarı"); istemciye dönen yanıt DEĞİŞMEZ.
- **Mükerrer (Pending) davranışı**: hata KODU aynı (`COMMON_MESSAGE_RECORD_DUPLICATE`), yeni mail
  GÖNDERİLMEZ, mesaj metni "talepte bulunmuştunuz, onayı bekleniyor" şeklinde netleşir.