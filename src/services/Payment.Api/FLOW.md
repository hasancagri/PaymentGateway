# Payment.Api — Domain Süreci

**BC ne yapar:** Store (merchant sitesi) için **hosted ödeme** akışını yürütür — iyzico Checkout Form'da
ödeme linki üretir, müşterinin kart bilgisine hiç dokunmadan (yalnız iyzico'da) sonucu iyzico'dan bağımsız
doğrular, store'a **imzalı** sonuç bildirimi gönderir ve müşteriyi bir dönüş sayfasına yönlendirir.

> Domain-önce anlatı (EventStorming altitude). Sağdaki `(…)` = koda atlama köprüsü, süreç değil.
> Süreç değişince (yeni/silinen adım-event-policy) bu dosya güncellenir; mekanik rename'i guard yakalar.

## Süreç

1. **Merchant statüsü izlenir (arka plan, önkoşul).** `merchant.lifecycle` fanout'u (`MerchantCreated` /
   `MerchantProvisioned` / `MerchantStatusChanged`) tüketilir; yerel statü + API-key hash referansı upsert
   edilir `(MerchantLifecycleEventHandler.Handle → MerchantStatusReference, MerchantApiKeyReference)`.
2. **Store ödeme başlatır.** `POST /hosted-payment` (X-Api-Key; header SHA-256'lanıp
   `MerchantApiKeyReference` ile merchant'a çözülür) `(ApiKeyAuthenticationHandler.HandleAuthenticateAsync)`
   → tutar/para birimi/sipariş referansı/dönüş adresi taşıyan komut yayınlanır
   `(InitiateHostedPayment.InitiateHostedPaymentCommand)`.
3. **Alan + statü kapısı.** Yalnız TRY kabul edilir; tutar/alan boşsa reddedilir; merchant referansı yoksa
   veya `Active` değilse fail-closed RET (sağlayıcıya hiç gidilmez).
4. **İdempotent tekrar-başlatma kontrolü.** Aynı (merchant, OrderRef) için hâlâ Pending + HostedUrl'li bir
   girişim varsa yeni girişim açılmaz, var olan HostedUrl aynen döner (çift kayıt yok).
5. **Girişim doğar (Pending) + per-session secret üretilir.** Tahmin edilemez `CallbackToken` üretilir;
   aggregate Pending olarak başlar `(HostedPaymentSession.Start)`.
6. **iyzico Checkout Form initialize edilir.** Sentetik sandbox buyer/adres + `CallbackUrl` (PG base +
   secret `CallbackToken`) ile iyzico'ya istek atılır; başarısızsa/timeout'ta sağlayıcı-erişilemez hatası
   döner, girişim Pending kalır.
7. **Hosted URL bağlanır, store'a dönülür.** iyzico'dan gelen checkout-form token + ödeme sayfası URL'i
   girişime bağlanır `(HostedPaymentSession.AttachCheckoutForm)`; store'a `HostedUrl` + `PgPaymentRef`
   döner. Müşteri bu URL'de kartıyla öder (kart PG'ye hiç girmez).
8. **iyzico dönüşü işlenir (kimliksiz uç, secret-token kapılı).** `POST
   /internal/payments/callback/{callbackToken}` — bilinmeyen token → girişim bulunamaz, işlenmeden 404
   `(CompleteHostedPayment.CompleteHostedPaymentCommandHandler.Handle → SESSION_NOT_FOUND)`.
9. **Zaten terminal ise retrieve atlanır (idempotency).** Girişim daha önce Succeeded/Failed olduysa
   iyzico'ya tekrar sorulmaz; aynı sonuçla store-bildirimi yeniden yayınlanır.
10. **Sonuç iyzico'dan bağımsız doğrulanır.** Checkout-form retrieve çağrısı ile gerçek ödeme durumu
    çekilir (sahte "başarılı" gövdesi kabul edilmez); sağlayıcı geçici erişilemezse girişim Pending kalır,
    dönüş yeniden denenebilir.
11. **Girişim terminale taşınır.** Doğrulanan sonuca göre başarılı veya başarısız işaretlenir; terminalden
    terminale geri dönüş reddedilir (başarılı → başarılı kalır) `(HostedPaymentSession.MarkSucceeded |
    HostedPaymentSession.MarkFailed)`.
