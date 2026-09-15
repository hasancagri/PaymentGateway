# Quickstart / Canlı Doğrulama: Hosted-CF Ödeme (041)

Sandbox iyzico ile uçtan uca doğrulama. Detaylar: [data-model.md](./data-model.md),
[contracts/pg-external.md](./contracts/pg-external.md), [contracts/iyzico-cf-wire.md](./contracts/iyzico-cf-wire.md).

## Ön koşullar

- Sistem Aspire AppHost'tan ayakta: `dotnet run --project src/aspire/AppHost/AppHost.csproj`
  (Postgres + RabbitMQ; Marten şema auto-apply).
- iyzico sandbox anahtarları user-secrets'ta:
  `dotnet user-secrets set "IyzicoProviderSettings:ApiKey" <k> --project src/services/Payment.Api`
  (aynısı `SecretKey`, `BaseUrl=https://sandbox-api.iyzipay.com`).
- `HostedPaymentOptions` user-secrets: `CallbackSecret`, `IyzicoCallbackUrl`
  (= Payment.Api dış-erişilebilir `.../internal/payments/callback`).
- En az bir **Active** merchant + MerchantKey (onboarding akışından; `merchant.lifecycle` event'i Payment
  referanslarını beslemiş olmalı). Sandbox test kartı: 5406670000000009 (İş Maximum).

## Senaryo 1 — Ödeme başlat (US1, P1)

1. `POST /hosted-payment` `X-Api-Key: {MerchantKey}` body `{Amount:100.0, Currency:"TRY", OrderRef:"T-001",
   CallbackUrl:"http://localhost:xxxx/store-callback"}`.
2. **Beklenen**: 200 `{ HostedUrl, PgPaymentRef }`. `HostedUrl` tarayıcıda iyzico CF sayfasını açar.
   paymentDb'de `HostedPaymentSession(Pending)` + CheckoutFormToken saklı.
3. Aynı OrderRef ile tekrar `POST` → yeni kayıt AÇILMAZ, aynı HostedUrl döner (FR-004).
4. Pasif/Provisioning merchant anahtarıyla → RET (Active değil, FR-002).
5. `Currency:"USD"` → RET (yalnız TRY, FR-012).

## Senaryo 2 — Ödeme tamamlanma + store bildirimi (US2, P1)

1. HostedUrl'de sandbox kartıyla öde (başarı).
2. iyzico tarayıcıyı `/internal/payments/callback` (token) ucuna döndürür.
3. **Beklenen**: PG CF retrieve çağırır → `paymentStatus SUCCESS`; session `Succeeded` (ProviderPaymentId dolu).
   Store `CallbackUrl`'ine `X-Signature`'lı POST gider `{TxRef:"T-001", PgPaymentRef, Status:"Success"}`.
   Müşteri `/payments/return/{pgPaymentRef}` başarı sayfasını görür.
4. Başarısız/iptal ödeme → session `Failed`; store'a `Status:"Failed"` + ReasonCode; müşteri başarısız sayfası.
5. Store callback adresi erişilemezken → sonuç paymentDb'de kalır, Wolverine retry ile yeniden POST (FR-009).

## Senaryo 3 — Güvenlik + idempotency (US3, P2)

1. **İmza doğrulama**: store gelen POST'u `HMAC-SHA256(CallbackSecret, raw_body)` ile doğrular →
   `X-Signature` eşleşir. Yanlış sırla → store reddeder (SC-004).
2. **Tekrar-callback**: Succeeded session'a aynı iyzico dönüşü tekrar gelir → durum DEĞİŞMEZ (terminal),
   store'a aynı `Success` sonucu tekrar bildirilir (FR-008, SC-003).
3. **PAN sızıntısı yok**: paymentDb + loglarda PAN/CVV aranır → hiçbir yerde yok (SC-005).

## Kontrol listesi (spec eşlemesi)

- [ ] SC-001: başlatma < 5 sn, çalışan HostedUrl.
- [ ] SC-002: başarıda store imzalı bildirim alır, imza %100 doğrular.
- [ ] SC-003: tekrar-başlatma + tekrar-callback çift kayıt/çift terminal ÜRETMEZ.
- [ ] SC-004: yanlış-imzalı bildirim store'da reddedilir.
- [ ] SC-005: PAN hiçbir yerde görünmez.

## Domain birim testleri (test-first — İlke VI)

`tests/Payment.Api.Tests/HostedPaymentSessionTests.cs`: Start (Pending), MarkSucceeded (Pending→Succeeded),
MarkFailed (Pending→Failed), terminal-idempotent (Succeeded'a MarkSucceeded no-op), terminal-ihlali
(Succeeded'a MarkFailed RET), AttachCheckoutForm guard.
