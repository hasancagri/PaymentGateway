# Payment.Api — Domain Süreci

**BC ne yapar:** Store (merchant sitesi) için **hosted ödeme** akışını yürütür — müşterinin kart bilgisine
hiç dokunmadan ödeme sağlayıcısında sonucu toplar, store'a **imzalı** sonuç bildirimi gönderir ve müşteriyi
bir dönüş sayfasına yönlendirir.

> **088 Phase-0 durumu:** ödeme sağlayıcısına (eski kanal) giden initialize/retrieve çağrıları SÖKÜLDÜ;
> sağlayıcı entegrasyonu Phase-1'de banka PaymentLink adapter'ı (anayasa İlke VII port/adapter) ile
> bağlanacak. Aşağıdaki süreç bu iskeletin **kavramsal** akışıdır; sağlayıcı adımları o ana dek
> `PROVIDER_UNAVAILABLE` ile stub'lıdır.

> Domain-önce anlatı (EventStorming altitude). Sağdaki `(…)` = koda atlama köprüsü, süreç değil.
> Süreç değişince (yeni/silinen adım-event-policy) bu dosya güncellenir; mekanik rename'i guard yakalar.

## Süreç

1. **Store ödeme başlatır.** `POST /hosted-payment` — tutar/para birimi/sipariş referansı/dönüş adresi +
   tenant merchant taşıyan komut yayınlanır `(InitiateHostedPayment.InitiateHostedPaymentCommand)`.
   *(088: yetki GEÇİCİ `payment.write` JWT scope; tenant gövdeden — Phase-1'de platform IdP + anonim token.)*
2. **Alan doğrulama.** Yalnız TRY kabul edilir; tutar/alan boşsa reddedilir (`UNSUPPORTED_CURRENCY` /
   `INVALID_REQUEST`). *(088: merchant Active-gate + sağlayıcı initialize Phase-1'e devredildi → stub RET.)*
3. **Girişim doğar (Pending) + per-session secret üretilir.** Tahmin edilemez bir callback-token üretilir;
   aggregate Pending olarak başlar `(HostedPaymentSession.Start)`. *(Phase-1: adapter'dan dönen hosted
   sayfa URL'i `(HostedPaymentSession.AttachCheckoutForm)` ile bağlanır.)*
4. **Sağlayıcı dönüşü işlenir (kimliksiz uç, secret-token kapılı).** `POST
   /internal/payments/callback/{callbackToken}` — bilinmeyen token → girişim bulunamaz, işlenmeden 404
   `(CompleteHostedPayment.CompleteHostedPaymentCommandHandler.Handle)` → `SESSION_NOT_FOUND`.
5. **Zaten terminal ise sonuç yeniden bildirilir (idempotency).** Girişim daha önce Succeeded/Failed olduysa
   aynı sonuçla store-bildirimi yeniden yayınlanır (sağlayıcıya tekrar sorulmaz).
6. **Girişim terminale taşınır.** *(Phase-1: adapter sonuç teyidine göre)* başarılı/başarısız işaretlenir;
   terminalden terminale geri dönüş reddedilir `(HostedPaymentSession.MarkSucceeded |
   HostedPaymentSession.MarkFailed)`.
7. **Store'a imzalı sonuç bildirimi outbox'a düşer.** Terminal geçişte `[Transactional]` handler içinde
   mesaj yayınlanır — yalnız DB commit'i başarılıysa gönderim tetiklenir
   `(CompleteHostedPayment.CompleteHostedPaymentCommandHandler.PublishStoreCallback →
   StoreCallbackDelivery.Deliver)`.
8. **Bildirim HMAC ile imzalanıp store'a POST edilir.** Ham JSON gövde `CallbackSecret` ile HMAC-SHA256
   imzalanır, `X-Signature` header'ıyla store'un `CallbackUrl`'ine gönderilir; 2xx dışı yanıt exception
   fırlatır → Wolverine durable retry (kayıpsız yeniden gönderim) `(StoreCallbackDelivery.DeliverHandler.Handle)`.
9. **Müşteri dönüş sayfasına yönlendirilir.** Callback ucu işleme sonunda müşteri tarayıcısını
   `GET /payments/return/{pgPaymentRef}` sayfasına yönlendirir; sayfa girişim durumuna göre başarı/
   başarısızlık metni gösterir, hassas veri taşımaz `(HostedPaymentEndpointExtension.BuildReturnPage)`.

## Domain kuralları (süreci yöneten değişmezler)

- **Terminal tek-yön (FR-008).** Succeeded/Failed'e ulaşan girişim asla geri dönmez; aynı yönde tekrar
  çağrı idempotent no-op'tur (ilk sonuç korunur), ters yönde çağrı `TERMINAL_VIOLATION` ile reddedilir.
- **CallbackToken = kimliksiz callback ucunun beyan-edilen yetkisi (C1).** Bilinmeyen/eksik token hiçbir
  girişimi bulamaz; uç `AllowAnonymous` olsa da secret-token kapısı fiili auth'tur.
- **(Merchant, OrderRef) çifti tekildir (FR-004).** Aynı sipariş referansıyla ikinci başlatma yeni kayıt
  açmaz, var olan Pending girişimi döner (Phase-1 idempotent başlatma).
- **Store bildirimi kayıpsız (FR-009).** Terminal geçiş + store-bildirim yayını aynı `[Transactional]`
  sınır içinde outbox'a yazılır; teslimat anlık başarısız olsa da sonuç DB'de kalıcıdır, retry ile yeniden denenir.
- **Kart verisi hiçbir aşamada PG'ye girmez (anayasa İlke VI).** Aggregate PAN/CVV alanı taşımaz; yalnız
  sağlayıcı ödeme referansı saklanır.
- **Yalnız TRY (FR-012, alan kısıtı).** Başka para birimiyle başlatma reddedilir.

## Sınır (bu BC'nin dokunmadığı)

Store'un kendi sipariş/ödeme durumu (Confirmed/Cancelled vb.) kendi BC'sinde yönetilir — Payment.Api yalnız
sonuç bildirir. Banka sağlayıcısına erişim Phase-1'de BC-sahipli adapter arkasında olacak (İlke VII); banka
wire tipleri bu BC'ye sızmaz. Terk tespiti store tarafında yapılır (v1 kapsam dışı). İade/void, ödeme-durum
sorgu ucu ve per-merchant `CallbackSecret` rotasyonu bu sürecin parçası değildir. **088 öncesi merchant
statü/anahtar referansı + eski ödeme kanalı entegrasyonu söküldü** (Merchant BC öldü; sağlayıcı Phase-1).
