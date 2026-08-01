# UI Contract: Settlement Hesabı Ekranları (005)

Admin BFF (Razor Pages) ekran kontratları. Kullanıcı: gateway admin. Tüm veri 004
settlement-accounts API'sinden; banka dropdown Commission `/banks/catalog`'tan. Mesajlar `_Messages`
partial'ı + `MessageText` Türkçe çeviri. Yetki yok.

---

## Giriş noktası — Merchants/Details.cshtml (DEĞİŞİR)

Aksiyon barına, mevcut "Komisyonları" butonunun yanına:

```razor
<a class="btn" asp-page="/SettlementAccounts/Index" asp-route-merchantId="@m.Id">Settlement Hesapları</a>
```

---

## 1. Index — `/SettlementAccounts` (US1)

**Rota**: `GET /SettlementAccounts?merchantId={guid?}`

**Davranış**:
- Merchant dropdown (Merchant.Api `GetAllAsync`). Seçim `onchange` submit (MerchantCommissions deseni).
- `merchantId` boş → "Hesapları görmek için merchant seç" bilgisi.
- Seçili → `SettlementAccountApiClient.GetAccountsAsync(merchantId)` → tablo.
- Boş liste → "Bu merchant için hesap yok. Yeni ekle" bağlantısı (US1 senaryo 2; boş = hata değil).

**Tablo kolonları**: Banka (`{BankCode} ({BankName})`) · IBAN · Sahip · Durum (Active/Passive rozet)
· aksiyon (**Düzenle** → Edit).

**Üst aksiyon**: "Yeni hesap" → `Create?merchantId={id}` (yalnız merchant seçiliyken).

---

## 2. Create — `/SettlementAccounts/Create` (US2)

**Rota**: `GET/POST /SettlementAccounts/Create?merchantId={guid}`

**GET**: banka dropdown (Commission `GetBankCatalogAsync(onlyAvailable:false)`) + boş form.

**Form alanları**:
- **Banka** — `<select>` katalogdan (`value=Code`, metin `Code — Name`). Serbest giriş yok (FR-006).
- **IBAN** — text.
- **Hesap Sahibi** — text.
- **Hesap No** — text (opsiyonel).
- **Açıklama** — text (opsiyonel).

**POST**: `CreateSettlementAccountRequest` → `POST /api/v1/merchants/{merchantId}/settlement-accounts`.
- Başarı → `Flash = "Hesap eklendi."`, `RedirectToPage("Index", new { merchantId })`.
- Hata → `AddErrors(result.Messages)`, banka dropdown yeniden yüklenir, `return Page()` (girdiler korunur).

**Beklenen hata kodları** (400): `COMMON_MESSAGE_INVALID_FORMAT` (IBAN/bankCode), `..._VALUE_IS_REQUIRED`,
`..._RECORD_NOT_FOUND` (banka/merchant), `..._RECORD_DUPLICATE` (aynı IBAN).

---

## 3. Edit — `/SettlementAccounts/Edit` (US3)

**Rota**: `GET/POST /SettlementAccounts/Edit?merchantId={guid}&accountId={guid}`

**GET**: `GetAccountAsync(merchantId, accountId)`; null → `NotFound` bilgisi (tenant sızıntısı yok —
başka merchant'ın accountId'si 404). Form mevcut değerlerle dolu + banka dropdown.

**İki aksiyon**:
1. **Kaydet** (POST default) → `UpdateSettlementAccountRequest` → `PUT /{accountId}`.
   - Başarı → Flash + Index'e redirect. Hata → mesaj + form korunur (bozuk IBAN → eski değer API'de korunur).
2. **Aktif/Pasif yap** (POST handler `OnPostToggleStatus`) → `SetSettlementAccountStatusRequest{ IsActive }`
   → `PUT /{accountId}/status`. Buton mevcut duruma göre "Pasife al" / "Aktif et". Silme YOK.

---

## Tüketilen client — `ISettlementAccountApiClient` (YENİ)

`BaseAddress = http://merchant-api` (Program.cs `AddHttpClient`).

| Metot | Çağrı |
|-------|-------|
| `GetAccountsAsync(merchantId)` | `GET /api/v1/merchants/{merchantId}/settlement-accounts` |
| `GetAccountAsync(merchantId, accountId)` | `GET .../{accountId}` |
| `CreateAsync(merchantId, req)` | `POST .../` |
| `UpdateAsync(merchantId, accountId, req)` | `PUT .../{accountId}` |
| `SetStatusAsync(merchantId, accountId, req)` | `PUT .../{accountId}/status` |

Banka dropdown: mevcut `ICommissionApiClient.GetBankCatalogAsync(onlyAvailable:false)` (yeni metot değil).

## Notlar

- Rota-body tutarlılığı: `merchantId`/`accountId` her zaman sayfa rotasından; forma gömülü Guid taşınmaz
  (gizli alan olarak route değeri).
- `bankName` yanıt-türevi; UI saklamaz, listede gösterir.
- IBAN normalize (boşluksuz/upper) API'den döner; UI olduğu gibi gösterir.