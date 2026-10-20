# EventStorming — Payment BC: Pay-by-Link (Perde-1)

**BC ne yapar:** Bir e-ticaret store'u için **pay-by-link** ödeme akışını yürütür — kart bilgisi sohbete/
agent'a girmeden, PG'nin ürettiği tek-kullanımlık TTL'li link üzerinden PG hosted sayfada toplanır, banka
adapter'ı ile otorize edilir, sonuç store'a imzalı webhook'la bildirilir.

> **Durum:** Phase-1 storm taslağı (mizansen Perde-1). Kod YOK → sticky'ler backtick-SİZ + `_(planned)_`.
> Gerçek-kaynak bu dosya; Miro = bundan üretilen salt-okunur ayna (ECommerceAgent mantığı).
> Mizansen rolleri: 👤 Müşteri (kart sahibi) kullanıcı; store/PG/banka kolaylaştırıcı tarafından seslendirildi.

**Notasyon:** 🟦 komut · 🟧 event (🟧⭐ pivotal) · 🟪 policy · 🟨 aggregate · 🟩 read-model · 🩷 dış sistem ·
🟥 hotspot (open|closed) · 👤 aktör · BC-aşan dikiş `⇨` (tüketiciler) / `⇦` (yayıncı).

## Süreç

1. 👤 **Müşteri** — Claude Desktop'ta store'un agent'ıyla konuşur; güvendiği bağlam = bu sohbet.
2. 🟦 **StartPayment** (agent tool) — ödeme anı gelince agent tetikler ⇨ Store ⇨ PG _(planned)_
3. 🟦 **CreatePaymentLink** → 🟧 **PaymentLinkCreated** — store/agent PG'den link ister (tutar + orderRef +
   dönüş adresi); PG tek-kullanımlık TTL'li link üretir _(planned)_
