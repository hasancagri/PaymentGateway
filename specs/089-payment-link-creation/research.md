# Research: PaymentLink Üretimi (Faz 0)

Tüm NEEDS CLARIFICATION `/speckit-clarify` (Session 2026-10-20) ile kapandı. Bu belge kalan teknik kararları (desen/altyapı seçimleri) kayıt altına alır.

## D1 — Aktif-tekil enforcement: Postgres partial unique index

- **Karar**: `(MerchantId, OrderRef)` üzerindeki mutlak unique index (seed `Program.cs:22-24`), aktif durumlara (**Pending=0, Opened=1**) filtreli **partial unique** index'e evrilir. Marten `IndexDefinition.Predicate` ile Postgres `WHERE`'li unique index üretilir (ör. `data->>'Status' in ('0','1')` / computed status kolonu).
- **Gerekçe**: Aktif-tekil kararı (clarify) terminal (Expired/Cancelled) satırların tekillik slot'unu bırakmasını gerektirir; mutlak unique bunu engeller. Partial unique, DB düzeyinde çift-create yarışını da kapatır (ikinci commit çakışır → handler var olanı döner).
- **Alternatifler**: (a) Uygulama-düzeyi önce-sorgula-sonra-yaz — yarışa açık, RED. (b) Mutlak unique + orderRef'e sürüm ekleme — orderRef'i kirletir, idempotent-return'ü bozar, RED.
- **Tuzak (lazy ile etkileşim)**: Süresi zaman olarak geçmiş ama henüz lazy-flip edilmemiş bir satır hâlâ "aktif" (Pending/Opened) görünür ve partial slot'u tutar. Create handler'ı var olan aktif satırı yüklerken **önce lazy expiry uygular**: past-expiry ise `Expire(now)` ile Expired'a çevirip saklar (slot boşalır), sonra yeni link üretir; değilse idempotent olarak var olanı döner. Bkz. data-model §Create akışı.

## D2 — Lazy expiry (erişim anında), watchdog yok

- **Karar**: TTL dolumu erişim anında hesaplanır — `Open`/`Pay`/`Create` yollarında `now > ExpiresAt` ise aggregate Expired'a geçer. Proaktif `Process/` watchdog bu dilimde YAZILMAZ.
- **Gerekçe**: Clarify kararı + seed'de TTL alanı/`Process/` zaten yok (YAGNI). Erişilmeyen süresi-dolmuş link, bir sonraki erişimde doğru terminal duruma gelir; görünür yan etki yok (açılamaz/ödenemez).
- **Alternatif**: Wolverine `ScheduleAsync` tick watchdog — ek dayanıklı süreç + test; sonraki dilime ertelendi.

## D3 — TTL 30 dk + Opened oturuma ~5 dk grace

- **Karar**: `ExpiresAt = CreatedTime + Ttl` (varsayılan 30 dk). **Açılış** (Pending→Opened) `now <= ExpiresAt` ister. **Ödeme** (Opened iken "Öde") `now <= ExpiresAt + GraceWindow` (varsayılan 5 dk) ister. Süreler `PaymentLinkOptions` (non-secret, appsettings).
- **Gerekçe**: Clarify kararı. Grace yalnız **açılmış** oturumun ödemeye başlama penceresidir (sayfadayken süre dolan müşteri cezalanmaz). Bu dilimde "Öde" stub olsa da invariant + sınır testi yazılır (Phase-1 çekim gelince canlanır).
- **Alternatif**: Hard expiry (grace yok) — clarify'da reddedildi.

## D4 — Açılış token'ı: gizli, kimliksiz, OpenToken

- **Karar**: Her link için handler tahmin-edilemez bir **OpenToken** üretir (ör. 256-bit URL-safe). Link URL'i `LinkBaseUrl/{OpenToken}`. Açılış/ödeme uçları `AllowAnonymous` + OpenToken lookup ile kapılı (eşleşmezse 404, fail-closed). OpenToken Marten'da unique index'li. Seed'in `CallbackToken`'ı (sağlayıcı→PG callback, C1) bu dilimde KULLANILMAZ — sağlayıcı callback'i sonraki dilim; bu dilimde yüzey yalnız müşteri açılışı.
- **Gerekçe**: İlke V anonim-müşteri yüzeyi = per-session secret token, fail-closed. Kimlik yok (müşteri login'i yok). Opak `PgPaymentRef` (aggregate Id) store'a döner; OpenToken müşteriye giden link içinde.
- **Alternatif**: Aggregate Id'yi doğrudan URL'de kullanmak — tahmin edilebilir/enumerable, İlke V ihlali, RED.
- **Sır disiplini**: OpenToken yanıt DTO'sunda yalnız link URL'inin parçası olarak müşteriye gider; log/trace'te görünmez (İlke VI).

## D5 — HostedPaymentSession → PaymentLink evrimi (yeni aggregate değil, rename+genişletme)

- **Karar**: Seed aggregate `PaymentLink`'e **rename + genişletilir** (yeni paralel aggregate oluşturulmaz): alanlar eklenir (Currency, ReturnUrl, StoreName, OrderSummary, ExpiresAt, OpenToken), durum enum'u `Pending/Opened/Paid/Expired/Cancelled`'a genişler, sağlayıcı-sonrası alanlar (CheckoutFormToken/HostedUrl/ProviderPaymentId/FailureReason) bu dilimde DÜŞÜRÜLÜR (sonraki dilim geri getirir). Testler `PaymentLinkTests`'e evrilir.
- **Gerekçe**: 088 `HostedPaymentSession` tohumunu bu dilimde `PaymentLink`'e evirmek spec bağlamında yazılı; paralel iki aggregate bırakmak ölü kod + şema çakışması üretir. Dev aşaması: geriye-uyum/migration üretilmez (feedback: dev'de defansif migration yok) — şema yeniden kurulur.
- **Alternatif**: Seed'i korup yeni aggregate — iki aktif session tipi, index çakışması, RED.

## D6 — Güven bağlamı verisi store-sağlanır (Merchant BC yok)

- **Karar**: StoreName + OrderSummary create isteğinde store tarafından taşınır, `PaymentLink`'te saklanır, açılış sayfasında render edilir. Tutar + son geçerlilik zaten aggregate'te.
- **Gerekçe**: FR-006 "mağaza adı + sipariş özeti" ister; Merchant BC söküldü (İlke I — başka BC'ye erişim yok). Store zaten bu veriye sahiptir, S2S istekte verir (non-secret).
- **Alternatif**: Ayrı mağaza-profil lookup — ölü BC, RED.

## D7 — Pay stub: aggregate guard + handler PROVIDER_UNAVAILABLE

- **Karar**: `PayPaymentLink` handler: link'i OpenToken ile yükle → lazy expiry uygula → aktif + grace içindeyse `PROVIDER_UNAVAILABLE` döndür (durum DEĞİŞMEZ, Pending/Opened kalır); terminal/expired/grace-dışı ise ilgili red. Aggregate'te çekim metodu YOK (sağlayıcı yok); guard `IsPayable(now)` benzeri saf kontrol handler'da değil aggregate'te değerlendirilir (invariant aggregate'te).
- **Gerekçe**: FR-010 — "Öde" sağlayıcıya gitmez, link terminale taşınmaz. Invariant (terminal/expired reddi) aggregate'te; stub cevabı handler'da.
