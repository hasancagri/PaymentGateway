# Quickstart: Payment.Api iyzico Wire Material Geçişi (028)

Davranış yok → doğrulama grep + build + test. Detay: [data-model.md](./data-model.md).

## S1 — `Payment.Api/Domains/` sağlayıcı-türeyenden TAM arındı (SC-001)

```bash
grep -rlE "class .*: *(BaseRequestV2|ProviderResourceV2)" src/services/Payment.Api/Domains
```
Beklenen: **çıktı yok** (Domains tamamen boşaldı).

```bash
for d in Payments Installments StoredCards; do
  test -d src/services/Payment.Api/Domains/$d && echo "$d HALA VAR (HATA)" || echo "$d dagitildi (OK)"
done
```

## S2 — Provider dosya sayıları (SC-002)

```bash
echo "Payments:    $(ls src/services/Payment.Api/Provider/Payments/ | wc -l)   (28 beklenir)"
echo "Installments:$(ls src/services/Payment.Api/Provider/Installments/ | wc -l)   (6 beklenir)"
echo "StoredCards: $(ls src/services/Payment.Api/Provider/StoredCards/ | wc -l)   (6 beklenir)"
```

## S3 — Derleme + testler (SC-003, FR-006)

```bash
dotnet build src/services/Payment.Api/Payment.Api.csproj      # 0 hata
dotnet build PaymentGateway.slnx                              # 0 hata (tam çözüm)
dotnet test tests/Merchant.Api.Tests tests/Commission.Api.Tests   # Merchant 30 + Commission 20 yeşil
```

## Başarı ölçütleri eşlemesi

| Doğrulama | SC |
|-----------|-----|
| S1 | SC-001 (+FR-001/002/003/007) |
| S2 | SC-002 |
| S3 | SC-003 (+FR-006) |

> Davranış (canlı iyzico ödeme/taksit/kart) BU İŞTE YOK — charge akışı davranış spec'inde. Payment.Api
> test projesi yok; charge akışı gelince saf domain testleri eklenir.
