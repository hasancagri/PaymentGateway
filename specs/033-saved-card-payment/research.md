# Research: Kayıtlı Kartla Ödeme (033)

**Date**: 2026-08-14 | **Spec**: [spec.md](spec.md)

## R1 — iyzico ödeme wire tipleri HAZIR + çekirdek KANITLI

**Decision**: `Provider/Payments/` tam set: `Payment.Create(CreatePaymentRequest, ProviderOptions)`
→ POST `/payment/auth` (NonSecure); `CreatePaymentRequest{Price, PaidPrice, Installment, PaymentCard,
Buyer, ShippingAddress, BillingAddress, BasketItems, PaymentChannel, PaymentGroup, Currency}`;
`PaymentCard{CardToken, CardUserKey, ...}` (saved-card — CVC/CardNumber verilmez); yanıt `Payment :
PaymentResource{PaymentStatus, PaymentId, IyziCommissionRateAmount, IyziCommissionFee, ...}`.
Taksit: `InstallmentInfo.Retrieve(RetrieveInstallmentInfoRequest{BinNumber, Price}, opts)` → POST
`/payment/iyzipos/installment` (spike'la KANITLANDI, 2026-08-14: `Status=success`).

**Rationale**: SDK 020'den uyarlı, sağlayıcı sınırı korunuyor. ProviderOptions user-secrets (032).

## R2 — Saved-card NonSecure: PaymentCard yalnız CardToken+CardUserKey

**Decision**: `PaymentCard{CardToken=StoredCard.CardToken, CardUserKey=StoredCard.CardUserKey}` —
CardNumber/Cvc GÖNDERİLMEZ. iyzico saklı kartla CVC-siz çeker (Model A kazancı, 032). Vault token →
StoredCard çözümü handler'da (`session.LoadAsync<StoredCard>(vaultToken)`; MerchantId eşleşme +
Status==Active kontrolü — Revoked reddedilir, FR-002).

**Rationale**: Spec FR-001/FR-002; 032 altyapısı bunun için kuruldu (SC-005).

## R3 — payment.charge scope: cards.write zincirinin aynadı (Active-only)

**Decision**: Yeni `payment.charge` scope, `cards.write` deseniyle 4 noktada:
- `AuthorizationScopes.PaymentCharge = "payment.charge"` (Common)
- `Identity.Server Config.ScopeResources["payment.charge"] = "payment.api"` + AllApiScopes'a girer
- `MerchantClientEventHandler.AddMerchantPermissions`: **yalnız Active** demetine ekle (cards.write
  yanına; Provisioning ALMAZ — fail-closed, FR-007)
- `Payment.Api` `AddAuthenticationAndAuthorizationExtension(..., PaymentCharge)` + charge/installment
  uçları `RequireAuthorization(PaymentCharge, MerchantScoped)`

**Rationale**: Anayasa V "Active tam demet charge dahil" öngörüsü (013). Scope adı merchant-başına
çoğaltılmaz; statü ile açılır. `admin-ui`/statik istemciler bu scope'u istemez (yalnız merchant Active).

**Not**: Merchant OAuth istemcisi Active'e geçince payment.charge kazanır; mevcut token'lar 15 dk
sonra yenilenince gelir. Merchant client'ı payment.charge scope'unu `connect/token`'da istemeli
(ECommerce PaymentGatewayClient scope'una eklenir — US3).

## R4 — Payment aggregate (YENİ, gateway): çekim kaydı

**Decision**: `Domains/Payments/Payment : AggregateRoot` — `Id` (Guid, Marten identity);
`ProviderPaymentId` (iyzico PaymentId), `MerchantId`, `VaultToken`, `Price`, `PaidPrice`,
`Installment`, `IyzicoCommission` (oransal, string→decimal), `IyzicoFee` (sabit), `Status`
(`PaymentStatus.Success/Failed`), `CreatedTime`. Fabrikalar: `Succeeded(...)` + `Failed(...)`
(ResultDomain). İptal/iade + audit temeli.

**Rationale**: FR-003; Cancel/Refund (ayrı iş) bu kaydın ProviderPaymentId'sini kullanacak.

## R5 — Yeni event: PaymentChargedEvent (iyzico maliyeti taşır)

**Decision**: Mevcut `Shared.IntegrationEvents.PaymentCompletedEvent(PaymentId, OrderNumber, Amount,
BankCode)` iyzico maliyeti TAŞIMIYOR (eski model kalıntısı, tüketici yok). Yeni event:
`PaymentChargedEvent(Guid PaymentId, Guid MerchantId, decimal Price, decimal PaidPrice, int
Installment, string IyzicoCommission, string IyzicoFee, string ProviderPaymentId)` — `Shared`'a
eklenir; `merchant.payment` fanout exchange (yeni) veya mevcut PaymentCompleted exchange'e yeni
mesaj. Başarılı çekimde `[Transactional]` outbox yayınlanır (FR-005). Tüketici YOK (komisyon
tüketimi deferred — FR-008); yalnız yayın + bağlantı noktası.

**Rationale**: Efektif komisyon Commission BC'nin işi; event iyzico maliyetini taşır (string alanlar,
030 `CalculateEffectiveCommission` girdi imzasıyla uyumlu). Eski PaymentCompletedEvent'e
dokunulmaz (Order BC gelince o kullanılır).

