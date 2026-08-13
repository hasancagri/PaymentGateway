# Phase 1 Data Model: Payment.Api iyzico Wire Material Geçişi

Yeni domain entity ÜRETİLMEZ — 40 wire tipi sağlayıcı sınırına taşınır. Payment.Api gerçek domain'i
YOK → `Domains/` boşalır.

## Taşıma envanteri (Domains → Provider)

| Kaynak klasör | Dosya | Hedef | Namespace |
|---------------|-------|-------|-----------|
| `Domains/Payments/` | 28 | `Provider/Payments/` | `Payment.Api.Domains.Payments` → `Payment.Api.Provider.Payments` |
| `Domains/Installments/` | 6 | `Provider/Installments/` | `...Domains.Installments` → `...Provider.Installments` |
| `Domains/StoredCards/` | 6 | `Provider/StoredCards/` | `...Domains.StoredCards` → `...Provider.StoredCards` |

**Payments (28)**: resource+çağrı `Payment`/`PaymentPreAuth`/`PaymentPostAuth`/`Cancel`/`Refund`/
`RefundChargedFromMerchant`/`PaymentItem`/`PaymentResource`; PKI istek `CreatePaymentRequest`/
`CreatePaymentPostAuthRequest`/`CreateCancelRequest`/`CreateRefundRequest`/
`CreateAmountBasedRefundRequest`/`RetrievePaymentRequest`/`UpdatePaymentItemRequest`; DTO `Buyer`/
`Address`/`BasketItem`/`PaymentCard`/`LoyaltyReward`/`ConvertedPayout`; enum `BasketItemType`/
`PaymentChannel`/`PaymentGroup`/`Currency`/`Locale`/`Status`/`RefundReason`.

**Installments (6)**: `InstallmentInfo`/`BinNumber` (resource); `RetrieveInstallmentInfoRequest`/
`RetrieveBinNumberRequest` (PKI); `InstallmentDetail`/`InstallmentPrice` (DTO).

**StoredCards (6)**: `Card`/`CardList` (resource); `CreateCardRequest`/`DeleteCardRequest`/
`RetrieveCardListRequest` (PKI); `CardInformation` (DTO).

Sonuç: `Payment.Api/Domains/` altında `BaseRequestV2`/`ProviderResourceV2` türeyen tip HİÇ KALMAZ;
`Domains/` boşalır (SC-001).

## Domain — YOK (Payment.Api ara durum)

Payment.Api'nin gerçek domain'i yok (022 pivot). Bu iş domain üretmez; charge akışı domain'i
(Payment/StoredCard aggregate'leri, slice'lar) sonraki davranış spec'inde kurulur.

## Sınır çevirisi (davranış spec'ine bırakılan — BU İŞTE YOK)

- Canlı iyzico ödeme (`Payment.Create`/PreAuth/PostAuth/Cancel/Refund), taksit/BIN sorgu, kart vault
  çağrıları.
- Wire → domain temsili çeviri (charge akışı aggregate'leri).

## Doğrulama kuralları

- `Payment.Api/Domains/` altında sağlayıcı-türeyen = 0 (SC-001).
- `Provider/Payments/` 28, `Provider/Installments/` 6, `Provider/StoredCards/` 6; `Domains/` klasörleri
  yok (SC-002).
- Build 0 hata + diğer BC testleri (Merchant 30 + Commission 20) yeşil (SC-003).
- Yeni endpoint = 0 (SC-004).
