# Contract: PaymentLink Yüzeyi (089)

Sürüm segmentsiz yollar (dış store kontratı sabit yol bekler — 041 D11 deseni). 4 uç. Tüm hata gövdesi Result pattern: `{ messages: [{ code }] }` (code = `PaymentLinkResourceConstants` sabiti).

---

## 1) POST `/payment-links` — Store ödeme linki üretir (US1, FR-001/003/004)

**Yetki**: `payment.write` JWT scope (**geçici** — nihai store→gateway platform IdP, sonraki iş, İlke V). Tenant `MerchantId` gövdede (geçici).

**İstek gövdesi**:
```json
{
  "merchantId": "guid",
  "amount": 149.90,
  "currency": "TRY",
  "orderRef": "ORD-2026-0001",
  "returnUrl": "https://store.example/orders/0001/return",
  "storeName": "Kolay Fırsat",
  "orderSummary": "1x Kablosuz Kulaklık"
}
```

**Yanıt 200** (opak referans + link; sır İÇERMEZ — İlke VI):
```json
{ "linkUrl": "https://pg.example/payment-links/<OpenToken>", "pgPaymentRef": "<guid>" }
```

**Davranış**:
- Geçerli istek → Pending link; `linkUrl` + `pgPaymentRef` döner (AS-1).
- Aynı `(merchantId, orderRef)` için AKTİF link varsa → **aynı link** döner (idempotent, AS-2, SC-002).
- `currency != TRY` → 400 `UNSUPPORTED_CURRENCY`; `amount <= 0` / eksik alan → 400 `INVALID_REQUEST` (AS-3).

---

## 2) GET `/payment-links/{openToken}` — Müşteri linki açar, güven bağlamı (US2, FR-006/007)

**Yetki**: `AllowAnonymous` + gizli `openToken` kapısı (fail-closed).

**Davranış** (`OpenPaymentLink` command'ını çağırır — plan.md CQRS sapması):
- Geçerli token + Pending → güven bağlamı HTML render (**mağaza adı + sipariş özeti + tutar + son geçerlilik**, kart formundan ÖNCE) + link Opened'a geçer (AS-1, SC-004).
- Zaten Opened → aynı sayfa (idempotent).
- Bilinmeyen/sahte token → **404** (sayfa açılmaz, fail-closed, AS-2).
- `now > ExpiresAt` → link Expired + açılmaz (404/expired sayfa, US3 AS-1).

**Yanıt**: `text/html` (güven bağlamı sayfası). Sır/kart alanı yok.

---

## 3) POST `/payment-links/{openToken}/pay` — "Öde" (US2 AS-3, FR-010 — stub)

**Yetki**: `AllowAnonymous` + gizli `openToken` kapısı.

**Davranış** (`PayPaymentLink` — stub):
- Opened + `now <= ExpiresAt + GraceWindow` → **400 `PROVIDER_UNAVAILABLE`**; link **terminale taşınmaz** (Pending/Opened kalır).
- `now > ExpiresAt + GraceWindow` → link Expired → `LINK_EXPIRED`.
- Terminal link → `TERMINAL_VIOLATION` / `LINK_CANCELLED`.
- Bilinmeyen token → 404.

> Bu dilimde çekim YOK; sağlayıcı entegrasyonu Phase-1 (İlke VII adapter).

---

## 4) POST `/payment-links/{pgPaymentRef}/cancel` — Store iptal (US3, FR-008)

**Yetki**: `payment.write` JWT scope (geçici).

**Davranış** (`CancelPaymentLink`):
- Aktif (Pending/Opened) → Cancelled; 200 (AS-2).
- Terminal → 400 `TERMINAL_VIOLATION` (tek-yön, AS-3).
- Bilinmeyen `pgPaymentRef` → 404 `LINK_NOT_FOUND`.
- İptal sonrası açma/ödeme reddedilir (AS-2, SC-003).

---

## Hata kodları

| Code | Durum |
|---|---|
| `UNSUPPORTED_CURRENCY` | currency != TRY |
| `INVALID_REQUEST` | amount<=0 / eksik alan |
| `LINK_NOT_FOUND` | bilinmeyen openToken/pgPaymentRef (fail-closed) |
| `LINK_EXPIRED` | TTL (+grace) aşımı |
| `LINK_CANCELLED` | iptal edilmiş linke işlem |
| `TERMINAL_VIOLATION` | terminalden geçiş denemesi |
| `PROVIDER_UNAVAILABLE` | "Öde" stub (çekim sonraki dilim) |
