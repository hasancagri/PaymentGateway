# Quickstart: Iyzico.Provider Çekirdek Çıkarımı — Doğrulama Rehberi

Refactor doğrulaması: davranış-koruma + tekrar-eliminasyonu. Sıra önemli.

## Ön koşul

- Branch: `034-iyzico-provider-extraction`
- Temiz çalışma ağacı (staged 032/033 sonrası küçük Common/Commission edit'leri hariç)

## Adım 0 — Taşıma ÖNCESİ referans imza al (davranış-koruma için)

Taşımadan önce çekirdek dosyaların md5'ini kaydet (özdeşlik kanıtı) — bu değerler taşınan
dosyalarla eşleşmeli (yalnız `RestHttpClientV2.cs` public değişimi + namespace satırı değişir).

```bash
cd src/services
for f in BaseRequestV2 DigestHelper HashGeneratorV2 HttpClient JsonBuilder PagingRequest \
         ProviderConstants ProviderOptions ProviderResourceV2 RequestFormatter \
         RequestStringConvertible RestHttpClientV2 StringHelper ToStringRequestBuilder; do
  md5 -q Payment.Api/Provider/$f.cs
done
```

## Adım 1 — Build 0 hata (FR-006)

```bash
dotnet build
```

Beklenen: 0 hata. Kritik risk noktaları:
- Merchant Onboarding `using Iyzico.Provider;` eklendi mi? (bugün Provider global using'i yoktu)
- `RestHttpClientV2` public mi? (alt klasörler ayrı assembly'den çağırıyor)
- Payment Program.cs `new Iyzico.Provider.ProviderOptions` map'i güncel mi?

## Adım 2 — Testler yeşil (FR-006, US3)

```bash
dotnet test
```

Beklenen: taşımadan önceki gibi %100 geçer.

## Adım 3 — Tekrar elimine edildi (SC-001, US1)

Hiçbir BC'de çekirdek transport dosyası KALMADI:

```bash
cd src/services
for bc in Payment.Api Merchant.Api Commission.Api; do
  echo "=== $bc çekirdek kalıntı (0 olmalı) ==="
  ls $bc/Provider/*.cs 2>/dev/null | grep -E "RestHttpClientV2|HashGeneratorV2|JsonBuilder|DigestHelper|BaseRequestV2|ProviderResourceV2|ProviderOptions|RequestFormatter|StringHelper|ToStringRequestBuilder|PagingRequest|ProviderConstants|RequestStringConvertible|HttpClient" | wc -l
done
```

Beklenen: her BC için `0`. Paylaşılan projede 14 dosya:

```bash
ls ../others/Iyzico.Provider/*.cs | wc -l   # 14
```

## Adım 4 — Sınır kuralı korundu (SC-004, US2)

Paylaşılan projede BC-özel tip (istek/yanıt veya secret'lı config) YOK:

```bash
cd src/others/Iyzico.Provider
grep -rlE "SubMerchant|Payout|CrossBooking|TransactionReport|CreatePaymentRequest|IyzicoProviderSettings|ApiKey.*SecretKey" *.cs | wc -l   # 0 olmalı
```

Bir BC başka BC'nin tipini göremez (izolasyon derleme ile zorlanır — Adım 1 yeşilse geçerli).

## Adım 5 — Davranış bit-düzeyinde aynı (FR-007, US3) [opsiyonel canlı]

En güçlü kanıt: üretilen iyzico istek gövde/başlık/imzası taşımadan önce ile aynı. Sandbox smoke
(Aspire üzerinden), memory'deki test kartıyla (bkz. `reference_iyzico_sandbox_test_cards`):

```bash
dotnet run --project src/aspire/AppHost/AppHost.csproj
# Payment charge (ör. 540667 İş Maximum, taksitli) → iyzico işlem no döner, taksit/tutar önceki gibi
```

Beklenen: charge önceki gibi geçer; imza reddi (`signature`/`hash` hatası) YOK → transport davranışı korunmuş.

## Başarı özeti

| Kontrol | Beklenen |
|---|---|
| build | 0 hata |
| test | yeşil (önceki gibi) |
| BC çekirdek kalıntı | 0 |
| Iyzico.Provider dosya | 14 |
| BC-özel tip sızıntısı | 0 |
| canlı charge (ops.) | önceki gibi geçer |
