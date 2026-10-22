# Payment.Api — Domain Süreci

**BC ne yapar:** Store (merchant sitesi) için **pay-by-link** akışını yürütür — bir sipariş için tek-kullanımlık
TTL'li ödeme linki üretir, müşteri linki açınca kart bilgisine hiç dokunmadan **güven bağlamını** (mağaza +
sipariş + tutar + son geçerlilik) gösterir, linkin yaşam döngüsünü (açılış, süre dolumu, iptal) yönetir.

> **090 durum:** linkin doğuşu + müşteri açılışı/güven bağlamı + yaşam döngüsü (089) üstüne **NonSecure banka
> çekimi** bağlandı: müşteri kart girer, BIN'den issuer çözülür, router 3 simüle banka arasında aday sıralar
> (yetenek eleme → on-us + maliyet) ve Technical/Config redde sıradaki bankaya **canlı failover** yapar;
> onayda `Paid` terminal + store'a **HMAC-imzalı dayanıklı webhook** + `PaymentCompleted/Failed` olayı. Üstüne
> **3DS** (imzalı 3D form + callback HASH doğrulama + 3DModel tamamlama), iade/void, durum-sorgu ve in-doubt
> watchdog bağlandı. **Gün-sonu mutabakat çekişi ve nihai yetki (platform IdP) sonraki dilim.**

> Domain-önce anlatı (EventStorming altitude). Sağdaki `(…)` = koda atlama köprüsü, süreç değil.
> Süreç değişince (yeni/silinen adım-event-policy) bu dosya güncellenir; mekanik rename'i guard yakalar.

## Süreç

1. **Store ödeme linki ister.** `POST /payment-links` — tutar/para birimi/sipariş referansı/dönüş adresi +
   mağaza adı + sipariş özeti + tenant merchant taşıyan komut yayınlanır `(CreatePaymentLink.CreatePaymentLinkCommand)`.
   *(089: yetki GEÇİCİ `payment.write` JWT scope; tenant gövdeden — nihai platform IdP + anonim token.)*
2. **Alan doğrulama + aktif-tekil.** Yalnız TRY kabul (`UNSUPPORTED_CURRENCY`), tutar>0/alanlar dolu
   (`INVALID_REQUEST`); aynı (merchant, orderRef) için AKTİF link varsa yeni üretilmez, var olan döner
   (idempotent) `(CreatePaymentLink.CreatePaymentLinkCommandHandler)`.
3. **Link doğar (Pending) + gizli açılış token'ı üretilir.** Tahmin-edilemez `OpenToken` üretilir, TTL
   `ExpiresAt` hesaplanır; aggregate Pending olarak başlar `(PaymentLink.Create)`. Yanıt: link URL + opak
   `PgPaymentRef`. Zaman olarak geçmiş aktif kayıt bulunursa lazy `(PaymentLink.Expire)` ile Expired'a çevrilip
   yeni link üretilir (aktif-tekil slot boşalır).
4. **Müşteri linki açar, güven bağlamını görür.** `GET /payment-links/{openToken}` — bilinmeyen/sahte token
   hiçbir linki açmaz (fail-closed, 404) `(OpenPaymentLink.OpenPaymentLinkCommandHandler)`. Geçerli token +
   süresi geçmemiş → link Pending'den Opened'a geçer, kart formundan ÖNCE güven bağlamı render edilir
   `(PaymentLink.Open)`. Süresi geçmişse lazy Expired → açılmaz.
5. **Müşteri kartı girer ve "Öde"ye basar.** `POST /payment-links/{openToken}/pay` — kart gövdede taşınır
   (yalnız çekime, SAKLANMAZ — İlke VI). Ödenebilirlik guard'ı (aktif + TTL+grace içinde) değerlendirilir
   `(PaymentLink.IsPayable)` `(PayPaymentLink.PayPaymentLinkCommandHandler)`.
6. **Banka seçimi + çekim + failover.** Kart BIN'inden issuer/şema/kart-tipi çözülür `(BinCard)`; router iki
   aşamada aday sıralar — yetenek eleme + on-us (issuer == acquirer) + maliyet — ve sırayla dener: Approved/
   IssuerDecline/FraudDecline → DUR, TechnicalFailure/ConfigError → sıradaki uygun bankaya canlı geçiş
   `(BankChargeRouter)`; banka adapter'ı kartı gerçek InterVpos teline (`;;`) POST eder ve cevabı normalize
   onay/4-sınıf-red sonucuna çevirir `(InterVposBankAdapter)` `(InterVposResponseMapper)`. Denenen her banka
   link altına bir deneme kaydı olur `(PaymentLink.RecordAttempt)`.
