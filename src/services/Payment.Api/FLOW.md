# Payment.Api — Domain Süreci

**BC ne yapar:** Store (merchant sitesi) için **pay-by-link** akışını yürütür — bir sipariş için tek-kullanımlık
TTL'li ödeme linki üretir, müşteri linki açınca kart bilgisine hiç dokunmadan **güven bağlamını** (mağaza +
sipariş + tutar + son geçerlilik) gösterir, linkin yaşam döngüsünü (açılış, süre dolumu, iptal) yönetir. Ayrıca
store'un **aktivasyon (onboarding) döngüsünü** yürütür: store bankalarda kendi merchant hesabını açar, ilk banka
onayında canlı satışa geçer (091 — ikinci domain süreci, aşağıda ayrı bölüm).

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
5. **Müşteri kartın ilk hanelerini girer → taksit seçenekleri (093).** Kart no yazılınca (ilk 6) `GET
   /payment-links/{openToken}/installments?bin=` çağrılır; kart ailesi (issuer/şema/tip) çözülür `(BinCard)`
   ve yapılandırılmış her taksit için en-avantajlı (en düşük efektif komisyonlu) banka sunucuda seçilir;
   müşteriye **yalnız taksit + taksitli toplam + aylık tutar** gösterilir — acquirer banka GÖSTERİLMEZ (gerçek-
   hayat UX, routing gizli) `(QuoteInstallments.QuoteInstallmentsHandler)`. Seçilen banka taksite göre değişir;
   değeri fiyata yansır (her taksitin toplamı o taksitin en-ucuz bankasına göre).
6. **Müşteri taksit seçip "Öde"ye basar.** `POST /payment-links/{openToken}/pay` (kart + `installmentCount`) —
   kart gövdede taşınır (yalnız çekime, SAKLANMAZ — İlke VI). Ödenebilirlik guard'ı `(PaymentLink.IsPayable)`;
   seçilen taksitin acquirer'ı + taksitli toplam SUNUCUDA yeniden hesaplanır (client'a güvenilmez), plan işlenir
   `(PaymentLink.ApplyInstallment)` `(PayPaymentLink.PayPaymentLinkCommandHandler)`.
7. **Banka seçimi + çekim (per-taksit, FAILOVER YOK).** Router seçilen taksit için kart-ailesi öngörü oranından
   efektif maliyeti hesaplar, en-avantajlı (gösterilen) bankayı seçer `(BankChargeRouter.SelectCandidates)` —
   on-us yalnız eşit maliyette tiebreaker. **Yalnız o banka denenir** (gösterilen = çekilen); reddederse işlem
   başarısız, sıradaki bankaya GEÇİLMEZ `(BankChargeRouter.RouteAsync)`. Adapter kartı InterVpos teline (`;;`,
   `InstallmentCount` dahil) POST eder, cevabı normalize eder `(InterVposBankAdapter)` `(InterVposResponseMapper)`.
   Deneme link altına kaydedilir `(PaymentLink.RecordAttempt)`.
8. **Terminal sonuç + store bildirimi.** Onayda link `Paid` terminale geçer; `PaymentCompletedEvent` yayınlanır
   ve store'a HMAC-imzalı dayanıklı webhook gider (tutar = taksitli toplam FinalAmount) `(StoreResultDelivery.Deliver)`.
   Issuer/Fraud reddinde link `Opened` kalır (başka kartla/taksitle tekrar denenebilir), `CHARGE_DECLINED` +
   `PaymentFailedEvent` + webhook. Gösterilen banka teknik/config hata verirse `PROVIDER_UNAVAILABLE` (link Opened,
   olay/webhook YOK — geçici; failover yok, müşteri tekrar dener). Durum + olay + webhook aynı `[Transactional]` outbox'ta atomik.
9. **Link ölümü — iptal.** `POST /payment-links/{pgPaymentRef}/cancel` — store aktif (Pending/Opened) linki
   Cancelled terminale taşır `(PaymentLink.Cancel)`; terminal link'e iptal `TERMINAL_VIOLATION`
   `(CancelPaymentLink.CancelPaymentLinkCommandHandler)`.
10. **Link ölümü — süre dolumu (lazy).** Süresi geçen link, bir sonraki erişimde (açma/ödeme) Expired kabul
   edilir `(PaymentLink.Expire)`; proaktif watchdog YOK.
