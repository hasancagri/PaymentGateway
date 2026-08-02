# Quickstart — BinCard Katalog Admin UI (009)

Elle doğrulama rehberi (BFF smoke, 005 deseni). Kapsam: tekil detay + filtreli sayfalı liste +
hata/boş durumları. Backend birim testi filtre-parse saf yardımcısıyla; gerisi burada elle.

## Ön koşul

```bash
dotnet build
dotnet run --project src/aspire/AppHost/AppHost.csproj   # Postgres + payment-api + admin-web
```
- 008 seed startup'ta çalışır → katalog ~9957 kayıt (boşsa). Aspire dashboard'da `admin-web` +
  `payment-api` sağlıklı olmalı; `admin-web` artık `payment-api`'ye referanslı.

## Senaryo 1 — Tekil BIN çöz (US1)

1. Admin → "BIN Kataloğu" → "BIN Ara" (Resolve).
2. `365770` gir → **Beklenen**: banka `0124`, tip **Kredi**, marka **Troy**, program **Bonus**,
   ticari **Evet**, taksit-banka listesi (kart bankası `0124` başta). (008 paritesi, SC-003.)
3. Bir **banka kartı** BIN'i gir (tip Debit olan) → taksit-banka listesi **boş** + bilgi mesajı.
4. `999999` gir → **"bu BIN katalogda yok"** (404, çökme değil).
5. `36577012` (8 hane) gir → ilk 6 (`365770`) ile çözülür.
6. Boş / `abc` / `123` (kısa) gir → **Türkçe doğrulama mesajı**, çağrı yapılmaz.

## Senaryo 2 — Filtreli sayfalı liste (US2)

1. Admin → "BIN Kataloğu" (Index) → **Beklenen**: ilk sayfa (25 kayıt) + toplam/sayfa göstergesi;
   tüm katalog tek sayfada DEĞİL.
2. Banka kodu `0062` filtrele → yalnız `0062` kayıtları, sayfalı.
3. Ayrıca kart programı **Bonus** seç → kesişim (`0062` + Bonus).
4. Kart tipi **Kredi** / marka **Troy** / ticari **Evet** filtrelerini tek tek uygula → her biri
   sonucu daraltır; birlikte AND çalışır.
5. Uymayan kombinasyon (ör. banka `9999`) → **"sonuç yok"** (çökme değil).
6. Sonraki/önceki sayfa → doğru dilim + sayfa göstergesi.

## Senaryo 3 — Hata dayanıklılığı (FR-012)

1. Aspire'da `payment-api`'yi durdur.
2. Resolve/Index aç → **Türkçe sunucu hatası** mesajı; Admin paneli çökmez.

## Doğrulama uçları (opsiyonel, API doğrudan)

```bash
# detay
curl "http://<payment-api>/api/v1/bin-cards/365770"
# liste (filtre + sayfa)
curl "http://<payment-api>/api/v1/bin-cards?bankCode=0062&cardProgram=Bonus&page=1&pageSize=25"
```
- Detay JSON: enum'lar **ad** ("Credit"/"Bonus"/"Troy"), `installmentBankCodes` dolu (kredi+program).
- Liste JSON: `data` + `metaData` (totalItemCount/pageNumber/pageSize/pageCount); `data.length ≤ pageSize`.

## Notlar

- Enum→Türkçe etiket **sunum** (Admin); iş kuralı Payment.Api'de (FR-011).
- Banka **adı** gösterilmez, yalnız kod (bilinçli kapsam dışı).
- Yetki yok — uçlar/ekranlar korumasız (Identity BC'de kapanır).