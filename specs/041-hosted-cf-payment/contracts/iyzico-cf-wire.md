# Kontrat (iç): iyzico Checkout Form wire (041)

PG↔iyzico V2 wire. **Slice-nested** camelCase POCO (paylaşılan SDK yok — CLAUDE.md). Transport engine
`Payment.Api/Utils` (RestHttpClientV2 / ProviderResourceV2 imza header'ı / HashGeneratorV2). BC DIŞINA sızmaz;
VO/handler sınırında domain temsiline map (anti-corruption). Path/locale/currency = `IyzicoRequestOptions`.

## 1. CF initialize (ödeme başlat) — `InitiateHostedPayment` slice'ında nested

Path: `IyzicoRequestOptions.CheckoutFormInitializePath` = `/payment/iyzipos/checkoutform/initialize/auth/ecom`

**Request** (`InitializeCheckoutFormRequest`, camelCase JSON):

```
{
  locale, conversationId,               // OrderRef / PgPaymentRef türevli
  price, paidPrice,                     // = istek Amount (string, invariant culture)
  currency: "TRY",
  basketId, paymentGroup: "PRODUCT",
  callbackUrl,                          // = HostedPaymentOptions.IyzicoCallbackBaseUrl + "/" + session.CallbackToken (C1)
  enabledInstallments: [1],            // taksit söküldü — tek çekim
  buyer { id,name,surname,email,gsmNumber,identityNumber,registrationAddress,city,country,ip },  // SENTETİK (sandbox)
  shippingAddress { contactName,city,country,address },   // sentetik
  billingAddress  { contactName,city,country,address },   // sentetik
  basketItems [ { id,name,category1,itemType,price } ]    // tek kalem, price = Amount
}
```

**Response** (`CheckoutFormInitializeResult : ProviderResourceV2`):

```
{ status, token, paymentPageUrl, tokenExpireTime, errorCode?, errorMessage? }
```

- Başarı: `status == IyzicoRequestOptions.SuccessStatus` && token && paymentPageUrl dolu.
- PG: `paymentPageUrl` → store'a `HostedUrl`; `token` → session.CheckoutFormToken.

## 2. CF retrieve (sonuç doğrula) — `CompleteHostedPayment` slice'ında nested

Path: `IyzicoRequestOptions.CheckoutFormRetrievePath` = `/payment/iyzipos/checkoutform/auth/ecom/detail`

**Request** (`RetrieveCheckoutFormRequest`): `{ locale, conversationId, token }`

**Response** (`RetrieveCheckoutFormResult : ProviderResourceV2`):

```
{ status, paymentStatus, paymentId, price, paidPrice, currency,
  fraudStatus?, errorCode?, errorMessage? }
```

- Başarı: `status == success` && `paymentStatus == "SUCCESS"` → `MarkSucceeded(paymentId)`.
- Aksi (FAILURE / INIT_THREEDS / boş) → `MarkFailed(errorCode ?? paymentStatus)`.

## İmza + transport (Utils — DEĞİŞMEZ)

- `ProviderResourceV2.GetHttpHeadersWithRequestBody(request, uri, providerOptions, conversationId)` →
  IYZWSv2 HMAC-SHA256 Authorization header (randomKey + uriPath + camelCase body).
- `RestHttpClientV2.Create().PostAsync<T>(uri, headers, request)` → camelCase POST, T deserialize.
- Secret: `ProviderOptions { ApiKey, SecretKey, BaseUrl }` (IyzicoProviderSettings'ten singleton).
