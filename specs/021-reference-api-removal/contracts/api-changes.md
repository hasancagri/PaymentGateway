# API Contract Changes: Reference.Api Removal (021)

**Date**: 2026-08-13 | **Plan**: [../plan.md](../plan.md)

Yeni uç YOK. Değişimler: 1 uç kalkar, 3 istek/yanıt şekli sadeleşir. (Dev aşaması:
sürümleme/geriye-uyum yok; tek tüketici Admin BFF + Merchant.Agent, birlikte güncellenir.)

## Kalkan uç

### `GET banks/catalog` (Commission.Api — GetBankCatalog)

- Tamamen SİLİNİR (slice + endpoint kaydı + Admin client metodu `GetBankCatalogAsync` +
  `BankCatalogItem` modeli). Tüketicisi yalnız Admin'di (Banks/Create dropdown +
  SettlementAccounts Create/Edit dropdown — ikisi de serbest girişe döner).

## Değişen istek şekilleri

### `POST banks` (Commission.Api — CreateBank)

| Alan | Önce | Sonra |
|------|------|-------|
| `Code` | zorunlu; katalogda YOKSA red | zorunlu; yalnız kod-benzersizliği kontrolü |
| `Name` | YOK (katalogdan türetilirdi) | **YENİ, zorunlu** (kullanıcı girdisi) |
| `SupportedInstallments` | değişmez | değişmez |

## Değişen yanıt şekilleri

### `GET merchants/{id}` / `GET merchants/by-key/{key}` / MCP `get_merchant` (Merchant.Api)

- Country/City/MCC **ad** alanları yanıttan ÇIKAR; kod alanları kalır. (Agent tool
  çıktısı da aynı sadeleşmeyi alır — isim→id çözümü `Name`/`Email` üzerinden sürer,
  katalog alanlarına bağımlılığı yok.)

### `GET merchants/{merchantId}/settlement-accounts` (+ tekil) (Merchant.Api)

- `BankName` alanı yanıttan ÇIKAR; `BankCode` kalır. Admin listeleri kodu gösterir.

### `POST/PUT merchants/{merchantId}/settlement-accounts`

- Şekil DEĞİŞMEZ; davranış değişir: `BankCode` katalog-varlık kontrolü yapılmaz
  (NotFound dalı kalkar). IBAN/benzersizlik/merchant kontrolleri aynen.

## Policy etkisi

Yok — kalkan uç kendi policy'siyle birlikte gider; kalan uçların policy beyanları değişmez.