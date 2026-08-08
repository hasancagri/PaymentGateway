# Phase 0 Research — Merchant Onboarding (013)

Karar kayıtları. Her başlık: Karar / Gerekçe / Reddedilen. Interaktif tasarım oturumunda
(2026-08-08) spec'in US4 komisyon kısmı **B seçeneğine** göre sadeleştirildi — bkz. D5.

> **spec.md reconciliation gerekli**: US4 + FR-011..014 spec'te tam pazarlık döngüsü yazılı;
> 013 kararı bunu "gateway-otoriter komisyon, pazarlık YOK" olarak küçültür. Pazarlık (e-posta
> + Excel + ML-intent) 014'e taşındı (Obsidian `Yapılacaklar.md` vizyon maddesi). Plan
> artefaktları son gerçeği yansıtır; spec.md plandan sonra hizalanacak.

---

## D1. Merchant statü akışı ve "aktivasyon öncesi token yok"

**Karar**: Statü seti `{Provisioning, Active, Passive, Suspended}` (mevcut üçe Provisioning
eklenir; PendingReview/Rejected merchant statüsü DEĞİL — RegisterRequest'te yaşar). Merchant
onayda oluşur, statüsü **Provisioning**. OpenIddict client'ı oluşturmada DEĞİL, **aktivasyon
(MerchantProvisioned event)** anında provision edilir → "aktivasyon öncesi token yok" =
client yokluğu + Provisioning birlikte (fail-closed).

**Gerekçe**: MerchantKey aktivasyon sayfasında teslim; merchant o ana kadar secret'ı bilmez →
token alamaz. Defense-in-depth: client'ı da o ana kadar provision etme. Spec'in "yalnız
Provisioning eklenir" kısıtı korunur.

**Reddedilen**: Onayda client'ı hemen kurmak (aktivasyondan önce teorik token yolu, FR-010
çelişkisi); yeni "Created/PendingActivation" statüsü (gereksiz çoğaltma).

**Sonuç akış**:
```
RegisterRequest(Pending) --admin onay--> Merchant(Provisioning) + ActivationTicket
   (OpenIddict client YOK → token YOK)
Merchant --aktivasyon bileti kullan--> MerchantProvisioned
   --> Identity: client kur (secret=MerchantKey, Provisioning scope demeti)
Merchant --settlement + komisyon-hazır(event) + ReturnUrl (3/3)--> Active (otomatik)
   --> MerchantStatusChanged(Active) --> Identity: tam scope demeti
```

---

## D2. Provisioning vs Active scope demeti (İlke V amendment)

**Karar**:
- **Provisioning demeti**: `merchant.read`, `merchant.write` (kendi kaydı oku/tamamla,
  settlement hesabı, ReturnUrl). Charge YOK.
- **Active demeti**: Provisioning + (gelecek) charge scope'u. Charge scope bugün yok (G5) →
  demetler pratikte bugün eşdeğer; **gate kurulur**, ekstra scope G5'te eklenir.
- Merchant token'ında `commission.*` GEREKMEZ — komisyon merchant'a **mail (Excel)** ile
  bilgi olarak gider; merchant Commission.Api'ye gitmez (D5, B kararı).

**Gerekçe**: Scope = yetki türü, merchant-başına çoğaltılmaz (012). Kademeli yetki statü ile
(Provisioning→Active), scope adı üretilmez.

**Amendment (İlke V, v1.3.0 → v1.4.0, MINOR)**: "verme statü-kapılı (yalnız Active)" →
"verme statü-kapılı ve **kademeli**: Provisioning sınırlı demet (charge hariç), Active tam
demet (charge dahil); charge hiçbir alt-statüde verilmez." Plandan sonra `/speckit-constitution`.

---

## D3. Domain-control challenge (HTTP-01 tarzı)