12. **Store'a imzalı sonuç bildirimi outbox'a düşer.** Terminal geçişte `[Transactional]` handler içinde
    mesaj yayınlanır — yalnız DB commit'i başarılıysa gönderim tetiklenir
    `(CompleteHostedPayment.CompleteHostedPaymentCommandHandler.PublishStoreCallback →
    StoreCallbackDelivery.Deliver)`.
13. **Bildirim HMAC ile imzalanıp store'a POST edilir.** Ham JSON gövde `CallbackSecret` (MerchantKey'den
    ayrı) ile HMAC-SHA256 imzalanır, `X-Signature` header'ıyla store'un `CallbackUrl`'ine gönderilir; 2xx
    dışı yanıt exception fırlatır → Wolverine durable retry (kayıpsız yeniden gönderim)
    `(StoreCallbackDelivery.DeliverHandler.Handle)`.
14. **Müşteri dönüş sayfasına yönlendirilir.** Callback ucu işleme sonunda müşteri tarayıcısını
    `GET /payments/return/{pgPaymentRef}` sayfasına yönlendirir; sayfa girişim durumuna göre başarı/
    başarısızlık metni gösterir, hassas veri taşımaz `(HostedPaymentEndpointExtension.BuildReturnPage)`.

## Domain kuralları (süreci yöneten değişmezler)

- **Charge yetkisi yalnız Active merchant (İlke V, fail-closed).** Provisioning/Passive/Suspended statüde
  ödeme başlatma sağlayıcıya hiç gitmeden reddedilir.
- **Terminal tek-yön (FR-008).** Succeeded/Failed'e ulaşan girişim asla geri dönmez; aynı yönde tekrar
  çağrı idempotent no-op'tur (ilk sonuç korunur), ters yönde çağrı `TERMINAL_VIOLATION` ile reddedilir.
- **CallbackToken = kimliksiz iyzico-callback ucunun beyan-edilen yetkisi (C1).** Bilinmeyen/eksik token
  hiçbir girişimi bulamaz; uç `AllowAnonymous` olsa da secret-token kapısı fiili auth'tur.
- **Sonuç kaynağı iyzico'nun kendisi, callback gövdesi değil.** Terminal karar her zaman bağımsız retrieve
  çağrısına dayanır; callback'in taşıdığı token yalnız hangi girişimin sorgulanacağını gösterir.
- **(Merchant, OrderRef) çifti tekildir (FR-004).** Aynı sipariş referansıyla ikinci başlatma yeni kayıt
  açmaz, var olan Pending girişimi döner.
- **Store bildirimi kayıpsız (FR-009).** Terminal geçiş + store-bildirim yayını aynı `[Transactional]`
  sınır içinde outbox'a yazılır; teslimat anlık başarısız olsa da sonuç DB'de kalıcıdır, Wolverine retry ile
  yeniden denenir.
- **Kart verisi hiçbir aşamada PG'ye girmez (FR-011).** Aggregate PAN/CVV alanı taşımaz; yalnız iyzico
  provider-payment referansı saklanır.
- **Yalnız TRY (FR-012, alan kısıtı).** Başka para birimiyle başlatma girişimi sağlayıcıya gitmeden
  reddedilir.

## Sınır (bu BC'nin dokunmadığı)

Merchant'ın kendi yaşam döngüsü (Provisioning/Active/Suspended geçişleri) Merchant BC'nin; Payment.Api
yalnız fanout'tan beslenen salt-okur bir statü/anahtar referansı tutar, karar üretmez. Store'un kendi
sipariş/ödeme durumu (Confirmed/Cancelled vb.) kendi BC'sinde yönetilir — Payment.Api yalnız sonuç bildirir,
store'un o bildirimi nasıl işlediğiyle ilgilenmez. Terk (müşteri hiç ödemezse) tespiti store tarafında
yapılır; PG proaktif bir "terk" bildirimi göndermez (v1 kapsam dışı). İade/void, ayrı ödeme-durum sorgu
ucu ve per-merchant `CallbackSecret` rotasyonu bu sürecin parçası değildir.
