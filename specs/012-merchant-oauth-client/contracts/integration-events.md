# Contract: Merchant yaşam döngüsü event'leri

Taşıma: RabbitMQ fanout exchange **`merchant.lifecycle`**; Identity.Server kuyruğu
**`identity.merchant-sync`** (durable). Sabitler `Shared/RabbitMqConstants.cs`'e eklenir.
Kontrat tipleri `Shared/IntegrationEvents.cs`'te yaşar (İlke I — bilinçli paylaşım).

## MerchantCreated

```csharp
public record MerchantCreated(Guid MerchantId, string MerchantKey, string Status);
```

- Yayıncı: `CreateMerchantCommandHandler` (başarı yolunda, `IMessageBus.PublishAsync`).
- `Status`: yaratılış anındaki durum — bugün her zaman `"Active"`.
- Tüketici davranışı (Identity.Server): idempotent upsert —
  `FindByClientIdAsync(MerchantId)` yoksa create (secret=MerchantKey,
  Properties[merchant_id], izinler Status'a göre), varsa descriptor'ı aynı hedefe update.

## MerchantStatusChanged

```csharp
public record MerchantStatusChanged(Guid MerchantId, string NewStatus);
```

- Yayıncı: `SetMerchantStatusCommandHandler` (yeni slice, başarı yolunda).
- `NewStatus`: `"Active" | "Passive" | "Suspended"` (string — BC enum'u sızmaz).
- Tüketici davranışı: client bulunamazsa NO-OP + log (Created henüz işlenmemiş olabilir;
  dev fazında kabul — bkz. research D1 sıralama notu). Bulunursa: `Active` → izinleri
  geri yaz; diğerleri → izinleri boşalt. Secret'a DOKUNULMAZ.

## Sıralama ve teslim garantileri

- Fanout + tek tüketici kuyruk: FIFO beklenir ama garanti varsayılMAZ; her iki handler
  idempotent yazılır (aynı olay N kez → aynı sonuç).
- Backfill YOK: tüketici ayağa kalkmadan önce yayınlanmış olaylar durable kuyruğda
  bekler; kuyruk hiç yokken üretilmiş merchant'lar için çözüm ortam sıfırlama (dev fazı).