**Karar**: İki dosya. (1) `/.well-known/merchant-descriptor.json` (statik kimlik beyanı).
(2) Başvuruda gateway tek-kullanımlık `token` + beklenen değer üretir; aday
`/.well-known/merchant-challenge/{token}` yolunda yayınlar; gateway **senkron GET** ile
doğrular. Bilet TTL ~1 saat, tek kullanım. Doğrulama geçmeden RegisterRequest OLUŞMAZ.

**Doğrulama içeriği**: `merchant-challenge/{token}` dosyası gateway'in verdiği keyed değeri
döner (HTTP-01 gibi; ACME değil, basitleştirilmiş). Format `contracts/merchant-descriptor.md`.

**Gerekçe**: HTTP-01 sahipliği merchant'ta uç açmadan (2 pasif GET) kanıtlar; spec ile birebir.

**Reddedilen**: DNS TXT (dev'de ağır), e-posta doğrulama (sahiplik kanıtı değil), challenge'ı
descriptor'a gömmek (descriptor statik kalmalı).

---

## D4. Aktivasyon sayfası + tek-seferlik key teslimi

**Karar**: Aktivasyon sayfası **Identity.Server**'da Razor Pages (bugün token-only, sıfırdan
`Pages/Activation/`). Sayfa **key custody tutmaz**; bileti Merchant.Api'ye **senkron** iletir
(redeem). Merchant.Api: bilet doğrula (tek-kullanım, TTL) → statü Provisioning sabitle →
**MerchantProvisioned(merchantId, merchantKey, "Provisioning")** yayınla → key'i yanıtta bir
kez dön. Sayfa key'i bir kez render eder. Identity ayrıca MerchantProvisioned'ı tüketip
OpenIddict client'ı kurar (secret=merchantKey).

**Gerekçe**: Key + bilet + statü = Merchant BC domain'i (İlke I). Identity = UI host + kimlik
motoru. Senkron redeem "bir kez göster" garantisini verir (async event vermez). Tek
sanksiyonlu senkron çağrı (Complexity Tracking).

