# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Proje

DropShop: tedarikçi ürünlerini dropship modeliyle satan e-ticaret sistemi. Mimari
kurallar ECommerceWithAgentFramework projesinden devralındı: her mikroservis bir
Bounded Context, Vertical Slice + CQRS, zengin aggregate'ler, Result pattern,
Aspire + Marten + Wolverine.

**BÜYÜK PİVOT (021-022, 2026-08-13)**: sistem iyzico ödeme kanalına dönüyor. CP.VPOS,
BankRouter/PosAccount/BinCard, Reference.Api, SharedKernel, CardVault, Excel.Mcp ve TÜM eski
BC feature'ları SÖKÜLDÜ. Üç BC (**Payment**, **Merchant**, **Commission**) şu an "yapısal ara
durum"da: Domains'lerinde iyzico istemci malzemesi (davranışsız model/istek tipleri).
**037 (2026-08-15): `Iyzico.Provider` paylaşılan SDK'sı SÖKÜLDÜ ve SİLİNDİ.** Ölü V1 PKI zinciri
(BaseRequestV2/ToStringRequestBuilder/RequestFormatter/StringHelper/RequestStringConvertible/custom
HttpClient) atıldı — V2 akışı JSON+HMAC kullanır, `ToPKIRequestString` hiç çağrılmıyordu. Kalan
5 dosyalık transport engine `src/services/Payment.Api/Utils`'e (ns `Payment.Api.Utils`) tek kopya
alındı. iyzico wire request/response tipleri artık **kullanan slice'ın içine nested** (base tip yok,
düz camelCase JSON POCO; yanıtlar `Utils.ProviderResourceV2`'den türer). Merchant/Commission wire
kullanmıyordu → bağları kesildi (bkz. 037 memory). Kod tekrarı bilinçli kabul (kullanıcı kararı).
23-036 arası gerçek domain kuruldu (aggregate/slice/endpoint). 023 (SubMerchant merchant
modeli) ve 024 (komisyon: iyzico maliyeti + marj) gerçek domain'i bu malzemeden kuracak.
Yaşayan altyapı: **Admin** (Razor Pages BFF — çoğu ekranı ölü), **Identity.Server**
(OpenIddict), **Mail.Worker** (RabbitMQ→SMTP), **Mailpit**, agent host'ları (Payment.Agent,
Merchant.Agent — skill'leri ölü). Her iş kendi spec döngüsüyle (spec-kit, `specs/<NNN>/`).

## Komutlar

```bash
dotnet build                                        # tüm çözüm (PaymentGateway.slnx)
dotnet run --project src/aspire/AppHost/AppHost.csproj   # sistemi Aspire ile başlat (Postgres + RabbitMQ)
```

- Sistemi her zaman AppHost üzerinden başlat; servisler conn-string'leri Aspire'dan alır.
- Central Package Management açık ve İSTİSNASIZ: sürümler yalnız `Directory.Packages.props`'ta
  (022: CP.VPOS/Iyzipay adaları silindi).
- Test: `tests/Merchant.Api.Tests` (023 ile geri geldi — xUnit, saf domain, DB/ağ yok);
  `dotnet test` yeşil tutulur. Diğer BC'lerin testleri kendi spec'leriyle döner.

## Yapı ve kurallar

- `src/services/Payment.Api` — Payment BC. `Domains/<Aggregate>/Features/{Commands,Queries}`
  vertical slice düzeni; bir feature = bir static class (record command + Response + Handler + endpoint).