## R6 — İki slice: charge + installment-options

**Decision**: `Domains/Payments/Features/`:
- `Commands/ChargePayment.cs` — `POST /merchants/{merchantId}/payments`; body `{VaultToken, Price,
  PaidPrice, Installment, Buyer{...}, BasketItems[...]}`; handler StoredCard çöz → CreatePaymentRequest
  map → `Payment.Create` → başarı: Payment.Succeeded + Store + PaymentChargedEvent publish;
  başarısız: Payment.Failed + Store (olay yok) + hata. `[Transactional]`.
- `Queries/InstallmentOptions.cs` — `POST /merchants/{merchantId}/payments/installment-options`;
  body `{Bin, Price}`; `InstallmentInfo.Retrieve` → taksit tablosu map (taksit sayısı + PaidPrice).
Her ikisi `PaymentCharge + MerchantScoped`.

**Rationale**: III vertical slice; sağlayıcı tipleri handler'da map'lenir (aggregate görmez).

## R7 — Buyer/Address zorunlu alanları: curl'de temsilî, ECommerce'te gerçek

**Decision**: iyzico `createPayment` Buyer + Shipping/Billing Address zorunlu. Charge body Buyer
alt-kümesini taşır (Id, Name, Surname, Email, GsmNumber, IdentityNumber, RegistrationAddress, City,
Country, Ip); Address gateway'de Buyer'dan türetilir (aynı adres — sandbox yeterli). ECommerce
gerçek alıcı/adres gönderir (US3). Curl testinde sabit temsilî değerler (quickstart).

**Rationale**: Sağlayıcı zorunlu alanları; iş kuralı değil, geçiş verisi (spec Assumptions).

## R8 — US3 ECommerce: OrderService → gateway charge client (yeni)

**Decision**: ECommerce `Payment.Api` stub'ı yerine WebApp/Customer akışı gateway'i çağırır:
- Yeni `GatewayPaymentClient` (Customer.Api veya WebApp) — `GatewayCardTokenizer` deseni:
  `MerchantTokenProvider` (mevcut, cards.write + **payment.charge** scope eklenir) ile token, HTTP →
  gateway `/payments` + `/installment-options`
- Checkout: kayıtlı kart seçimi (mevcut) + taksit seçenekleri (yeni sorgu) + seçilen taksit → charge
- Order.Api: mevcut `CreateOrder(PaymentId)` — gateway'in döndürdüğü PaymentId ile sipariş "ödendi"
ECommerce detayları implement'te keşfedilir (P2); dış sözleşme = gateway 033 uçları.

**Rationale**: US3 P2; ECommerce'in stub ödemesi gerçek gateway'e bağlanır. İki repo.

## R9 — Test: aggregate saf + iyzico canlı (sandbox)

**Decision**: `Payment` aggregate saf testleri (Succeeded/Failed fabrikaları, alan doğrulama);
iyzico charge/installment gerçek çağrısı quickstart canlı (sandbox test kartı 5528790000000008 —
önce 032 ile saklanır, sonra token'la çekilir). Handler mock'lanmaz (Provider statik).
