# Contract: Merchant Hassas-Veri BFF Uçları + Sayfası (044)

Amaç: hassas kişisel veri (Email, GsmNumber, IdentityNumber, Iban, TaxNumber) admin düzleminde
YALNIZ bu dar yüzeyden akar; MCP sözleşmelerinde hiç yer almaz (FR-003/FR-004).

## `GET /api/v1/merchants/{merchantId}/sensitive`

- **Slice**: `Domains/Merchants/Features/Queries/GetMerchantSensitive.cs`
- **Auth**: `merchant.read` + `AdminPlaneOnly` (claim'li merchant token'ı GİREMEZ)
- **Yanıt**: `MerchantId, Name (başlık için), Type, Email, GsmNumber, IdentityNumber, Iban,
  TaxNumber`
- **Hatalar**: kayıt yok / silinmiş → NotFound Result.
- MerchantKey/SubMerchantKey bu uçta YOK (sır teslimi bu işin kapsamı dışı; mevcut düzen
  MerchantScoped `GetMerchant`'ta kalır).

## `PUT /api/v1/merchants/{merchantId}/sensitive`

- **Slice**: `Domains/Merchants/Features/Commands/UpdateMerchantSensitive.cs` (`[Transactional]`)
- **Auth**: `merchant.write` + `AdminPlaneOnly`
- **Gövde**: `Email, GsmNumber, IdentityNumber?, Iban, TaxNumber?`
- **Davranış**: aggregate yüklenir; `UpdateDetails` hassas-DIŞI alanlar MEVCUT değerlerden
  geçirilerek çağrılır. Tip-koşullu kurallar (Individual→TCKN, Company→vergi) aggregate'te
  aynen çalışır.
- **Yanıt**: GET ile aynı alan seti (güncel hali).

## Admin UI: `Pages/Merchants/Sensitive.cshtml`

- Rota: `/Merchants/Sensitive?merchantId=<guid>` — agent sohbette bu linki verir (FR-004,
  US2 senaryo 3).
- 042 admin login düzeniyle korunur; `MerchantApiClient` DARALTILIR: yalnız bu iki ucu çağırır.
- İçerik: hassas 5 alan formu + salt-okuma başlık (merchant adı/Id). Başka merchant alanı
  gösterilmez (hassas-dışı yönetim MCP'de).
- Hata durumu: bulunamayan merchant için anlaşılır mesaj (edge case).

## Söküm sonrası Admin UI yüzeyi

Kalan sayfalar: `Index` (giriş/karşılama), 042 login, `Merchants/Sensitive`. Silinen:
`Merchants/Index`, `Merchants/Details`, `CommissionPolicies/*`;
`CommissionPolicyApiClient` + `RegisterRequestApiClient` silinir; `_Layout` menüsü sadeleşir.