- `src/services/Merchant.Api` — Merchant BC (023 ile yeniden kuruldu). `Domains/Merchants/`:
  `Merchant` aggregate (iyzico SubMerchant sözleşmesiyle hizalı alan seti; tip-uyum matrisi
  Personal/PrivateCompany/LimitedOrJointStockCompany, TR IBAN mod-97 + e-posta inline
  doğrulama, statü makinesi Active/Passive/Suspended, `"mk_"+Guid` MerchantKey — yalnız
  oluşturma yanıtında bir kez) + 5 slice (CRUD + statü; yazma/liste/statü `AdminPlaneOnly`,
  tekil GET `MerchantScoped`). Oluşturmada `MerchantCreated`, gerçek statü değişiminde
  `MerchantStatusChanged` outbox'la yayınlanır (aynı statü idempotent no-op, yayın yok);
  Identity.Server tüketir (012 zinciri yaşıyor). **Merchant = gateway müşterisi SİTE**
  (ör. ECommerce) — pazaryeri/split DEĞİL. **037: iyzico bağı YOK** — Merchant hiçbir iyzico wire
  tipi kullanmıyordu (uyuyan Onboarding wire dormant'tı), `Iyzico.Provider` ProjectReference +
  global using kaldırıldı; proje iyzico'suz 0 hata derler. iyzico Onboarding/SubMerchant entegrasyonu
  gerçekten gelince wire slice'a nested yeniden yazılır (Payment.Api deseni). `SubMerchantKey`
  Merchant'ın kendi property'si (hep null; ayrı iş).
- `src/agents/Merchant.Agent` — A2A host (BC değil, stateless). **022 NOT**: Merchant/Commission
  MCP yüzeyleri söküldü — tüm skill'leri (register, komisyon pazarlığı) 023/024'e kadar ÖLÜ;
  proje derlenir.
- **MCP = yalnız Agent yüzeyi** (altyapı kuralı, 016 — yaşamaya devam eder): MCP tool'ları
  YALNIZ agent/LLM çağırır; servisler-arası veya BC→altyapı iletişimi ASLA MCP değil (messaging
  veya HTTP). (`Excel.Mcp` 022'de silindi; `document.generate` scope'u Identity seed'inden çıktı.)
- `src/others/Mail.Worker` — düz mail projesi (016; eski `Mail.Mcp` MCP'den çıkarıldı). **MCP DEĞİL** —
  HTTP yüzeyi/auth yok, yalnız Wolverine RabbitMQ consumer. `mail.delivery` fanout'unu durable queue
  (`mail.delivery-send`) ile tüketir; `SendEmailHandler` (tekil "Handler") `System.Net.Mail` → Mailpit ile
  gönderir. **Retry**: `Policies.OnException<SmtpException>().RetryWithCooldown(1s,5s,15s).Then.MoveToErrorQueue()`
  (backoff + dead-letter; message store yok → `ProcessInline`, RabbitMQ redelivery). Deterministik mailler
  (aktivasyon linki + başvuru ack + 019 komisyon teklifi) BC handler'ından `[Transactional]` outbox ile
  `bus.PublishAsync(new SendEmailRequested(to,subject,body,isHtml,attachment?))` — publish yalnız DB
  commit'te gider. 019: `EmailAttachmentTable(FileName,Headers,Rows)` opsiyonel eki ClosedXML ile .xlsx'e
  çevirip mail'e ekler (generic tablo — domain bilmez). `IMailSender`/`MailMcpClient` KALDIRILDI.
- `src/services/Commission.Api` — CommissionPolicy aggregate + slice'lar (024). **037: iyzico bağı YOK** —
  Commission hiçbir iyzico Payout/Reporting wire tipi kullanmıyordu (dormant'tı); `Iyzico.Provider`
  ProjectReference + global using'ler (`.Payout`/`.Reporting`) kaldırıldı; proje iyzico'suz 0 hata derler.
  iyzico payout/rapor entegrasyonu gelince wire slice'a nested yeniden yazılır. `merchant.commission` +
  `mail.delivery` yayın kayıtları Program.cs'te durur.
- `src/agents/Payment.Agent` — A2A host + LLM router + MCP client (007). Payment BC **DEĞİL** —
  kalıcılık yok, stateless delivery adaptörü. **022 NOT**: Payment.Api MCP yüzeyi söküldü —
  taksit/oturum skill'leri ödeme akışı yeniden kurulana kadar ÖLÜ; proje derlenir. `AddA2AServer(agent)` + `MapA2AJsonRpc` +
  `MapWellKnownAgentCard` (`/.well-known/agent-card.json`). LLM yalnız tool sırasını kurar
  (`get_installment_options` → `select_installment`); tutar/banka/kart üretmez (domain'den).
  Chat anahtarı agent config'inden (`OpenAI:ApiKey`/user-secrets). Tüm A2A/Agent Framework paketleri
  preview — `Directory.Packages.props`'ta pin.
- `src/services/Payment.Api` — Payment BC (StoredCard/Payment aggregate + kart-saklama/çekim/taksit
  slice'ları canlı). **037: `Iyzico.Provider` SDK SÖKÜLDÜ — her iyzico wire tipi kullanan slice'ın
  İÇİNE nested taşındı** (base tip yok, düz camelCase JSON POCO; yanıtlar `Utils.ProviderResourceV2`'den
  türer). Slice'ı açan iyzico çağrısını da orada görür (ChargePayment/TokenizeCard/RevokeCard/
  InstallmentOptions). Transport engine (5 dosya: RestHttpClientV2/ProviderResourceV2/HashGeneratorV2/
  ProviderConstants/ProviderOptions) `Utils/` altında tek kopya (ns `Payment.Api.Utils`) — süreç
  taşımaz, 4 slice ortak; feature'a gömülemez. **Sabit kural (037): handler metodu içinde Command/Query'den
  (kullanıcı) gelmeyen HİÇBİR değer literal yazılmaz** — locale/conversationId/kanal/grup/currency/itemType/
  endpoint yolları/success durumu/alias/email prefix+domain/id prefix'leri hepsi `Options/IyzicoRequestOptions`
  config POCO'sundan okunur (appsettings, non-secret; transport secret'ı ayrı `IyzicoProviderSettings`).
  Domain-uygun 4 tip hâlâ VO (`Buyer/Address/BasketItem` → Payments, `CardInformation` → StoredCards;
  `Domains/<Aggregate>/ValueObjects/`); handler VO'dan slice-nested wire'a map'ler (anti-corruption sınır).
  `CardAssociationMapper` `Domains/StoredCards/`'da. Wire tipleri BC DIŞINA SIZMAZ. Kod tekrarı bilinçli
  kabul (kullanıcı kararı — paylaşılan SDK istemedi, süreç netliği için wire slice'ta).
- `src/ui/Admin` — Razor Pages BFF (yetki yok). Merchant/Bank/komisyon/settlement ekranları; typed
  `HttpClient`'lar Aspire service discovery ile API'leri çağırır (`http://merchant-api` vb.). Backend'e
  kural sızdırmaz — yalnız API sonucunu (`ApiResult`/`MessageText` Türkçe) gösterir.
- Integration event'ler `src/others/Shared` (`PaymentCompletedEvent/PaymentFailedEvent`, fanout exchange).
  Henüz tüketici yok; Order BC gelince bağlanır. 012: `MerchantCreated/MerchantStatusChanged`
  (`merchant.lifecycle` fanout) — Merchant.Api yayınlar, Identity.Server tüketir (OpenIddict
  istemci senkronu; status string taşır, BC enum'u sızmaz).
- Ortak yapı taşları `src/others/Common`'da: domain base tipleri, Result pattern, DI marker'ları,
  auth, caching, exception handler.
- `src/services/Payment.Api/Utils` — iyzico V2 transport engine (037; `Iyzico.Provider` SDK'nın kalıntısı,
  ARTIK PAYLAŞILMAZ — yalnız Payment.Api'ye ait, ns `Payment.Api.Utils`). 5 dosya: `RestHttpClientV2`
  (POST/DELETE, camelCase JSON gövde), `ProviderResourceV2` (yanıt tabanı + HMAC imza header'ı),
  `HashGeneratorV2`, `ProviderConstants`, `ProviderOptions` (transport-config POCO — `IyzicoProviderSettings`
  secret'ından map'lenir). Ölü V1 PKI zinciri (`BaseRequestV2`/`ToStringRequestBuilder`/`RequestFormatter`/
  `StringHelper`/`RequestStringConvertible`/custom `HttpClient`) 037'de atıldı. Süreç taşımaz (saf transport),
  4 slice ortak kullanır. iyzico wire request/response tipleri buraya KONMAZ — kullanan slice'ın içinde
  nested durur (037 kuralı: wire = süreç, slice'ta; engine = plumbing, Utils'te). İkinci canlı iyzico
  tüketicisi (ör. Merchant onboarding, Commission payout) çıkarsa engine'i ortak lib'e terfi düşünülür
  (şimdilik YAGNI — tek tüketici).
- `src/others/Identity.Server` — OpenIddict tabanlı minimal M2M IdP (011). Sabit issuer
  `https://localhost:5101` (ECommerce Identity 5001'de; A2A'da iki sistem aynı anda koşar);
  tek uç `connect/token`, yalnız client_credentials. Scope claim'i JSON dizisi
  (`ScopeClaimArrayHandler` — tek-string'te policy'ler sessizce 403 verir, dokunma). Seed
  idempotent: 6 scope + 2 istemci (admin-ui, payment-agent); secret'lar config'ten
  (`Clients:<id>:Secret`). Kendi `identityDb`'si (EF Core — anayasanın izole-altyapı istisnası).
  012: Wolverine ile `merchant.lifecycle` tüketir (`MerchantClientEventHandlers` — idempotent
  upsert; message store YOK, bilinçli); access token ömrü GLOBAL 15 dk.
- Auth modeli (011+012): BC API'leri `AddAuthenticationAndAuthorizationExtension` (JwtBearer + scope
  policy) kullanır; her endpoint policy'yi AÇIKÇA beyan eder (`RequireAuthorization` — GET →
  `<bc>.read`, mutasyon → `<bc>.write`; sabitler `AuthorizationScopes`). Payment `/mcp` yüzeyi
  tek policy: `payment.write`. Admin BFF (`AdminTokenHandler`) ve Payment.Agent
  (`AgentTokenHandler`) client_credentials token'ını cache'ler (−30 sn yenileme).
- Merchant istemci düzlemi (012, G2 KARARLI): merchant = OAuth istemcisi (`client_id=merchantId`,
  `client_secret=MerchantKey`; MerchantKey yalnız `connect/token`'a gider). Token'da `merchant_id`
  claim'i; verme statü-kapılı (yalnız Active — izinler event'le açılır/kapanır, client silinmez).
  Enforcement `Common`'da: `MerchantScopeEvaluator` (saf çekirdek) + `MerchantScoped` (claim-route
  eşleşmesi, fail-closed) ve `AdminPlaneOnly` (claim'li token giremez — ör. `PUT
  merchants/{merchantId}/status`) policy'leri (`AuthorizationPolicies`). Merchant token'ı yalnız
  Merchant BC'de kendi kaydı + settlement-account uçlarına erişir; Payment/Commission audience
  zinciriyle kapalı. İnsan login + RBAC G3'te.
- Handler'lar `[Transactional]` + `IDocumentSession` (repository yok); sonuçlar
  `FeatureObjectResultModel<T>`/`ResultDomain` (exception değil).
- **Wolverine event-handler kuralı (sık hata: static/async + ad son eki)**: integration-event
  tüketicisi `public static class` + `public static async Task Handle(<Event> message, ...)`
  olacak — instance class, `async void`, sync `void Handle` YASAK. Sınıf adı **"Handler" ile
  TEKİL** bitecek (`SendEmailHandler` ✓); **"Handlers" (çoğul) Wolverine 6.4'te SESSİZCE
  keşfedilmiyor** — "No known handler ... discarded", dead-letter YOK, mesaj kaybolur (012'de
  `MerchantEventHandlers` ve `MerchantClientEventHandlers` bu yüzden tekile taşındı). Şablon:
  Identity.Server `MerchantClientEventHandler`. Canlı doğrulama: consumer log'unda
  "Successfully processed message" var, "No known handler" yok.

## Kod standartları

> **022 notu**: Aşağıdaki kurallardaki örnek tip adları (`Merchant.TryActivate`, `PosAccount`,
> `BinCardSeeder`, `PaymentSessionMcpTools`, `SettlementAccount.UpdateDetails`, MCP tool
> örnekleri…) 022 pivotunda SİLİNMİŞ tarihî koddan; KURALLAR aynen geçerli, örnekler 023+
> yeni domain kurulurken bu desenlerle yeniden doğar.

- **Sonuç sözleşmesi (014)**: Handler'dan (Command/Query slice) çağrılan aggregate davranış/fabrika
  metotları `ResultDomain` / `ResultDomain<T>` döner — **void mutator dahil** (durum değiştiren ama
  ham dönen metotlar da sarılır; ör. `Merchant.TryActivate()` → `ResultDomain`, çağıran
  `merchant.TryActivate().IsSuccess`). Fabrikalar başarısız olmasa bile `Ok(data)` sarılır (tek-tip
  imza, ileride doğrulama eklenince kırılmaz): `DomainControlChallenge.Issue`, `ActivationTicket.Issue`,
  `OnboardingNotification.Create` → `ResultDomain<T>.Ok(...)`; çağıran `.Data!` açar. Çok-durumlu
  domain sonucu (outcome-enum) `Ok(outcome)` olarak sarılır, "başarısız" enum değerleri `Error`'a
  eşlenmez (retry-able Failed teknik hata değil): örnek `DomainControlChallenge.Verify` →
  `ResultDomain<ChallengeOutcome>.Ok(outcome)`; çağıran `.Data!` ile enum'u alır. **Muaf**: saf
  getter/sorgu/lookup (property, `bool Is...`, hesap) handler'dan çağrılsa bile ham değer döner —
  ör. `PosAccount.GetCommissionRate(int) : decimal?`. `MessageItem` inşası için referans:
  `SettlementAccount.UpdateDetails`.
- **Aggregate-klasör**: `Domains/` hemen altındaki her klasör **tek** `: AggregateRoot` içerir; iç içe
  aggregate yok. İstisna (aggregate kökünde durabilir): `SharedKernel/`, domain-service (ör.
  `BankRouter`), seeder (ör. `BinCardSeeder`), MCP tool (ör. `PaymentSessionMcpTools`), endpoint
  extension, aggregate'e ait enum/status/mapping. Doğrulama:
  `grep -rlE "class .*: AggregateRoot" src/*/*/Domains` → her klasör tek dosya.
- **ValueObjects**: aggregate'e ait standalone value object (class/record, AggregateRoot değil) →
  `<Aggregate>/ValueObjects/` altına konur, aggregate kökünde durmaz. (016: tek örnek olan
  `MerchantDescriptor` push-inline ile silindi — kural yeni VO gerektiğinde geçerli.)
- **Aggregate metotları — private helper YOK (015)**: Aggregate'lerde private yardımcı metod yazma;
  ortak mantık private'a çıkarılıp çağrılmaz, **inline** yazılır (kod tekrarı bilinçli kabul). **VO
  MUAF** (VO'da private helper serbest — VO gerektiğinde). Örnek: `RegisterRequest`
  `InvalidState()` helper'ı kaldırıldı, `MessageItem` her metotta inline.
- **Aggregate metotları — yalnız handler'dan çağrılır (015)**: Bir aggregate public metodu SADECE
  handler'dan çağrılır; başka bir aggregate metodunun içinden ÇAĞRILMAZ (factory dahil). Yalnız
  domain-içi çağrılan metot ayrı bırakılmaz — gövdesi çağıran metoda inline edilir (ör. `Merchant.Provision`
  → `RedeemActivation`'a inline; `CreateAwaiting` challenge kurulumu inline, `IssueChallenge`'ı çağırmaz).
  Böylece bir domain metodunu görünce handler karşılığı olduğu kesindir. **VO MUAF**.
- **Aggregate metodu — iki not (015)**: Her aggregate public metoduna (1) `/// <summary>` metodun
  ne işe yaradığını, (2) `/// <remarks>Handler: <HandlerAdı></remarks>` onu çağıran Handler tipini
  yazar (iş-akışı takibi; çoklu handler virgülle; saga/event-handler sayılır). İç Handler tipini
  gösterir; dış slice rename'i etkilemez. **VO MUAF**.
- **Ayrı teknik klasör YOK — feature'lar Domains altında (015)**: `McpTools/` gibi teknik-katman
  klasörü açma; MCP tool'ları dahil tüm feature/iş süreçleri `Domains/<Aggregate>/` altında durur (MCP
  tool aggregate kökünde — Payment.Api `PaymentSessionMcpTools` deseni). `WithToolsFromAssembly` assembly
  tarar; konum registration'ı etkilemez. Örnek: `RegisterRequests/RegisterRequestMcpTools.cs`,
  `Merchants/MerchantMcpTools.cs`.
- **Agent/MCP yüzeyi izole (015)**: Agent'a açık işlemler `Domains/<Aggregate>/Features/Agents/` (klasör ÇOĞUL) altında,
  slice adı **`<X>ForAgent`** (ör. `SubmitRegistrationForAgent`, `RegistrationStatusForAgent`,
  `GetMerchantForAgent`). MCP tool YALNIZ bu Agent slice'ını çağırır. Agent slice `Features/Commands/` veya
  `Features/Queries/` class'larına **ASLA** gitmez — `IMessageBus` ile bile değil; kendi Query/Command +
  Response + Handler'ını taşır, okumayı/işlemi `IDocumentSession` ile doğrudan yapar (kod tekrarı bilinçli).
- **Config — Options pattern (strongly-typed)**: `IConfiguration`'dan DOĞRUDAN değer okunmaz —
  `config["Section:Key"]`, `GetValue<T>`, `GetSection(...).Value`, ad-hoc `Get<T>()` dahil hepsi YASAK.
  `IConfiguration`/`IConfigurationSection` hiçbir handler/servis ctor'una girmez. Her bölüm (ör.
  `DropShopGateway:{McpUrl,IdentityAddress,ClientId,ClientSecret}`) için bir Options POCO'su
  (`Options/` altında) tanımlanır ve bir `AddOptionsExt` uzantısında bağlanır —
  house-style (ECommerce `WebApp/Extensions/OptionsExt.cs` + `IdentityServerSettings`/`GatewayOption`
  referans):
  ```csharp
  services.AddOptions<T>().BindConfiguration(nameof(T)).ValidateDataAnnotations().ValidateOnStart();
  services.AddSingleton<T>(sp => sp.GetRequiredService<IOptions<T>>().Value); // POCO'yu unwrap et
  ```
  Tüketici `IOptions<T>` değil **düz POCO `T`**'yi ctor'dan enjekte eder. `BindConfiguration(nameof(T))`
  section adını tip adından alır → POCO adı section adıyla eşleşir (ör. section `GatewayOption`). Zorunlu
  alanlar DataAnnotations ile işaretlenir; türetilmiş değerler POCO'da computed property. Anahtar isimleri
  kod içinde string olarak dağıtılmaz.
  **İstisna (sabit POCO'ya map olmayan):** Aspire service-discovery anahtarları
  (`config["services:<ad>:http:0"]`) ve dinamik-keyed lookup (ör. `Clients:{clientId}:Secret`) doğrudan
  okunabilir — biri Aspire enjekte eder, öteki çalışma-anı anahtarı; ikisi de statik section değildir.

## Bilinçli ertelemeler

- Test: 022 sonrası test projesi YOK (ölü aggregate testleri silindi); 023+ saf domain birim
  testlerini geri getirir. Handler/HTTP/Razor Pages entegrasyonu test edilmez — quickstart
  senaryolarıyla elle doğrulanır.
- Diğer BC'ler (Catalog, Order, Supplier...) tasarım gereği henüz yok; her biri kendi
  spec döngüsüyle eklenecek.
- **Anayasa PATCH amendment bekliyor** (019 research R7): Anayasa II hâlâ "BaseModel'den türer" ve
  "Enumeration ile modellenir" diyor — ikisi de 2026-08-11 refactor'üyle silindi (AggregateRoot tek
  base, düz enum). `/speckit-constitution` ile ayrı iş olarak düzeltilecek.