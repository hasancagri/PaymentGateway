# Quickstart: Card Vault / Tokenization (canlı doğrulama)

Ön koşul: Aspire ayağa (`dotnet run --project src/aspire/AppHost/AppHost.csproj`). Postgres +
RabbitMQ + Identity.Server + Payment.Api + Merchant.Api hazır. Bir **Active** merchant + geçerli
merchant token (client_id=merchantId, client_secret=MerchantKey → `connect/token`, scope `cards.write`).

## S1 — Tokenize (P1)
`POST api/v1.0/merchants/{merchantId}/vault/cards` body `{pan, expiry, holderName}` (geçerli PAN).
Beklenen: 200, yanıt **yalnız** `{ token }`. PAN/last4/brand YOK. Postgres `stored_card` kaydı
`Active`, `EncryptedPan` düz PAN DEĞİL.

## S2 — Round-trip: token ile ödeme çözümü (P1)
S1 token'ını 007 payment session akışına ver (quote). Beklenen: token gerçek karta çözülür,
BIN/kart programı yönlendirmeye akar (fixture token'a gerek yok).

## S3 — Luhn/expiry RET (P1)
Geçersiz PAN (Luhn'suz) veya geçmiş expiry ile tokenize. Beklenen: iş-kuralı hatası, kayıt yok.

## S4 — Revoke soft (P2)
`DELETE .../vault/cards/{token}`. Beklenen: 200, kayıt `Revoked` (fiziksel durur). Sonra S2 tekrar →
RET (Revoked). Tekrar DELETE → idempotent 200.

## S5 — Update expiry/holder (P3)
`PUT .../vault/cards/{token}` `{expiry, holderName}`. Beklenen: 200 aynı token, alanlar güncel.
Revoked token'a PUT → RET.

## S6 — Tenant izolasyon (fail-closed)
Merchant A token'ıyla merchant B route'una (`/merchants/{B}/vault/cards`) tokenize/revoke. Beklenen:
403 (MerchantScoped claim≠route). Provisioning merchant token'ı (cards.write yok) → 403/401.
Ek: merchant token'ıyla `/mcp` veya `/pos-accounts` (payment.write) → 403 (merchant payment.write almaz).

## S7 — PAN sızıntısı yok
S1–S5 boyunca Payment.Api log'larında, HTTP yanıtlarında, RabbitMQ event'lerinde tam PAN ARANMAZ →
hiçbir yerde görünmemeli (en fazla last4, o da yalnız iç kayıtta).

## Domain birim testleri (tests/Payment.Api.Tests)
StoredCard: Luhn RET, expiry RET, Create Ok + türetilmiş bin/last4/brand, UpdateDetails yalnız
expiry/holder, Revoke idempotent, Revoked→Update RET. (Saf; host/HTTP yok — anayasa test kuralı.)