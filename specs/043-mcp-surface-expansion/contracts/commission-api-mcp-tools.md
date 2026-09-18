# Contract: Commission.Api `/mcp` — YENİ Yüzey (sıfırdan)

Bugün Commission.Api'de `/mcp` YOK. Bu feature ekler: `POST /mcp` (Streamable HTTP, Merchant.Api
deseniyle birebir), `RequireAuthorization(AuthorizationScopes.CommissionRead)`. `ecommerce-
onboarding` client'ının `commission.read`/`commission.write` scope'u hiç YOK — bu yüzden ek
`[RequiredScope]`/Wolverine middleware kurulumu GEREKMEZ (endpoint-seviye scope zaten yeterli
ayrım sağlıyor, tek tool var).

## admin_get_commission_policy (`Shared.CommissionAdminTools.GetCommissionPolicy`)

- **Scope**: `commission.read`
- **Input**: `merchantId: Guid`
- **Output**: `{ merchantId, marginSummary: string (ör. "%2.5 + 0.10 TL, 3 kademe"), status }`
  (tam `MarginTariff` şekli implementasyon aşamasında `CommissionPolicy.cs`'ten birebir yansıtılır
  — bkz. data-model.md).
- **Davranış**: mevcut `Features/Queries/GetCommissionPolicy.cs`'in domain sorgusunu agent slice'ı
  kendi kopyasıyla çalıştırır (bilinçli tekrar, conventions.md).
- **Hata**: politika tanımlı değilse hata DEĞİL — "tanımlı politika yok" bilgi mesajı (US4 AS2).

## create/update — MCP'DE YOK (bilinçli sınır)

FR-003/FR-010 — CommissionPolicy create/update bu feature'da MCP'ye açılmaz; mevcut
CommissionPolicies Admin ekranından (REST) yürütülmeye devam eder. Bir operatör MCP'de
oluşturma/güncelleme niyeti belirtirse tool bunu YAPMAZ, ekrana yönlendiren bir mesaj döner
(US4 AS3 — bu yönlendirme MCP tool'unun kendisi değil, LLM'in yanıt metnidir; sistem tarafında
engelleyici bir kontrol gerekmez çünkü ilgili tool zaten YOK).