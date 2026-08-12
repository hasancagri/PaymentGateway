# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Proje

DropShop: tedarikçi ürünlerini dropship modeliyle satan e-ticaret sistemi. Mimari
kurallar ECommerceWithAgentFramework projesinden devralındı: her mikroservis bir
Bounded Context, Vertical Slice + CQRS, zengin aggregate'ler, Result pattern,
Aspire + Marten + Wolverine.

Mevcut BC'ler: **Payment** (CP.VPOS sanal POS), **Merchant** (onboarding + settlement
hesapları), **Commission** (banka referansı + komisyon grid). Ayrıca **Admin** (Razor Pages
BFF) API'leri service discovery ile tüketir; **Identity.Server** (OpenIddict, BC değil —
altyapı servisi) makine token'ı verir. 013: **Merchant.Agent** (A2A başvuru host'u, BC değil),
**Excel.Mcp** (generic MCP altyapı servisi, BC değil), **Mailpit** (dev SMTP catch-all). 016:
**Mail.Worker** (düz mail projesi — MCP DEĞİL; RabbitMQ consumer → SMTP). Her BC kendi spec
döngüsüyle (spec-kit, `specs/<NNN>/`) eklendi.

## Komutlar

```bash
dotnet build                                        # tüm çözüm (PaymentGateway.slnx)
dotnet run --project src/aspire/AppHost/AppHost.csproj   # sistemi Aspire ile başlat (Postgres + RabbitMQ)
dotnet test tests/Merchant.Api.Tests                # saf domain birim testleri (Merchant)
dotnet test tests/Commission.Api.Tests              # saf domain birim testleri (Commission)
```

- Sistemi her zaman AppHost üzerinden başlat; servisler conn-string'leri Aspire'dan alır.
- Central Package Management açık: sürümler `Directory.Packages.props`'ta. Tek istisna
  `CP.VPOS.csproj` (bilinçli CPM dışı, kendi sürümlerini tutar — dokunma).

## Yapı ve kurallar

- `src/services/Payment.Api` — Payment BC. `Domains/<Aggregate>/Features/{Commands,Queries}`
  vertical slice düzeni; bir feature = bir static class (record command + Response + Handler + endpoint).