4. 🟨 **PaymentLink** (aggregate) — linkin ömrü: Pending → Opened → (Attempt'lar) → terminal; tek-kullanım +
   TTL değişmezleri; **çok-denemeli oturum** (altında PaymentAttempt'lar) _(planned)_
5. 🟪 **Policy** — agent linki SOHBETTE sunar ("işte güvenli ödeme bağlantın, 1.250 TL / ORD-7731"); müşteri
   soğuk kanaldan değil başlattığı konuşmadan görür _(planned)_
6. 👤 **Müşteri linke dokunur** → 🟧 **PaymentLinkOpened** — session Pending → Opened (açıldı ≠ ödendi) _(planned)_
7. 🟪 **Policy** — link müşteriyi sohbetten ÇIKARIR; tarayıcıda PG **hosted ödeme sayfası** açılır; kart YALNIZ
   burada girilir; agent/LLM/sohbet kartı ASLA görmez (İLKE VI) _(planned)_
8. 🟩 **Hosted sayfa güven bağlamı** (read-model) — kart formundan ÖNCE: mağaza adı + sipariş özeti + tutar +
   son geçerlilik; müşteri kendi siparişini tanır _(planned)_
9. 🟦 **ConfirmPayment** ("Öde") → 🩷 **Banka 3DS init** (adapter wire) — PG kartı bankaya gönderir (PAN
   tarayıcıdan bankaya, PG saklamaz; SAQ A-EP, İLKE VI) _(planned)_
10. 🩷 **Banka risk kararı** — banka riske göre ya ayrı ACS ekranı/OTP ister (challenge) ya hiç sormaz (frictionless); sonuç ikisinde de PG callback'ine döner _(planned)_
11. 🟪 **Policy** — challenge'da tarayıcı bankanın ACS sayfasına yönlenir; OTP orada; PG o ekranı görmez, kısa
    süre sahneden çıkar, callback bekler _(planned)_
12. 🟧 **3DSCompleted** — banka PG callback'ine POST eder; PG dönüş hash'ini doğrular (sahte gövde düşer).
    **Kimlik doğrulandı ≠ para onaylandı** _(planned)_
13. 🟦 **Authorize** → 🩷 **Banka** → 🟧 **PaymentAuthorized** | 🟧 **PaymentDeclined** — çekim AYRI adım;
    authentication(3DS) ile authorization burada ayrışır _(planned)_
14. 🟧⭐ **PaymentSucceeded** — ilk başarılı attempt link'i terminalize eder (tek-yön) _(planned)_
15. 🟪 **Policy** — terminal geçişte PG İKİ kanal tetikler: (a) müşteriye tarayıcı success sayfası (**kanıt
    DEĞİL, yalnız UX**), (b) store'a **HMAC-imzalı webhook** (durable outbox retry — otorite) _(planned)_
16. 🟧 **OrderPaid** ⇨ Store — store sipariş "ödendi"ye döner; agent/müşteriyi haberdar eder _(planned)_
17. 🟪 **Policy** — kesin onay SOHBETE döner: müşteri Claude'a dönüp "ödemem geçti mi?" dediğinde agent
    PG'nin **durum-sorgu ucuna** bakar (tarayıcıya değil), webhook-teyitli "onaylandı" der → **gerçek kapanış** _(planned)_

## Red / belirsizlik (Ders 16 taksonomisi)

- 🟪 **Normalize red taksonomisi** — adapter ResponseMapper banka kodunu çevirir (İLKE VII anti-corruption):
  - **IssuerDecline** (yetersiz bakiye / çalıntı / CVV / süre) → başka BANKAYA failover ANLAMSIZ (aynı issuer);
    müşteri başka KART (farklı issuer) deneyebilir = yeni attempt _(planned)_
  - **TechnicalFailure** (timeout / 5xx / bağlantı) → durum-sorgulu failover (önce çift-çekim emniyeti) _(planned)_
  - **ConfigError** (hash/merchant tanımsız) → failover değil, ALARM (operasyon) _(planned)_
  - **FraudDecline** → ASLA retry (card-testing deseni görünür) _(planned)_
- 🟪 **Policy — timeout ≠ başarısız** — timeout'ta ZORUNLU durum-sorgu ("bu OrderId geçti mi?"): geçtiyse bitti
  (cevap kaçmış, failover YOK), geçmediyse alternatif. Kör retry = çift çekim _(planned)_
- 🟨 **retry (başka kart, müşteri) ≠ failover (başka banka, routing)** — iki ayrı kavram _(planned)_

## Domain kuralları + yaşam döngüsü

- 🟨 **PaymentLink terminal durumları:** Paid (başarılı attempt, tüketildi) · Expired (TTL doldu; abandoned
  buraya düşer) · Cancelled (store iptal / müşteri vazgeçti) · Declined? (hotspot: hard/soft retry bitince).
- 🟨 **PaymentAttempt** — bir PaymentLink altında N deneme; her biri → banka → success/decline(+reason) izli;
  ilk başarılı attempt link'i terminalize; red'ler mutabakat/denetim için saklanır.
- 🟪 **Güven kaynağı = sohbet/webhook**, tarayıcı success ekranı değil (client güvenilmez, S2S imzalı otorite).
- 🟩 **Attempt geçmişi** (read-model) — denetim + mutabakat için.
- 🩷 **Banka = adapter arkası dış sistem** (İLKE VII): ortak port (NonSecure/3DSecure sale + ileride iptal/iade/
  durum/BIN); wire slice'ta nested; normalize domain cevabı; `MODEL_3D_SECURE` + `Regular` alınır, `3D_HOST` RED.

## Süreçler (Domains dışı — İLKE III)

- 🟩 **Process/ durum-sorgu (watchdog)** — timeout/callback-yok sonrası banka'ya inquiry; belirsizliği kapatır.
- 🟩 **Process/ mutabakat (reconcile)** — gün sonu banka raporu × PG kayıtları; kayıp callback + "banka OK / PG
  pending" tutarsızlığını yakalar (geç-terminalize ya da otomatik iade).
- 🟨 **Routing / circuit-breaker** — PosAccount sağlık state + yetenek eleme + fiyat (çok-banka; sonraki faz).

## İki kontrat kılavuzu (simetri: PG : store ≡ banka : PG)

- 🩷 **Downstream (Banka ← PG):** bankanın entegrasyon kılavuzuna PG UYAR (response kod, hash, inquiry,
  settlement). Referans: Obsidian DenizBank kontratı + pos-master `DataMapper/Response/Mapper/*`.
- 🏪 **Upstream (Store/agent ← PG):** PG KENDİ kılavuzunu YAYINLAR (chat-driven): CreatePaymentLink istek/yanıt,
  webhook imza+payload+durable retry, **durum-sorgu ucu** (store "geçti mi?" sorar — browser'a değil buna güvenir),
  idempotency anahtarı, sonuç taksonomisi (success/declined[4-sınıf]/pending-verify), iade/iptal kanalı.

## Açık Hotspot'lar

- 🟥 (open) ⭐ **Link güveni / phishing** — chat yolu için kapandı (içinde olunan sohbetten); klasik SMS/email
  teslim yolu için açık (gönderici marka, markalı HTTPS alan, landing özet).
- 🟥 (closed: chat) "link nereden geldi?" → agent sohbetinden. 🟥 (closed: mimari) "bilgilerimi kullanırlar mı?"
  → LLM hiç görmez, PG saklamaz.
- 🟥 (open) ⭐ **3DS belirsiz dönüş** — OTP gelmez/geç/kart 3DS kapalı; müşteri yarıda kaçar → belirsiz, terminal değil.
- 🟥 (open) ⭐ **auth ≠ authorization** — 3DS başarı ≠ ödeme başarı; model iki event'i ayırmalı.
- 🟥 (open) ⭐ **"banka OK / site FAIL"** — belirsizliği başarısız sayma; çözüm: bağımsız durum-sorgu + mutabakat + idempotency.
- 🟥 (open) **sohbete dönüş + agent farkındalığı (YARIŞ)** — müşteri webhook'tan önce sohbete dönerse agent "henüz
  göremiyorum" → polling/bekleme/push gerek.
- 🟥 (open) **TTL yarışı** — kullanıcı yavaş/dikkati dağınık, link dolarsa? (süre/yeni link). **abandon** — açıldı,
  tamamlanmadı → Expired.
- 🟥 (open) **çift "Öde" / çift start_payment / çift webhook** — idempotency (tek çekim).
- 🟥 (open) **PG store-kontratı transport** — HTTP mı gRPC mi? webhook imza şeması, iade kanalı (memory: storm sonrası).
- 🟥 (open) **ledger sınırı** — PG yalnız işlem event stream mi; bakiye/settlement/komisyon simülatörde mi (yalın gateway)?

## Sınır (bu BC'nin dokunmadığı)

Store'un sipariş/sepet durumu kendi BC'sinde; PG yalnız ödeme sonucunu bildirir. Banka otorizasyonu/ACS/3DS
challenge bankanın (PG yalnız başlatır + callback'i teyit eder). Bakiye/settlement/komisyon motoru banka
simülatöründe (ayrı repo). Kart saklama (vault) kapsam DIŞI (İLKE VI). PG MCP yüzeyi expose etmez (İLKE V).

## İlk dikey dilim adayları (storm sonrası karar — spec-kit girdisi)

- **PaymentLink üretimi** (CreatePaymentLink → PaymentLinkCreated + hosted sayfa iskeleti), ya da
- **NonSecure sale** (tek banka dejenere routing; 3DS'siz tam çekim) → sonra 3DSecure.
- Her iki dilimde de gerekli ortak kas: taksonomi + durum-sorgu + idempotency.
