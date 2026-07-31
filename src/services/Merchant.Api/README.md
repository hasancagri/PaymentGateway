# Merchant.Api

Global merchant registry — merchant kimliğinin source of truth'u. Marten + Wolverine BC.

- **Aggregate:** `Merchant` (isim, iletişim, adres kodu, MCC, webhook, `MerchantStatus`).
- **Doğrulama:** format aggregate'te (`Merchant.Create`); MCC/Country/City **varlık** doğrulaması
  handler'da (`I*Lookup`, kod-içi gömülü referans veri — DB'de değil).
- **Uçlar:** `POST/GET /api/v1/merchants`, `GET /api/v1/merchants/{id}`. Bu dilimde **korumasız**.

Spec: `specs/001-merchant-onboarding-key/`.

## Bu dilimde YOK (ertelendi → Obsidian `DropShop/Yapılacaklar.md`)

- **API key üretimi/hash + provision** → Identity dilimi (key Merchant.Api'de tutulmaz).
- **Yetki/scope enforcement** → Identity dilimi.
- **Marten conjoined multitenancy** → şimdilik tenant yok (global registry).
- **Country/City/MCC lookup → DB/Reference BC terfi** (yönetim gerekirse).
- **`IpList`** (IP whitelist).
- **`MerchantStatus` düz enum** (kullanıcı direktifi "şimdilik"); ileride Enumeration smart-enum'a
  dönüşebilir.