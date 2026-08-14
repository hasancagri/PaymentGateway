# Quickstart: Tutar-Kademeli Komisyon Marjı (030)

## Ön koşullar

1. AppHost ayakta (`dotnet run --project src/aspire/AppHost/AppHost.csproj`).
2. **Veri sıfırlama (R5)**: eski düz-marjlı CommissionPolicy dokümanları yeni şekle uymaz —
   commissionDb'deki politika dokümanlarını sil (pgAdmin'den `mt_doc_commissionpolicy` tablosu
   truncate, ya da volume sıfırla). Merchant kayıtları etkilenmez.
3. Aktif bir merchant var (029 akışıyla doğmuş olan yeterli).

## S1 — Kademeli tarife oluşturma (US1)

Admin → Komisyon Politikaları → merchant seç, grid'e üç kademe gir:
`0 / 0.025 / 1`, `1000 / 0.02 / 1`, `10000 / 0.018 / 0` → Oluştur.

**Beklenen**: listede tarife kompakt görünür (`0+: %2,5 + 1 TL · 1.000+: %2 + 1 TL · 10.000+: %1,8`), durum Active.

**Negatifler** (her biri kayıt üretmeden, kademe-işaretli hatayla dönmeli):
ilk kademe 500'den başlar; ikinci kademe FromAmount 1000'den küçük/eşit; oran 0.25; 11 kademe.

## S2 — Kademeden hesap (US2)

`POST /api/v1/commission-policies/effective-commission` (admin token) üç tutarla — iyzico maliyeti
örnek "2.50"/"0.25" sabit tutulur, yalnız marj satırı doğrulanır:

| paidPrice | Beklenen gatewayMargin |
|---|---|
| 500 | 13.50 |
| 1000 | 21.00 (sınır üst kademeye) |
| 20000 | 360.00 |

**Ek**: efektif > tutar koruması: 10 TL'lik tutar + yüksek iyzico maliyetiyle hata dönmeli.

## S3 — Tarife güncelleme + tarihçe (US3)

1. Tarife Düzenle → iki kademeli yeni tablo → kaydet → S2'nin 500 TL hesabı yeni orandan dönmeli.
2. Bozuk tabloyla güncelle (boşluklu) → hata, eski tarife duruyor.
3. Pasifleştir → yeni politika oluştur → listede eski kayıt Passive + kademeleriyle görünür.

## S4 — Tek kademe eşdeğerliği (SC-004)

Tek satırlık tarife (`0 / 0.02 / 1`) gir; 100 TL hesap → marj 3.00 (029 canlı senaryosundaki düz
modelle birebir).

## Kapanış

- `dotnet build` 0 hata; `dotnet test tests/Commission.Api.Tests` yeşil (kademeli matris dahil);
  `tests/Merchant.Api.Tests` yeşil (dokunulmaz, regresyon kontrolü).