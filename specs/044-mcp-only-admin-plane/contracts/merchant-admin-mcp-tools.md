# Contract: Merchant.Api Admin MCP Tool'ları (044)

Mount: `Merchant.Api /mcp` (policy `merchant.write`); admin tool'ları ayrıca
`[RequiredScope(merchant.admin)]` + Wolverine ScopeAuthorizationMiddleware (043 deseni).
Tool adları `Shared/McpToolNames.cs` sabitlerinden.

## YENİ: `admin_get_merchant`

- **Slice**: `Domains/Merchants/Features/Agents/Queries/AdminGetMerchant.cs`
- **Girdi**: `merchantId` (Guid, zorunlu)
- **Yanıt**: `MerchantId, Status, Type, Name, Address, ContactName, ContactSurname, TaxOffice,
  LegalCompanyTitle, CreatedTime`
- **YASAK alanlar**: Email, GsmNumber, IdentityNumber, Iban, TaxNumber, MerchantKey,
  SubMerchantKey — sözleşmede HİÇ yer almaz (boş string olarak bile değil).
- **Hatalar**: kayıt yok / silinmiş → `COMMON_MESSAGE_RECORD_NOT_FOUND`.

## YENİ: `admin_update_merchant`

- **Slice**: `Domains/Merchants/Features/Agents/Commands/AdminUpdateMerchant.cs` (`[Transactional]`)
- **Girdi**: `merchantId` (zorunlu) + hassas-DIŞI alanlar: `type, name, address, contactName,
  contactSurname, taxOffice, legalCompanyTitle`. Hassas alan parametresi SÖZLEŞMEDE YOK.
- **Davranış**: aggregate yüklenir; `UpdateDetails` hassas alanlar MEVCUT değerlerden geçirilerek
  çağrılır (kısmi yazma yok, invariant'lar aynen çalışır).
- **Yanıt**: `admin_get_merchant` ile aynı alan seti (güncel hali).
- **Hatalar**: kayıt yok; geçersiz `type`; aggregate doğrulama hataları (Result ile).

## DEĞİŞEN: `admin_get_merchants`

- `MerchantItem`'dan **Email + GsmNumber çıkarılır**. Kalan: `MerchantId, Status, Type, Name,
  Address, ContactName, ContactSurname`. Diğer davranış (statü filtresi) aynen.

## DEĞİŞEN: `admin_get_pending_registrations`

- Yanıt öğesinden **Email + GsmNumber çıkarılır** (varsa diğer kişisel alanlar da gözden
  geçirilir — başvuru sahibinin kimliğini belirleyen alan MCP'ye çıkmaz; iş görme için
  başvuru Id + site/işletme adı + tarih yeterli).

## DEĞİŞMEYEN

`submit_registration`, `registration_status`, `admin_approve_registration`,
`admin_reject_registration`, `admin_activate/deactivate/suspend_merchant` — sözleşmeleri aynen.