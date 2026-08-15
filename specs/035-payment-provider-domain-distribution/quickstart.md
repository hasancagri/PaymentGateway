# Quickstart: Payment Provider Domain Dağıtımı — Doğrulama

Sıra: yapı → build → test → davranış-koruma.

## Ön koşul

- Branch `035-payment-provider-domain-distribution` (034 üstüne)
- 034 Iyzico.Provider mevcut, Payment referans veriyor

## Adım 0 — Taşıma ÖNCESİ charge isteği referansı (davranış-koruma)

Dağıtımdan önce bir charge'ın ürettiği iyzico istek gövdesini not al (log/breakpoint) — dağıtımdan
sonra alan-alan aynı olmalı (buyer/address/basket/card/tutar/imza).

## Adım 1 — Provider/ klasörü kalktı (SC-001, SC-002, US1)

```bash
cd src/services/Payment.Api
test -d Provider && echo "HATA: Provider/ hâlâ var" || echo "OK: Provider/ yok"
# iyzico iş süreçleri yalnız Domains altında:
find Domains -name "*.cs" | grep -iE "ValueObjects/(Buyer|Address|BasketItem|CardInformation)" | wc -l   # 4
```

Beklenen: `Provider/` yok; 4 VO Domains altında.

## Adım 2 — SDK genişledi

```bash
ls ../../others/Iyzico.Provider/Payments ../../others/Iyzico.Provider/Installments ../../others/Iyzico.Provider/StoredCards 2>/dev/null | head
# saf-wire tipler SDK namespace'inde:
grep -rl "namespace Iyzico.Provider.Payments" ../../others/Iyzico.Provider/Payments | head -1
```

## Adım 3 — Anemik record → VO (SC-003, US2)

```bash
cd src/services/Payment.Api
# BuyerInput/BasketItemInput yalnız HTTP DTO olarak kalabilir; domain VO'lar var:
grep -rl "class Buyer\|record Buyer" Domains/Payments/ValueObjects/ && echo "Buyer VO OK"
# VO private ctor + Create:
grep -n "private .*(" Domains/Payments/ValueObjects/Buyer.cs; grep -n "static ResultDomain<Buyer> Create" Domains/Payments/ValueObjects/Buyer.cs
```

Beklenen: 4 VO private ctor + `Create` ile; iyzico serileştirme (`ToPKIRequestString`) VO'da YOK.

## Adım 4 — Build + test (SC-004, US3)

```bash
cd /Users/macbook/Desktop/PaymentGateway
dotnet build          # 0 hata
dotnet test           # mevcut + yeni VO Create doğrulama testleri yeşil
```

Yeni VO testleri: geçersiz email/kimlik/Luhn/expiry → `Create` `Error` döner; geçerli → `Ok`.

## Adım 5 — Davranış bit-korundu (SC-005, US3)

```bash
# iyzico serileştirme VO'ya sızmadı:
cd src/services/Payment.Api
grep -rl "ToPKIRequestString\|CamelCase\|Newtonsoft" Domains/ | grep ValueObjects && echo "HATA: wire domain'e sızdı" || echo "OK: VO iyzico bilmez"
```

- Üretilen charge isteği Adım 0 referansıyla alan-alan AYNI (buyer/address/basket/card/tutar/imza).
- Payment kalıcı şeması değişmedi (VO aggregate alanı değil).

## Adım 6 — (opsiyonel canlı) charge smoke

`dotnet run --project src/aspire/AppHost/AppHost.csproj` + test kartı (`reference_iyzico_sandbox_test_cards`)
→ charge önceki gibi geçer, imza reddi yok.

## Başarı özeti

| Kontrol | Beklenen |
|---|---|
| Provider/ dizini | yok |
| Domains VO | 4 (private ctor + Create) |
| VO'da iyzico serileştirme | yok |
| build | 0 hata |
| test (mevcut + VO) | yeşil |
| charge isteği | Adım 0 ile bit-aynı |
| Payment kalıcı şema | değişmedi |
