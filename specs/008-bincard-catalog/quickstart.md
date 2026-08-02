# Quickstart — BinCard Kataloğu (008) Doğrulama

Elle doğrulama rehberi. Kapsam: seed + çözümleme + idempotent import + okuma yolu paritesi.

## Önkoşullar

- .NET 10 SDK, Docker (Aspire Postgres için)
- Sistem AppHost'tan kalkar; `payment-api` `paymentDb`'ye bağlı.

## Çalıştırma

```bash
dotnet build
dotnet run --project src/aspire/AppHost/AppHost.csproj    # Postgres + payment-api (seed startup'ta)
dotnet test tests/Payment.Api.Tests                       # saf domain birim testleri
```

## Senaryolar

### S1 — Seed (Story 2)

1. Boş `paymentDb` ile sistemi başlat.
2. **Beklenen**: startup sonrası BinCard katalogu ~9900 kayıt içerir (Marten şemasında `bin_card` tablosu).
3. Sistemi durdur/başlat → kayıt sayısı **değişmez** (seed idempotent, AC-2).

Doğrula: Postgres'te `select count(*) from payment.mt_doc_bincard;` (şema/tablo adı Marten'e göre) ~9900.

### S2 — Çözümleme paritesi (Story 1)

1. Bilinen bir BIN seç (ör. `540667` gibi katalogda olan bir kredi kartı BIN'i).
2. Çözümleme çağır (debug endpoint varsa `GET api/v1/bin-cards/{bin}` veya bir ödeme/quote akışıyla dolaylı).
3. **Beklenen**: dönen banka/kredi/taksit-banka listesi, CP.VPOS `CreditCardBinQuery` sonucuyla **birebir aynı** (SC-001). Kart bankası taksit listesinin başında.
4. Banka kartı BIN'i → `IsCreditCard=false`, taksit-banka listesi boş.
5. **Bilinmeyen BIN** (ör. `000000`) → **null** (istisna yok, sahte-default yok, SC-005).
6. 8 haneli girdi → ilk 6 ile çözülür.

### S3 — Idempotent import (Story 3)

1. Küçük bir liste hazırla: bazıları var olan (değiştirilmiş `bankCode`/`cardProgram`), bazıları yeni BIN.
2. `POST api/v1/bin-cards/import` → yanıt `imported/updated/skipped` sayıları.
3. **Beklenen**: var olanlar güncellendi, yeniler eklendi. Değişen BIN'i tekrar çözümle → yeni değer döner (SC-002: derleme/deploy yok).
4. **Aynı listeyi tekrar** import et → içerik ve toplam kayıt sayısı **değişmez** (SC-004).
5. Listeye geçersiz kayıt (binNumber boş) koy → o kayıt `skipped`, diğerleri yüklenir (FR-010).

### S4 — Okuma yolu switch

1. Bir ödeme/quote akışını tetikle (BIN çözümü kullanan).
2. **Beklenen**: BIN çözümü DB katalogundan gelir; `VPOSClient.CreditCardBinQuery` artık çağrılmaz (kod incelemesi + davranış). CP.VPOS derlemede ama BIN için kullanılmıyor.

## Birim test kapsamı (tests/Payment.Api.Tests)

- `BinCardMapping`: CP.VPOS enum/int → domain enum (tüm değerler + bilinmeyen→Unknown).
- 8→6 fallback seçimi.
- `InstallmentBankCodes` türetme (kart bankası başta; banka kartı/bilinmeyen program → boş).
- Import upsert idempotency (aynı kayıt iki kez → tek kayıt).
- Bilinmeyen BIN → null.

## Kapsam dışı (doğrulanmaz)

Gerçek 8-haneli veri, uluslararası alanlar, admin UI, çağıranın bilinmeyen-BIN politikası (reddet/peşin).