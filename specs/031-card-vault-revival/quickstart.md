# Quickstart: Kart Vault Dirilişi (031)

## Ön koşullar

1. PaymentGateway AppHost ayakta; Payment.Api http://localhost:5201 (launchSettings doğrula).
2. Aktif bir merchant + MerchantKey (029 akışından mevcut: ECommerce Demo).
3. S3 için ECommerce AppHost + MerchantInformation kaydı (033 formuyla girilmişti) +
   `DropShopVault` config'inin `VaultBaseUrl`'ü Payment.Api'yi göstermeli (mevcut config).

## S1 — Tokenize (curl)

```bash
MTOKEN=$(curl -sk https://localhost:5101/connect/token \
  -d "grant_type=client_credentials&client_id=<merchantId>&client_secret=<mk_...>&scope=cards.write" | jq -r .access_token)

curl -s -X POST "http://localhost:5201/api/v1.0/merchants/<merchantId>/vault/cards" \
  -H "Authorization: Bearer $MTOKEN" -H "Content-Type: application/json" \
  -d '{"pan":"4111 1111 1111 1111","expiry":"12/29","holderName":"CARD HOLDER"}'
```

**Beklenen**: `{"token":"card_..."}` — başka alan yok. Boşluklu PAN normalize edilip geçer.

**Negatifler**: Luhn bozuk (`4111111111111112`) → `INVALID_FORMAT`; geçmiş expiry (`01/20`) →
`INVALID_FORMAT`; eksik holderName → `VALUE_IS_REQUIRED`; BAŞKA merchantId'li route + aynı token →
403. Aynı kart ikinci kez → FARKLI yeni token.

**PAN sızma kontrolü (SC-002)**: `docker exec ... psql -d paymentDb -c "select data from
payment.mt_doc_storedcard"` → açık PAN GÖRÜNMEMELİ (yalnız base64 `EncryptedPan` + bin/last4).

## S2 — Revoke (curl)

`DELETE .../vault/cards/{token}` → `{"token":"..."}`; aynısını TEKRAR → yine 200 (idempotent);
uydurma token → `RECORD_NOT_FOUND`.

## S3 — Uçtan uca ECommerce (SC-001, sıfır dokunuş kanıtı)

1. İki AppHost birlikte; ECommerce'te OTURUM AÇIK kullanıcıyla Profil → Kartlarım
2. Kart ekle: `4111 1111 1111 1111`, 12/29, etiket — **Beklenen**: listede Visa •1111 belirir
   (bugün bu akış gateway ucu olmadığından kırık — canlanması dirilişin kanıtı)
3. Gateway tarafı: paymentDb'de tek StoredCard (Active, merchant = ECommerce Demo)
4. Kartı sil — **Beklenen**: listeden düşer; gateway kaydı `Revoked` (tarihçede durur)
5. ECommerce loglarında "Vault tokenize başarısız" UYARISI OLMAMALI

## Kapanış

- `dotnet build` 0 hata; `dotnet test tests/Payment.Api.Tests` yeşil (Luhn/expiry/türetim/revoke
  matrisi); Merchant + Commission test regresyonu yeşil.
