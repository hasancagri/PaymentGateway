# Contract: Hosted Ödeme (`POST /payment-links/{openToken}/pay`)

Müşteri "Öde"ye basınca hosted sayfanın çağırdığı uç. Anonim + gizli `openToken` kapılı (fail-closed, İlke V). Kart verisi yalnız bu istekte taşınır, saklanmaz (İlke VI).

## Request

`POST /payment-links/{openToken}/pay`

Body (JSON):
```json
{
  "cardNumber": "4242424242424242",
  "holderName": "AD SOYAD",
  "expiryMonth": "12",
  "expiryYear": "30",
  "cvv": "123"
}
```

- `openToken` path'te; bilinmeyen/eksik → 404 (`LINK_NOT_FOUND`), hiçbir link açılmaz.
- Kart alanları yalnız çekime iletilir; yanıt/loglar/kalıcı depo bu alanları İÇERMEZ.

## Response (normalize)

**Onay (200)** — link `Paid`:
```json
{ "isSuccess": true, "data": { "status": "Paid", "redirectUrl": "<ReturnUrl>" } }
```

**Red (200/400, outcome taşır)** — link açık kalır:
```json
{ "isSuccess": false, "messages": [ { "code": "CHARGE_DECLINED" } ] }
```

Red kod eşlemesi (serbest metin yok, resource sabiti):

| DeclineClass | Resource kod | Müşteri anlamı |
|---|---|---|
| IssuerDecline | `CHARGE_DECLINED` | "Banka reddetti — başka kartla deneyin" |
| FraudDecline | `CHARGE_DECLINED` | (asla retry; mesaj genel) |
| TechnicalFailure/ConfigError (liste tükendi) | `PROVIDER_UNAVAILABLE` | "Şu an işlem alınamadı, tekrar deneyin" |

## Durum geçişi

```
Opened + onay  → Paid (terminal)
Opened + red   → Opened (yeni deneme mümkün; başka kart)
terminal link  → reddedilir (TERMINAL_VIOLATION)
TTL+grace dışı → LINK_EXPIRED (lazy)
```

## İnvariant

- Paid link ikinci `/pay` çağrısında çekim YAPMAZ (çift-çekim guard).
- Yanıt asla kart verisi / banka sırrı döndürmez (İlke VI, kontrol-düzlemi sır döndürmez).
