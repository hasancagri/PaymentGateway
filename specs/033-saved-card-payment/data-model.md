# Data Model: Kayıtlı Kartla Ödeme (033)

**Date**: 2026-08-14 | **Spec**: [spec.md](spec.md) | **Research**: [research.md](research.md)

## Payment (YENİ aggregate — Payment.Api, `Domains/Payments/`)

`Payment : AggregateRoot`. Marten identity = `Id` (Guid, AggregateRoot default).

| Alan | Tip | Not |
|---|---|---|
| `Id` | `Guid` | gateway ödeme kaydı kimliği (Marten identity) |
| `ProviderPaymentId` | `string` | iyzico `PaymentId` (Cancel/Refund girdisi) |
| `MerchantId` | `Guid` | kiracı |
| `VaultToken` | `string` | çekimde kullanılan kayıtlı kart token'ı (032) |
| `Price` | `decimal` | sepet tutarı |
| `PaidPrice` | `decimal` | taksitli toplam (vade farkı dahil) |
| `Installment` | `int` | taksit sayısı (1 = tek çekim) |
| `IyzicoCommission` | `string` | iyzico oransal maliyet (yanıttan; string — 030 girdi imzası) |
| `IyzicoFee` | `string` | iyzico sabit maliyet |
| `Status` | `PaymentStatus` | `Success=1 / Failed=2` |
| `CreatedTime` | `DateTime` | AggregateRoot'tan |

### Davranışlar (ResultDomain; `<remarks>Handler:</remarks>`)

| Metot | İmza | Kural | Handler |
|---|---|---|---|
| `Succeeded` | `static ResultDomain<Payment> Succeeded(Guid merchantId, string vaultToken, decimal price, decimal paidPrice, int installment, string providerPaymentId, string iyzicoCommission, string iyzicoFee)` | zorunlu alanlar; Status=Success | `ChargePaymentCommandHandler` |
| `Failed` | `static ResultDomain<Payment> Failed(Guid merchantId, string vaultToken, decimal price, int installment)` | Status=Failed; ProviderPaymentId boş | `ChargePaymentCommandHandler` |

Aggregate iyzico tipi GÖRMEZ — handler map'ler (sağlayıcı sınırı).

## PaymentStatus (YENİ enum — `Domains/Payments/PaymentStatus.cs`)

`Success = 1, Failed = 2` (Cancel/Refund statüleri ayrı işte gelir).

## StoredCard (MEVCUT — 032, yalnız okunur)

Charge handler `session.LoadAsync<StoredCard>(vaultToken)` → `MerchantId` eşleşme (kiracı) +
`Status == Active` (Revoked reddi) → `CardUserKey`/`CardToken` çekime girer. Değişmez.

## PaymentChargedEvent (YENİ — `Shared/IntegrationEvents.cs`)

```
record PaymentChargedEvent(Guid PaymentId, Guid MerchantId, decimal Price, decimal PaidPrice,
    int Installment, string IyzicoCommission, string IyzicoFee, string ProviderPaymentId);
```
Başarılı çekimde `[Transactional]` outbox publish (FR-005). Fanout exchange (yeni `merchant.payment`
veya mevcut PaymentCompleted exchange). Tüketici YOK (komisyon deferred — FR-008). Eski
`PaymentCompletedEvent` DEĞİŞMEZ (Order BC için durur).

## payment.charge scope zinciri (R3)

| Nokta | Değişiklik |
|---|---|
| `Common/AuthorizationScopes.cs` | `+ PaymentCharge = "payment.charge"` |
| `Identity.Server/Config.cs` | `ScopeResources["payment.charge"] = "payment.api"` |
| `Identity.Server/EventHandlers/MerchantClientEventHandler.cs` | Active demetine `payment.charge` ekle (cards.write yanı; Provisioning ALMAZ) |
| `Payment.Api/Program.cs` | `AddAuthenticationAndAuthorizationExtension(..., PaymentCharge)` |
| charge + installment endpoint | `RequireAuthorization(PaymentCharge, MerchantScoped)` |

## Slice haritası

```
src/services/Payment.Api/Domains/Payments/                 # YENİ
├── Payment.cs · PaymentStatus.cs
├── PaymentEndpointExtension.cs                            # api/v1/merchants/{merchantId}/payments grubu
└── Features/
    ├── Commands/ChargePayment.cs                          # /payments (charge + event)
    └── Queries/InstallmentOptions.cs                      # /payments/installment-options
src/others/Shared/IntegrationEvents.cs                     # +PaymentChargedEvent
src/others/Common/Utils/Constants/AuthorizationScopes.cs   # +PaymentCharge
src/others/Identity.Server/{Config.cs, EventHandlers/MerchantClientEventHandler.cs}
src/services/Payment.Api/Program.cs                        # auth scope + event publish wire

# ECommerce (US3, ayrı repo)
GatewayPaymentClient + checkout taksit/charge akışı + MerchantTokenProvider scope'una payment.charge
```

## Kalıcılık

paymentDb — yeni `mt_doc_payment` (Marten oluşturur; Schema.For gerekmez, default Guid Id). Migration yok.
