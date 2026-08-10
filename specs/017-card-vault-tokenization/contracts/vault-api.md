# Contract: Card Vault HTTP API

Base: `api/v{version:apiVersion}/merchants/{merchantId:guid}/vault/cards`
Auth (hepsi): scope `payment.vault` (capability scope; `payment.write` DEĞİL — mcp/pos merchant'a
kapalı) + policy `MerchantScoped` (claim `merchant_id` == route `{merchantId}`, fail-closed). Yalnız
**Active** merchant `payment.vault` alır (Provisioning RET). Payment.Api'nin ilk merchant-scoped grubu.

## POST .../vault/cards — TokenizeCard

İstek:
```json
{ "pan": "4111111111111111", "expiry": "12/28", "holderName": "AHMET YILMAZ" }
```
Yanıt 200 (YALNIZ token; PAN/last4/brand/bin YOK):
```json
{ "token": "card_9f3a1c..." }
```
Hatalar (Result): Luhn RET / expiry geçmiş RET / Active değil RET / cross-merchant RET.

## PUT .../vault/cards/{token} — UpdateCard

İstek (PAN YOK):
```json
{ "expiry": "01/30", "holderName": "AHMET Y YILMAZ" }
```
Yanıt 200: `{ "token": "card_9f3a1c..." }` (aynı token). Revoked → RET. Cross-merchant → RET.

## DELETE .../vault/cards/{token} — RevokeCard

Yanıt 200: soft revoke (Status=Revoked). Idempotent (zaten Revoked → Ok). Cross-merchant → RET.

## Resolve (HTTP değil — internal port)

`ICardVault.ResolveCardInfoAsync(token)` — ödeme akışı çağırır (mevcut sözleşme değişmez).
Token yok/Revoked → Error; değilse `CardInfo` (Bin üzerinden `ResolveBinCard`). Merchant eşleşmesi
bu feature'da resolve'da YOK (bkz. research R3; charge feature'ında gelir).

## Değişmeyen sözleşmeler

- `ICardVault` imzası (resolve-only) — write metodu EKLENMEZ.
- `CardInfo` (BankCode, IsCreditCard, InstallmentBankCodes) — 007 tüketimi aynen.