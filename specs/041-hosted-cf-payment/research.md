# Phase 0 Research: Hosted-CF Ödeme Yüzeyi (041)

Tüm NEEDS CLARIFICATION çözüldü. Kararlar mevcut kod + spec varsayımları + ECom 077 karşı-kontratı ile hizalı.

## D1 — Store→PG kimlik doğrulama şeması

- **Decision**: Mevcut `ApiKeyAuthenticationHandler` (X-Api-Key = MerchantKey → SHA-256 → `MerchantApiKeyReference`
  → `merchant_id` claim). Ödeme başlatma ucu YENİ policy `HostedPaymentApiKey` kullanır: ApiKey şeması +
  `RequireAuthenticatedUser()` — **route'ta {merchantId} YOK**, tenant claim'den okunur.
- **Rationale**: ECom 077 kontratı `X-Api-Key: {MerchantKey}` gönderir (OAuth token değil); route path
  `/hosted-payment` merchantId taşımaz. Mevcut `MerchantApiKey` policy `MerchantScopeRequirement` içerir ve
  route'ta merchantId yoksa **fail-closed RET** eder (`MerchantScopeEvaluator`) → bu uçta kullanılamaz.
- **Alternatives**: (a) OAuth client_credentials + PaymentCharge scope — reddedildi: store kontratı X-Api-Key
  sabitledi, MerchantKey `connect/token` yerine header'da taşınır. (b) Route'a {merchantId} eklemek — reddedildi:
  kimlik sızması + kontrat kırılır.

## D2 — Charge statü kapısı (Active-only, fail-closed)

- **Decision**: Handler, `merchant_id` claim'iyle `MerchantStatusReference` yükler; kayıt yok veya
  `Status != "Active"` → RET (`HOSTED_PAYMENT_MERCHANT_NOT_ACTIVE`). Sökülen `ChargeSavedCardForAgent`'ın
  statü-kapısı deseni birebir.
- **Rationale**: Anayasa İlke V — charge yetkisi yalnız Active; Provisioning/Passive/Suspended reddedilir
  (FR-002). Statü referansı `merchant.lifecycle` fanout'undan beslenir (mevcut `MerchantLifecycleEventHandler`).
- **Alternatives**: Merchant.Api'ye senkron sorma — reddedildi: BC izolasyonu + mevcut event-fed referans yeter.

## D3 — iyzico Checkout Form initialize wire (ödeme)

