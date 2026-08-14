# Contract: MCP Tool Yüzeyi (Merchant.Api `/mcp`)

**Transport**: Streamable HTTP, stateless. **Auth**: Bearer (client_credentials), yüzey tek policy
`merchant.write`. **Tüketici**: ECommerce ChatAgent admin personası (`ecommerce-onboarding`
istemcisi). Tool ADLARI dış sözleşmedir — değişmez.

## `submit_registration`

Merchant kayıt başvurusu açar. Kimlik/sır kabul etmez, merchant OLUŞTURMAZ (admin onayı bekler).

| Parametre | Tip | Zorunlu | Açıklama |
|---|---|---|---|
| `type` | string | ✓ | `Personal` \| `PrivateCompany` \| `LimitedOrJointStockCompany` (case-insensitive) |
| `name` | string | ✓ | İşyeri/site adı |
| `email` | string | ✓ | İletişim + başvuru kimliği (statü sorgusu anahtarı) |
| `gsmNumber` | string | ✓ | Telefon |
| `address` | string | ✓ | Adres |
| `iban` | string | ✓ | TR IBAN (mod-97 doğrulanır) |
| `contactName` | string | ✓ | Yetkili adı |
| `contactSurname` | string | ✓ | Yetkili soyadı |
| `identityNumber` | string? | tipe göre | Personal + PrivateCompany zorunlu |
| `taxOffice` | string? | tipe göre | PrivateCompany + LimitedOrJointStockCompany zorunlu |
| `taxNumber` | string? | tipe göre | LimitedOrJointStockCompany zorunlu |
| `legalCompanyTitle` | string? | tipe göre | PrivateCompany + LimitedOrJointStockCompany zorunlu |

**Başarı yanıtı**: `{ requestId, status: "Pending", message }` (message: alındı + admin onayı bekliyor).

**Hata**: `FeatureObjectResultModel` messages — alan bazlı kod (`INVALID_VALUE`, tip-uyum,
`RECORD_DUPLICATE` bekleyen başvuru, `INVALID_OPERATION_ERROR` zaten onaylı e-posta).

## `registration_status`

| Parametre | Tip | Zorunlu | Açıklama |
|---|---|---|---|
| `email` | string | ✓ | Başvurudaki e-posta (case-insensitive; en SON başvuru esas alınır) |

**Yanıt** (duruma göre):

| Status | Ek alanlar |
|---|---|
| `Pending` | `message` (beklemede) |
| `Rejected` | `rejectReason`, `message` (yeniden başvurabilir) |
| `Approved` | `merchantId`, `merchantKey`, `message` (033 formuna girilecek — dev-açık karar) |
| kayıt yok | `RECORD_NOT_FOUND` hatası |
