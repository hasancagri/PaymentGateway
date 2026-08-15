# Contract: Ödeme Uçları (Payment.Api) — 033

**Auth**: Bearer client_credentials — `payment.charge` (Active merchant, YENİ scope) + `MerchantScoped`.

## `POST /api/v1.0/merchants/{merchantId}/payments/installment-options` — Taksit sorgusu (US2)

Gövde:
```json
{ "bin": "552879", "price": 100.00 }
```
İç akış: `InstallmentInfo.Retrieve({BinNumber, Price})` → `/payment/iyzipos/installment`.
**200**:
```json
{ "installmentDetails": [
  { "installmentNumber": 1, "totalPrice": 100.00 },
  { "installmentNumber": 3, "totalPrice": 106.00 },
  { "installmentNumber": 6, "totalPrice": 112.00 } ] }
```
(banka vade farkı dahil totalPrice). Taksit desteklenmezse yalnız `{1, price}`.

## `POST /api/v1.0/merchants/{merchantId}/payments` — Çekim (US1)

Gövde (CVC/CardNumber YOK — saklı kart):
```json
{ "vaultToken": "card_...", "price": 100.00, "paidPrice": 106.00, "installment": 3,
  "buyer": { "name": "Ada", "surname": "Yılmaz", "email": "ada@example.com",
             "gsmNumber": "+905551112233", "identityNumber": "11111111110",
             "registrationAddress": "İstanbul", "city": "İstanbul", "country": "Türkiye", "ip": "85.34.78.112" },
  "basketItems": [ { "id": "SKU1", "name": "Ürün", "category1": "Genel", "price": 100.00 } ] }
```
İç akış: vaultToken → StoredCard (MerchantId eşleşme + Active) → `CreatePaymentRequest{PaymentCard{
CardToken, CardUserKey}, Price, PaidPrice, Installment, Buyer, Shipping/BillingAddress, BasketItems,
Currency=TRY}` → `Payment.Create` (`/payment/auth` NonSecure).
**200 (başarı)**:
```json
{ "paymentId": "guid", "providerPaymentId": "12345", "status": "Success",
  "price": 100.00, "paidPrice": 106.00, "installment": 3 }
```
+ `PaymentChargedEvent` yayınlanır (iyzico maliyeti taşır).
**400**: sağlayıcı reddi / iptal edilmiş kart / doğrulama → `INVALID_OPERATION_ERROR` veya alan hatası;
Payment Failed kaydı; olay YOK.
**403**: charge yetkisiz (Active değil / scope yok) veya route merchantId ≠ claim.

## iyzico dış uçları (gateway → iyzico)

| İşlem | iyzico ucu | İstek | Yanıt |
|---|---|---|---|
| createPayment | `POST /payment/auth` | `CreatePaymentRequest{PaymentCard{CardToken,CardUserKey}, Price, PaidPrice, Installment, Buyer, Addresses, BasketItems}` | `Payment{PaymentStatus, PaymentId, IyziCommissionRateAmount, IyziCommissionFee}` |
| installment | `POST /payment/iyzipos/installment` | `RetrieveInstallmentInfoRequest{BinNumber, Price}` | `InstallmentInfo{InstallmentDetails[]}` |

Auth: IYZWSv2 imza (HashGeneratorV2 — kanıtlı). CVC gönderilmez.

## ECommerce (US3) — gateway sözleşmesini tüketir

ECommerce `GatewayPaymentClient` yukarıdaki iki ucu çağırır (merchant token + payment.charge scope);
checkout kayıtlı kart token'ını (Wallet'tan) + seçilen taksiti iletir. Order.Api dönen PaymentId ile
sipariş "ödendi".
