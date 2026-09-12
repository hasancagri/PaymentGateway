# Quickstart: Hosted Kart Saklama (075) — Uçtan Uca Doğrulama

Kod değil, **doğrulama** rehberi. İki repo birlikte (PG + store) + iyzico sandbox.

## Önkoşullar

- PG Aspire ayakta: `dotnet run --project src/aspire/AppHost/AppHost.csproj` (Postgres + RabbitMQ + Mailpit).
- iyzico **sandbox** apiKey/secretKey PG yapılandırmasında (Payment.Api `IyzicoProviderSettings`).
- Merchant kaydı + X-Api-Key (mevcut onboarding / seed).
- Store (ECommerceWithAgentFramework) ayakta; `PgCardOptions:BaseUrl` PG'ye bakar; Claude Desktop bağlı.
- iyzico sandbox test kartı (ör. `5528790000000008`, ileri SKT, CVC `123`).

## Senaryo 1 — Hosted kart ekleme (US1) [P1]

1. Store'dan "kart ekle" → PG `POST /vault/card-sessions` → `{addUrl, conversationId}`.
2. `addUrl` tarayıcıda → **iyzico hosted form**; test kartı girilir.
3. Store `GET /vault/card-sessions/{conversationId}` → `{status:"success", pgUserHandle}`.
- **Beklenen:** PG kaydında/loğunda PAN/CVV **yok** (SC-001). `CardSession` Completed; `StoredCard` yazıldı.

## Senaryo 2 — Listeleme (US2) [P1]

1. `GET /vault/cards?userHandle=<pgUserHandle>`.
- **Beklenen:** kart marka+son4+SKT+alias+`cardHandle`; PAN/CVV yok. Handle yoksa `{cards:[]}`.

## Senaryo 3 — NON-3D çekim (US3) [P2]

1. `POST /merchants/{merchantId}/payments` `{correlationKey, userHandle, cardHandle, price, buyer}`.
- **Beklenen:** `{status:"success", pgPaymentId}`; 3DS ekranı YOK; taksit YOK. Aynı correlationKey ile
  tekrar → yeni çekim yok (aynı sonuç, SC-004).

## Senaryo 4 — Silme (US4) [P2]

1. `DELETE /vault/cards` `{userHandle, cardHandle}` → `{deleted:true}`.
- **Beklenen:** sonraki listede yok. Başka userHandle'ın cardHandle'ıyla → etkisiz/red (SC-005).

## Senaryo 5 — İki kart (R2 gruplama) [P2]

1. İkinci "kart ekle" — store `Wallet.PgUserHandle` doluysa add-session'a `userHandle` gönderir.
- **Beklenen:** ikinci kart AYNI cardUserKey'e eklenir; liste iki kart döner.

## Senaryo 6 — Eski PAN yolu yok (US5/SC-006)

```bash
# PG'de PAN kabul eden uç kalmadı:
grep -rn "TokenizeCard" ~/dev/PaymentGateway/src/services/Payment.Api --include=*.cs
```
- **Beklenen:** 0 sonuç (kart girişi yalnız hosted form).

## Regresyon

- `dotnet test` — `CardSession` domain testleri (Start/Complete boş-cardUserKey red/Fail) yeşil.
- Mevcut charge/retrieve (039) + saklı-kartla çekim (033) kırılmadı (handle girdisiyle).
- Store 075 quickstart 5 senaryosu iki repo birlikte PASS.