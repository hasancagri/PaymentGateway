# Quickstart: PaymentLink Üretimi — Doğrulama

Uçtan-uca doğrulama. Kontrat detayı: [contracts/pg-payment-link.md](./contracts/pg-payment-link.md). Veri modeli: [data-model.md](./data-model.md).

## Ön koşullar

- Sistem Aspire AppHost'tan ayağa kalkar (Postgres + RabbitMQ otomatik):
  ```bash
  dotnet run --project src/aspire/AppHost/AppHost.csproj
  ```
- `payment.write` scope'lu JWT (Identity.Server `https://localhost:5101`, client_credentials) — store S2S çağrıları için.
- `PaymentLinkOptions` appsettings'te: `Ttl=00:30:00`, `GraceWindow=00:05:00`, `LinkBaseUrl`, güven-sayfa metinleri.

## Domain testleri (İlke VIII — önce bunlar yeşil)

```bash
dotnet test                      # tests/Payment.Api.Tests/PaymentLinkTests
```
Kapsam: `Create` (TRY/amount/alan doğrulama + Pending), `Open` (Pending→Opened, idempotent, lazy Expire), `IsPayable` (grace içi/dışı), `Cancel` (aktif→Cancelled, terminal red), terminal tek-yön.

## Senaryo 1 — Link üretimi (US1, SC-001/002)

1. `POST /payment-links` (geçerli gövde, `payment.write`) → **200**, `linkUrl` + `pgPaymentRef`. (SC-001: < 1 sn.)
2. Aynı `(merchantId, orderRef)` ile tekrar → **aynı** `linkUrl`/`pgPaymentRef` (idempotent, SC-002).
3. `currency:"USD"` → 400 `UNSUPPORTED_CURRENCY`; `amount:0` → 400 `INVALID_REQUEST`.

**Beklenen**: Marten'da tek Pending kayıt; `(merchantId,orderRef)` partial unique.

## Senaryo 2 — Açılış + güven bağlamı (US2, SC-004)

1. Senaryo-1 `linkUrl`'ini tarayıcıda aç (`GET /payment-links/{openToken}`) → güven bağlamı sayfası: **mağaza adı + sipariş özeti + tutar + son geçerlilik**, kart alanı GÖRÜNMEDEN. Link Opened'a geçer.
2. Sahte token ile aç → **404** (fail-closed, SC-003).
3. Sayfadan "Öde" (`POST /payment-links/{openToken}/pay`) → 400 `PROVIDER_UNAVAILABLE`; link Pending/Opened kalır (terminale geçmez).

## Senaryo 3 — Yaşam döngüsü: süre + iptal (US3, SC-003)

1. **İptal**: `POST /payment-links/{pgPaymentRef}/cancel` (`payment.write`) → 200; link Cancelled. Sonra açma → 404/`LINK_CANCELLED`; cancel tekrar → `TERMINAL_VIOLATION`.
2. **Süre dolumu (lazy)**: `Ttl`'i kısa (ör. `00:00:05`) ayarla; link üret; TTL geçtikten sonra aç → açılmaz, link **Expired** (erişimde hesaplanır).
3. **Terminal sonrası re-create**: Expired/Cancelled link sonrası aynı `orderRef` ile tekrar `POST /payment-links` → **yeni** link üretilir (aktif-tekil; idempotent-return yalnız aktif link varken).

## PAN sızıntı denetimi (SC-005)

- Tüm akış boyunca kart numarası hiçbir log/DB kayıt/yanıtta görünmez — bu dilimde PAN sağlayıcıya dahi iletilmez (Pay stub). `OpenToken` log/trace'te görünmez (İlke VI).

## Guard'lar

```bash
scripts/check-claude-spec-links.sh      # BC haritası spec yolları
scripts/check-flow-links.sh             # FLOW.md kenar-anchor tip adları (PaymentLink.* kodda var)
dotnet build                            # 0 hata
```