11. **İptal / iade / durum (çekim-sonrası).** Store onaylı çekimi geri alır: gün-sonu öncesi `/void`
    `(VoidPaymentLink)` → bankaya `VoidAsync`; sonrası `/refund` (kısmi) `(RefundPaymentLink)` → `RefundAsync`;
    sonuç denetim kaydı `(PaymentLink.RecordReversal)` + store webhook. `/status` `(QueryPaymentStatus)` →
    bankaya `QueryStatusAsync` (otorite). Referans = çekimdeki `BankOrderId` (orgOrderId; attempt'te saklanır).
12. **3D Secure ödeme (NonSecure'a alternatif).** Müşteri "3D ile öde" der `GET /payment-links/{openToken}/3d`:
    link açılır, PG banka 3D gateway'ine gidecek İMZALI formu üretir (PAN YOK — kart alanlarını tarayıcı ekler,
    doğrudan bankaya POST eder; PG sunucusu kartı GÖRMEZ, İlke VI) `(Initiate3DPayment.Handler)`
    `(InterVposBankAdapter.Build3DForm)`. Banka seçimi PAN'sız (maliyet sırası, ilk 3D-yetenekli banka)
    `(BankChargeRouter.SelectThreeDAdapter)`. Banka ACS doğrulamasından sonra sonucu `POST .../3d-callback`'e
    auto-POST eder; PG callback HASH'ini doğrular (sahte/değişmiş → fail-closed), 3DStatus=1 ise 3DModel
    tamamlama (`Complete3DAsync`, SecureType=NonSecure+MD) ile asıl çekimi tetikler, link'i terminalize eder +
    store webhook + müşteriyi ReturnUrl'e yönlendirir `(Complete3DPayment.Handler)`.
13. **Gün-sonu mutabakat (pull, PG'nin kendi dayanıklı süreci).** Günlük cron `POST /internal/reconcile` ile
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
- **Per-taksit maliyet routing + FAILOVER YOK (093).** Aday = yetenek eleme (kart tipi/işlem) → her aday için
  **efektif maliyet = kart-ailesi öngörü oranı(şema,tip,taksit) × tutar** → en düşük maliyet önce; on-us yalnız
  EŞİT maliyette tiebreaker. En avantajlı banka taksit sayısına göre DEĞİŞİR (093). **Yalnız o taksitin gösterilen
  (en-avantajlı) bankası denenir** — reddederse (Issuer/Fraud/Technical/Config farkı yalnız red mesajı) işlem
  başarısız, SIRADAKİ BANKAYA GEÇİLMEZ (gösterilen = çekilen; müşteri tekrar dener). Oran matrisi
  `BankCommissionRate` read-model'inde (banka otorite, PG öngörü); bilinmeyen BIN → aile null, on-us yok,
  DefaultCommissionRate (fail-open). *(090'ın çok-banka failover'ı 093'te bu bağlı-acquirer modeliyle değişti.)*
- **Taksitli fiyatlama (093).** Müşteri BIN girince taksit seçenekleri gelir; her biri o taksitin en-avantajlı
  bankası + taksitli toplam (= Amount × (1+oran), vade farkı) + aylık tutar. Seçilen taksitin banka+tutarı Pay'de
  SUNUCUDA yeniden hesaplanır (client'a güvenilmez); çekim + store bildirim FinalAmount üzerinden. Acquirer
  banka müşteriye GÖSTERİLMEZ (gerçek-hayat UX; routing gizli iç karar) — ekranda yalnız taksit + tutar.
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

## Store Onboarding / Aktivasyon Süreci (091)

> Ayrı domain süreci (pay-by-link'ten bağımsız): super-admin bir store'u internet satışına açar. Banka = adapter
> arkası dış sistem; banka çalışan-onayı ayrı repo (BankSimulator + AgentPlatform MCP), bu BC'nin DIŞI.

1. **EC store aktivasyonunu başlatır.** `POST /store-onboarding` (store-api-key) — MerchantId + correlation +
   mağaza adı taşıyan komut `(CreateStoreOnboarding.CreateStoreOnboardingCommandHandler)`. Aktif-tekil: aynı
   store için aktif süreç varsa idempotent döner (`DUPLICATE_ONBOARDING` yarış-kapısı partial-unique).
2. **Süreç doğar (Draft) + gizli token'lar.** Tahmin-edilemez `OpenToken` (form açılışı) + `CallbackToken`
   (banka→PG webhook kapısı) + TTL üretilir `(StoreOnboarding.Create)`. Yanıt: opak `PgStoreRef` + form linki
   (credential/sır İÇERMEZ, İlke VI).
3. **Store hosted formu açar.** `GET /store-onboarding/{openToken}` — bilinmeyen/expired/Draft-değil → fail-closed
   404 `(OpenOnboardingForm.OpenOnboardingFormCommandHandler)`; geçerliyse TEK sabit form render edilir (banka
   başına ayrı ekran YOK).
4. **Store çekirdek profilini gönderir + bankalara fan-out.** `POST /store-onboarding/{openToken}/submit` —
   ünvan/vergi no/IBAN/MCC/URL/yetkili doğrulanır `(StoreProfile.Create)`; geçersizse `INVALID_PROFILE`, bankalara
   İLETİLMEZ. Geçerliyse her yapılandırılmış bankaya PARALEL ayrı referral `(IBankOnboardingAdapter)`
   `(SimulatorBankOnboardingAdapter)`; banka-özel eksik alan → o banka submit-öncesi reddedilir (B kararı),
   diğerleri Referred. Per-banka başvuru kaydı `(StoreOnboarding.SubmitProfile)`; store `Submitted`.
5. **Banka async inceler → sonucu PG'ye webhook'lar.** Banka çalışanı (ayrı repo) onaylar/reddeder; sonuç
   `POST /internal/onboarding/callback/{callbackToken}`'a imzalı gelir — CallbackToken kapısı + X-Signature HMAC
   doğrulanır (eşleşmezse `CALLBACK_REJECTED`, fail-closed) `(ReceiveBankOnboardingOutcome.Handler)`. Banka ham
   gövdesi normalize edilir `(SimulatorBankOnboardingAdapter.NormalizeCallback)`.
6. **Onay → credential güvenli saklanır + ilk onayda store Live.** Approved'da banka credential'ı at-rest şifreli
   saklanır (opak `CredentialRef`; sır aggregate'e/dönüşe girmez, İlke VI) `(EncryptedBankCredentialStore)`; başvuru
   Approved `(StoreOnboarding.RecordBankOutcome)`. İLK onayda store `Live` (çift-aktivasyon no-op). İlk aktivasyonda
   store'a HMAC-imzalı "Live" webhook (credential'sız) `(OnboardingResultDelivery.Deliver)`.
7. **Red + yeniden başvuru.** Red callback o bankayı `Rejected`+sebep yapar (diğerleri + Live etkilenmez);
   super-admin `POST /store-onboarding/{pgStoreRef}/banks/{bankCode}/resubmit` ile tekrar başvurur
   `(ResubmitBankApplication)` `(StoreOnboarding.Resubmit)`.
8. **Kaçan sonuç mutabakatı (watchdog).** Submit her Referred banka için zamanlanmış `Check` yayınlar; dayanıklı
   süreç bankanın güncel başvuru durumunu sorar — Approved/Rejected uygula, hâlâ "inceleniyor" → erken terminal YOK,
   sınırlı yeniden dene `(OnboardingReconciliation)` `(ReconcileDecision)`.
9. **Durum sorgu.** `GET /store-onboarding/{pgStoreRef}/status` (store-api-key) — genel durum + per-banka durum/red
   sebebi; credential/sır/iç token YOK `(QueryOnboardingStatus)`.

### Onboarding domain kuralları

- **İlk onayda Live (FR-009).** İlk banka `Approved` → store `Live` (tek-yön); sonraki onaylar Live tutar, çift
   aktivasyon tetiklemez; aynı banka tekrar onayı idempotent no-op.
- **Per-banka izolasyon (FR-011).** Bir bankanın red/onayı ötekini + store Live'lığını (≥1 Approved varsa)
   ETKİLEMEZ. Red başvuruları silinmez (denetim).
- **Live-gate (FR-010).** Store `Live` değilse ödeme linki üretilmez (`STORE_NOT_LIVE`) `(CreatePaymentLink)` —
   aktivasyon gerçek charge'ın önkoşuludur (aynı BC içi cross-aggregate okuma).
- **Runtime credential at-rest şifreli (İlke VI, anayasa v3.1.0).** Banka credential'ı yalnız imzalı webhook
   gövdesinde akar; at-rest AES-GCM şifreli saklanır (DEK secret-store), aggregate yalnız opak `CredentialRef`
   tutar; plaintext hiçbir sorgu/dönüş/log/trace'te materyalize olmaz.
- **Banka otorite.** Onay kararı + credential bankaya aittir; PG yalnız sonucu yansıtır. Belirsiz/sürüyor sonuç
   terminal sayılmaz (reconcile bekler).

## Sınır (bu BC'nin dokunmadığı)

Store'un kendi sipariş/ödeme durumu kendi BC'sinde yönetilir — Payment.Api linki, çekimi ve imzalı sonucu
bildirir. **İşlem-bazlı mutabakat eşleme (BatchDetail OrderId düzeyinde; şu an özet/toplam düzeyinde),
canlı BIN besleme, klasik SMS/email link teslimi, health/circuit tabanlı proaktif eleme, TuruncuBank 3DS ve
nihai yetki modeli (platform IdP) bu dilimin DIŞINDADIR** (sonraki dilim[ler]). Banka wire tipleri adapter
içinde nested kalır, bu BC'nin domain'ine sızmaz (İlke VII anti-corruption).
