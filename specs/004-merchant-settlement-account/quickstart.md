# Quickstart / Validation: Merchant Settlement Hesabı (004)

Feature'ın uçtan uca çalıştığını doğrulama rehberi. Detaylar: [data-model.md](./data-model.md),
[contracts/](./contracts/settlement-accounts.http.md).

## Önkoşullar

- Sistem Aspire ile ayağa: `dotnet run --project src/aspire/AppHost/AppHost.csproj`
  (Postgres + RabbitMQ + servisler; Merchant.Api conn-string'i Aspire'dan alır).
- En az bir mevcut `Merchant` (004 onun `Id`'sini kullanır). Yoksa önce
  `POST api/v1/merchants` ile oluştur (bkz. 001).
- Geçerli test IBAN'ı: gerçek TR IBAN (mod-97 doğru). Örn. banka test IBAN'ı kullan;
  uydurma `TR00...` mod-97'den geçmez.

## Senaryo 1 — Hesap ekle (US1 / SC-001)

```bash
MID=<mevcut-merchant-guid>
curl -X POST "http://<merchant-api>/api/v1/merchants/$MID/settlement-accounts" \
  -H "Content-Type: application/json" \
  -d '{"bankCode":"0062","iban":"<gecerli-TR-IBAN>","accountOwnerName":"ACME","accountNo":"123","accountDescription":"TL"}'
```
**Beklenen**: `200` + `{ "id": "..." }`. Hesap `Active`.

## Senaryo 2 — Doğrulama hataları (US1 / SC-002)

- Geçersiz IBAN (`"iban":"TR00"`) → `400`, `Code=COMMON_MESSAGE_INVALID_FORMAT`, `Property=Iban`.
- Katalogda olmayan bankCode (`"bankCode":"0000"`) → `400`, `COMMON_MESSAGE_RECORD_NOT_FOUND`, `Property=BankCode`.
- Olmayan merchant (rota GUID rastgele) → `400`, `COMMON_MESSAGE_RECORD_NOT_FOUND`, `Property=MerchantId`.
- Aynı IBAN'ı ikinci kez ekle → `400`, `COMMON_MESSAGE_RECORD_DUPLICATE`, `Property=Iban` (SC-005).
- **Kısmi kayıt bırakmaz**: hatadan sonra liste (`GET /`) yeni kayıt göstermez.

## Senaryo 3 — Listele + tenant izolasyonu (US2 / SC-003)

```bash
curl "http://<merchant-api>/api/v1/merchants/$MID/settlement-accounts"
```
**Beklenen**: yalnız `$MID`'nin hesapları. Başka merchant'ın (`$MID2`) hesabı bu listede YOK.
Her item `bankName` (lookup türevi) içerir.

## Senaryo 4 — Tekil görüntüle (US2)

```bash
curl "http://<merchant-api>/api/v1/merchants/$MID/settlement-accounts/<accountId>"
```
**Beklenen**: `200` + tam ayrıntı. Başka merchant'ın accountId'si → `404` (tenant sızıntısı yok).

## Senaryo 5 — Güncelle (US3)

```bash
curl -X PUT "http://<merchant-api>/api/v1/merchants/$MID/settlement-accounts/<accountId>" \
  -H "Content-Type: application/json" \
  -d '{"bankCode":"0010","iban":"<yeni-gecerli-IBAN>","accountOwnerName":"ACME 2","accountNo":"456","accountDescription":""}'
```
**Beklenen**: `200`; `GET` yeni değerleri döner. Geçersiz IBAN'la `PUT` → `400`, eski değerler korunur.

## Senaryo 6 — Pasife al (US3 / SC-004)

```bash
curl -X PATCH "http://<merchant-api>/api/v1/merchants/$MID/settlement-accounts/<accountId>/status" \
  -H "Content-Type: application/json" -d '{"isActive":false}'
```
**Beklenen**: `200` + `status="Passive"`. `GET` `Passive` gösterir; kayıt hâlâ var (silinmedi).

## Saf domain birim testleri (host'suz — öncelik)

Test projesi eklenince `MerchantSettlementAccount` için:
- Geçerli TR IBAN → `Create` Ok; IBAN normalize (boşluksuz, upper).
- Bozuk mod-97 IBAN → Error `INVALID_FORMAT`.
- TR dışı IBAN (`DE...`) → Error.
- Zorunlu alan boş → Error `VALUE_IS_REQUIRED`.
- `Deactivate` → `Status=Passive`, `IsActive=false`; kayıt korunur.
- `BankCodeLookup.Exists`: katalog kodu true, bilinmeyen false.

## Doğrulama tamam sayılır

- [ ] 6 senaryo beklenen sonuçları verir.
- [ ] Tenant izolasyonu: cross-merchant erişim `404`/boş.
- [ ] Hatalar kısmi kayıt bırakmaz.
- [ ] `dotnet build` yeşil; Aspire ayağa kalkar.