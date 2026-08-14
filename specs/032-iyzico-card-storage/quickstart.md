# Quickstart: iyzico Saklı Kart'a Geçiş (Model A) — 032

## Ön koşullar

1. **iyzico sandbox key**: `dotnet user-secrets set` ile Payment.Api'ye (git'e girmez):
   ```bash
   dotnet user-secrets --project src/services/Payment.Api set "IyzicoProviderSettings:ApiKey" "sandbox-..."
   dotnet user-secrets --project src/services/Payment.Api set "IyzicoProviderSettings:SecretKey" "sandbox-..."
   dotnet user-secrets --project src/services/Payment.Api set "IyzicoProviderSettings:BaseUrl" "https://sandbox-api.iyzipay.com"
   ```
2. **Veri sıfırlama (R7)**: `mt_doc_storedcard` truncate (031 EncryptedPan'li kayıtlar uymaz).
3. AppHost ayakta; Payment.Api http://localhost:5201. Aktif merchant (ECommerce Demo) + MerchantKey.
4. iyzico sandbox test kartı: `5528790000000008` (Mastercard), expiry gelecekte (`12/30`), CVC gerekmez.

## S1 — Tokenize (iyzico Saklı Kart, curl)

```bash
MTOKEN=$(curl -sk https://localhost:5101/connect/token -d "grant_type=client_credentials&client_id=<merchantId>&client_secret=<mk_...>&scope=cards.write" | jq -r .access_token)
curl -s -X POST "http://localhost:5201/api/v1.0/merchants/<merchantId>/vault/cards" \
  -H "Authorization: Bearer $MTOKEN" -H "Content-Type: application/json" \
  -d '{"pan":"5528790000000008","expiry":"12/30","holderName":"CARD HOLDER"}'
```
**Beklenen**: `{"token":"card_..."}` — 031 ile aynı yüzey.

**Gateway DB kontrolü (SC-002)**: `mt_doc_storedcard` satırında `CardUserKey`+`CardToken` (iyzico
kimlikleri) + `Bin`/`Last4`/`Brand` VAR; **açık PAN veya `EncryptedPan` YOK**.
```sql
select data->>'CardUserKey', data->>'CardToken', data->>'Bin', data->>'Last4', data->>'Brand' from paymentmanagement.mt_doc_storedcard;
select count(*) from paymentmanagement.mt_doc_storedcard where data::text like '%5528790000000008%';  -- 0 olmalı
```

**Negatifler**: bozuk PAN (`5528790000000009`) → iyzico reddi → 400, kayıt yok; yabancı merchantId
route → 403.

## S2 — Revoke (iyzico + yerel)

`DELETE .../vault/cards/{token}` → `{"token":"..."}`; gateway kaydı `Revoked`; iyzico'da kart silinir.
Tekrar → 200 (idempotent); uydurma token → `RECORD_NOT_FOUND`.

**iyzico tarafı kontrolü**: silinen token ile ödeme denenirse iyzico "kart bulunamadı" döner (ödeme
akışı geldiğinde; şimdilik gateway Revoked yeterli).

## S3 — Uçtan uca ECommerce (SC-001, sıfır dokunuş)

1. İki AppHost; ECommerce'te oturum açık kullanıcıyla Profil → Kartlarım → kart ekle
   (`5528790000000008`, 12/30) → **Beklenen**: listede Mastercard •0008
2. Gateway: paymentDb'de StoredCard (Active, CardUserKey/CardToken dolu, PAN yok)
3. Sil → listeden düşer + gateway Revoked + iyzico'dan silinir
4. ECommerce reposunda `git status` temiz (sıfır dokunuş kanıtı, FR-008)

## Kapanış

- `dotnet build` 0 hata; `dotnet test tests/Payment.Api.Tests` yeşil (aggregate saf testleri —
  Luhn/normalize çıktı, Create-kimliklerle + Revoke kaldı); Merchant + Commission regresyon yeşil.
- SC-005: kayıtta CardUserKey+CardToken var → CVC-siz ödeme akışının önkoşulu hazır.