**Reddedilen**: Key'i Identity'de saklamak (domain sızar), aktivasyonu event-only yapmak
("bir kez göster" async'te kırılgan), key'i OpenIddict hash'inden okumak (geri döndürülemez).

**Not**: MerchantProvisioned MerchantKey taşır (mevcut MerchantCreated deseni; secret yalnız
Identity'ye). Onboarding'de MerchantCreated aktivasyona kadar YAYINLANMAZ; yerini
MerchantProvisioned alır.

---

## D5. Komisyon — gateway-otoriter, pazarlık YOK (B kararı), Excel mail

**Karar**: 013'te komisyon **gateway'in dediğidir**; merchant dışına çıkamaz, pazarlık/
kabul/ret/karşı-teklif YOK. Akış:
- Admin, onay sonrası merchant grid'ini mevcut ekrandan tanımlar (`BulkUpsert`). Grid önce
  **Draft** (kısmi olabilir); **sürümleme GEREKMEZ** (kabul-sürüm bağı 014'e).
- Admin grid'i **finalize** eder (bütünlük: `IsMissing` yok, `BelowBankCeiling` yok) → grid
  **Ready**. Ready anında Commission.Api `MerchantCommissionGridReady(merchantId)` yayınlar
  (**aynı `[Transactional]` içinde statü→Ready + event = outbox, dual-write yok — D13**). Bu
  event **deterministik** (Active koşulu #2; LLM'e bağlanmaz). Draft'ta event/Excel YOK
  (erken tetikleme önlenir).
- Grid statüsü: `Draft / Ready` (Approved/Rejected DEĞİL — kabul kavramı yok).
- Komisyon **Excel maili = harici LLM/MCP orkestrasyon** (D14): 013 MCP yüzeylerini sağlar;
  bir LLM/MCP client (araç seçimi 013 dışı) tool'ları sıralar (Merchant.Api
  `get_merchant` → Commission.Api `get_merchant_commission_grid` → Excel.Mcp
  `generate_spreadsheet` → Mail.Mcp `send_email`). Commission.Api **MCP read yüzeyi** açar;
  BC handler Excel/mail üretMEZ.
- **Komisyon kabulü KALDIRILDI** (B): Active koşulu #2 = "grid hazır" (event ile Merchant
  BC'ye taşınır), merchant aksiyonu yok.

**Gerekçe (kullanıcı)**: "Bizim dediğimiz olsun, merchant dışına çıkamasın." Gerçek pazarlık
(e-posta + insan cevabı + ML-intent) ayrı, büyük iş → 014. 013 sadeleşir: agent komisyon
tool'ları, grid sürümleme, kabul kaydı hepsi 013 DIŞI.

**Reddedilen**:
- A (take-it kabul): merchant yine bir aksiyon yapardı; kullanıcı sıfır-aksiyon B'yi seçti.
- Grid sürümleme 013'te: kabul-sürüm bağı olmadan gereksiz; 014 pazarlığı geldiğinde eklenir.

**014'e taşınan** (Obsidian vizyon): giden Excel-mail üstüne gelen IMAP okuma + **ML intent
sınıflandırma** (ayrı PyTorch/A2A servisi, fraud kalıbı; intent≠sentiment) + default-reject
+ sürüm korelasyonu + hücre-bazlı karşı-teklif.

---

## D6. Merchant.Agent + iç MCP tool yüzeyi (dışa kapalı)

**Karar**: Yeni **Merchant.Agent** = Payment.Agent birebir şablonu (A2A host + LLM router +
MCP client). 013'te tek işi **başvuru kanalı** (US1): merchant adayının A2A başvurusunu alır,
Merchant.Api MCP yüzeyine taşır. Merchant.Agent tool seti (013): `submit_registration` (domain +
descriptor çek + challenge doğrula + RegisterRequest), `registration_status` (opsiyonel).
Komisyon müzakere tool'ları (accept/reject) **013 DIŞI** (B kararı; 014).

**MCP read yüzeyleri (harici LLM için)**: Merchant.Api `/mcp` ayrıca `get_merchant` (read),
Commission.Api `/mcp` yeni yüzey `get_merchant_commission_grid` (read) açar. Bunları **bespoke
agent DEĞİL**, harici LLM/MCP client (araç seçimi 013 dışı) komisyon Excel orkestrasyonunda
tüketir (D14). Policy: Merchant `/mcp` `merchant.read/write`, Commission `/mcp` `commission.read`;
harici client admin-düzlemi token (merchant_id claim'siz).

**Merchant kimliği A2A'da**: LLM yalnız tool sırası; sır/karar üretmez. Başvuruda kimlik yok
(henüz merchant yok) — domain başvurunun anahtarı. Agent→Merchant.Api /mcp çağrısı agent'ın
kendi token'ıyla (yeni `merchant-agent` benzeri client, `merchant.write`).

**Gerekçe**: 007/012 desenleri hazır. Başvurunun agentic olması US1 gereği.

**Reddedilen**: Komisyon tool'larını 013'te tutmak (B ile anlamsız), LLM'e karar verdirmek
(anayasa: LLM yalnız sıra).

---

## D7. Mail — generic Mail.Mcp + Common IMailSender + plan A auth

**Karar**:
- **Mail.Mcp** (`src/others/Mail.Mcp`) = generic MCP server, tool `send_email(to, subject,
  body, isHtml, attachments?)`. Domain bilmez. `MapMcp("/mcp").RequireAuthorization("mail.send")`.
  SMTP host/port/kimlik config'ten; dev = Mailpit (`:1025`), gerçek = Gmail SMTP. KALICILIK YOK.
  **attachments** = Excel eki için (D12).
- **IMailSender** (`src/others/Common/Mail`) = paylaşılan MCP client soyutlaması; BC inject
  eder, `SendAsync(to, subject, body, attachments?)`, MCP detayını görmez. Token handler
  (AgentTokenHandler deseni) `mail.send` scope'lu token cache'ler.
- **IMailSender yalnız DETERMİNİSTİK mailler için** (D14): aktivasyon maili (tek-seferlik
  MerchantKey linki — güvenlik) + admin "yeni başvuru" bildirimi. Bunlar BC handler'ından
  otomatik gider; LLM'e/operatör komutuna bağlanamaz.
- **Komisyon Excel maili IMailSender KULLANMAZ** — harici LLM/MCP orkestrasyon Mail.Mcp'yi
  doğrudan çağırır (D5/D14).
- **Auth (plan A)**: yeni `mail.send` scope; mail atan BC başına Identity client (ör.
  `merchant-api`), `mail.send`'li. İzlenebilir.
- **FR-019**: deterministik maillerin gönderim kaydı domain'de (Onboarding Bildirimleri) —
  deneme/başarı/başarısızlık; mail çökse akış sessizce "başarılı" saymaz. (Agentik komisyon
  maili harici LLM client'ın gözünde; başarı/başarısızlık orada görünür.)

**Gerekçe**: "Her şey MCP" + generic yeniden kullanım + BC izolasyonu (içerik/şablon domain'de).
Kendi server = .NET/Aspire/CPM tutarlılığı, dış churn yok.

**Reddedilen**: Hazır npm/npx SMTP-MCP (Node runtime, churn), doğrudan SMTP (MCP tercihini
karşılamaz), Mail.Mcp'ye domain bilgisi (İlke I).

**Dev görünürlük**: Mailpit catch-all (`:1025` alır, `:8025` web UI). Gerçek adres olmadan tüm
mail tek inbox'ta; AppHost container resource.

---

## D8. RegisterRequest — merchant'tan ayrı yaşam döngüsü

**Karar**: `RegisterRequest` yeni aggregate (Merchant BC, ayrı slice). Alanlar: domain,
doğrulanmış descriptor kopyası (legalName, taxId, contactEmail, webhookUrl), challenge sonucu,
durum (Pending/Approved/Rejected), değerlendirme bilgisi (karar zamanı, opsiyonel not).
Merchant ANCAK onayla doğar (mevcut CreateMerchant; key o an üretilir). Mükerrer koruma
(FR-020): aynı domain için Pending talep VEYA kayıtlı merchant varsa yeni talep açılmaz.

**Gerekçe**: Spec çekirdek pivotu — başvuru merchant değil; onay öncesi merchant kimliği/veri/
yetki olamaz (SC-004).

**Reddedilen**: Merchant'a PendingReview statüsü (spec reddediyor).

---

## D9. ReturnUrl + externalRef

**Karar**:
- **ReturnUrl**: Merchant aggregate'ine yeni alan; geçerli **HTTPS** (aggregate metodu
  doğrular). Provisioning merchant kendi token'ıyla set/update (`merchant.write`). Active
  koşulu #3.
- **externalRef**: opak string, opsiyonel. Merchant'a dönük kayıt uçlarında kabul/sakla/aynen
  dön. Gateway anlamlandırmaz, son-kullanıcı kimliği tutmaz. Asıl kullanım charge (G5).

**Gerekçe**: FR-015/FR-018. HTTPS zorunlu (ödeme dönüşü güvenli).

**Reddedilen**: ReturnUrl'e HTTP izni, externalRef'i yapılandırılmış tip.

---

## D10. 3-koşul→Active otomatik geçişi (cross-BC, event-driven)

**Karar**: Merchant aggregate üç koşulu izler: (1) ≥1 settlement hesabı, (2) **komisyon grid
hazır** (event ile — B kararı, kabul DEĞİL), (3) ReturnUrl tanımlı. Üçü sağlanınca aggregate
metodu insan müdahalesi olmadan Active'e geçer + `MerchantStatusChanged(Active)` yayınlar.
Koşul #2 Commission BC'de olur → `MerchantCommissionGridReady` event'i Merchant BC'ye taşır.
Merchant BC bu event + settlement + ReturnUrl durumunu birleştirip geçişi tetikler.

**Kontrol noktaları**: Geçiş, koşulu değiştiren her olayda yeniden değerlendirilir (settlement
eklendi / ReturnUrl set / grid-ready event geldi). **İdempotent**: zaten Active ise no-op.

**Gerekçe**: FR-016 "komisyon koşulu ayrı context, event ile taşınır". Merchant BC statüsünün
sahibi.

**Reddedilen**: Commission BC'nin merchant statüsünü değiştirmesi (İlke I), polling
(event-driven var, SC-007 ≤1dk).

---

## D11. Simüle aday site (dev doğrulama)

**Karar**: AppHost'a küçük statik host resource — `/.well-known/merchant-descriptor.json` +
`/.well-known/merchant-challenge/{token}` sunar. E1 gerçek işi kapsam dışı (`ecommerce-side-notes.md`).

**Gerekçe**: Spec Assumptions: aday site simüle. E1 ayrı bağımlılık.

**Reddedilen**: Gerçek ECommerce sitesi beklemek (feature'ı bloklar).

---

## D12. Excel.Mcp — generic belge (spreadsheet) MCP server

**Karar**: Yeni **Excel.Mcp** (`src/others/Excel.Mcp`, altyapı — BC değil, Mail.Mcp gibi).
Tek tool `generate_spreadsheet(sheetName, columns, rows)` → `.xlsx` bytes (base64). Domain
bilmez. Kütüphane **ClosedXML** (MIT, Excel kurulumu gerektirmez) → CPM'e ekle.
`MapMcp("/mcp").RequireAuthorization("document.generate")` (öneri) veya `mail.send`. **Tüketen
= harici LLM/MCP client** (D14): grid'i Commission.Api `get_merchant_commission_grid`'den alır,
Excel.Mcp'ye satır/sütun verir, xlsx'i Mail.Mcp `send_email` attachment'ına koyar. Commission.Api
handler'ı Excel/mail üretMEZ.

**Gerekçe**: Grid tablo → Excel doğal insan-yüzlü format. Generic doc-gen servisi "her şey
MCP" çizgisine oturur, yeniden kullanılır. .NET/CPM içi (ClosedXML), dış churn yok.

**Reddedilen**: Excel'i Commission.Api içinde üretmek (generic altyapı değil, tekrar
kullanılamaz), hazır doc-gen MCP (churn/dış bağımlılık), CSV (biçimlendirme zayıf; insan Excel
bekler). **Excel geri-ingest (marked-up .xlsx okuma) YOK** — en kırılgan halka, 014 pazarlığı
gelirse yapısal kanaldan.

**Açık (küçük)**: Excel.Mcp scope'u `mail.send` mi ayrı `document.generate` mı — plan detayı;
öneri ayrı scope (temiz sınır).

---

## D13. Dual-write / cross-BC tutarlılık — transactional outbox

**Karar**: B'nin getirdiği iki cross-BC sıçrama (Commission→Merchant "grid hazır";
Merchant→Identity "Active") **Marten + Wolverine transactional outbox** ile atomiktir (stack'te
zaten var: `IntegrateWithWolverine()` + `[Transactional]` + durable inbox). Event, state
değişikliğiyle **aynı Marten transaction'ında** outbox'a yazılır; broker relay ayrı, tekrar-
denemeli adım. "DB yaz + broker'a it" ASLA iki ayrı işlem değil.

- Commission.Api: `BulkUpsert` `[Transactional]` — grid satırları + `MerchantCommissionGridReady`
  aynı commit (outbox).
- Merchant.Api: event tüketici (durable inbox, `[Transactional]`) — koşul #2 flag set + 3-koşul
  değerlendir + (dolduysa) Active + `MerchantStatusChanged(Active)` aynı commit.
- Identity.Server: `MerchantStatusChanged(Active)` tüket → tam scope (mevcut 012, idempotent).

**Zorunluluklar**:
- **İdempotenlik**: teslim at-least-once; tüketici "zaten yapılmış mı?" kontrol (flag set,
  statü zaten Active → no-op).
- **Wolverine tekil-Handler** (CLAUDE.md): `public static class ...Handler` (TEKİL) +
  `public static async Task Handle(...)`. Çoğul "Handlers" sessizce keşfedilmez → mesaj kaybolur.
- **Eventual consistency kabul**: Active geçişi event gecikmesi kadar; SC-007 ≤1dk, outbox
  relay saniyeler.
- Koşul #2 tek-yön: grid "ready" olunca ready kalır; grid güncellemesi un-ready yapmaz;
  `MerchantCommissionGridReady` ilk tanımda bir kez (idempotent).

**Gerekçe**: Dual-write anti-pattern'i outbox çözer; repo zaten bu mekanizmayı kullanıyor
(012 merchant.lifecycle). Yeni bir şey icat edilmez.

**Reddedilen**: DB-sonra-publish iki ayrı işlem (tutarsızlık riski), 2PC/distributed tx
(gereksiz ağır, broker desteklemez), event yerine senkron cross-BC HTTP (İlke I; anlık
tutarlılık gerekmiyor — geçiş eventual olabilir).

---

## D14. Komisyon Excel'i — harici LLM/MCP orkestrasyon (bespoke agent YOK)

**Karar**: Komisyon Excel'inin üretilip mail'lenmesi, gateway içinde bir C# pipeline VEYA
bespoke ops-agent DEĞİL — **harici LLM/MCP client** ile orkestre edilir. 013 yalnız **MCP
yüzeylerini** sağlar; orkestratör **client seçimi 013 dışı** (belirli araç bağlanmaz — Claude
Desktop dahil hiçbir client 013'te sabitlenmez). Akış (client neyse):
```
Merchant.Api /mcp   get_merchant(domain)               → contactEmail, ad
Commission.Api /mcp get_merchant_commission_grid(id)    → satır/sütun
Excel.Mcp           generate_spreadsheet(cols, rows)    → .xlsx (base64)
Mail.Mcp            send_email(contactEmail, ..., [xlsx])
```
Gateway yalnız MCP yüzeylerini açar; kompozisyon LLM'de. Bespoke agent host YOK. 013'te
yüzeyler tool-bazında doğrulanır (uçtan-uca LLM orkestrasyonu 014/sonrası).

**Deterministik/agentik SINIR (kritik)**:
- **Agentik** (LLM sürer): komisyon Excel maili. İnsan-yüzlü bildirim, kritik yol değil.
- **Deterministik** (LLM'e verilMEZ, BC handler `IMailSender`): (a) aktivasyon maili —
  tek-seferlik MerchantKey linki, güvenlik; (b) admin "yeni başvuru" bildirimi. Operatörün
  komut yazmasına / LLM'in doğru sıralamasına bağlanamaz.
- **Deterministik** (event): Active koşulu #2 "grid hazır" (`MerchantCommissionGridReady`) —
  finansal statü LLM'e bağlanmaz. Grid tanımlama (BulkUpsert) bunu otomatik yayar; komisyon
  maili atılmasa bile koşul sağlanır.

**Gerekçe (kullanıcı)**: "LLM üzerinden MCP kullanırım ve MCP üzerinden Merchant bilgilerini
çekerim." "Her şey MCP/agent" fantezisi. Generic MCP yüzeyleri yeniden kullanılır; kompozisyon
esnek (LLM), yeni proje maliyeti yok.

**Reddedilen**: BC handler pipeline (agentic tercihe aykırı), bespoke ops-agent projesi
(MCP yüzeyleri yeterli; orkestratör client ertelendi), kritik mailleri de LLM'e vermek
(güvenlik/güvenilirlik — key linki fuzzy sıralamaya emanet edilemez).