- `src/services/Merchant.Api` — Merchant BC. `Merchant` aggregate (onboarding) + `MerchantSettlementAccount`
  aggregate (payout banka hesabı; IBAN mod-97 saf doğrulama, yerel `BankCatalog` kopyası, endpoint'ler
  merchant-scoped `merchants/{merchantId}/settlement-accounts`). Aynı vertical slice deseni.
  013 onboarding: `RegisterRequest` aggregate (başvuru — merchant'tan AYRI, onayla doğar) +
  `DomainControlChallenge` (HTTP-01 tarzı sahiplik bileti) + `ActivationTicket` (tek-kullanım key
  teslim bileti) + `OnboardingNotification` (deterministik mail kaydı, FR-019). `Merchant`'a
  **Provisioning** statüsü + `ReturnUrl`/`ExternalRef`/`HasSettlementAccount`/`CommissionGridReady` +
  `TryActivate()` (3-koşul idempotent → Active). `/mcp` yüzeyi (`submit_registration`,
  `registration_status`, `get_merchant`; policy `merchant.write`). `merchant.commission` fanout tüketir
  (`MerchantCommissionGridReadyHandler` tekil — Active koşulu #2). Redeem ucu
  `POST merchants/activation/redeem` (AdminPlaneOnly; `MerchantProvisioned` yayınlar, key bir kez döner).
- `src/agents/Merchant.Agent` — 013 A2A başvuru host'u (Payment.Agent şablonu). BC değil, stateless.
  013 skill'leri: `register` + `registration_status`; Merchant.Api `/mcp`'yi kendi client'ıyla
  (`merchant-agent`, scope `merchant.read merchant.write commission.write`) tüketir. **019**: ikinci MCP
  client Commission.Api `/mcp`'ye (aynı token, iki audience); 4 komisyon skill'i: `propose_commission`
  (get_merchant isim→id+email → submit_commission_proposal), `revise_commission_draft` (yalnız admin'in
  AÇIK değerleri; diff yankılanır), `show_commission_draft`, `commission_proposal_status`. Admin komisyon
  pazarlığını bu metin kanalıyla yürütür; mail yalnız açık "gönder" komutuyla çıkar.
- `src/others/Excel.Mcp` — generic altyapı MCP server'ı (BC değil, domain bilmez). `generate_spreadsheet`
  (ClosedXML → .xlsx base64). Scope-korumalı: `document.generate`. **MCP = yalnız Agent yüzeyi** (altyapı
  kuralı, 016): MCP tool'ları YALNIZ agent/LLM çağırır; servisler-arası veya BC→altyapı iletişimi ASLA MCP
  değil (messaging veya HTTP). Nerede MCP nerede HTTP belirsizliğini bu kural keser.
- `src/others/Mail.Worker` — düz mail projesi (016; eski `Mail.Mcp` MCP'den çıkarıldı). **MCP DEĞİL** —
  HTTP yüzeyi/auth yok, yalnız Wolverine RabbitMQ consumer. `mail.delivery` fanout'unu durable queue
  (`mail.delivery-send`) ile tüketir; `SendEmailHandler` (tekil "Handler") `System.Net.Mail` → Mailpit ile
  gönderir. **Retry**: `Policies.OnException<SmtpException>().RetryWithCooldown(1s,5s,15s).Then.MoveToErrorQueue()`
  (backoff + dead-letter; message store yok → `ProcessInline`, RabbitMQ redelivery). Deterministik mailler
  (aktivasyon linki + başvuru ack + 019 komisyon teklifi) BC handler'ından `[Transactional]` outbox ile
  `bus.PublishAsync(new SendEmailRequested(to,subject,body,isHtml,attachment?))` — publish yalnız DB
  commit'te gider. 019: `EmailAttachmentTable(FileName,Headers,Rows)` opsiyonel eki ClosedXML ile .xlsx'e
  çevirip mail'e ekler (generic tablo — domain bilmez). `IMailSender`/`MailMcpClient` KALDIRILDI.
- `src/services/Commission.Api` — Commission BC. `Bank` aggregate (katalogdan kod+ad) + banka/merchant
  komisyon grid'leri (kombinasyon-bazlı; banka grid'i atomik toplu upsert). Banka seed yok. **019 teklif
  akışı**: `CommissionDraft` (merchant başına TEK çalışma kopyası, Id=MerchantId; deterministik sıralı +
  1-tabanlı satır no'lu `DraftRow` — "satır 37" adreslemesi; `CreateFromBankGrid` = banka oranı +
  `CommissionProposalOption.DefaultMarginPoints`; `Revise` set/delta işlemleri SUNUCUDA hesaplar, taban
  bekçisi BÜTÜN-veya-hiç, diff döner; kabulde `Lock`) + `CommissionProposal` (gönderilmiş immutable
  fotoğraf; Pending/Accepted/Rejected/Superseded; tek-kullanımlık + TTL `DecisionTicket`). `/mcp` yüzeyi
  tek policy `commission.write` (013'teki commission.read'den yükseltildi); tool'lar:
  `submit_commission_proposal`, `revise_commission_draft`, `show_commission_draft`,
  `commission_proposal_status`, `get_merchant_commission_grid`. Karar uçları ANONİM mini HTML
  (`commission-proposals/decision/{ticket}/accept|reject` — yetki=bilet, aktivasyon redeem emsali);
  kabul tek `[Transactional]`da: Accept + draft Lock + satırlar `MerchantCommission`'a kopya (banka
  çakışmasında MAX oran) + `MerchantCommissionGridReady` publish (mevcut aktivasyon zinciri). **Finalize
  + `MerchantCommissionGrid`/`GridStatus` SÖKÜLDÜ (FR-013)** — merchant komisyonunun TEK yazma yolu
  teklif kabulü; merchant-commission upsert uçları da kaldırıldı (yalnız GET kaldı). Admin komisyon
  ekranı salt-okuma + teklif durumu. LLM oran üretmez/hesaplamaz (yalnız açık değer taşır).
- `src/agents/Payment.Agent` — A2A host + LLM router + MCP client (007). Payment BC **DEĞİL** —
  kalıcılık yok, stateless delivery adaptörü. `AddA2AServer(agent)` + `MapA2AJsonRpc` +
  `MapWellKnownAgentCard` (`/.well-known/agent-card.json`). LLM yalnız tool sırasını kurar
  (`get_installment_options` → `select_installment`); tutar/banka/kart üretmez (domain'den).
  Chat anahtarı agent config'inden (`OpenAI:ApiKey`/user-secrets). Tüm A2A/Agent Framework paketleri
  preview — `Directory.Packages.props`'ta pin.
- Payment.Api MCP yüzeyi (007): agent'a açık işlemler `Domains/PaymentSessions/Features/**Agent**/`
  altında (Commands/Queries değil); MCP tool'ları `PaymentSessionMcpTools.cs`'te her tool ayrı
  `[McpServerToolType]`, yalnız `IMessageBus.InvokeAsync` ile slice'ı sarar. Kayıt:
  `AddMcpServer().WithToolsFromAssembly()` + `MapMcp("/mcp")`. `PaymentSession` aggregate = A2A
  akışının kalıcı izdüşümü (faz: Opened→QuoteProvided→InstallmentSelected/Failed). Token→BIN çözümü
  `CardVault/ICardVault` (`SimulatedCardVault` → 008 `ResolveBinCard`; PAN kanala girmez). Model A
  quote = kullanıcı tutarı sepet tutarı (komisyon yalnız `BankRouter` POS seçiminde). Çekim 007 dışı.
- `src/ui/Admin` — Razor Pages BFF (yetki yok). Merchant/Bank/komisyon/settlement ekranları; typed
  `HttpClient`'lar Aspire service discovery ile API'leri çağırır (`http://merchant-api` vb.). Backend'e
  kural sızdırmaz — yalnız API sonucunu (`ApiResult`/`MessageText` Türkçe) gösterir.
- `src/otherProjects/CP.VPOS` — sanal POS kütüphanesi, OLDUĞU GİBİ taşındı (eski stil, nullable
  kapalı). `otherProjects` altında (versiyonlanmaz) ama Payment BC'nin aktif bağımlılığı —
  Payment.Api buradan referans verir. CP.VPOS tipleri slice sınırını GEÇMEZ: handler
  `SaleResponse`'u domain'e çevirir.
- `BankRouter` (domain service, saf hesap): komisyon + kart BIN/programı + taksit desteğine göre
  maliyet sıralı banka adayları döner. Failover: handler sıralı adayları dener; 3D'de yalnız ilk aday.
- `PosAccount` aggregate: banka POS anlaşması (credentials + taksit başına komisyon). Komisyon
  oranları buradan yönetilir; router'ın girdisidir.
- Integration event'ler `src/others/Shared` (`PaymentCompletedEvent/PaymentFailedEvent`, fanout exchange).
  Henüz tüketici yok; Order BC gelince bağlanır. 012: `MerchantCreated/MerchantStatusChanged`
  (`merchant.lifecycle` fanout) — Merchant.Api yayınlar, Identity.Server tüketir (OpenIddict
  istemci senkronu; status string taşır, BC enum'u sızmaz).
- Ortak yapı taşları `src/others/Common`'da: domain base tipleri, Result pattern, DI marker'ları,
  auth, caching, exception handler.
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
  TEKİL** bitecek (`ReferenceEventHandler` ✓); **"Handlers" (çoğul) Wolverine 6.4'te SESSİZCE
  keşfedilmiyor** — "No known handler ... discarded", dead-letter YOK, mesaj kaybolur (012'de
  `MerchantEventHandlers` ve `MerchantClientEventHandlers` bu yüzden tekile taşındı). Şablon:
  Commission `ReadModels/ReferenceBankReadModel.cs`. Canlı doğrulama: consumer log'unda
  "Successfully processed message" var, "No known handler" yok.

## Kod standartları

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

- Test: saf domain birim testleri var (`tests/Merchant.Api.Tests`, `tests/Commission.Api.Tests`).
  Handler/HTTP/Razor Pages entegrasyonu test edilmez — quickstart senaryolarıyla elle doğrulanır.
- Diğer BC'ler (Catalog, Order, Supplier...) tasarım gereği henüz yok; her biri kendi
  spec döngüsüyle eklenecek.
- **Anayasa PATCH amendment bekliyor** (019 research R7): Anayasa II hâlâ "BaseModel'den türer" ve
  "Enumeration ile modellenir" diyor — ikisi de 2026-08-11 refactor'üyle silindi (AggregateRoot tek
  base, düz enum). `/speckit-constitution` ile ayrı iş olarak düzeltilecek.