# Contract: Commission.Api Admin MCP Tool'ları (044)

Mount: `Commission.Api /mcp` (policy `commission.read`). Yazma tool'ları
`[RequiredScope(commission.write)]` + Commission.Api'ye taşınan Wolverine
ScopeAuthorizationMiddleware ile korunur (research R4 — yeni scope üretilmez).
Kademe modeli: `TierDto(FromAmount, RatePercent, FixedFee)` (024/030 sözleşmesi).

## YENİ: `admin_create_commission_policy`

- **Slice**: `Features/Agents/Commands/AdminCreateCommissionPolicy.cs` (`[Transactional]`)
- **Girdi**: `merchantId` (Guid) + `tiers` (liste: fromAmount, ratePercent, fixedFee)
- **Davranış**: merchant başına tek aktif politika kuralı (handler sorgusu, 024 deseni);
  kademe doğrulaması `MarginTariff.Create`'te.
- **Yanıt**: `PolicyId, MerchantId, Tiers, Status`
- **Hatalar**: aktif politika zaten var → `COMMON_MESSAGE_RECORD_DUPLICATE`; kademe doğrulama
  hataları Result ile.

## YENİ: `admin_update_commission_margin`

- **Slice**: `Features/Agents/Commands/AdminUpdateCommissionMargin.cs` (`[Transactional]`)
- **Girdi**: `merchantId` + `tiers` (tam yeni kademe seti — kısmi patch yok)
- **Yanıt**: güncel `PolicyId, MerchantId, Tiers, Status`
- **Hatalar**: politika yok; kademe doğrulama hataları.

## YENİ: `admin_change_commission_status`

- **Slice**: `Features/Agents/Commands/AdminChangeCommissionStatus.cs` (`[Transactional]`)
- **Girdi**: `merchantId` + `status` (string; Active | Passive)
- **Davranış**: geçiş kuralları `CommissionPolicy.ChangeStatus`'ta; geçersiz değer/geçiş
  Result hatası.
- **Yanıt**: `PolicyId, MerchantId, Status`

## DEĞİŞMEYEN

`admin_get_commission_policy` — sözleşmesi aynen (043).