- **Decision**: Slice-nested camelCase POCO (sökülen `StartCardSession` init wire'ından uyarlanır):
  `InitializeCheckoutFormRequest { Locale, ConversationId, Price, PaidPrice, Currency, BasketId, PaymentGroup,
  CallbackUrl, EnabledInstallments=[1], Buyer, ShippingAddress, BillingAddress, BasketItems }`. Yanıt
  `CheckoutFormInitializeResult : ProviderResourceV2 { Token, PaymentPageUrl, TokenExpireTime }`.
  Path = `IyzicoRequestOptions.CheckoutFormInitializePath` (`/payment/iyzipos/checkoutform/initialize/auth/ecom`).
  `CallbackUrl` (iyzico'ya verilen) = **PG-sahipli** `HostedPaymentOptions.IyzicoCallbackUrl` (store'unki DEĞİL).
- **Rationale**: 040'ta kart-kaydet için kanıtlanmış wire; ödeme için `Price/PaidPrice = istek Amount`,
  `EnabledInstallments=[1]` (taksit söküldü). CF init/retrieve path'i kart-kaydet ile ödeme için AYNI (repurpose).
- **Alternatives**: NonSecure `/payment/auth` (PAN gateway'e girer) — reddedildi (FR-011). 3DS init — CF zaten
  3DS/hosted kapsar, sandbox'ta yeterli.

## D4 — Buyer/basket sentezi (store buyer taşımaz)

- **Decision**: Store→PG kontratı yalnız `Amount/Currency/OrderRef/CallbackUrl` gönderir → buyer + tek sepet
  kalemi + adresler **sentezlenir** (038 sentetik-sepet + 040 sandbox-buyer deseni). Sepet kalemi
  `Price = Amount`; buyer sandbox nominal fixture. Kart-ekleme aksine sentez ÖDEMEDE de geçerli (gerçek
  müşteri bilgisi kontratta yok).
- **Rationale**: iyzico CF initialize buyer/basketItems/address ZORUNLU kılar; store bunları göndermez.
  Sandbox'ta sentetik buyer kabul (canlı KYC/gerçek-buyer backlog).
- **Alternatives**: Kontrata buyer eklemek — reddedildi: ECom 077 sabit, kapsam-dışı; sandbox sentezi yeter.

## D5 — iyzico→PG callback + sonuç doğrulama (CF retrieve)

- **Decision**: `POST /internal/payments/callback/{callbackToken}` — **secret-token kapılı** (C1 fix; İlke V
  "yetki açıkça beyan"). `callbackToken` = PG'nin Start'ta ürettiği tahmin-edilemez per-session sır;
  iyzico'ya verilen callback URL'inin path segmentidir. Uç: token ile session bul (bilinmeyen → 404 red),
  form-body iyzico `token`'ı ile `RetrieveCheckoutFormRequest { Locale, ConversationId, Token }` → CF retrieve
  (`CheckoutFormRetrievePath`). Yanıt `RetrieveCheckoutFormResult : ProviderResourceV2 { PaymentStatus,
  PaymentId, ... }`. `PaymentStatus=="SUCCESS"` (+ Status==success) → `MarkSucceeded`; aksi → `MarkFailed`.
- **Rationale**: İki katman: (1) secret callbackToken uca girişi kapılar (beyan-edilen yetki, İlke V karşılanır —
  varsayılan-açık DEĞİL); (2) sonuç iyzico bağımsız retrieve ile TEYİT edilir (kaynak-of-truth, sahte gövde
  düşer). iyzico'ya OAuth/X-Api-Key ekletemeyiz → sır URL'e gömülür (sağlayıcı aynen geri çağırır).
- **Alternatives**: Kimliksiz uç (yalnız retrieve teyidi) — reddedildi (C1: İlke V durum-değiştiren uçta yetki
  beyanı ZORUNLU). Callback gövdesindeki sonuca güvenmek — reddedildi (sahtelenebilir). iyzico webhook imza —
  sandbox CF'de retrieve + secret-token yeter; webhook-imza backlog.

## D6 — Idempotency (başlatma tekilliği + terminal tek-yön)

- **Decision**: (a) `HostedPaymentSession` Marten'de **(MerchantId, OrderRef) bileşik tekil index**. İkinci
  başlatma pending session bulursa yeni kayıt AÇMAZ; mevcut `HostedUrl`+`PgPaymentRef` döner (FR-004).
  (b) Terminal geçiş aggregate'te tek-yön guard: `MarkSucceeded`/`MarkFailed` zaten terminal ise no-op,
  mevcut sonucu korur (FR-008). Tekrar-callback aynı sonucu tekrar yayınlar (store'a idempotent bildirim).
- **Rationale**: Çift ödeme kaydı + çift terminal engeli (SC-003). Marten unique index + aggregate guard.
- **Alternatives**: Sadece uygulama-katı kontrol — reddedildi: yarış koşulunda DB tekil index gerekli.

## D7 — Store'a imzalı sonuç bildirimi + kayıpsız yeniden gönderim

- **Decision**: Session terminal olunca handler `[Transactional]` ile **durable local Wolverine mesajı**
  (`StoreCallbackDelivery.Deliver`) publish eder (outbox — yalnız DB commit'te gider). Handler ham JSON gövde
  üretir, `X-Signature = HMAC-SHA256(CallbackSecret, raw_body)` header'ıyla store `CallbackUrl`'ine POST eder.
  Başarısızlıkta Wolverine retry (durable local queue). Gövde: `{ TxRef=OrderRef, PgPaymentRef, Status, ReasonCode? }`.
- **Rationale**: FR-009 "kaybolmaz + yeniden gönderilebilir" = deterministik outbox + durable retry (mevcut
  `UseDurableLocalQueues`). FR-007 imza = HMAC-SHA256(CallbackSecret). Dış HTTP + dinamik URL → integration
  event (fanout) değil, local durable mesaj.
- **Alternatives**: Endpoint içinde senkron POST — reddedildi: ağ hatası sonucu kaybeder. RabbitMQ fanout —
  reddedildi: hedef dış store HTTP, per-session dinamik URL (fanout modeli uymaz).

## D8 — CallbackSecret kaynağı

- **Decision**: v1 = paylaşılan PG config `HostedPaymentOptions.CallbackSecret` (Options pattern, user-secrets).
  MerchantKey'den AYRI. Per-merchant üretim/rotasyon backlog.
- **Rationale**: Spec varsayımı (CallbackSecret v1 = paylaşılan config). Handler'da literal yasak → Options.
- **Alternatives**: Per-merchant secret — reddedildi (v1 kapsam-dışı, backlog).

## D9 — Müşteri dönüş sayfası

- **Decision**: `GET /payments/return/{pgPaymentRef}` (veya callback sonrası 302) minimal HTML dönüş sayfası
  sunar (başarılı/başarısız metni `HostedPaymentOptions`'tan). Callback işleme sonrası tarayıcı buraya
  yönlendirilir.
- **Rationale**: FR-010 — müşteriye sonuca uygun dönüş sayfası ("ödemen alındı / başarısız"). Kontrat PG
  scope'unda ("Tarayıcı dönüş sayfası").
- **Alternatives**: Store'a redirect — reddedildi: store return-URL kontratı v1'de yok; PG-sunulan sayfa yeter.

## D10 — Para birimi kısıtı

- **Decision**: Yalnız `TRY` kabul; `Currency != "TRY"` → RET (`HOSTED_PAYMENT_UNSUPPORTED_CURRENCY`).
- **Rationale**: Anayasa alan kısıtı + FR-012.
- **Alternatives**: Yok.

## D11 — Endpoint yolu + sürümleme

- **Decision**: Store ucu `/hosted-payment` (ECom 077 sabit kontrat yolu). Sürüm segmenti eklenmez (dış
  tüketici sabit path bekler); gateway (YARP) yönlendirmesi altyapı. iyzico callback `/internal/payments/callback`,
  dönüş sayfası `/payments/return/{pgPaymentRef}`.
- **Rationale**: Dış kontrat kazanır; store PgBaseUrl + sabit path çağırır. İç uçlar (callback/return) PG-sahipli.
- **Alternatives**: `/v1/hosted-payment` — reddedildi (kontrat path'i sabit; kırar).
