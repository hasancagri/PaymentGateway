# Data Model: Settlement Hesabı Yönetim Ekranları (005)

Bu katman **veri saklamaz**. Modeller yalnız (a) 004 API yanıtlarını okuyan DTO'lar ve (b) form
girdisi/görünüm modelleridir. Kalıcılık + invariant 004 Merchant.Api'de. Tümü `src/ui/Admin/Clients/
ApiModels.cs` (DTO) ve sayfa `PageModel` sınıflarında (form/view) yaşar.

## Tüketilen API (004 settlement-accounts)

Base: `api/v1/merchants/{merchantId:guid}/settlement-accounts`. Zarf: başarı → `Data`; hata →
`{ isSuccess:false, messages:[{property,code}] }`; bulunamadı → `404`.

| İşlem | Metot + rota | İstek | Yanıt |
|-------|--------------|-------|-------|
| Ekle | `POST /` | Create body | `{ id }` |
| Listele | `GET /` | — | `{ accounts:[...] }` |
| Tekil | `GET /{accountId}` | — | tam ayrıntı |
| Güncelle | `PUT /{accountId}` | Update body (=Create alanları) | `{ id }` |
| Durum | `PUT /{accountId}/status` | `{ isActive }` | `{ id, status }` |

## DTO'lar (`ApiModels.cs`'e eklenir)

```
// İstekler
record CreateSettlementAccountRequest(string BankCode, string Iban, string AccountOwnerName,
                                       string AccountNo, string AccountDescription);
record UpdateSettlementAccountRequest(string BankCode, string Iban, string AccountOwnerName,
                                      string AccountNo, string AccountDescription);
record SetSettlementAccountStatusRequest(bool IsActive);

// Yanıtlar
class SettlementAccountsResponse { List<SettlementAccountListItem> Accounts }
class SettlementAccountListItem {           // GET / (liste)
    Guid Id; string BankCode; string? BankName; string Iban;
    string AccountOwnerName; string Status;
}
class SettlementAccountDetail {             // GET /{accountId}
    Guid Id; Guid MerchantId; string BankCode; string? BankName; string Iban;
    string AccountOwnerName; string AccountNo; string AccountDescription;
    string Status; DateTime CreatedTime;
}
// { id } için mevcut IdResult yeniden kullanılır.
// Durum yanıtı { id, status } için küçük IdStatusResult { Guid Id; string Status } (yeni) veya IdResult (status yok sayılır).
```

- Alan adları API JSON'ıyla eşleşir (`JsonSerializerDefaults.Web`, camelCase). `BankName` nullable
  (lookup türevi; bilinmeyen kod → null).
- Banka dropdown kaynağı için mevcut `BankCatalogItem { Code, Name }` (Commission client) yeniden kullanılır.

## Görünüm / form modelleri (PageModel içi)

| Sayfa | Model | Alanlar |
|-------|-------|---------|
| Index | `Merchants: List<MerchantListItem>`, `Accounts: List<SettlementAccountListItem>`, `MerchantId?` (BindProperty SupportsGet) | merchant dropdown + tablo |
| Create | `Input` (`BankCode, Iban, AccountOwnerName, AccountNo, AccountDescription`), `Banks: List<BankCatalogItem>`, `MerchantId` | form + banka dropdown |
| Edit | `Input` (Create ile aynı) + `AccountId`, `MerchantId`, `Banks`, `Status` | dolu form + durum aksiyonu |

## Durum gösterimi

- `Status` string API'den gelir: `"Active"` / `"Passive"`. Liste rozet: Active → normal, Passive →
  soluk/etiket. Silme yok — Passive kayıt listede kalır (SC-005).

## İlişkiler

```
Merchant (001 listesi)
   │ seç
   ▼
SettlementAccounts/Index ──(merchantId)──▶ 004 GET /  (yalnız o merchant)
   │
   ├─▶ Create ──(bankCode dropdown)──▶ Commission GET /banks/catalog
   └─▶ Edit ──▶ 004 GET/PUT /{accountId} + PUT /{accountId}/status
```

- Tenant sınırı: her API çağrısı `merchantId` taşır; UI başka merchant verisini istemez/göstermez (SC-004).
- Bu katmanda yeni kalıcı entity YOK.