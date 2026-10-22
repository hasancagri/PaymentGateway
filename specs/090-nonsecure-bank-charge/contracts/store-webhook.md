# Contract: Store Sonuç Webhook (HMAC-imzalı, dayanıklı)

Terminal sonuçta (Paid/Failed) PG store'a sonucu bildirir. 041 `StoreCallbackDelivery` deseninin yeniden kurulumu.

## Mesaj (iç — Wolverine local durable)

```csharp
public record Deliver(string CallbackUrl, string TxRef, string PgPaymentRef, string Status, string? ReasonCode);
```

- `PayPaymentLink` handler `[Transactional]` içinde `bus.PublishAsync(new StoreResultDelivery.Deliver(...))` → DB commit'te gider (outbox). Geçici teslim hatası → `UseDurableLocalQueues` retry.
- Tekil `DeliverHandler` (`*Handler` suffix; gerekirse `Program.cs`'e `opts.Discovery.IncludeType(typeof(StoreResultDelivery.DeliverHandler))`).

## HTTP (PG → store)

`POST {CallbackUrl}`

Header: `X-Signature: <hmac-sha256-hex-lowercase>`

Body (ham JSON — hem imzalanan hem gönderilen):
```json
{ "pgPaymentRef": "<opak>", "orderRef": "ORD-7731", "status": "Paid", "amount": 1250.00, "currency": "TRY", "bankCode": "SIM-BANK-1", "reasonCode": null }
```

- İmza: `HMACSHA256(CallbackSecret, rawBody)` → lowercase hex. Store aynı ham gövdeyi aynı sırla doğrular.
- `CallbackSecret` `StoreCallbackOptions`'tan (secret-store: user-secrets/env). ASLA log/trace/body/dönüş DTO'sunda görünmez (İlke VI).
- Non-2xx → throw → durable retry. İdempotent teslim: terminal durum memoize (tekrar publish zararsız).

## Integration event (paralel, Order BC için)

Aynı `[Transactional]` içinde:
- onay → `PaymentCompletedEvent(PaymentId, OrderNumber, Amount, BankCode)` → fanout `payment.completed`
- red → `PaymentFailedEvent(PaymentId, OrderNumber, Amount, Reason)` → fanout `payment.failed`

Tüketici YOK (Order BC geldiğinde bağlanır); kontrat + routing hazır (Program.cs).

## İnvariant

- İmza doğrulanabilir olmalı (store ham body + CallbackSecret ile yeniden üretir).
- Sır asla gövdede/log'da. Tarayıcı success sayfası kanıt değil; otorite bu imzalı webhook.
