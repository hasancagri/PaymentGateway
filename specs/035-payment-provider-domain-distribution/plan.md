# Implementation Plan: Payment Provider Domain Dağıtımı

**Branch**: `035-payment-provider-domain-distribution` | **Date**: 2026-08-15 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/035-payment-provider-domain-distribution/spec.md`

## Summary

Payment.Api'deki `Provider/` klasörünü (46 iyzico wire dosyası) kaldır. 42 saf-wire tipi paylaşılan
`Iyzico.Provider` SDK'ya (`Iyzico.Provider.{Payments,Installments,StoredCards}` namespace); 4
domain-uygun tip Payment domain'ine VO olarak (`Domains/Payments/ValueObjects/{Buyer,Address,
BasketItem}` + `Domains/StoredCards/ValueObjects/CardInformation`). Handler = anti-corruption sınır:
HTTP Input DTO → domain VO (`Create`+doğrulama) → SDK wire DTO. Yapısal uyarlama şimdi, davranış
sonra; çalışma-anı bit-korunur. 034 Iyzico.Provider üstüne genişler.

## Technical Context

**Language/Version**: C# / .NET 10 (net10.0)

**Primary Dependencies**: 034 `Iyzico.Provider` (transport çekirdeği + genişleyen SDK), Marten (mevcut), Wolverine

**Storage**: Marten (Payment/StoredCard aggregate — ŞEMA DEĞİŞMEZ; VO'lar kalıcı değil)

**Testing**: xUnit saf domain birim testleri — yeni VO `Create` doğrulaması (`tests/Payment.Api.Tests`); mevcut testler yeşil kalır

**Target Platform**: sunucu (Aspire)

**Project Type**: BC iç refactor (Payment.Api) + paylaşılan SDK genişletme (Iyzico.Provider)

**Performance Goals**: N/A (davranış nötr)

**Constraints**: davranış bit-korunur (üretilen iyzico isteği aynı); iyzico serileştirme domain VO'ya sızmaz; Payment kalıcı şema değişmez; kapsam yalnız Payment

**Scale/Scope**: 42 wire dosya SDK'ya taşınır; 4 VO oluşturulur; 3 handler (ChargePayment, TokenizeCard, RevokeCard) + InstallmentOptions using güncellenir; Provider/ silinir

## Constitution Check

*GATE: Phase 0 öncesi; Phase 1 sonrası yeniden.*

- **İlke II (Zengin Domain / anti-anemik) — PASS, GÜÇLENDİRİR.** Anemik wire tiplerinin domain kısmı
  VO'ya (private ctor + statik `Create`) yükseltilir — anayasa VO kuralıyla birebir. "Davranış sonra"
  (ince doğrulama şimdi) mevcut anemik record'lardan kesin ileri; tam invariant ileride (`iyzico_sdk_ddd_adaptation`).
- **İlke IV (Result) — PASS.** VO `Create` fabrikaları `ResultDomain<T>` döner (CLAUDE.md domain-result
  standardı); doğrulama hatası `Error(MessageItem)`, exception değil.
- **CP.VPOS sınırı — PASS, GÜÇLENDİRİR.** Sağlayıcı (wire) tipleri artık BC'de değil SDK'da; handler
  sınırında domain'e çevrilir. `Provider/` klasörünün kalkması bu kuralın tam uygulanışı.
- **İlke I (BC İzolasyon) — PASS.** Iyzico.Provider paylaşılan transport/SDK (034); domain VO'lar
  Payment'ta. Cross-BC yok.
- **CPM / İlke III / V — PASS/N/A.** Iyzico.Provider zaten CPM; slice düzeni korunur (VO →
  `<Aggregate>/ValueObjects/`, CLAUDE.md); auth yüzeyi değişmez.

**Sonuç: gate geçti. Complexity Tracking boş.**

**Not (araya-giren VO katmanı)**: HTTP body DTO (BuyerInput/BasketItemInput) JSON deserialize için
KALIR (VO private-ctor deserialize edilemez); VO handler'da Input↔SDK arasına girer. Bu ekstra katman
bilinçli — domain doğrulaması + wire izolasyonu için (bkz. research R3).

## Project Structure

### Documentation (this feature)

```text
specs/035-payment-provider-domain-distribution/
├── plan.md              # bu dosya
├── research.md          # Phase 0 — dağıtım kararları (SDK taşıma, VO araya-girme, Address türetme)
├── data-model.md        # Phase 1 — 4 VO (alan + Create + doğrulama)
├── quickstart.md        # Phase 1 — doğrulama (Provider/ yok, build, test, davranış, istek karşılaştırma)
├── checklists/requirements.md
└── tasks.md             # /speckit-tasks (bu komutta ÜRETİLMEZ)
```

contracts/ **üretilmez** — dış kontrat/endpoint değişmez (HTTP body şekli korunur, davranış aynı).

### Source Code (repository root)

```text
src/others/Iyzico.Provider/                  # 034 çekirdek + GENİŞLER
├── Payments/         <- Payment.Api/Provider/Payments saf-wire tipleri (Buyer/Address/BasketItem/
│                        CardInformation HARİÇ) taşınır; namespace Iyzico.Provider.Payments
├── Installments/     <- Payment.Api/Provider/Installments tümü
└── StoredCards/      <- Payment.Api/Provider/StoredCards saf-wire (CardInformation HARİÇ)

src/services/Payment.Api/
├── Provider/                                 # SİLİNİR (klasör tamamen kalkar)
├── Domains/Payments/
│   ├── ValueObjects/Buyer.cs                 # YENİ VO (private ctor + Create + email/kimlik/IP doğrulama)
│   ├── ValueObjects/Address.cs               # YENİ VO (Buyer'dan türetilir — research R4)
│   ├── ValueObjects/BasketItem.cs            # YENİ VO
│   └── Features/Commands/ChargePayment.cs    # BuyerInput/BasketItemInput = HTTP DTO kalır; handler VO kurar→SDK map
├── Domains/StoredCards/
│   ├── ValueObjects/CardInformation.cs       # YENİ VO (Pan/Expiry/Holder — Luhn/expiry ince doğrulama)
│   └── Features/Commands/{TokenizeCard,RevokeCard}.cs  # VO araya-girer
└── GlobalUsings.cs                           # Payment.Api.Provider.* → Iyzico.Provider.*

tests/Payment.Api.Tests/                       # YENİ VO Create doğrulama testleri
```

**Structure Decision**: SDK (Iyzico.Provider) transport + tüm saf-wire tipleri taşır; Payment.Api
yalnız `Domains/` (VO + slice). `Provider/` klasörü silinir → her iyzico iş süreci Domains'ten
görünür. Handler anti-corruption sınırı (mevcut desen) domain VO ↔ SDK wire çevirir.

## Complexity Tracking

> Constitution ihlali yok — boş.