7. **Terminal sonuç + store bildirimi.** Onayda link `Paid` terminale geçer; `PaymentCompletedEvent` yayınlanır
   ve store'a HMAC-imzalı dayanıklı webhook gider `(StoreResultDelivery.Deliver)`. Issuer/Fraud reddinde link
   `Opened` kalır (başka kartla tekrar denenebilir), `CHARGE_DECLINED` + `PaymentFailedEvent` + webhook. Tüm
   uygun bankalar teknik hata verirse `PROVIDER_UNAVAILABLE` döner (link Opened, olay/webhook YOK — geçici).
   Durum değişikliği + olay + webhook aynı `[Transactional]` outbox'ta atomiktir.
8. **Link ölümü — iptal.** `POST /payment-links/{pgPaymentRef}/cancel` — store aktif (Pending/Opened) linki
   Cancelled terminale taşır `(PaymentLink.Cancel)`; terminal link'e iptal `TERMINAL_VIOLATION`
   `(CancelPaymentLink.CancelPaymentLinkCommandHandler)`.
9. **Link ölümü — süre dolumu (lazy).** Süresi geçen link, bir sonraki erişimde (açma/ödeme) Expired kabul
   edilir `(PaymentLink.Expire)`; proaktif watchdog YOK.
10. **İptal / iade / durum (çekim-sonrası).** Store onaylı çekimi geri alır: gün-sonu öncesi `/void`
    `(VoidPaymentLink)` → bankaya `VoidAsync`; sonrası `/refund` (kısmi) `(RefundPaymentLink)` → `RefundAsync`;
    sonuç denetim kaydı `(PaymentLink.RecordReversal)` + store webhook. `/status` `(QueryPaymentStatus)` →
    bankaya `QueryStatusAsync` (otorite). Referans = çekimdeki `BankOrderId` (orgOrderId; attempt'te saklanır).
11. **3D Secure ödeme (NonSecure'a alternatif).** Müşteri "3D ile öde" der `GET /payment-links/{openToken}/3d`:
    link açılır, PG banka 3D gateway'ine gidecek İMZALI formu üretir (PAN YOK — kart alanlarını tarayıcı ekler,
    doğrudan bankaya POST eder; PG sunucusu kartı GÖRMEZ, İlke VI) `(Initiate3DPayment.Handler)`
    `(InterVposBankAdapter.Build3DForm)`. Banka seçimi PAN'sız (maliyet sırası, ilk 3D-yetenekli banka)
    `(BankChargeRouter.SelectThreeDAdapter)`. Banka ACS doğrulamasından sonra sonucu `POST .../3d-callback`'e
    auto-POST eder; PG callback HASH'ini doğrular (sahte/değişmiş → fail-closed), 3DStatus=1 ise 3DModel
    tamamlama (`Complete3DAsync`, SecureType=NonSecure+MD) ile asıl çekimi tetikler, link'i terminalize eder +
    store webhook + müşteriyi ReturnUrl'e yönlendirir `(Complete3DPayment.Handler)`.
12. **Gün-sonu mutabakat (pull, PG'nin kendi dayanıklı süreci).** Günlük cron `POST /internal/reconcile` ile
    tetiklenir; dayanıklı mesaj devreye girer `(EndOfDayReconciliation.RunHandler)`. Her banka için operasyon
    yüzünden (ödeme yüzünden AYRI: `/api/*` JSON + SubscriptionKey) KAPALI batch özeti çekilir
    `(BankOperationsClient)` ve PG'nin Paid link görünümüyle (banka başına adet + brüt) karşılaştırılır
    `(EndOfDayReconciliation.Compare)`; uyuşmazlık log uyarısı (gözden-geçir). Banka haber vermez, PG sorar.

## Domain kuralları (süreci yöneten değişmezler)

- **Terminal tek-yön (FR-008).** `Paid`/`Expired`/`Cancelled`'e ulaşan link geri dönmez; aynı yönde tekrar
  idempotent no-op, ters yönde `TERMINAL_VIOLATION`.
- **Tek-kullanım + TTL + çift-çekim guard (FR-002/FR-005).** "Tüketim" yalnız başarılı ödeme (`Paid`); `Paid`
  link ikinci çekim YAPAMAZ (`RecordAttempt` → `TERMINAL_VIOLATION`). Henüz ödenmemiş (Pending/Opened) link
  süre/iptal sınırına dek tekrar açılıp ödenebilir. `ExpiresAt` aşımı açma/ödeme kapatır.
- **Failover taksonomisi (FR-005).** Red normalize 4 sınıfa düşer {IssuerDecline, TechnicalFailure,
  ConfigError, FraudDecline}; yalnız Technical/Config sıradaki bankaya geçer, Issuer/Fraud durur (aynı issuer
  boşuna; fraud asla retry). Tümü technical → `PROVIDER_UNAVAILABLE` (link Opened, geçici). **Çift-çekim
  penceresi:** failover canlı + durum-sorgu YOK → 1. banka çekmiş olabilir; simülatörde bakiye yok (zararsız),
  gerçek banka öncesi durum-sorgu ZORUNLU (sonraki dilim).
- **Akıllı routing (FR-016).** Aday = yetenek eleme (kart tipi/işlem) → on-us (kart issuer'ı == acquirer
  bankası) önce → maliyet sırası. Acquirer öngörü verisi config'te (banka otorite, PG kopyası); bilinmeyen
  BIN on-us'u atlar (fail-open).
- **In-doubt / çift-çekim emniyeti (Ders 16).** Çekim çağrısı cevapsız kalırsa (ağ/timeout) banka adapter'ı
  kör "başarısız" demeden AYNI OrderId'yi durum-sorgular `(InterVposResponseMapper)` — banka Authorized/
  Settled gösteriyorsa onay KURTARILIR, değilse NOTCHARGED (güvenli) ya da INDOUBT. INDOUBT'ta dayanıklı
  **watchdog süreci** AYNI OrderId'yi sonra tekrar sorgular (sınırlı yeniden deneme); banka çekmişse link
  `Paid`'e kurtarılır (+webhook), çözülemezse gün-sonu mutabakatına kalır `(InDoubtReconciliation)`. Otorite
  = bankanın kaydı, HTTP cevabı değil.
- **Aktif-tekil (FR-003).** (merchant, orderRef) yalnız AKTİF durumlarda ({Pending, Opened}) tekildir
  (partial unique); terminal sonrası aynı referansla YENİ link serbest.
- **Açılış token'ı = kimliksiz açılış ucunun beyan-edilen yetkisi (FR-007).** Bilinmeyen/eksik `OpenToken`
  hiçbir linki açamaz; uç `AllowAnonymous` olsa da gizli-token kapısı fiili auth'tur (fail-closed, İlke V).
- **Süre dolumu lazy.** Expired geçişi erişim anında hesaplanır (watchdog yok). Opened oturuma TTL dolumunda
  kısa grace (`GraceWindow`) tanınır.
- **Kart verisi kalıcı ortama girmez (anayasa İlke VI).** Aggregate/deneme kaydı PAN/CVV taşımaz; kart yalnız
  çekim anında (komut → adapter → banka wire) transient geçer, dayanıklı outbox'a/loga/DB'ye yazılmaz (komut
  `InvokeAsync`-only). Deneme kaydı yalnız banka kodu/ref/onay kodu/ham kod tutar.
- **Sonuç bildirimi sır döndürmez (İlke VI).** Store webhook'u ham gövde üzerinden HMAC-imzalanır; `CallbackSecret`
  yalnız secret-store'da, log/trace/gövde/dönüş DTO'sunda görünmez.
- **Yalnız TRY (FR-004, alan kısıtı).** Başka para birimiyle üretim reddedilir.

## Sınır (bu BC'nin dokunmadığı)

Store'un kendi sipariş/ödeme durumu kendi BC'sinde yönetilir — Payment.Api linki, çekimi ve imzalı sonucu
bildirir. **İşlem-bazlı mutabakat eşleme (BatchDetail OrderId düzeyinde; şu an özet/toplam düzeyinde),
canlı BIN besleme, klasik SMS/email link teslimi, health/circuit tabanlı proaktif eleme, TuruncuBank 3DS ve
nihai yetki modeli (platform IdP) bu dilimin DIŞINDADIR** (sonraki dilim[ler]). Banka wire tipleri adapter
içinde nested kalır, bu BC'nin domain'ine sızmaz (İlke VII anti-corruption).
